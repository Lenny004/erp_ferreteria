using System.Globalization;
using Ferreteria.PuntoVenta.Services.SalesHistory;

namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Compone el reporte de corte para papel térmico de 32 o 48 columnas.</summary>
public static class CashRegisterReportComposer
{
    /// <summary>Genera el texto monoespaciado del corte.</summary>
    /// <param name="data">Datos de la sesión y sus totales.</param>
    /// <param name="columnWidth">Ancho permitido: 32 o 48 columnas.</param>
    /// <param name="partial">Marca el reporte como corte parcial sin cerrar turno.</param>
    /// <returns>Texto listo para vista previa o impresión.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el ancho no es 32 ni 48.</exception>
    public static string Compose(
        CashRegisterReportData data,
        int columnWidth,
        bool partial = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (columnWidth is not 32 and not 48)
        {
            throw new ArgumentOutOfRangeException(nameof(columnWidth), "El reporte solo admite 32 o 48 columnas.");
        }

        var difference = CashRegisterCalculator.CalculateDifference(
            data.Summary.ExpectedCash,
            data.DeclaredCash,
            0m);
        var lines = new List<string>
        {
            Center(partial ? "CORTE PARCIAL" : "CORTE DE CAJA", columnWidth),
            Center(partial ? "NO CIERRA TURNO" : "REPORTE INTERNO", columnWidth),
            new string('-', columnWidth),
            Fit($"Caja: {data.CashRegisterCode}", columnWidth),
            Fit($"Cajero: {data.EmployeeDisplayName}", columnWidth),
            Fit($"Apertura: {FormatDate(data.OpenedAtUtc)}", columnWidth),
            Fit($"Cierre: {(data.ClosedAtUtc is null ? "--" : FormatDate(data.ClosedAtUtc.Value))}", columnWidth),
            new string('-', columnWidth),
            Pair("Ventas", data.Summary.CompletedSales.ToString(CultureInfo.InvariantCulture), columnWidth),
            Pair("Total vendido", FormatAmount(data.Summary.TotalSold), columnWidth),
            Pair("IVA", FormatAmount(data.Summary.TaxAmount), columnWidth),
            Pair("Efectivo ventas", FormatAmount(data.Summary.CashPayments), columnWidth),
            Pair("Tarjeta", FormatAmount(data.Summary.CardPayments), columnWidth),
            Pair("Transferencia", FormatAmount(data.Summary.TransferPayments), columnWidth),
            Pair("Otros", FormatAmount(data.Summary.OtherPayments), columnWidth),
            Pair("Fondo inicial", FormatAmount(data.Summary.OpeningAmount), columnWidth),
            Pair("Devoluciones", FormatAmount(data.Summary.CashRefunds), columnWidth),
            Pair("Efectivo esperado", FormatAmount(data.Summary.ExpectedCash), columnWidth),
            Pair("Efectivo contado", FormatAmount(data.DeclaredCash), columnWidth),
            Pair(difference.DisplayText, FormatAmount(difference.Difference), columnWidth),
            new string('-', columnWidth),
            Fit("MOVIMIENTOS", columnWidth)
        };

        foreach (var movement in data.Summary.Movements)
        {
            lines.Add(Fit(
                $"{movement.LocalTimeText} {movement.PaymentMethod} {FormatAmount(movement.Amount)} {movement.DocumentNumber}",
                columnWidth));
        }

        if (!string.IsNullOrWhiteSpace(data.Notes))
        {
            lines.Add(new string('-', columnWidth));
            lines.Add(Fit($"Nota: {data.Notes.Trim()}", columnWidth));
        }

        lines.Add(new string('-', columnWidth));
        lines.Add(Center("No es documento fiscal", columnWidth));
        // Requisitos del corte Z, conservación, IVA y reporte diario: a verificar con contador / normativa MH.
        return string.Join(Environment.NewLine, lines);
    }

    private static string Pair(string label, string value, int width)
    {
        var availableLabelWidth = Math.Max(1, width - value.Length - 1);
        return Fit(label.PadRight(Math.Min(label.Length, availableLabelWidth)) + new string(' ', Math.Max(1, width - label.Length - value.Length)) + value, width);
    }

    private static string Fit(string value, int width)
    {
        var normalized = value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= width ? normalized : normalized[..width];
    }

    private static string Center(string value, int width)
    {
        var text = Fit(value, width);
        var leftPadding = Math.Max(0, (width - text.Length) / 2);
        return new string(' ', leftPadding) + text;
    }

    private static string FormatAmount(decimal amount)
    {
        return string.Format(CultureInfo.InvariantCulture, "${0:N2}", amount);
    }

    private static string FormatDate(DateTime utc)
    {
        return TimeZoneSupport.ToElSalvadorTime(utc).ToString("dd/MM/yyyy HH:mm");
    }
}
