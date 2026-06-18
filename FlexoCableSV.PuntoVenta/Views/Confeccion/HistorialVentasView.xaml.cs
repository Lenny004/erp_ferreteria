using System.Windows;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Services.Domain;

namespace FlexoCableSV.PuntoVenta.Views.Confeccion;

/// <summary>
/// Bandeja de órdenes de confección (pendientes y completadas) con acción de facturación.
/// </summary>
public partial class HistorialVentasView : UserControl
{
    private readonly IOrderService _orderService;
    private readonly ICurrentSessionService _currentSession;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();

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
        await LoadConfectionOrdersAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _searchCoordinator.Dispose();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await LoadConfectionOrdersAsync();
    }

    private async void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadConfectionOrdersAsync();
    }

    private async Task LoadConfectionOrdersAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();

        try
        {
            var selectedStatus = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                ?? SalesDomainConstants.OrderStatuses.Pending;

            var orders = await _orderService.GetConfectionOrdersAsync(
                selectedStatus,
                SearchTextBox.Text,
                cancellationToken: cancellationToken);

            OrdersListBox.ItemsSource = orders;
            EmptyStateText.Visibility = orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var today = DateTime.Today;
            OrdersTodayText.Text = orders.Count(order => order.CreatedAt.ToLocalTime().Date == today).ToString();
            PendingText.Text = orders.Count(order => order.Status == SalesDomainConstants.OrderStatuses.Pending).ToString();
            CompletedText.Text = orders.Count(order => order.Status == SalesDomainConstants.OrderStatuses.Completed).ToString();
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
        if (sender is not Button { DataContext: ConfectionOrderSummary orderSummary })
        {
            return;
        }

        if (orderSummary.Status != SalesDomainConstants.OrderStatuses.Pending)
        {
            MessageBox.Show(
                "Solo se pueden facturar ordenes pendientes.",
                "Confeccion",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_currentSession.CurrentEmployee is null)
        {
            MessageBox.Show(
                "No hay cajero autenticado.",
                "Confeccion",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirmBilling = MessageBox.Show(
            $"Facturar la orden {orderSummary.OrderNumber} por {orderSummary.TotalText}?",
            "Confeccion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmBilling != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _orderService.CompleteConfectionOrderAsync(new CompleteConfectionOrderRequest(
                orderSummary.OrderId,
                _currentSession.CurrentEmployee.Id,
                CashSessionId: null,
                Payments: new[]
                {
                    new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, orderSummary.Total)
                }));

            await LoadConfectionOrdersAsync();
            MessageBox.Show(
                "Orden facturada. DTE pendiente de Fase 4.",
                "Confeccion",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo facturar la orden: {ex.Message}",
                "Confeccion",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
