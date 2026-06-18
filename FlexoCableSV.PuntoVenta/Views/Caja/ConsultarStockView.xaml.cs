using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;

namespace FlexoCableSV.PuntoVenta.Views.Caja;

public partial class ConsultarStockView : UserControl
{
    private readonly IInventoryService _inventoryService;
    private CancellationTokenSource? _searchCancellation;

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
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
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
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            var selectedStatus = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TODOS";
            var products = await _inventoryService.SearchProductsAsync(
                SearchTextBox.Text,
                selectedStatus,
                cancellationToken: _searchCancellation.Token);

            ProductsItemsControl.ItemsSource = products;
            ActiveProductsText.Text = products.Count.ToString();
            StockLowText.Text = products.Count(p => p.Status == "BAJO").ToString();
            OutOfStockText.Text = products.Count(p => p.Status == "AGOTADO").ToString();
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
