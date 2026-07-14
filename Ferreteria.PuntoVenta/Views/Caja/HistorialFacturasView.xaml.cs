using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Historial de ventas completadas (mostrador y taller facturado).
/// </summary>
public partial class HistorialFacturasView : UserControl
{
    private readonly IOrderService _orderService;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

    public HistorialFacturasView(IOrderService orderService)
    {
        _orderService = orderService;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadCompletedSalesAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadCompletedSalesAsync();
    }

    private async Task LoadCompletedSalesAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();

        try
        {
            var completedSales = await _orderService.GetCompletedSalesAsync(
                SearchTextBox.Text,
                cancellationToken: cancellationToken);

            SalesListBox.ItemsSource = completedSales;
            EmptyStateText.Visibility = completedSales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var today = DateTime.Today;
            IssuedTodayText.Text = completedSales.Count(sale => sale.CreatedAt.ToLocalTime().Date == today).ToString();
            TotalBilledText.Text = completedSales.Sum(sale => sale.Total).ToString("C2");
            ShownText.Text = completedSales.Count.ToString();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EmptyStateText.Text = $"No se pudo cargar historial: {ex.Message}";
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }
}
