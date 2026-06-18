using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Services.Domain;

namespace FlexoCableSV.PuntoVenta.Views.Caja;

/// <summary>
/// Punto de venta de mostrador: búsqueda de productos, carrito y registro de venta.
/// </summary>
public partial class FacturacionView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private readonly ObservableCollection<CartLineItem> _cartLineItems = new();
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

    public FacturacionView(
        IInventoryService inventoryService,
        IOrderService orderService,
        ICurrentSessionService currentSession)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _currentSession = currentSession;

        InitializeComponent();
        CartItemsControl.ItemsSource = _cartLineItems;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RenderSaleTotals();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await SearchProductsAsync();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
    }

    private async void OnProductSearchChanged(object sender, TextChangedEventArgs e)
    {
        await SearchProductsAsync();
    }

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
            SetStatusMessage($"No se pudo buscar productos: {ex.Message}", isError: true);
        }
    }

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

    private async void OnFacturarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is null)
        {
            SetStatusMessage("No hay cajero autenticado.", isError: true);
            return;
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
                CashSessionId: null,
                CustomerId: null,
                ClientRequestId: Guid.NewGuid(),
                Lines: _cartLineItems.Select(line => new CashSaleLineRequest(line.ProductId, line.Quantity)).ToList(),
                Payments: new[] { new CashSalePaymentRequest(paymentMethod, grandTotal) },
                Notes: "Venta registrada desde WPF"));

            _cartLineItems.Clear();
            RenderSaleTotals();
            await SearchProductsAsync();
            SetStatusMessage($"Venta registrada: {saleResult.OrderId}. DTE pendiente de Fase 4.");
        }
        catch (Exception ex)
        {
            SetStatusMessage($"No se pudo registrar la venta: {ex.Message}", isError: true);
        }
    }

    private void OnLimpiarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _cartLineItems.Clear();
        RenderSaleTotals();
        SetStatusMessage("Orden limpiada.");
    }

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

    private void RefreshCartItems()
    {
        CartItemsControl.ItemsSource = null;
        CartItemsControl.ItemsSource = _cartLineItems;
    }

    private decimal CalculateSubtotal()
    {
        return _cartLineItems.Sum(line => line.Subtotal);
    }

    private decimal CalculateGrandTotal()
    {
        return TaxAmountCalculator.CalculateGrandTotal(CalculateSubtotal());
    }

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

    private void SetStatusMessage(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "FlexoError" : "FlexoSuccess");
    }

    /// <summary>Línea temporal del carrito de venta en mostrador.</summary>
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
