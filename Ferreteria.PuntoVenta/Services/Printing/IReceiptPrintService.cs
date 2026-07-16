namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Servicio de impresión térmica ESC/POS para comprobantes DTE y reportes de caja.
/// </summary>
public interface IReceiptPrintService
{
    /// <summary>
    /// Lista las impresoras instaladas en Windows (nombres del spooler).
    /// </summary>
    /// <returns>Colección de nombres de impresora, distintos y ordenados.</returns>
    IReadOnlyList<string> GetInstalledWindowsPrinters();

    /// <summary>
    /// Imprime el ticket/comprobante DTE en la impresora indicada.
    /// </summary>
    /// <param name="document">Documento a imprimir.</param>
    /// <param name="printer">Configuración de la impresora destino.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que completa cuando el envío a la impresora finaliza.</returns>
    Task PrintReceiptAsync(ReceiptDocument document, PrinterConfig printer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imprime una página de prueba (encabezado + patrón + corte).
    /// </summary>
    /// <param name="printer">Configuración de la impresora destino.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que completa cuando el envío a la impresora finaliza.</returns>
    Task PrintTestPageAsync(PrinterConfig printer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imprime un reporte de texto monoespaciado (ej. corte de caja).
    /// </summary>
    /// <param name="title">Título del reporte, centrado en el encabezado.</param>
    /// <param name="body">Cuerpo del reporte; se respetan los saltos de línea.</param>
    /// <param name="printer">Configuración de la impresora destino.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que completa cuando el envío a la impresora finaliza.</returns>
    Task PrintTextReportAsync(string title, string body, PrinterConfig printer, CancellationToken cancellationToken = default);
}
