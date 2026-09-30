using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
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
    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private readonly ISaleReceiptPrinter _saleReceiptPrinter;
    private readonly PrintingOptions _printingOptions;
    private readonly ILogger<FacturacionView> _logger;
    private readonly CashSessionOpeningFlow _cashSessionOpeningFlow;
    private Guid? _lastOrderId;
    private readonly ObservableCollection<CartLineItem> _cartLineItems = new();
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

    /// <summary>Inicializa la vista de facturación y enlaza el carrito a la UI.</summary>
    public FacturacionView(
        IInventoryService inventoryService,
        IOrderService orderService,
        ICurrentSessionService currentSession,
        ISaleReceiptPrinter saleReceiptPrinter,
        IOptions<PrintingOptions> printingOptions,
        ILogger<FacturacionView> logger,
        CashSessionOpeningFlow cashSessionOpeningFlow)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
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
        await SearchProductsAsync();
    }

    /// <summary>Libera el coordinador de búsquedas asíncronas al salir de la vista.</summary>
    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
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

    /// <summary>Agrega el producto seleccionado al carrito con la cantidad indicada.</summary>
    private void OnAgregarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ProductResultsListBox.SelectedItem is not InventoryProductResult selectedProduct)
        {
            SetStatusMessage("Seleccione un producto.", isError: true);
            return;
        }

        if (!decimal.TryParse(QuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity))
        {
            SetStatusMessage("Ingrese una cantidad valida.", isError: true);
            return;
        }

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

        try
        {
            var saleResult = await _orderService.CreateCashSaleAsync(new CreateCashSaleRequest(
                _currentSession.CurrentEmployee.Id,
                CashSessionId: cashSessionId,
                CustomerId: null,
                ClientRequestId: Guid.NewGuid(),
                Lines: _cartLineItems.Select(line => new CashSaleLineRequest(line.ProductId, line.Quantity)).ToList(),
                Payments: new[] { new CashSalePaymentRequest(paymentMethod, grandTotal) },
                Notes: "Venta registrada desde WPF"));

            _lastOrderId = saleResult.OrderId;
            _cartLineItems.Clear();
            RenderSaleTotals();
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error técnico al registrar una venta");
            SetStatusMessage("No se pudo registrar la venta. Revise la caja e intente nuevamente.", isError: true);
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
        _cartLineItems.Clear();
        RenderSaleTotals();
        SetStatusMessage("Orden limpiada.");
    }

    /// <summary>Quita una línea concreta del carrito.</summary>
    private void OnRemoveCartLineClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CartLineItem cartLine })
        {
            return;
        }

        _cartLineItems.Remove(cartLine);
        RenderSaleTotals();
        SetStatusMessage("Linea removida.");
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

        HeaderTotalText.Text = grandTotal.ToString("C2");
        SubtotalText.Text = $"Subtotal: {subtotal:C2}";
        TaxText.Text = $"IVA: {taxAmount:C2}";
        TotalText.Text = $"Total: {grandTotal:C2}";
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
