using Ferreteria.PuntoVenta.Services.Dte;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>Datos simples de una venta necesarios para formar un comprobante.</summary>
public sealed record ReceiptSaleData(
    Guid OrderId,
    string CashierName,
    string CustomerName,
    string? CustomerDocument,
    IReadOnlyList<TicketLineItem> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string PaymentMethod,
    decimal AmountPaid,
    DateTime IssuedAt);

/// <summary>Datos del emisor provenientes de la configuración activa.</summary>
public sealed record ReceiptIssuerData(
    string BusinessName,
    string? BusinessTradeName,
    string BusinessNit,
    string BusinessNrc,
    string BusinessAddress,
    string? BusinessPhone);

/// <summary>Datos fiscales opcionales del DTE emitido.</summary>
public sealed record ReceiptDteData(
    string TypeCode,
    string TypeName,
    string ControlNumber,
    string GenerationCode,
    string? Seal,
    string Environment,
    string? ConsultaUrl,
    bool IsContingency,
    decimal Tax);

/// <summary>Fábrica pura de documentos internos y DTE para impresión térmica.</summary>
public sealed class ReceiptDocumentFactory
{
    /// <summary>Crea un comprobante interno o DTE a partir de datos ya consultados.</summary>
    /// <param name="sale">Datos de venta.</param>
    /// <param name="issuer">Emisor activo o valores de verificación.</param>
    /// <param name="dte">Datos del DTE, si existe.</param>
    /// <param name="internalFooter">Leyenda configurable del comprobante interno.</param>
    /// <returns>Documento listo para renderizar.</returns>
    public ReceiptDocument Create(
        ReceiptSaleData sale,
        ReceiptIssuerData issuer,
        ReceiptDteData? dte,
        string internalFooter)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(internalFooter);

        bool isInternal = dte is null;
        decimal tax = dte?.Tax ?? sale.Tax;
        string typeCode = dte?.TypeCode ?? ReceiptDocumentTypes.InternalReceipt;
        string typeName = dte?.TypeName ?? ReceiptDocumentTypes.InternalReceiptTitle;
        decimal change = Math.Max(0m, sale.AmountPaid - sale.Total);

        return new ReceiptDocument(
            issuer.BusinessName,
            issuer.BusinessTradeName,
            issuer.BusinessNit,
            issuer.BusinessNrc,
            issuer.BusinessAddress,
            issuer.BusinessPhone,
            typeName,
            typeCode,
            dte?.ControlNumber ?? string.Empty,
            dte?.GenerationCode ?? string.Empty,
            dte?.Seal,
            dte?.Environment ?? string.Empty,
            sale.IssuedAt,
            sale.CashierName,
            sale.CustomerName,
            sale.CustomerDocument,
            sale.Items,
            sale.Subtotal,
            tax,
            sale.Total,
            SpanishNumberToWords.Convert(sale.Total),
            sale.PaymentMethod,
            sale.AmountPaid,
            change,
            dte?.ConsultaUrl ?? string.Empty,
            dte?.IsContingency ?? false,
            isInternal ? internalFooter : null)
        { OrderId = sale.OrderId };
    }
}
