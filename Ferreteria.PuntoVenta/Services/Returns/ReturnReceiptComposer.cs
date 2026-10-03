using System.Globalization;
using Ferreteria.PuntoVenta.Services.Time;

namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Datos puros usados para componer un comprobante interno de devolución.</summary>
/// <param name="OrderId">Orden original.</param>
/// <param name="ControlNumber">Número de control real, si existe.</param>
/// <param name="SaleCreatedAtUtc">Fecha de la venta original.</param>
/// <param name="Lines">Líneas devueltas.</param>
/// <param name="Subtotal">Subtotal bruto.</param>
/// <param name="DiscountAmount">Descuento.</param>
/// <param name="TaxAmount">IVA calculado.</param>
/// <param name="Total">Total acreditado.</param>
/// <param name="RefundMethod">Método de reintegro.</param>
/// <param name="Reason">Motivo visible.</param>
/// <param name="ExecutedBy">Empleado ejecutor.</param>
/// <param name="AuthorizedBy">Empleado autorizador.</param>
/// <param name="FiscalStatus">Estado fiscal interno.</param>
/// <param name="Legend">Leyenda configurada.</param>
public sealed record ReturnReceiptData(
    Guid OrderId,
    string? ControlNumber,
    DateTime SaleCreatedAtUtc,
    IReadOnlyList<ReturnReceiptLine> Lines,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    string RefundMethod,
    string Reason,
    string ExecutedBy,
    string AuthorizedBy,
    string FiscalStatus,
    string Legend);

/// <summary>Línea de texto del comprobante interno.</summary>
/// <param name="Description">Descripción a truncar.</param>
/// <param name="Quantity">Cantidad devuelta.</param>
/// <param name="Amount">Importe de la línea.</param>
public sealed record ReturnReceiptLine(string Description, decimal Quantity, decimal Amount);

/// <summary>Compone texto monoespaciado de 32 o 48 columnas exactas.</summary>
public static class ReturnReceiptComposer
{
    /// <summary>Genera el comprobante interno sin emitir ni imprimir un DTE.</summary>
    /// <param name="data">Datos de la devolución.</param>
    /// <param name="columnWidth">Ancho permitido, 32 o 48.</param>
    /// <returns>Texto con cada línea exactamente al ancho seleccionado.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el ancho no es 32 ni 48.</exception>
    public static string Compose(ReturnReceiptData data, int columnWidth)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (columnWidth is not 32 and not 48)
        {
            throw new ArgumentOutOfRangeException(nameof(columnWidth), "El comprobante solo admite 32 o 48 columnas.");
        }

        var shortOrder = data.OrderId.ToString("N")[..8].ToUpperInvariant();
        var fiscalText = data.FiscalStatus switch
        {
            ReturnDomainConstants.FiscalStatuses.Pending => "Documento tributario pendiente",
            ReturnDomainConstants.FiscalStatuses.RequiresValidation => "Requiere validación fiscal",
            _ => data.FiscalStatus
        };
        var lines = new List<string>
        {
            Center("COMPROBANTE INTERNO DE DEVOLUCIÓN", columnWidth),
            FitExact(data.Legend, columnWidth),
            new string('-', columnWidth),
            FitExact($"Venta: ORD-{shortOrder}", columnWidth),
            FitExact($"Control: {data.ControlNumber ?? "No disponible"}", columnWidth),
            FitExact($"Fecha venta: {TimeZoneSupport.ToLocalTime(data.SaleCreatedAtUtc):dd/MM/yyyy HH:mm}", columnWidth),
            new string('-', columnWidth)
        };

        foreach (var line in data.Lines)
        {
            var description = Truncate(line.Description, Math.Max(4, columnWidth - 18));
            lines.Add(FitExact($"{description} {line.Quantity:0.###} {FormatAmount(line.Amount)}", columnWidth));
        }

        lines.Add(new string('-', columnWidth));
        lines.Add(Pair("Subtotal", FormatAmount(data.Subtotal), columnWidth));
        lines.Add(Pair("Descuento", FormatAmount(data.DiscountAmount), columnWidth));
        lines.Add(Pair("IVA", FormatAmount(data.TaxAmount), columnWidth));
        lines.Add(Pair("Total", FormatAmount(data.Total), columnWidth));
        lines.Add(FitExact($"Reintegro: {data.RefundMethod}", columnWidth));
        lines.Add(FitExact($"Motivo: {data.Reason}", columnWidth));
        lines.Add(FitExact($"Ejecuta: {data.ExecutedBy}", columnWidth));
        lines.Add(FitExact($"Autoriza: {data.AuthorizedBy}", columnWidth));
        lines.Add(FitExact(fiscalText, columnWidth));
        lines.Add(new string('-', columnWidth));
        lines.Add(Center("No es documento fiscal", columnWidth));
        return string.Join(Environment.NewLine, lines);
    }

    private static string Pair(string label, string value, int width)
    {
        var spaces = Math.Max(1, width - label.Length - value.Length);
        return FitExact(label + new string(' ', spaces) + value, width);
    }

    private static string Center(string value, int width)
    {
        var text = Truncate(value, width);
        var left = Math.Max(0, (width - text.Length) / 2);
        return FitExact(new string(' ', left) + text, width);
    }

    private static string FitExact(string value, int width)
    {
        var normalized = value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return normalized.Length >= width ? normalized[..width] : normalized.PadRight(width);
    }

    private static string Truncate(string value, int width)
    {
        var normalized = value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= width ? normalized : normalized[..width];
    }

    private static string FormatAmount(decimal amount) => string.Format(CultureInfo.InvariantCulture, "${0:0.00}", amount);
}
