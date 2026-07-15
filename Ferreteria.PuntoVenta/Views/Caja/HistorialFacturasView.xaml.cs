using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Historial de ventas completadas (Order en estado facturado): mostrador y taller.
/// Permite buscar y ver totales del día para el módulo Caja.
/// </summary>
public partial class HistorialFacturasView : UserControl
{
    private readonly IOrderService _orderService;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

    /// <summary>Inicializa el historial con el servicio de órdenes.</summary>
    public HistorialFacturasView(IOrderService orderService)
    {
        _orderService = orderService;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Carga ventas completadas al entrar a la vista.</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadCompletedSalesAsync();
    }

    /// <summary>Libera el coordinador de búsquedas al salir.</summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
    }

    /// <summary>Handler de búsqueda por texto (número, cliente, etc.).</summary>
    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadCompletedSalesAsync();
    }

    /// <summary>
    /// Obtiene ventas completadas y actualiza contadores (hoy, total facturado, mostrados).
    /// </summary>
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
