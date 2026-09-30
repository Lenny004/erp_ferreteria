using Microsoft.Extensions.Logging;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>Resultado de la impresión posterior a una venta ya guardada.</summary>
public enum SaleReceiptPrintStatus
{
    /// <summary>El ticket fue enviado.</summary>
    Printed,
    /// <summary>No existe impresora predeterminada.</summary>
    NoDefaultPrinter,
    /// <summary>No se pudo preparar el comprobante.</summary>
    CompositionFailed,
    /// <summary>Falló el envío a la impresora.</summary>
    PrinterFailed,
    /// <summary>El empleado no tiene acceso a la venta solicitada.</summary>
    NotAuthorized
}

/// <summary>Resultado detallado y seguro para mostrar al cajero.</summary>
/// <param name="Status">Estado final de la impresión.</param>
/// <param name="UserMessage">Mensaje seguro para mostrar al cajero.</param>
public sealed record SaleReceiptPrintResult(SaleReceiptPrintStatus Status, string UserMessage);

/// <summary>Orquesta consulta de impresora, composición e impresión sin afectar la venta guardada.</summary>
public interface ISaleReceiptPrinter
{
    /// <summary>Imprime una venta indicando si la operación es una reimpresión.</summary>
    /// <param name="orderId">Identificador de la venta guardada.</param>
    /// <param name="isReprint">True para incluir la leyenda y auditar la reimpresión.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado seguro para la UI.</returns>
    Task<SaleReceiptPrintResult> PrintSaleAsync(Guid orderId, bool isReprint = false, CancellationToken cancellationToken = default);
}

/// <summary>Implementación de impresión segura para ventas ya persistidas.</summary>
public sealed class SaleReceiptPrinter : ISaleReceiptPrinter
{
    private readonly IPrinterConfigService _printerConfig;
    private readonly IReceiptCompositionService _composition;
    private readonly IReceiptPrintService _printing;
    private readonly ILogger<SaleReceiptPrinter> _logger;
    private readonly ISalesHistoryService _salesHistory;
    private readonly IAuditService _auditService;
    private readonly ICurrentSessionService _currentSession;

    /// <summary>Inicializa la orquestación con sus dependencias de infraestructura.</summary>
    /// <param name="printerConfig">Servicio de configuración de impresoras.</param>
    /// <param name="composition">Servicio que compone el comprobante.</param>
    /// <param name="printing">Servicio que envía el comprobante a la impresora.</param>
    /// <param name="logger">Registrador de errores operativos.</param>
    /// <param name="salesHistory">Servicio que actualiza reimpresiones.</param>
    /// <param name="auditService">Servicio de auditoría.</param>
    /// <param name="currentSession">Sesión del empleado actual.</param>
    public SaleReceiptPrinter(
        IPrinterConfigService printerConfig,
        IReceiptCompositionService composition,
        IReceiptPrintService printing,
        ILogger<SaleReceiptPrinter> logger,
        ISalesHistoryService salesHistory,
        IAuditService auditService,
        ICurrentSessionService currentSession)
    {
        _printerConfig = printerConfig ?? throw new ArgumentNullException(nameof(printerConfig));
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _printing = printing ?? throw new ArgumentNullException(nameof(printing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _salesHistory = salesHistory ?? throw new ArgumentNullException(nameof(salesHistory));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
    }

    /// <summary>Imprime una venta y, si corresponde, registra e incrementa la reimpresión.</summary>
    /// <param name="orderId">Identificador de la venta.</param>
    /// <param name="isReprint">Indica que el cajero solicitó una reimpresión.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado seguro para mostrar al cajero.</returns>
    /// <remarks>El contador y la auditoría solo se ejecutan después de que la impresora acepta el ticket. Si la trazabilidad posterior falla, el ticket ya salió y el resultado conserva <c>Printed</c>.</remarks>
    public async Task<SaleReceiptPrintResult> PrintSaleAsync(Guid orderId, bool isReprint = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (isReprint)
            {
                var employee = _currentSession.CurrentEmployee;
                if (employee is null)
                {
                    _logger.LogWarning("Se rechazó la reimpresión {OrderId}: no hay empleado autenticado.", orderId);
                    return new(SaleReceiptPrintStatus.NotAuthorized, "Debe iniciar sesión para reimprimir este comprobante.");
                }

                if (!await _salesHistory.CanAccessOrderAsync(orderId, employee.Id, cancellationToken))
                {
                    _logger.LogWarning(
                        "Se rechazó la reimpresión {OrderId} para el empleado {EmployeeId}: venta fuera de alcance.",
                        orderId,
                        employee.Id);
                    return new(SaleReceiptPrintStatus.NotAuthorized, "No tiene permiso para reimprimir esta venta.");
                }
            }

            var printer = await _printerConfig.GetDefaultAsync(cancellationToken);
            if (printer is null)
            {
                return new(SaleReceiptPrintStatus.NoDefaultPrinter, "No hay impresora predeterminada configurada.");
            }

            var document = await _composition.ComposeForOrderAsync(orderId, isReprint, cancellationToken);
            if (document is null)
            {
                return new(SaleReceiptPrintStatus.CompositionFailed, "No se pudo preparar el comprobante.");
            }

            await _printing.PrintReceiptAsync(document,
                new PrinterConfig(printer.Name, printer.ConnectionType, printer.IpAddress, printer.NetworkPort, printer.PaperWidth),
                cancellationToken);
            if (isReprint)
            {
                try
                {
                    await _salesHistory.IncrementReprintsAsync(orderId, cancellationToken);
                    await _auditService.RecordChangeAsync(
                        SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint,
                        SalesDomainConstants.SalesHistoryAuditActions.OrdersTableName,
                        orderId.ToString(),
                        null,
                        new
                        {
                            Evento = SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprintEvent,
                            OrderId = orderId,
                            Resultado = "IMPRESO"
                        },
                        _currentSession.CurrentEmployee?.Id,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception,
                        "El ticket de la venta {OrderId} salió, pero falló su trazabilidad posterior.",
                        orderId);
                    return new(
                        SaleReceiptPrintStatus.Printed,
                        "Ticket enviado; no se pudo actualizar la trazabilidad de la reimpresión.");
                }
            }
            return new(SaleReceiptPrintStatus.Printed, "Ticket enviado a la impresora.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PrinterException ex)
        {
            _logger.LogError(ex, "Falló la impresión de la venta {OrderId}.", orderId);
            return new(SaleReceiptPrintStatus.PrinterFailed, "La impresora no respondió.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la preparación o configuración de impresión de la venta {OrderId}.", orderId);
            return new(SaleReceiptPrintStatus.CompositionFailed, "No se pudo preparar el comprobante.");
        }
    }
}
