using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Services.Domain;

namespace FlexoCableSV.PuntoVenta.Views.Confeccion;

/// <summary>
/// Captura de órdenes de taller. La orden queda pendiente hasta facturación en caja.
/// </summary>
public partial class OrdenesConfeccionView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private readonly ObservableCollection<WorkOrderLineItem> _lineItems = new();

    public OrdenesConfeccionView(
        IInventoryService inventoryService,
        IOrderService orderService,
        ICurrentSessionService currentSession)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _currentSession = currentSession;

        InitializeComponent();
        LinesItemsControl.ItemsSource = _lineItems;
        Loaded += OnLoaded;
        RenderOrderTotals();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ProductComboBox.ItemsSource = await _inventoryService.SearchProductsAsync(null, null, take: 500);
            ProductComboBox.SelectedIndex = ProductComboBox.Items.Count > 0 ? 0 : -1;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar catalogo: {ex.Message}", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnAddLineClick(object sender, RoutedEventArgs e)
    {
        if (ProductComboBox.SelectedItem is not InventoryProductResult selectedProduct)
        {
            MessageBox.Show("Seleccione un codigo.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(QuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity)
            || quantity <= 0)
        {
            MessageBox.Show("Ingrese una cantidad valida.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existingLine = _lineItems.FirstOrDefault(line => line.ProductId == selectedProduct.Id);
        if (existingLine is not null)
        {
            existingLine.Quantity += quantity;
            RefreshLineItems();
        }
        else
        {
            _lineItems.Add(new WorkOrderLineItem(selectedProduct, quantity));
        }

        QuantityTextBox.Text = "1";
        RenderOrderTotals();
    }

    private async void OnCreateOrderClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is null)
        {
            MessageBox.Show("No hay empleado autenticado.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_lineItems.Count == 0)
        {
            MessageBox.Show("Agregue al menos un codigo.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var createdOrder = await _orderService.CreateConfectionOrderAsync(new CreateConfectionOrderRequest(
                _currentSession.CurrentEmployee.Id,
                CustomerId: null,
                ClientRequestId: Guid.NewGuid(),
                CustomerName: CustomerNameTextBox.Text,
                CustomerPhone: null,
                Lines: _lineItems.Select(line => new CashSaleLineRequest(line.ProductId, line.Quantity)).ToList(),
                Notes: NotesTextBox.Text));

            _lineItems.Clear();
            CustomerNameTextBox.Clear();
            NotesTextBox.Clear();
            RenderOrderTotals();

            MessageBox.Show(
                $"Orden pendiente creada: {createdOrder.OrderId}",
                "Confeccion",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo crear la orden: {ex.Message}", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _lineItems.Clear();
        RenderOrderTotals();
    }

    private void RefreshLineItems()
    {
        LinesItemsControl.ItemsSource = null;
        LinesItemsControl.ItemsSource = _lineItems;
    }

    private void RenderOrderTotals()
    {
        var subtotal = _lineItems.Sum(line => line.Subtotal);
        var taxAmount = TaxAmountCalculator.CalculateTaxAmount(subtotal);
        var grandTotal = TaxAmountCalculator.CalculateGrandTotal(subtotal);

        ItemsText.Text = _lineItems.Count.ToString();
        SubtotalText.Text = subtotal.ToString("C2");
        TaxText.Text = taxAmount.ToString("C2");
        TotalText.Text = grandTotal.ToString("C2");
    }

    /// <summary>Línea editable de la orden en memoria antes de persistir.</summary>
    private sealed class WorkOrderLineItem(InventoryProductResult product, decimal quantity)
    {
        public Guid ProductId { get; } = product.Id;
        public string Description { get; } = $"{product.Code} {product.Description}";
        public string Measurement { get; } = product.Measurement;
        public decimal UnitPrice { get; } = product.SalePrice;
        public decimal Quantity { get; set; } = quantity;
        public decimal Subtotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
        public string QuantityText => Quantity.ToString("N3");
        public string SubtotalText => Subtotal.ToString("C2");
    }
}
