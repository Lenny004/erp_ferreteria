using Microsoft.Extensions.Logging;

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
    PrinterFailed
}

/// <summary>Resultado detallado y seguro para mostrar al cajero.</summary>
public sealed record SaleReceiptPrintResult(SaleReceiptPrintStatus Status, string UserMessage);

/// <summary>Orquesta consulta de impresora, composición e impresión sin afectar la venta guardada.</summary>
public interface ISaleReceiptPrinter
{
    /// <summary>Intenta imprimir una venta y convierte fallos técnicos en un resultado de usuario.</summary>
    /// <param name="orderId">Identificador de la venta guardada.</param>
    /// <param name="cancellationToken">Token de cancelación del flujo de UI.</param>
    /// <returns>Resultado sin lanzar fallos técnicos.</returns>
    /// <remarks>La cancelación explícita sí se propaga; los demás errores se registran y se encapsulan.</remarks>
    Task<SaleReceiptPrintResult> PrintSaleAsync(Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>Implementación de impresión segura para ventas ya persistidas.</summary>
public sealed class SaleReceiptPrinter : ISaleReceiptPrinter
{
    private readonly IPrinterConfigService _printerConfig;
    private readonly IReceiptCompositionService _composition;
    private readonly IReceiptPrintService _printing;
    private readonly ILogger<SaleReceiptPrinter> _logger;

    /// <summary>Inicializa la orquestación con sus dependencias de infraestructura.</summary>
    public SaleReceiptPrinter(
        IPrinterConfigService printerConfig,
        IReceiptCompositionService composition,
        IReceiptPrintService printing,
        ILogger<SaleReceiptPrinter> logger)
    {
        _printerConfig = printerConfig ?? throw new ArgumentNullException(nameof(printerConfig));
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _printing = printing ?? throw new ArgumentNullException(nameof(printing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SaleReceiptPrintResult> PrintSaleAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var printer = await _printerConfig.GetDefaultAsync(cancellationToken);
            if (printer is null)
            {
                return new(SaleReceiptPrintStatus.NoDefaultPrinter, "No hay impresora predeterminada configurada.");
            }

            var document = await _composition.ComposeForOrderAsync(orderId, cancellationToken);
            if (document is null)
            {
                return new(SaleReceiptPrintStatus.CompositionFailed, "No se pudo preparar el comprobante.");
            }

            await _printing.PrintReceiptAsync(document,
                new PrinterConfig(printer.Name, printer.ConnectionType, printer.IpAddress, printer.NetworkPort, printer.PaperWidth),
                cancellationToken);
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
