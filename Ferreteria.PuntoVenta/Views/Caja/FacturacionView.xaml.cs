using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Security;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Ferreteria.PuntoVenta.Services.Printing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Punto de venta de mostrador (módulo Caja): búsqueda de Product, carrito temporal
/// y registro de venta vía Order / CreateCashSale (CashSession opcional en Fase posterior).
/// </summary>
public partial class FacturacionView : UserControl
{
    /// <summary>Comando POS F2 para enfocar la búsqueda de producto.</summary>
    public static readonly RoutedCommand FocusProductSearchCommand = new();

    /// <summary>Comando POS F9 para cobrar la venta actual.</summary>
    public static readonly RoutedCommand ChargeSaleCommand = new();

    /// <summary>Comando POS Esc para cerrar búsquedas o cancelar la venta.</summary>
    public static readonly RoutedCommand CancelSaleCommand = new();

    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly ICurrentSessionService _currentSession;
    private readonly ISaleReceiptPrinter _saleReceiptPrinter;
    private readonly PrintingOptions _printingOptions;
    private readonly ILogger<FacturacionView> _logger;
    private readonly CashSessionOpeningFlow _cashSessionOpeningFlow;
    private Guid? _lastOrderId;
    private readonly ObservableCollection<CartLineItem> _cartLineItems = new();
    private readonly AsyncSearchCoordinator _searchCoordinator = new();
    private readonly AsyncSearchCoordinator _customerSearchCoordinator = new();
    private readonly SaleAttemptTracker _saleAttemptTracker = new();
    private Customer? _selectedCustomer;
    private bool _suppressCustomerSearch;

    /// <summary>Inicializa la vista de facturación y enlaza el carrito a la UI.</summary>
    /// <remarks>
    /// Atajos POS: F2 enfoca la búsqueda de producto; F9 cobra; Esc cierra una búsqueda abierta
    /// y, si no existe, solicita confirmación antes de perder la venta en curso.
    /// </remarks>
    public FacturacionView(
        IInventoryService inventoryService,
        IOrderService orderService,
        ICustomerService customerService,
        ICurrentSessionService currentSession,
        ISaleReceiptPrinter saleReceiptPrinter,
        IOptions<PrintingOptions> printingOptions,
        ILogger<FacturacionView> logger,
        CashSessionOpeningFlow cashSessionOpeningFlow)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _customerService = customerService;
        _currentSession = currentSession;
        _saleReceiptPrinter = saleReceiptPrinter;
        _printingOptions = printingOptions.Value;
        _logger = logger;
        _cashSessionOpeningFlow = cashSessionOpeningFlow ?? throw new ArgumentNullException(nameof(cashSessionOpeningFlow));

        InitializeComponent();
        CartItemsControl.ItemsSource = _cartLineItems;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RenderSaleTotals();
    }

    /// <summary>Al cargar, ejecuta la primera búsqueda de productos.</summary>
    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ProductSearchTextBox.Focus();
        await SearchProductsAsync();
    }

    /// <summary>Libera el coordinador de búsquedas asíncronas al salir de la vista.</summary>
    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
        _customerSearchCoordinator.Dispose();
    }

    /// <summary>Handler de cambio en el cuadro de búsqueda de productos.</summary>
    private async void OnProductSearchChanged(object sender, TextChangedEventArgs e)
    {
        await SearchProductsAsync();
    }

    /// <summary>Busca productos en inventario (cancela búsquedas previas en curso).</summary>
    private async Task SearchProductsAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();

        try
        {
            ProductResultsListBox.ItemsSource = await _inventoryService.SearchProductsAsync(
                ProductSearchTextBox.Text,
                stockStatus: null,
                take: 25,
                cancellationToken: cancellationToken);
            ProductResultsListBox.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo buscar productos en la vista de facturación");
            SetStatusMessage("No se pudo buscar productos. Intente nuevamente.", isError: true);
        }
    }

    private void OnProductSearchGotFocus(object sender, RoutedEventArgs e)
    {
        ProductResultsListBox.Visibility = Visibility.Visible;
    }

    private async void OnCustomerSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressCustomerSearch)
        {
            return;
        }

        await SearchCustomersAsync();
    }

    private void OnCustomerSearchGotFocus(object sender, RoutedEventArgs e)
    {
        if (CustomerResultsListBox.Items.Count > 0)
        {
            CustomerResultsListBox.Visibility = Visibility.Visible;
        }
    }

    private async Task SearchCustomersAsync()
    {
        var searchText = CustomerSearchTextBox.Text.Trim();
        if (searchText.Length == 0)
        {
            CustomerResultsListBox.ItemsSource = null;
            CustomerResultsListBox.Visibility = Visibility.Collapsed;
            return;
        }

        var cancellationToken = _customerSearchCoordinator.BeginNewSearch();
        try
        {
            CustomerResultsListBox.ItemsSource = await _customerService.GetCustomersAsync(
                searchText,
                includeInactive: false,
                take: 25,
                cancellationToken: cancellationToken);
            CustomerResultsListBox.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo buscar clientes en la vista de facturación");
            SetStatusMessage("No se pudo buscar clientes. Intente nuevamente.", isError: true);
        }
    }

    private void OnCustomerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (CustomerResultsListBox.SelectedItem is not Customer customer)
        {
            return;
        }

        SelectCustomer(customer);
        CustomerResultsListBox.SelectedItem = null;
    }

    private void SelectCustomer(Customer customer)
    {
        _selectedCustomer = customer;
        SelectedCustomerText.Text = BuildCustomerDisplay(customer);
        CustomerResultsListBox.Visibility = Visibility.Collapsed;
        _suppressCustomerSearch = true;
        CustomerSearchTextBox.Clear();
        _suppressCustomerSearch = false;
        SetStatusMessage("Cliente seleccionado.");
    }

    private void OnUseWalkInCustomerClick(object sender, RoutedEventArgs e)
    {
        ClearSelectedCustomer();
    }

    private void ClearSelectedCustomer()
    {
        _selectedCustomer = null;
        SelectedCustomerText.Text = "Consumidor final";
        CustomerResultsListBox.ItemsSource = null;
        CustomerResultsListBox.Visibility = Visibility.Collapsed;
        _suppressCustomerSearch = true;
        CustomerSearchTextBox.Clear();
        _suppressCustomerSearch = false;
    }

    private async void OnCreateCustomerClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is null)
        {
            SetStatusMessage("No hay cajero autenticado.", isError: true);
            return;
        }

        var dialog = new CustomerCreateDialog(_customerService, _currentSession.CurrentEmployee.Id)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var customer = await _customerService.GetByIdAsync(dialog.CreatedCustomerId);
            if (customer is null || !customer.IsActive)
            {
                SetStatusMessage("El cliente se creó, pero no pudo seleccionarse.", isError: true);
                return;
            }

            SelectCustomer(customer);
            SetStatusMessage("Cliente creado y seleccionado.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo cargar el cliente recién creado");
            SetStatusMessage("No se pudo seleccionar el cliente creado.", isError: true);
        }
    }

    private void OnDteTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomerRequirementText is null)
        {
            return;
        }

        CustomerRequirementText.Text = GetSelectedDteType() == DteConstants.TiposDte.CreditoFiscal
            ? "Crédito fiscal: requiere cliente activo con NIT y NRC."
            : "Factura: cliente opcional.";
    }

    private string GetSelectedDteType() =>
        (DteTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? DteConstants.TiposDte.Factura;

    private static string BuildCustomerDisplay(Customer customer)
    {
        var identifiers = new[] { customer.Nit, customer.Nrc, customer.Dui }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        var identifierText = string.Join(" · ", identifiers);
        return string.IsNullOrWhiteSpace(identifierText)
            ? customer.Name
            : $"{customer.Name} ({identifierText})";
    }

    /// <summary>Agrega el producto seleccionado al carrito con la cantidad indicada.</summary>
    private void OnAgregarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ProductResultsListBox.SelectedItem is not InventoryProductResult selectedProduct)
        {
            SetStatusMessage("Seleccione un producto.", isError: true);
            return;
        }

        var quantityResult = DecimalInputParser.Parse(QuantityTextBox.Text, precision: 12, scale: 3, allowNegative: false);
        if (!quantityResult.IsValid)
        {
            SetStatusMessage(quantityResult.ErrorMessage!, isError: true);
            return;
        }

        var quantity = quantityResult.Value!.Value;

        if (quantity <= 0)
        {
            SetStatusMessage("La cantidad debe ser mayor que cero.", isError: true);
            return;
        }

        if (quantity > selectedProduct.CurrentStock)
        {
            SetStatusMessage("No hay stock suficiente para esa cantidad.", isError: true);
            return;
        }

        var existingLine = _cartLineItems.FirstOrDefault(line => line.ProductId == selectedProduct.Id);
        if (existingLine is not null)
        {
            if (existingLine.Quantity + quantity > selectedProduct.CurrentStock)
            {
                SetStatusMessage("No hay stock suficiente para acumular esa cantidad.", isError: true);
                return;
            }

            existingLine.Quantity += quantity;
            RefreshCartItems();
        }
        else
        {
            _cartLineItems.Add(new CartLineItem(selectedProduct, quantity));
        }

        QuantityTextBox.Text = "1";
        SetStatusMessage("Producto agregado.");
        RenderSaleTotals();
    }

    /// <summary>
    /// Registra la venta (Order + Payments) con el Employee cajero de la sesión actual.
    /// </summary>
    private async void OnFacturarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        await RegisterSaleAsync();
    }

    private async void OnChargeSaleCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        await RegisterSaleAsync();
        e.Handled = true;
    }

    private void OnChargeSaleCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = FacturarButton?.IsEnabled == true;
        e.Handled = true;
    }

    private async Task RegisterSaleAsync()
    {
        if (_currentSession.CurrentEmployee is null)
        {
            SetStatusMessage("No hay cajero autenticado.", isError: true);
            return;
        }

        if (_currentSession.ActiveCashSessionId is not Guid cashSessionId)
        {
            SetStatusMessage("Abra la caja antes de cobrar una venta.", isError: true);
            if (MessageBox.Show(
                    Window.GetWindow(this),
                    "No hay una caja abierta. ¿Desea abrirla ahora?",
                    "Abrir caja",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes
                || !await _cashSessionOpeningFlow.EnsureOpenAsync(Window.GetWindow(this)))
            {
                return;
            }

            if (_currentSession.ActiveCashSessionId is not Guid openedCashSessionId)
            {
                return;
            }

            cashSessionId = openedCashSessionId;
        }

        if (_cartLineItems.Count == 0)
        {
            SetStatusMessage("Agregue al menos un producto.", isError: true);
            return;
        }

        var paymentMethod = (PaymentMethodCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? SalesDomainConstants.PaymentMethods.Cash;
        var grandTotal = CalculateGrandTotal();
        var attemptId = _saleAttemptTracker.GetOrCreate(new SaleAttemptInput(
            _cartLineItems.Select(line => new SaleAttemptLine(line.ProductId, line.Quantity)).ToArray(),
            paymentMethod,
            grandTotal,
            _selectedCustomer?.Id,
            GetSelectedDteType()));
        FacturarButton.IsEnabled = false;

        try
        {
            var saleResult = await _orderService.CreateCashSaleAsync(new CreateCashSaleRequest(
                _currentSession.CurrentEmployee.Id,
                CashSessionId: cashSessionId,
                CustomerId: _selectedCustomer?.Id,
                ClientRequestId: attemptId,
                Lines: _cartLineItems.Select(line => new CashSaleLineRequest(line.ProductId, line.Quantity)).ToList(),
                Payments: new[] { new CashSalePaymentRequest(paymentMethod, grandTotal) },
                Notes: "Venta registrada desde WPF",
                DocumentType: GetSelectedDteType()));

            _lastOrderId = saleResult.OrderId;
            _saleAttemptTracker.MarkSucceeded(attemptId);
            ResetSaleState();
            await SearchProductsAsync();
            SetStatusMessage($"Venta registrada: {saleResult.OrderId}.");
            if (_printingOptions.AutoPrintOnSale)
            {
                await TryPrintOrderAsync(saleResult.OrderId);
            }
        }
        catch (InvalidOrderException exception)
        {
            _logger.LogWarning(exception, "Venta rechazada por una regla de caja u orden");
            SetStatusMessage(exception.Message, isError: true);
        }
        catch (UnauthorizedOperationException exception)
        {
            SetStatusMessage(exception.Message, isError: true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error técnico al registrar una venta");
            SetStatusMessage("Ocurrió un error técnico. Puede reintentar sin duplicar la venta.", isError: true);
        }
        finally
        {
            FacturarButton.IsEnabled = true;
        }
    }

    /// <summary>Imprime nuevamente el último comprobante guardado.</summary>
    private async void OnPrintLastClick(object sender, RoutedEventArgs e)
    {
        if (_lastOrderId is not Guid orderId)
        {
            SetStatusMessage("Aún no hay una venta para imprimir.", isError: true);
            return;
        }

        await TryPrintOrderAsync(orderId);
    }

    private async Task TryPrintOrderAsync(Guid orderId)
    {
        var result = await _saleReceiptPrinter.PrintSaleAsync(orderId);
        if (result.Status == SaleReceiptPrintStatus.Printed)
        {
            SetStatusMessage(result.UserMessage);
            return;
        }

        SetStatusMessage($"La venta se guardó, pero no se pudo imprimir: {result.UserMessage}", isError: true);
        var dialog = new PrintFailureDialog(result.UserMessage)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            await TryPrintOrderAsync(orderId);
        }
    }

    /// <summary>Vacía el carrito sin registrar venta.</summary>
    private void OnLimpiarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ConfirmAndClearSale();
    }

    /// <summary>Quita una línea concreta del carrito.</summary>
    private void OnRemoveCartLineClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CartLineItem cartLine })
        {
            return;
        }

        var confirmation = MessageBox.Show(
            Window.GetWindow(this),
            $"Se quitará la línea '{cartLine.Description}' ({cartLine.QuantityText}). ¿Desea continuar?",
            "Confirmar quitar línea",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _cartLineItems.Remove(cartLine);
        RenderSaleTotals();
        SetStatusMessage("Línea removida.");
    }

    private void OnFocusProductSearchCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ProductSearchTextBox.Focus();
        ProductSearchTextBox.SelectAll();
        ProductResultsListBox.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void OnCancelSaleCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (ProductResultsListBox.Visibility == Visibility.Visible)
        {
            ProductResultsListBox.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        if (CustomerResultsListBox.Visibility == Visibility.Visible)
        {
            CustomerResultsListBox.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        ConfirmAndClearSale();
        e.Handled = true;
    }

    private void ConfirmAndClearSale()
    {
        if (_cartLineItems.Count == 0)
        {
            ClearSelectedCustomer();
            return;
        }

        var confirmation = MessageBox.Show(
            Window.GetWindow(this),
            "Se perderán todas las líneas, el cliente y el tipo de documento de la venta en curso. ¿Desea cancelar?",
            "Confirmar cancelar venta",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        ResetSaleState();
        SetStatusMessage("Venta cancelada.");
    }

    private void ResetSaleState()
    {
        _cartLineItems.Clear();
        ClearSelectedCustomer();
        DteTypeCombo.SelectedIndex = 0;
        QuantityTextBox.Text = "1";
        RenderSaleTotals();
    }

    /// <summary>Fuerza refresco visual del ItemsControl del carrito.</summary>
    private void RefreshCartItems()
    {
        CartItemsControl.ItemsSource = null;
        CartItemsControl.ItemsSource = _cartLineItems;
    }

    /// <summary>Suma de líneas (precio × cantidad) antes de IVA.</summary>
    private decimal CalculateSubtotal()
    {
        return _cartLineItems.Sum(line => line.Subtotal);
    }

    /// <summary>Total con IVA incluido.</summary>
    private decimal CalculateGrandTotal()
    {
        return TaxAmountCalculator.CalculateGrandTotal(CalculateSubtotal());
    }

    /// <summary>Actualiza subtotal, IVA y total en la cabecera y pie de la vista.</summary>
    private void RenderSaleTotals()
    {
        var subtotal = CalculateSubtotal();
        var taxAmount = TaxAmountCalculator.CalculateTaxAmount(subtotal);
        var grandTotal = TaxAmountCalculator.CalculateGrandTotal(subtotal);

        HeaderTotalText.Text = NumberFormatter.Currency(grandTotal);
        SubtotalText.Text = $"Subtotal: {NumberFormatter.Currency(subtotal)}";
        TaxText.Text = $"IVA: {NumberFormatter.Currency(taxAmount)}";
        TotalText.Text = $"Total: {NumberFormatter.Currency(grandTotal)}";
    }

    /// <summary>Escribe un mensaje de estado (éxito o error) en la barra inferior.</summary>
    private void SetStatusMessage(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "AppError" : "AppSuccess");
    }

    /// <summary>Línea temporal del carrito de venta en mostrador (no persistida hasta facturar).</summary>
    private sealed class CartLineItem(InventoryProductResult product, decimal quantity)
    {
        public Guid ProductId { get; } = product.Id;
        public string Description { get; } = product.Description;
        public decimal UnitPrice { get; } = product.SalePrice;
        public decimal Quantity { get; set; } = quantity;
        public decimal Subtotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
        public string QuantityText => Quantity.ToString("N3");
        public string UnitPriceText => UnitPrice.ToString("C2");
        public string SubtotalText => Subtotal.ToString("C2");
    }
}
