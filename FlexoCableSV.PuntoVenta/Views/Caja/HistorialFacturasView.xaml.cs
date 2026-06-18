using System.Windows;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;

namespace FlexoCableSV.PuntoVenta.Views.Caja;

public partial class HistorialFacturasView : UserControl
{
    private readonly IOrderService _orderService;
    private CancellationTokenSource? _loadCancellation;

    public HistorialFacturasView(IOrderService orderService)
    {
        _orderService = orderService;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadSalesAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadSalesAsync();
    }

    private async Task LoadSalesAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();

        try
        {
            var sales = await _orderService.GetCompletedSalesAsync(
                SearchTextBox.Text,
                cancellationToken: _loadCancellation.Token);

            SalesListBox.ItemsSource = sales;
            EmptyStateText.Visibility = sales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var today = DateTime.Today;
            IssuedTodayText.Text = sales.Count(s => s.CreatedAt.ToLocalTime().Date == today).ToString();
            TotalBilledText.Text = sales.Sum(s => s.Total).ToString("C2");
            ShownText.Text = sales.Count.ToString();
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
