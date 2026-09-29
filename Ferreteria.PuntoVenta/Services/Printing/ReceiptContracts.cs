namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>Tipos de documento usados por la impresión de comprobantes.</summary>
public static class ReceiptDocumentTypes
{
    /// <summary>Código del comprobante interno sin DTE.</summary>
    public const string InternalReceipt = "INTERNO";

    /// <summary>Título impreso en el comprobante interno (sin DTE).</summary>
    public const string InternalReceiptTitle = "COMPROBANTE INTERNO";

    /// <summary>Leyenda de reimpresión, pendiente de verificar con contador y normativa MH.</summary>
    // A VERIFICAR con contador / normativa MH: texto y obligatoriedad de esta leyenda.
    public const string ReprintLegend = "REIMPRESIÓN";
}

/// <summary>
/// Representa una línea de detalle del ticket (un producto o servicio vendido).
/// </summary>
/// <param name="Description">Descripción del producto o servicio.</param>
/// <param name="Quantity">Cantidad numérica vendida.</param>
/// <param name="QuantityText">Cantidad formateada para mostrar (ej. "2.00" o "1 UND").</param>
/// <param name="UnitPrice">Precio unitario.</param>
/// <param name="LineTotal">Total de la línea (cantidad por precio unitario).</param>
public sealed record TicketLineItem(
    string Description,
    decimal Quantity,
    string QuantityText,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>
/// Documento de comprobante (DTE) listo para renderizar en la impresora térmica.
/// </summary>
/// <param name="BusinessName">Razón social del emisor.</param>
/// <param name="BusinessTradeName">Nombre comercial del emisor (opcional).</param>
/// <param name="BusinessNit">NIT del emisor.</param>
/// <param name="BusinessNrc">NRC del emisor.</param>
/// <param name="BusinessAddress">Dirección del emisor.</param>
/// <param name="BusinessPhone">Teléfono del emisor (opcional).</param>
/// <param name="DteTypeName">Nombre legible del tipo de DTE (ej. "FACTURA CONSUMIDOR FINAL").</param>
/// <param name="DteTypeCode">Código del tipo de DTE ("01" | "03" | "05").</param>
/// <param name="NumeroControl">Número de control del DTE.</param>
/// <param name="CodigoGeneracion">Código de generación (UUID) del DTE.</param>
/// <param name="SelloRecibido">Sello de recepción del MH; nulo si está en contingencia o pendiente.</param>
/// <param name="Ambiente">Ambiente del DTE ("00" pruebas / "01" producción).</param>
/// <param name="IssuedAt">Fecha y hora de emisión.</param>
/// <param name="CashierName">Nombre del cajero que emite.</param>
/// <param name="CustomerName">Nombre del cliente.</param>
/// <param name="CustomerDocument">Documento del cliente (NIT/NRC/DUI) o nulo.</param>
/// <param name="Items">Detalle de líneas del comprobante.</param>
/// <param name="Subtotal">Subtotal (sin impuestos o gravado según el caso).</param>
/// <param name="Tax">Monto de IVA.</param>
/// <param name="Total">Total a pagar.</param>
/// <param name="TotalInWords">Total expresado en letras.</param>
/// <param name="PaymentMethod">Forma de pago (ej. "EFECTIVO").</param>
/// <param name="AmountPaid">Monto pagado por el cliente.</param>
/// <param name="Change">Cambio entregado al cliente.</param>
/// <param name="ConsultaUrl">Contenido del QR (enlace de consulta pública del MH).</param>
/// <param name="IsContingency">Indica si el DTE fue emitido en contingencia.</param>
/// <param name="FooterNote">Nota adicional para el pie del ticket (opcional).</param>
/// <param name="IsReprint">Indica que el comprobante es una reimpresión.</param>
public sealed record ReceiptDocument(
    string BusinessName,
    string? BusinessTradeName,
    string BusinessNit,
    string BusinessNrc,
    string BusinessAddress,
    string? BusinessPhone,
    string DteTypeName,
    string DteTypeCode,
    string NumeroControl,
    string CodigoGeneracion,
    string? SelloRecibido,
    string Ambiente,
    DateTime IssuedAt,
    string CashierName,
    string CustomerName,
    string? CustomerDocument,
    IReadOnlyList<TicketLineItem> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string TotalInWords,
    string PaymentMethod,
    decimal AmountPaid,
    decimal Change,
    string ConsultaUrl,
    bool IsContingency,
    string? FooterNote,
    bool IsReprint = false)
{
    /// <summary>Orden asociada para auditoría de impresión, si aplica.</summary>
    public Guid? OrderId { get; init; }
}

/// <summary>
/// Configuración de la impresora térmica destino.
/// </summary>
/// <param name="Name">Nombre de la impresora en Windows (spooler) o etiqueta descriptiva.</param>
/// <param name="ConnectionType">Tipo de conexión ("USB" | "RED" | "BLUETOOTH").</param>
/// <param name="IpAddress">Dirección IP cuando la conexión es por red; nulo en otros casos.</param>
/// <param name="NetworkPort">Puerto TCP cuando la conexión es por red (por defecto 9100).</param>
/// <param name="PaperWidthMm">Ancho de papel en milímetros (80 o 58).</param>
public sealed record PrinterConfig(
    string Name,
    string ConnectionType,
    string? IpAddress,
    int? NetworkPort,
    int PaperWidthMm);

/// <summary>Opciones de impresión configurables en la sección <c>Printing</c>.</summary>
public sealed class PrintingOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "Printing";

    /// <summary>Indica si se imprime automáticamente después de guardar una venta.</summary>
    public bool AutoPrintOnSale { get; set; } = true;

    /// <summary>Tiempo máximo de conexión y escritura de una impresora de red.</summary>
    public int NetworkTimeoutSeconds { get; set; } = 5;

    /// <summary>Leyenda del comprobante interno, pendiente de validación fiscal.</summary>
    // A VERIFICAR con contador / normativa MH: la leyenda aplicable al comprobante interno.
    public string InternalReceiptFooter { get; set; } = "COMPROBANTE INTERNO - SIN VALOR FISCAL";
}
