using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Consulta de inventario con filtros por estado de stock.</summary>
public partial class ConsultarStockView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

    public ConsultarStockView(IInventoryService inventoryService)
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

    private async void OnActualizarClick(object sender, System.Windows.RoutedEventArgs e)
    {
        await LoadProductsAsync();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadProductsAsync();
    }

    private async void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();

        try
        {
            var selectedStockFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                ?? SalesDomainConstants.StockFilters.All;

            var products = await _inventoryService.SearchProductsAsync(
                SearchTextBox.Text,
                selectedStockFilter,
                cancellationToken: cancellationToken);

            ProductsItemsControl.ItemsSource = products;
            ActiveProductsText.Text = products.Count.ToString();
            StockLowText.Text = products.Count(product => product.Status == SalesDomainConstants.StockFilters.Low).ToString();
            OutOfStockText.Text = products.Count(product => product.Status == SalesDomainConstants.StockFilters.Depleted).ToString();
            LastUpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");
            EmptyStateText.Visibility = products.Count == 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EmptyStateText.Text = $"No se pudo consultar inventario: {ex.Message}";
            EmptyStateText.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
