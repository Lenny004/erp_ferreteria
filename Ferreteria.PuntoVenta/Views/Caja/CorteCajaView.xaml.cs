using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Printing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Presenta el resumen, conciliación y cierre del turno de caja activo.</summary>
/// <remarks>
/// La autorización y el cálculo definitivo se repiten en <see cref="ICashSessionService"/> dentro de PostgreSQL.
/// Esta vista solo coordina la captura táctil, la presentación y la impresión posterior al cierre.
/// </remarks>
public partial class CorteCajaView : UserControl
{
    private readonly ICashSessionService _cashSessionService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IPrinterConfigService _printerConfigService;
    private readonly IReceiptPrintService _receiptPrintService;
    private readonly CashRegisterOptions _options;
    private readonly ILogger<CorteCajaView> _logger;
    private CashSession? _openSession;
    private CashRegisterSummary? _summary;
    private Guid? _sessionId;
    private string? _lastReportText;
    private bool _turnClosed;

    /// <summary>Inicializa la vista de corte con servicios resueltos por DI.</summary>
    /// <param name="cashSessionService">Servicio transaccional de caja.</param>
    /// <param name="currentSession">Sesión del empleado autenticado.</param>
    /// <param name="printerConfigService">Consulta de impresora predeterminada.</param>
    /// <param name="receiptPrintService">Servicio de impresión del reporte.</param>
    /// <param name="options">Configuración de caja y ancho del reporte.</param>
    /// <param name="logger">Registrador técnico.</param>
    public CorteCajaView(
        ICashSessionService cashSessionService,
        ICurrentSessionService currentSession,
        IPrinterConfigService printerConfigService,
        IReceiptPrintService receiptPrintService,
        IOptions<CashRegisterOptions> options,
        ILogger<CorteCajaView> logger)
    {
        _cashSessionService = cashSessionService ?? throw new ArgumentNullException(nameof(cashSessionService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        _printerConfigService = printerConfigService ?? throw new ArgumentNullException(nameof(printerConfigService));
        _receiptPrintService = receiptPrintService ?? throw new ArgumentNullException(nameof(receiptPrintService));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeComponent();
    }

    /// <summary>Carga la sesión abierta de la caja y su resumen desde el servidor.</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    /// <summary>Obtiene y presenta el resumen autorizado del turno.</summary>
    /// <remarks>El servicio vuelve a consultar empleado, sesión, ventas y pagos en servidor.</remarks>
    private async Task LoadAsync()
    {
        var employee = _currentSession.CurrentEmployee;
        if (employee is null)
        {
            SetStatus("No hay un empleado autenticado.", true);
            return;
        }

        try
        {
            if (_sessionId is null)
            {
                var activeSessionId = _currentSession.ActiveCashSessionId;
                if (activeSessionId is Guid localSessionId)
                {
                    _openSession = await _cashSessionService.GetOpenSessionAsync(_options.Codigo);
                    if (_openSession?.Id != localSessionId)
                    {
                        _currentSession.ClearActiveCashSession();
                        SetStatus("El turno local ya no está abierto en el servidor.", true);
                        return;
                    }

                    _sessionId = localSessionId;
                }
                else
                {
                    _openSession = await _cashSessionService.GetOpenSessionAsync(_options.Codigo);
                    if (_openSession is null)
                    {
                        SetStatus("No hay una caja abierta para consultar.", true);
                        return;
                    }

                    _sessionId = _openSession.Id;
                    if (_openSession.EmployeeId == employee.Id)
                    {
                        _currentSession.SetActiveCashSession(_openSession.Id);
                    }
                }
            }

            _summary = await _cashSessionService.GetSummaryAsync(_sessionId.Value, employee.Id);
            RenderSummary();
            CloseButton.IsEnabled = !_turnClosed;
            CalculateButton.IsEnabled = !_turnClosed;
            SetStatus(string.Empty);
        }
        catch (CashSessionException exception)
        {
            _logger.LogWarning(exception, "No se pudo cargar el corte para {EmployeeId}", employee.Id);
            SetStatus("No se pudo cargar el corte. Verifique que el empleado esté activo y autorizado y que la sesión siga abierta.", true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error técnico al cargar el corte de caja");
            SetStatus("No se pudo cargar el corte. Intente nuevamente.", true);
        }
    }

    /// <summary>Calcula la diferencia visible a partir del efectivo contado.</summary>
    private void OnCalculateClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadDeclaredCash(out var declaredCash) || _summary is null)
        {
            return;
        }

        var difference = CashRegisterCalculator.CalculateDifference(
            _summary.ExpectedCash,
            declaredCash,
            _options.UmbralDiferencia);
        DifferenceText.Text = $"Diferencia: {difference.DisplayText}";
        DifferenceText.Foreground = (System.Windows.Media.Brush)FindResource(
            difference.Classification == CashDifferenceClassification.Balanced ? "AppSuccess" : "AppWarning");
    }

    /// <summary>Confirma y cierra el turno, luego conserva e imprime el reporte interno.</summary>
    /// <remarks>El cierre se recalcula y audita atómicamente en una transacción Serializable.</remarks>
    private async void OnCloseClick(object sender, RoutedEventArgs e)
    {
        var employee = _currentSession.CurrentEmployee;
        if (employee is null || _sessionId is not Guid sessionId || _summary is null || _openSession is null)
        {
            SetStatus("No hay un turno abierto para cerrar.", true);
            return;
        }

        if (!TryReadDeclaredCash(out var declaredCash))
        {
            return;
        }

        var difference = CashRegisterCalculator.CalculateDifference(
            _summary.ExpectedCash,
            declaredCash,
            _options.UmbralDiferencia);
        if (difference.RequiresObservation && string.IsNullOrWhiteSpace(NotesTextBox.Text))
        {
            SetStatus("Registre una observación cuando la diferencia supera el umbral configurado.", true);
            return;
        }

        if (MessageBox.Show(
                Window.GetWindow(this),
                $"¿Confirma cerrar el turno? {difference.DisplayText}.",
                "Confirmar cierre",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            CloseButton.IsEnabled = false;
            var result = await _cashSessionService.CloseAsync(
                sessionId,
                declaredCash,
                NotesTextBox.Text,
                employee.Id);
            var report = CashRegisterReportComposer.Compose(
                new CashRegisterReportData(
                    _openSession.CashRegisterCode,
                    _openSession.Employee is null
                        ? $"Empleado {_openSession.EmployeeId.ToString()[..8]}"
                        : $"{_openSession.Employee.FirstName} {_openSession.Employee.LastName}",
                    _openSession.OpenedAt,
                    result.ClosedAtUtc,
                    result.DeclaredCash,
                    result.Summary,
                    NotesTextBox.Text),
                _options.AnchoReporte);

            _lastReportText = report;
            _currentSession.ClearActiveCashSession();

            _sessionId = null;
            _summary = result.Summary;
            _turnClosed = true;
            RenderSummary();
            CloseButton.IsEnabled = false;
            CalculateButton.IsEnabled = false;
            ReprintButton.IsEnabled = true;
            SetStatus("Turno cerrado. El cobro permanece bloqueado hasta abrir otra caja.");
            await TryPrintReportAsync(report);
        }
        catch (CashSessionException exception)
        {
            _logger.LogWarning(exception, "No se pudo cerrar la sesión {SessionId}", sessionId);
            SetStatus("No se pudo cerrar el turno. Verifique que el empleado esté activo y autorizado y que el turno siga abierto.", true);
            CloseButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error técnico al cerrar la sesión {SessionId}", sessionId);
            SetStatus("No se pudo cerrar el turno. Verifique el estado e intente nuevamente.", true);
            CloseButton.IsEnabled = true;
        }
    }

    /// <summary>Lee y valida el monto contado con la misma regla del servicio.</summary>
    /// <param name="declaredCash">Monto normalizado.</param>
    /// <returns><c>true</c> cuando la entrada es válida.</returns>
    private bool TryReadDeclaredCash(out decimal declaredCash)
    {
        if (!decimal.TryParse(CashCountKeypad.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            && !decimal.TryParse(CashCountKeypad.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            declaredCash = 0m;
            SetStatus("Ingrese un efectivo contado válido.", true);
            return false;
        }

        try
        {
            declaredCash = CashRegisterInputRules.ValidateAmount(amount, "El efectivo contado", _options.MontoMaximo);
            return true;
        }
        catch (ArgumentException exception)
        {
            _logger.LogInformation(exception, "Monto contado rechazado por validación");
            declaredCash = 0m;
            SetStatus("El efectivo contado debe ser un monto válido dentro del límite configurado.", true);
            return false;
        }
    }

    /// <summary>Presenta los totales y movimientos del resumen autorizado.</summary>
    private void RenderSummary()
    {
        if (_summary is null)
        {
            return;
        }

        SalesText.Text = _summary.TotalSold.ToString("C2");
        CashText.Text = _summary.CashPayments.ToString("C2");
        CardText.Text = _summary.CardPayments.ToString("C2");
        TransferText.Text = _summary.TransferPayments.ToString("C2");
        DteText.Text = _summary.DteCount.ToString(CultureInfo.InvariantCulture);
        SessionText.Text = $"Esperado: {_summary.ExpectedCash:C2}";
        MovementItemsControl.ItemsSource = _summary.Movements;
    }

    /// <summary>Reintenta imprimir el reporte del último cierre.</summary>
    private async void OnReprintClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastReportText))
        {
            SetStatus("No hay un reporte de cierre reciente para reimprimir.", true);
            return;
        }

        ReprintButton.IsEnabled = false;
        try
        {
            await TryPrintReportAsync(_lastReportText);
        }
        finally
        {
            ReprintButton.IsEnabled = true;
        }
    }

    /// <summary>Intenta imprimir el reporte y muestra una vista previa si no existe impresora o falla.</summary>
    /// <remarks>La impresión es posterior al cierre y nunca modifica ni revierte la sesión persistida.</remarks>
    private async Task TryPrintReportAsync(string report)
    {
        try
        {
            var printer = await _printerConfigService.GetDefaultAsync();
            if (printer is null)
            {
                ShowPreview(report, "No hay una impresora configurada. Se muestra la vista previa del reporte.");
                return;
            }

            await _receiptPrintService.PrintTextReportAsync(
                "CORTE DE CAJA",
                report,
                new PrinterConfig(printer.Name, printer.ConnectionType, printer.IpAddress, printer.NetworkPort, printer.PaperWidth));
            SetStatus("La impresora aceptó el reporte de corte.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo imprimir el reporte de corte");
            ShowPreview(report, "La impresora no aceptó el reporte. Se muestra la vista previa para que pueda revisarlo.");
        }
    }

    /// <summary>Muestra el reporte en pantalla con una explicación operacional.</summary>
    /// <param name="report">Texto del reporte guardado.</param>
    /// <param name="message">Mensaje que explica por qué se usa la vista previa.</param>
    private void ShowPreview(string report, string message)
    {
        SetStatus(message, true);
        try
        {
            new TicketPreviewDialog(report)
            {
                Owner = Window.GetWindow(this)
            }.ShowDialog();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo mostrar la vista previa del reporte de corte");
            SetStatus("No se pudo imprimir ni mostrar la vista previa del reporte; el cierre ya quedó guardado.", true);
        }
    }

    /// <summary>Actualiza el mensaje visible sin exponer detalles técnicos.</summary>
    /// <param name="message">Mensaje operacional seguro.</param>
    /// <param name="isError">Indica si se presenta como error.</param>
    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "AppError" : "AppSuccess");
    }
}
