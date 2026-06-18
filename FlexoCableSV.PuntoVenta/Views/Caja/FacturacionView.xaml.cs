using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;

namespace FlexoCableSV.PuntoVenta.Views.Caja;

public partial class FacturacionView : UserControl
{
    private const decimal IvaRate = 0.13m;

    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private readonly ObservableCollection<CartLine> _cartLines = new();
    private CancellationTokenSource? _searchCancellation;

    public FacturacionView(
        IInventoryService inventoryService,
        IOrderService orderService,
        ICurrentSessionService currentSession)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _currentSession = currentSession;

        InitializeComponent();
        CartItemsControl.ItemsSource = _cartLines;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RenderTotals();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await SearchProductsAsync();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
    }

    private async void OnProductSearchChanged(object sender, TextChangedEventArgs e)
    {
        await SearchProductsAsync();
    }

    private async Task SearchProductsAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            ProductResultsListBox.ItemsSource = await _inventoryService.SearchProductsAsync(
                ProductSearchTextBox.Text,
                null,
                take: 25,
                cancellationToken: _searchCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo buscar productos: {ex.Message}", isError: true);
        }
    }

    private void OnAgregarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ProductResultsListBox.SelectedItem is not InventoryProductResult product)
        {
            SetStatus("Seleccione un producto.", isError: true);
            return;
        }

        if (!decimal.TryParse(QuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity))
        {
            SetStatus("Ingrese una cantidad valida.", isError: true);
            return;
        }

        if (quantity <= 0)
        {
            SetStatus("La cantidad debe ser mayor que cero.", isError: true);
            return;
        }

        if (quantity > product.CurrentStock)
        {
            SetStatus("No hay stock suficiente para esa cantidad.", isError: true);
            return;
        }

        var existingLine = _cartLines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existingLine is not null)
        {
            if (existingLine.Quantity + quantity > product.CurrentStock)
            {
                SetStatus("No hay stock suficiente para acumular esa cantidad.", isError: true);
                return;
            }

            existingLine.Quantity += quantity;
            RefreshCart();
        }
        else
        {
            _cartLines.Add(new CartLine(product, quantity));
        }

        QuantityTextBox.Text = "1";
        SetStatus("Producto agregado.");
        RenderTotals();
    }

    private async void OnFacturarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is null)
        {
            SetStatus("No hay cajero autenticado.", isError: true);
            return;
        }

        if (_cartLines.Count == 0)
        {
            SetStatus("Agregue al menos un producto.", isError: true);
            return;
        }

        var paymentMethod = (PaymentMethodCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "EFECTIVO";
        var total = CalculateTotal();

        try
        {
            var result = await _orderService.CreateCashSaleAsync(new CreateCashSaleRequest(
                _currentSession.CurrentEmployee.Id,
                CashSessionId: null,
                CustomerId: null,
                ClientRequestId: Guid.NewGuid(),
                Lines: _cartLines.Select(l => new CashSaleLineRequest(l.ProductId, l.Quantity)).ToList(),
                Payments: new[] { new CashSalePaymentRequest(paymentMethod, total) },
                Notes: "Venta registrada desde WPF"));

            _cartLines.Clear();
            RenderTotals();
            await SearchProductsAsync();
            SetStatus($"Venta registrada: {result.OrderId}. DTE pendiente de Fase 4.");
        }
        catch (Exception ex)
        {
            SetStatus($"No se pudo registrar la venta: {ex.Message}", isError: true);
        }
    }

    private void OnLimpiarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _cartLines.Clear();
        RenderTotals();
        SetStatus("Orden limpiada.");
    }

    private void OnRemoveCartLineClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CartLine line })
        {
            return;
        }

        _cartLines.Remove(line);
        RenderTotals();
        SetStatus("Linea removida.");
    }

    private void RefreshCart()
    {
        CartItemsControl.ItemsSource = null;
        CartItemsControl.ItemsSource = _cartLines;
    }

    private decimal CalculateSubtotal()
    {
        return _cartLines.Sum(l => l.Subtotal);
    }

    private decimal CalculateTotal()
    {
        var subtotal = CalculateSubtotal();
        return subtotal + Math.Round(subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
    }

    private void RenderTotals()
    {
        var subtotal = CalculateSubtotal();
        var tax = Math.Round(subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;

        HeaderTotalText.Text = total.ToString("C2");
        SubtotalText.Text = $"Subtotal: {subtotal:C2}";
        TaxText.Text = $"IVA: {tax:C2}";
        TotalText.Text = $"Total: {total:C2}";
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "FlexoError" : "FlexoSuccess");
    }

    private sealed class CartLine(InventoryProductResult product, decimal quantity)
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
