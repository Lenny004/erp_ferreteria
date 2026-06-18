using System.Windows;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;

namespace FlexoCableSV.PuntoVenta.Views.Confeccion;

public partial class HistorialVentasView : UserControl
{
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private CancellationTokenSource? _loadCancellation;

    public HistorialVentasView(
        IOrderService orderService,
        ICurrentSessionService currentSession)
    {
        _orderService = orderService;
        _currentSession = currentSession;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadOrdersAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();

        try
        {
            var status = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "PENDIENTE";
            var orders = await _orderService.GetConfectionOrdersAsync(
                status,
                SearchTextBox.Text,
                cancellationToken: _loadCancellation.Token);

            OrdersListBox.ItemsSource = orders;
            EmptyStateText.Visibility = orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var today = DateTime.Today;
            OrdersTodayText.Text = orders.Count(o => o.CreatedAt.ToLocalTime().Date == today).ToString();
            PendingText.Text = orders.Count(o => o.Status == "PENDIENTE").ToString();
            CompletedText.Text = orders.Count(o => o.Status == "COMPLETADA").ToString();
            ShownText.Text = orders.Count.ToString();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EmptyStateText.Text = $"No se pudieron cargar ordenes: {ex.Message}";
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }

    private async void OnCompleteOrderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ConfectionOrderSummary order })
        {
            return;
        }

        if (order.Status != "PENDIENTE")
        {
            MessageBox.Show("Solo se pueden facturar ordenes pendientes.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_currentSession.CurrentEmployee is null)
        {
            MessageBox.Show("No hay cajero autenticado.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Facturar la orden {order.OrderNumber} por {order.TotalText}?",
            "Confeccion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _orderService.CompleteConfectionOrderAsync(new CompleteConfectionOrderRequest(
                order.OrderId,
                _currentSession.CurrentEmployee.Id,
                CashSessionId: null,
                Payments: new[] { new CashSalePaymentRequest("EFECTIVO", order.Total) }));

            await LoadOrdersAsync();
            MessageBox.Show("Orden facturada. DTE pendiente de Fase 4.", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo facturar la orden: {ex.Message}", "Confeccion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
