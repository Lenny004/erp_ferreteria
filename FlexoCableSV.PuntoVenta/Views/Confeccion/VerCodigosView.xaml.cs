using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;

namespace FlexoCableSV.PuntoVenta.Views.Confeccion;

public partial class VerCodigosView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private CancellationTokenSource? _searchCancellation;

    public VerCodigosView(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await LoadProductsAsync();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadProductsAsync();
    }

    private void OnSelectedProductChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderSelectedProduct(ProductsListBox.SelectedItem as InventoryProductResult);
    }

    private async Task LoadProductsAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            var products = await _inventoryService.SearchProductsAsync(
                SearchTextBox.Text,
                null,
                cancellationToken: _searchCancellation.Token);

            ProductsListBox.ItemsSource = products;
            ProductsListBox.SelectedIndex = products.Count > 0 ? 0 : -1;
            EmptyStateText.Visibility = products.Count == 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EmptyStateText.Text = $"No se pudo consultar catalogo: {ex.Message}";
            EmptyStateText.Visibility = System.Windows.Visibility.Visible;
        }
    }

    private void RenderSelectedProduct(InventoryProductResult? product)
    {
        if (product is null)
        {
            DetailCodeText.Text = "Sin seleccion";
            DetailDescriptionText.Text = "Selecciona un codigo del listado.";
            DetailFamilyText.Text = "Familia: -";
            DetailMeasurementText.Text = "Tipo medida: -";
            DetailUnitText.Text = "Unidad: -";
            DetailStockText.Text = "Stock actual: -";
            DetailPriceText.Text = "Precio: -";
            DetailStatusText.Text = "SIN DATOS";
            return;
        }

        DetailCodeText.Text = product.Code;
        DetailDescriptionText.Text = product.Description;
        DetailFamilyText.Text = $"Familia: {product.Family}";
        DetailMeasurementText.Text = $"Tipo medida: {product.Measurement}";
        DetailUnitText.Text = $"Unidad: {product.UnitLabel}";
        DetailStockText.Text = $"Stock actual: {product.FormattedStock}";
        DetailPriceText.Text = $"Precio: {product.PriceText}";
        DetailStatusText.Text = product.DetailStatus;
    }
}
