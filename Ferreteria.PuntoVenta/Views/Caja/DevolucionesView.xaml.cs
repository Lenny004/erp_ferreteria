using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Returns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Vista guiada para buscar, calcular y confirmar devoluciones POS.</summary>
public partial class DevolucionesView : UserControl
{
    private readonly IReturnService _returnService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IReturnFiscalPolicy _fiscalPolicy;
    private readonly ReturnOptions _options;
    private readonly ILogger<DevolucionesView> _logger;
    private readonly Dictionary<Guid, ReturnableLine> _lines = new();
    private readonly Dictionary<Guid, decimal> _quantities = new();
    private readonly Dictionary<Guid, bool> _restock = new();
    private ReturnableSaleSummary? _selectedSale;
    private Guid? _pendingClientRequestId;

    /// <summary>Inicializa la vista y carga catálogos configurables.</summary>
    /// <param name="returnService">Servicio real de búsqueda y validación.</param>
    /// <param name="currentSession">Sesión del empleado autenticado.</param>
    /// <param name="fiscalPolicy">Política fiscal de vista previa.</param>
    /// <param name="options">Catálogo de motivos y reintegros.</param>
    /// <param name="logger">Logger para registrar fallos inesperados sin exponer detalles tecnicos.</param>
    public DevolucionesView(
        IReturnService returnService,
        ICurrentSessionService currentSession,
        IReturnFiscalPolicy fiscalPolicy,
        IOptions<ReturnOptions> options,
        ILogger<DevolucionesView> logger)
    {
        _returnService = returnService ?? throw new ArgumentNullException(nameof(returnService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        _fiscalPolicy = fiscalPolicy ?? throw new ArgumentNullException(nameof(fiscalPolicy));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeComponent();
        ReasonCombo.ItemsSource = _options.Motivos;
        ReasonCombo.SelectedIndex = 0;
        RefundCombo.ItemsSource = _options.MetodosReintegroPermitidos;
        RefundCombo.SelectedIndex = 0;
        AvailabilityText.Text = _returnService.Capabilities.CanConfirmReturns
            ? "La persistencia está disponible. La confirmación requiere PIN de autorización."
            : _returnService.Capabilities.Message;
        UpdateSummary();
    }

    /// <summary>Busca ventas completadas con el filtro visible.</summary>
    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is null)
        {
            TechnicalMessageText.Text = "No hay un empleado autenticado para buscar ventas.";
            return;
        }

        try
        {
            var results = await _returnService.SearchReturnableSalesAsync(
                new ReturnableSalesFilter(SearchText: SearchTextBox.Text),
                _currentSession.CurrentEmployee.Id);
            SalesList.ItemsSource = results;
            TechnicalMessageText.Text = results.Count == 0 ? "No se encontraron ventas disponibles." : string.Empty;
        }
        catch (Exception)
        {
            TechnicalMessageText.Text = "No se pudo buscar la venta. Revise la conexión e intente de nuevo.";
        }
    }

    /// <summary>Carga las líneas de la venta seleccionada y reinicia sus cantidades.</summary>
    private async void OnSaleSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SalesList.SelectedItem is not ReturnableSaleSummary sale
            || _currentSession.CurrentEmployee is null)
        {
            return;
        }

        try
        {
            _selectedSale = sale;
            var lines = await _returnService.GetReturnableLinesAsync(sale.OrderId, _currentSession.CurrentEmployee.Id);
            _lines.Clear();
            _quantities.Clear();
            _restock.Clear();
            foreach (var line in lines)
            {
                _lines[line.OrderDetailId] = line;
                _quantities[line.OrderDetailId] = 0m;
                _restock[line.OrderDetailId] = true;
            }

            SelectedSaleText.Text = $"Venta ORD-{sale.ShortOrderId} | {sale.CustomerDisplayName} | Total original ${sale.Total:0.00}";
            RenderLines();
            UpdateSummary();
        }
        catch (Exception)
        {
            TechnicalMessageText.Text = "No se pudieron cargar las líneas de la venta.";
        }
    }

    /// <summary>Actualiza el cálculo cuando cambia un motivo, observación o reintegro.</summary>
    private void OnInputChanged(object sender, RoutedEventArgs e)
    {
        UpdateSummary();
    }

    /// <summary>Actualiza el cálculo cuando cambia la observación.</summary>
    private void OnNotesChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSummary();
    }

    /// <summary>Solicita autorización, persiste la devolución y muestra el comprobante interno.</summary>
    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSale is null || _currentSession.CurrentEmployee is null || !ConfirmButton.IsEnabled)
        {
            return;
        }

        try
        {
            var requestLines = _quantities.Where(item => item.Value > 0m).Select(item => new ReturnLineRequest(item.Key, item.Value, _restock.GetValueOrDefault(item.Key, true))).ToArray();
            var method = RefundCombo.SelectedItem?.ToString() ?? ReturnDomainConstants.RefundMethods.None;
            var authorizationDialog = new ReturnAuthorizationDialog { Owner = Window.GetWindow(this) };
            if (authorizationDialog.ShowDialog() != true)
            {
                return;
            }

            _pendingClientRequestId ??= Guid.NewGuid();
            var previewRequest = new ReturnRequest(_pendingClientRequestId.Value, _selectedSale.OrderId, _currentSession.CurrentEmployee.Id, Guid.Empty, ReasonCombo.SelectedValue?.ToString() ?? string.Empty, NotesBox.Text, ReturnDomainConstants.RefundMethods.None, requestLines, 0m);
            var preview = ReturnCalculator.Calculate(previewRequest, _selectedSale, _lines.Values.ToArray(), null, _options);
            var request = previewRequest with { RefundMethod = method, RefundAmount = method.Equals(ReturnDomainConstants.RefundMethods.None, StringComparison.OrdinalIgnoreCase) ? 0m : preview.Total };
            var result = await _returnService.CreateReturnAsync(request, authorizationDialog.AuthorizerPin);
            var receipt = ReturnReceiptComposer.Compose(
                new ReturnReceiptData(
                    result.OrderId,
                    _selectedSale.ControlNumber,
                    _selectedSale.CreatedAtUtc,
                    result.Calculation.Lines.Select(line => new ReturnReceiptLine(
                        _lines.TryGetValue(line.OrderDetailId, out var source) ? source.ProductDescription : line.OrderDetailId.ToString("N"),
                        line.Quantity,
                        line.Total)).ToArray(),
                    result.Calculation.Subtotal,
                    result.Calculation.DiscountAmount,
                    result.Calculation.TaxAmount,
                    result.Calculation.Total,
                    request.RefundMethod,
                    request.ReasonCode,
                    $"{_currentSession.CurrentEmployee.FirstName} {_currentSession.CurrentEmployee.LastName}",
                    result.AuthorizedByEmployeeId == Guid.Empty ? "Autorizador validado" : result.AuthorizedByEmployeeId.ToString("N")[..8],
                    result.FiscalDecision.FiscalStatus,
                    _options.LeyendaComprobante),
                _options.AnchoComprobante);
            var previewWindow = new ReturnReceiptPreviewWindow(receipt) { Owner = Window.GetWindow(this) };
            previewWindow.ShowDialog();
            TechnicalMessageText.Text = $"Devolución registrada correctamente: {result.ReturnId}.";
            ResetAssistant();
        }
        catch (InvalidReturnException exception)
        {
            TechnicalMessageText.Text = exception.Message;
        }
        catch (ReturnsUnavailableException exception)
        {
            TechnicalMessageText.Text = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error no controlado al confirmar devolución");
            TechnicalMessageText.Text = "No se pudo confirmar la devolución. Revise la conexión e intente de nuevo.";
        }
    }

    private void RenderLines()
    {
        LinesPanel.Children.Clear();
        foreach (var line in _lines.Values)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            var description = new TextBlock
            {
                Text = $"{line.ProductCode} - {line.ProductDescription}\nDisponible: {line.AvailableQuantity:0.###}",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };
            Grid.SetColumn(description, 0);
            row.Children.Add(description);
            var restock = new CheckBox
            {
                Content = "Reingresa a inventario",
                IsChecked = _restock.GetValueOrDefault(line.OrderDetailId, true),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            restock.Checked += (_, _) => SetRestock(line.OrderDetailId, true);
            restock.Unchecked += (_, _) => SetRestock(line.OrderDetailId, false);
            Grid.SetColumn(restock, 1);
            row.Children.Add(restock);
            var minus = CreateQuantityButton("−", () => ChangeQuantity(line.OrderDetailId, -1m));
            Grid.SetColumn(minus, 2);
            row.Children.Add(minus);
            var quantity = new TextBlock
            {
                Text = QuantityText(line.OrderDetailId),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(quantity, 3);
            row.Children.Add(quantity);
            var plus = CreateQuantityButton("+", () => ChangeQuantity(line.OrderDetailId, 1m));
            Grid.SetColumn(plus, 4);
            row.Children.Add(plus);
            LinesPanel.Children.Add(row);
        }
    }

    private void SetRestock(Guid lineId, bool restock)
    {
        _restock[lineId] = restock;
        UpdateSummary();
    }

    private static Button CreateQuantityButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Width = 90,
            Height = 90,
            FontSize = 28,
            Margin = new Thickness(2)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void ChangeQuantity(Guid lineId, decimal delta)
    {
        if (!_lines.TryGetValue(lineId, out var line))
        {
            return;
        }

        var current = _quantities[lineId];
        var next = Math.Clamp(current + delta, 0m, line.AvailableQuantity);
        _quantities[lineId] = Math.Round(next, 3, MidpointRounding.AwayFromZero);
        RenderLines();
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (_selectedSale is null || _currentSession.CurrentEmployee is null)
        {
            ConfirmButton.IsEnabled = false;
            SubtotalText.Text = "Subtotal: $0.00";
            DiscountText.Text = "Descuento: $0.00";
            TaxText.Text = "IVA: $0.00";
            TotalText.Text = "Total crédito: $0.00";
            FiscalText.Text = string.Empty;
            return;
        }

        var requests = _quantities
            .Where(item => item.Value > 0m)
            .Select(item => new ReturnLineRequest(item.Key, item.Value, _restock.GetValueOrDefault(item.Key, true)))
            .ToArray();
        if (requests.Length == 0)
        {
            ConfirmButton.IsEnabled = false;
            TotalText.Text = "Total crédito: $0.00";
            FiscalText.Text = string.Empty;
            return;
        }

        try
        {
            var previewRequest = new ReturnRequest(
                Guid.NewGuid(),
                _selectedSale.OrderId,
                _currentSession.CurrentEmployee.Id,
                _currentSession.CurrentEmployee.Id,
                ReasonCombo.SelectedValue?.ToString() ?? string.Empty,
                NotesBox.Text,
                ReturnDomainConstants.RefundMethods.None,
                requests,
                0m);
            var preview = ReturnCalculator.Calculate(previewRequest, _selectedSale, _lines.Values.ToArray(), null, _options);
            var refundMethod = RefundCombo.SelectedItem?.ToString() ?? ReturnDomainConstants.RefundMethods.None;
            var calculation = refundMethod.Equals(ReturnDomainConstants.RefundMethods.None, StringComparison.OrdinalIgnoreCase)
                ? preview
                : ReturnCalculator.Calculate(previewRequest with { RefundMethod = refundMethod, RefundAmount = preview.Total }, _selectedSale, _lines.Values.ToArray(), null, _options);
            var fiscal = _fiscalPolicy.Decide(_selectedSale.DteType, calculation.ReturnType);
            SubtotalText.Text = $"Subtotal: ${calculation.Subtotal:0.00}";
            DiscountText.Text = $"Descuento: ${calculation.DiscountAmount:0.00}";
            TaxText.Text = $"IVA: ${calculation.TaxAmount:0.00}";
            TotalText.Text = $"Total crédito: ${calculation.Total:0.00} ({calculation.ReturnType})";
            FiscalText.Text = fiscal.UserMessage;
            ConfirmButton.IsEnabled = _returnService.Capabilities.CanConfirmReturns && calculation.Total > 0m;
            TechnicalMessageText.Text = string.Empty;
        }
        catch (InvalidReturnException exception)
        {
            ConfirmButton.IsEnabled = false;
            TechnicalMessageText.Text = exception.Message;
        }
    }

    private string QuantityText(Guid lineId) => _quantities.TryGetValue(lineId, out var quantity) ? NumberFormatter.Quantity(quantity) : "0";

    private void ResetAssistant()
    {
        _pendingClientRequestId = null;
        _selectedSale = null;
        _lines.Clear();
        _quantities.Clear();
        _restock.Clear();
        SalesList.SelectedItem = null;
        LinesPanel.Children.Clear();
        SelectedSaleText.Text = "Seleccione una venta para ver sus líneas";
        UpdateSummary();
    }
}
