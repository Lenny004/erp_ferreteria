using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;

namespace FlexoCableSV.PuntoVenta.Views.Confeccion;

public partial class OrdenesConfeccionView : UserControl
{
    private const decimal IvaRate = 0.13m;

    private readonly IInventoryService _inventoryService;
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private readonly ObservableCollection<WorkOrderLine> _lines = new();

    public OrdenesConfeccionView(
        IInventoryService inventoryService,
        IOrderService orderService,
        ICurrentSessionService currentSession)
    {
        _inventoryService = inventoryService;
        _orderService = orderService;
        _currentSession = currentSession;

        InitializeComponent();
        LinesItemsControl.ItemsSource = _lines;
        Loaded += OnLoaded;
        RenderTotals();
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
        if (ProductComboBox.SelectedItem is not InventoryProductResult product)
        {
            MessageBox.Show("Seleccione un codigo.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(QuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) || quantity <= 0)
        {
            MessageBox.Show("Ingrese una cantidad valida.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            RefreshLines();
        }
        else
        {
            _lines.Add(new WorkOrderLine(product, quantity));
        }

        QuantityTextBox.Text = "1";
        RenderTotals();
    }

    private async void OnCreateOrderClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is null)
        {
            MessageBox.Show("No hay empleado autenticado.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_lines.Count == 0)
        {
            MessageBox.Show("Agregue al menos un codigo.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = await _orderService.CreateConfectionOrderAsync(new CreateConfectionOrderRequest(
                _currentSession.CurrentEmployee.Id,
                CustomerId: null,
                ClientRequestId: Guid.NewGuid(),
                CustomerName: CustomerNameTextBox.Text,
                CustomerPhone: null,
                Lines: _lines.Select(l => new CashSaleLineRequest(l.ProductId, l.Quantity)).ToList(),
                Notes: NotesTextBox.Text));

            _lines.Clear();
            CustomerNameTextBox.Clear();
            NotesTextBox.Clear();
            RenderTotals();

            MessageBox.Show($"Orden pendiente creada: {result.OrderId}", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo crear la orden: {ex.Message}", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        RenderTotals();
    }

    private void RefreshLines()
    {
        LinesItemsControl.ItemsSource = null;
        LinesItemsControl.ItemsSource = _lines;
    }

    private void RenderTotals()
    {
        var subtotal = _lines.Sum(l => l.Subtotal);
        var tax = Math.Round(subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;

        ItemsText.Text = _lines.Count.ToString();
        SubtotalText.Text = subtotal.ToString("C2");
        TaxText.Text = tax.ToString("C2");
        TotalText.Text = total.ToString("C2");
    }

    private sealed class WorkOrderLine(InventoryProductResult product, decimal quantity)
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
