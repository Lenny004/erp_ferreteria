using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Ferreteria.PuntoVenta.Services.Printing;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Time;
using Microsoft.Extensions.Logging;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Vista táctil para consultar, detallar y reimprimir ventas.</summary>
public partial class HistorialFacturasView : UserControl
{
    private const int PageSize = 25;
    private readonly ISalesHistoryService _history;
    private readonly ISaleReceiptPrinter _printer;
    private readonly ICurrentSessionService _session;
    private readonly ILogger<HistorialFacturasView> _logger;
    private readonly BusinessCalendar _calendar;
    private readonly AsyncSearchCoordinator _searchCoordinator = new();
    private SalesHistoryPage? _currentPage;
    private SalesHistoryDateShortcut _shortcut = SalesHistoryDateShortcut.Today;
    private DateTime? _fromUtc;
    private DateTime? _toUtc;
    private int _page = 1;

    /// <summary>Inicializa la vista con sus servicios.</summary>
    /// <param name="history">Servicio de consulta del historial.</param>
    /// <param name="printer">Servicio de impresión de comprobantes.</param>
    /// <param name="session">Sesión del empleado actual.</param>
    /// <param name="logger">Registrador de errores de la vista.</param>
    /// <param name="calendar">Calendario de negocio configurado.</param>
    public HistorialFacturasView(
        ISalesHistoryService history,
        ISaleReceiptPrinter printer,
        ICurrentSessionService session,
        ILogger<HistorialFacturasView> logger,
        BusinessCalendar calendar)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _printer = printer ?? throw new ArgumentNullException(nameof(printer));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        InitializeComponent();
        InitializeFilters();
        UpdateDateSelection(TodayButton);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void InitializeFilters()
    {
        AddFilterItem(OrderStatusCombo, "Completadas", SalesDomainConstants.OrderStatuses.Completed);
        AddFilterItem(OrderStatusCombo, "Todas", SalesDomainConstants.OrderStatuses.All);
        AddFilterItem(OrderStatusCombo, "Pendientes", SalesDomainConstants.OrderStatuses.Pending);
        AddFilterItem(OrderStatusCombo, "Canceladas", SalesDomainConstants.OrderStatuses.Cancelled);
        AddFilterItem(MhStatusCombo, "Todos", null);
        AddFilterItem(MhStatusCombo, "Procesado", DteConstants.EstadosMh.Procesado);
        AddFilterItem(MhStatusCombo, "Pendiente", DteConstants.EstadosMh.Pendiente);
        AddFilterItem(MhStatusCombo, "Rechazado", DteConstants.EstadosMh.Rechazado);
        AddFilterItem(MhStatusCombo, "Contingencia", DteConstants.EstadosMh.Contingencia);
        AddFilterItem(DteTypeCombo, "Todos", null);
        AddFilterItem(DteTypeCombo, "01 Factura", "01");
        AddFilterItem(DteTypeCombo, "03 Crédito fiscal", "03");
        AddFilterItem(DteTypeCombo, "05 NC", "05");
        AddFilterItem(DteTypeCombo, "06 ND", "06");
        AddFilterItem(DteTypeCombo, "11 FEX", "11");
        AddFilterItem(DteTypeCombo, "Sin DTE", SalesHistoryFilter.NoDteFilterValue);
        AddFilterItem(PaymentMethodCombo, "Todos", null);
        AddFilterItem(PaymentMethodCombo, "Efectivo", SalesDomainConstants.PaymentMethods.Cash);
        AddFilterItem(PaymentMethodCombo, "Tarjeta", SalesDomainConstants.PaymentMethods.Card);
        AddFilterItem(PaymentMethodCombo, "Transferencia", SalesDomainConstants.PaymentMethods.Transfer);
        AddFilterItem(PaymentMethodCombo, "Otro", SalesDomainConstants.PaymentMethods.Other);
        OrderStatusCombo.SelectedIndex = 0;
        MhStatusCombo.SelectedIndex = 0;
        DteTypeCombo.SelectedIndex = 0;
        PaymentMethodCombo.SelectedIndex = 0;
    }

    private static void AddFilterItem(ComboBox combo, string content, string? tag)
    {
        combo.Items.Add(new ComboBoxItem { Content = content, Tag = tag });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadAsync();
    private void OnUnloaded(object sender, RoutedEventArgs e) => _searchCoordinator.Dispose();

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            _page = 1;
            await LoadAsync();
        }
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && e.AddedItems.Count > 0)
        {
            _page = 1;
            await LoadAsync();
        }
    }

    private async void OnDateShortcutClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && Enum.TryParse(button.Tag?.ToString(), out SalesHistoryDateShortcut shortcut))
        {
            _shortcut = shortcut;
            _fromUtc = null;
            _toUtc = null;
            _page = 1;
            UpdateDateSelection(button);
            await LoadAsync();
        }
    }

    private async void OnRangeClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SalesHistoryRangeDialog(_calendar.Today()) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            var range = SalesHistoryFilter.CreateLocalDateRange(dialog.From, dialog.To, _calendar);
            _fromUtc = range.FromUtc;
            _toUtc = range.ToUtc;
            _shortcut = SalesHistoryDateShortcut.None;
            _page = 1;
            UpdateDateSelection(RangeButton);
            await LoadAsync();
        }
    }

    private async void OnPreviousPageClick(object sender, RoutedEventArgs e)
    {
        if (_currentPage?.HasPreviousPage == true)
        {
            _page--;
            await LoadAsync();
        }
    }

    private async void OnNextPageClick(object sender, RoutedEventArgs e)
    {
        if (_currentPage?.HasNextPage == true)
        {
            _page++;
            await LoadAsync();
        }
    }

    private async void OnReprintClick(object sender, RoutedEventArgs e)
    {
        if (SalesListBox.SelectedItem is not SalesHistoryRow selected)
        {
            MessageBox.Show(
                "Seleccione una venta para reimprimir.",
                "Historial",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(
                "¿Confirma reimprimir el comprobante seleccionado?",
                "Confirmar reimpresión",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _printer.PrintSaleAsync(selected.OrderId, true);
            var messageImage = result.Status == SaleReceiptPrintStatus.Printed
                ? MessageBoxImage.Information
                : MessageBoxImage.Warning;
            MessageBox.Show(
                result.UserMessage,
                "Reimpresión",
                MessageBoxButton.OK,
                messageImage);
            if (result.Status == SaleReceiptPrintStatus.Printed)
            {
                await LoadAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falló la reimpresión de la venta seleccionada.");
            MessageBox.Show("No se pudo reimprimir el comprobante.", "Reimpresión", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnDetailClick(object sender, RoutedEventArgs e) => await ShowDetailAsync();
    private async void OnSaleDoubleClick(object sender, MouseButtonEventArgs e) => await ShowDetailAsync();

    private async void OnSalesListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ShowDetailAsync();
        }
    }

    private async Task ShowDetailAsync()
    {
        if (SalesListBox.SelectedItem is not SalesHistoryRow selected || _session.CurrentEmployee is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var detail = await _history.GetDetailAsync(selected.OrderId, _session.CurrentEmployee.Id);
            if (detail is null)
            {
                MessageBox.Show("No tiene acceso a esta venta.", "Historial", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new SalesHistoryDetailWindow(detail) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falló la consulta del detalle de la venta.");
            MessageBox.Show("No se pudo cargar el detalle de la venta.", "Historial", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = SalesListBox.SelectedItem is SalesHistoryRow;
        ReprintButton.IsEnabled = hasSelection;
        DetailButton.IsEnabled = hasSelection;
    }

    private async Task LoadAsync()
    {
        var cancellationToken = _searchCoordinator.BeginNewSearch();
        SetBusy(true);
        LoadingText.Visibility = Visibility.Visible;
        EmptyStateText.Visibility = Visibility.Collapsed;
        try
        {
            var employee = _session.CurrentEmployee;
            if (employee is null)
            {
                SalesListBox.ItemsSource = Array.Empty<SalesHistoryRow>();
                EmptyStateText.Text = "No hay una sesión activa.";
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }

            var (fromUtc, toUtc) = _shortcut == SalesHistoryDateShortcut.None
                ? (_fromUtc, _toUtc)
                : SalesHistoryFilter.CreateShortcutRange(_shortcut, _calendar);
            var filter = new SalesHistoryFilter(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Shortcut: _shortcut,
                SearchText: SearchTextBox.Text,
                OrderStatus: GetTag(OrderStatusCombo),
                DteType: GetTag(DteTypeCombo),
                MhStatus: GetTag(MhStatusCombo),
                EmployeeId: null,
                PaymentMethod: GetTag(PaymentMethodCombo),
                Page: _page,
                PageSize: PageSize);
            _currentPage = await _history.SearchAsync(filter, employee.Id, cancellationToken);
            SalesListBox.ItemsSource = _currentPage.Rows;
            if (_currentPage.Rows.Count == 0
                && employee.CanCashier
                && _session.ActiveCashSessionId is null)
            {
                EmptyStateText.Text = "Abra caja para ver las ventas de su turno.";
            }
            else
            {
                EmptyStateText.Text = "No hay ventas para este filtro";
            }
            EmptyStateText.Visibility = _currentPage.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SalesCountText.Text = _currentPage.Summary.Sales.ToString();
            TotalBilledText.Text = _currentPage.Summary.Total.ToString("C2");
            TaxText.Text = _currentPage.Summary.Tax.ToString("C2");
            ContingencyText.Text = _currentPage.Summary.Contingencies.ToString();
            ReprintsText.Text = _currentPage.Summary.Reprints.ToString();
            PageText.Text = $"Página {_page}";
            PreviousPageButton.IsEnabled = _currentPage.HasPreviousPage;
            NextPageButton.IsEnabled = _currentPage.HasNextPage;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falló la consulta del historial de ventas.");
            EmptyStateText.Text = "No se pudo cargar el historial. Intente nuevamente.";
            EmptyStateText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingText.Visibility = Visibility.Collapsed;
            SetBusy(false);
        }
    }

    private static string? GetTag(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private void UpdateDateSelection(Button selectedButton)
    {
        TodayButton.FontWeight = FontWeights.Normal;
        YesterdayButton.FontWeight = FontWeights.Normal;
        WeekButton.FontWeight = FontWeights.Normal;
        MonthButton.FontWeight = FontWeights.Normal;
        RangeButton.FontWeight = FontWeights.Normal;
        selectedButton.FontWeight = FontWeights.Bold;
    }

    private void SetBusy(bool isBusy)
    {
        TodayButton.IsEnabled = !isBusy;
        YesterdayButton.IsEnabled = !isBusy;
        WeekButton.IsEnabled = !isBusy;
        MonthButton.IsEnabled = !isBusy;
        RangeButton.IsEnabled = !isBusy;
        OrderStatusCombo.IsEnabled = !isBusy;
        MhStatusCombo.IsEnabled = !isBusy;
        DteTypeCombo.IsEnabled = !isBusy;
        PaymentMethodCombo.IsEnabled = !isBusy;
        PreviousPageButton.IsEnabled = !isBusy && (_currentPage?.HasPreviousPage == true);
        NextPageButton.IsEnabled = !isBusy && (_currentPage?.HasNextPage == true);
        var hasSelection = SalesListBox.SelectedItem is SalesHistoryRow;
        ReprintButton.IsEnabled = !isBusy && hasSelection;
        DetailButton.IsEnabled = !isBusy && hasSelection;
    }
}
