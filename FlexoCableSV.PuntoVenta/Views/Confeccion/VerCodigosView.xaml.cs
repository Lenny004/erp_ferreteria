using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Services.Domain;

namespace FlexoCableSV.PuntoVenta.Views.Confeccion;

/// <summary>Consulta de códigos de producto para el módulo de confección.</summary>
public partial class VerCodigosView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

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
        _searchCoordinator.Dispose();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadProductsAsync();
    }

    private void OnSelectedProductChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderSelectedProductDetails(ProductsListBox.SelectedItem as InventoryProductResult);
    }

    private async Task LoadProductsAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();

        try
        {
            var products = await _inventoryService.SearchProductsAsync(
                SearchTextBox.Text,
                stockStatus: null,
                cancellationToken: cancellationToken);

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

    private void RenderSelectedProductDetails(InventoryProductResult? selectedProduct)
    {
        if (selectedProduct is null)
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

        DetailCodeText.Text = selectedProduct.Code;
        DetailDescriptionText.Text = selectedProduct.Description;
        DetailFamilyText.Text = $"Familia: {selectedProduct.Family}";
        DetailMeasurementText.Text = $"Tipo medida: {selectedProduct.Measurement}";
        DetailUnitText.Text = $"Unidad: {selectedProduct.UnitLabel}";
        DetailStockText.Text = $"Stock actual: {selectedProduct.FormattedStock}";
        DetailPriceText.Text = $"Precio: {selectedProduct.PriceText}";
        DetailStatusText.Text = selectedProduct.DetailStatus;
    }
}
