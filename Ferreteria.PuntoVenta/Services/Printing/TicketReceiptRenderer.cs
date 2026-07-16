using System.Globalization;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Convierte un <see cref="ReceiptDocument"/> en bytes ESC/POS siguiendo el
/// layout de ticket DTE 2026 para El Salvador.
/// </summary>
internal static class TicketReceiptRenderer
{
    private static readonly CultureInfo Money = CultureInfo.InvariantCulture;

    /// <summary>
    /// Renderiza el comprobante completo a bytes ESC/POS.
    /// </summary>
    /// <param name="document">Documento a renderizar.</param>
    /// <param name="paperWidthMm">Ancho de papel en milímetros (80 o 58).</param>
    /// <returns>Bytes crudos listos para enviar a la impresora.</returns>
    public static byte[] Render(ReceiptDocument document, int paperWidthMm)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new EscPosDocumentBuilder(paperWidthMm);

        RenderBusinessHeader(builder, document);
        RenderDocumentTitle(builder, document);
        RenderFiscalData(builder, document);
        RenderItems(builder, document);
        RenderTotals(builder, document);
        RenderQrBlock(builder, document);
        RenderLegalFooter(builder, document);

        builder.Feed(2).Cut();
        return builder.Build();
    }

    private static void RenderBusinessHeader(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendCenter(document.BusinessName.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(document.BusinessTradeName))
        {
            builder.AppendCenter(document.BusinessTradeName);
        }

        builder.AppendCenter($"NIT: {document.BusinessNit}");
        builder.AppendCenter($"NRC: {document.BusinessNrc}");

        if (!string.IsNullOrWhiteSpace(document.BusinessAddress))
        {
            foreach (string line in Center(document.BusinessAddress, builder.Columns))
            {
                builder.AppendCenter(line);
            }
        }

        if (!string.IsNullOrWhiteSpace(document.BusinessPhone))
        {
            builder.AppendCenter($"Tel: {document.BusinessPhone}");
        }
    }

    private static void RenderDocumentTitle(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendSeparator();

        string pruebaTag = document.Ambiente == "00" ? "  *PRUEBA*" : string.Empty;
        builder.AppendCenter($"{document.DteTypeName}{pruebaTag}");
    }

    private static void RenderFiscalData(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendSeparator();

        builder.AppendLeft("Num. Control:");
        builder.AppendLeft(document.NumeroControl);
        builder.AppendLeft("Cod. Generacion:");
        builder.AppendLeft(document.CodigoGeneracion);

        string sello = string.IsNullOrWhiteSpace(document.SelloRecibido)
            ? "PENDIENTE / CONTINGENCIA"
            : document.SelloRecibido;
        builder.AppendLeft("Sello:");
        builder.AppendLeft(sello);

        builder.AppendLeft($"Fecha: {document.IssuedAt.ToString("dd/MM/yyyy HH:mm:ss", Money)}");
        builder.AppendLeft($"Cajero: {document.CashierName}");
        builder.AppendLeft($"Cliente: {document.CustomerName}");

        if (!string.IsNullOrWhiteSpace(document.CustomerDocument))
        {
            builder.AppendLeft($"Documento: {document.CustomerDocument}");
        }
    }

    private static void RenderItems(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendSeparator();
        builder.AppendColumns("CANT  DESCRIPCION", "TOTAL");
        builder.AppendSeparator();

        foreach (TicketLineItem item in document.Items)
        {
            builder.AppendWrapped(item.Description);

            string quantityAndPrice = $"{item.QuantityText} x {FormatMoney(item.UnitPrice)}";
            string lineTotal = FormatMoney(item.LineTotal);
            builder.AppendColumns($"  {quantityAndPrice}", lineTotal);
        }
    }

    private static void RenderTotals(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendSeparator();

        builder.AppendColumns("Subtotal:", FormatMoney(document.Subtotal));
        builder.AppendColumns("IVA (13%):", FormatMoney(document.Tax));
        builder.AppendColumns("TOTAL:", FormatMoney(document.Total), emphasized: true);

        builder.AppendLeft("Son:");
        builder.AppendWrapped(document.TotalInWords);

        builder.AppendSeparator();
        builder.AppendColumns("Forma de pago:", document.PaymentMethod);
        builder.AppendColumns("Pago:", FormatMoney(document.AmountPaid));
        builder.AppendColumns("Cambio:", FormatMoney(document.Change));
    }

    private static void RenderQrBlock(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendSeparator();
        builder.AppendCenter("Consulta este DTE en linea:");

        if (!string.IsNullOrWhiteSpace(document.ConsultaUrl))
        {
            builder.AppendQr(document.ConsultaUrl);

            foreach (string line in Center(document.ConsultaUrl, builder.Columns))
            {
                builder.AppendCenter(line);
            }
        }

        builder.AppendCenter($"Cod. Gen.: {document.CodigoGeneracion}");
    }

    private static void RenderLegalFooter(EscPosDocumentBuilder builder, ReceiptDocument document)
    {
        builder.AppendSeparator();
        builder.AppendCenter("Documento Tributario Electronico");

        if (document.IsContingency)
        {
            builder.AppendCenter("EMITIDO EN CONTINGENCIA");
            builder.AppendCenter("Pendiente de sello MH");
        }

        if (!string.IsNullOrWhiteSpace(document.FooterNote))
        {
            foreach (string line in Center(document.FooterNote, builder.Columns))
            {
                builder.AppendCenter(line);
            }
        }

        builder.AppendCenter("Gracias por su compra!");
    }

    private static string FormatMoney(decimal value)
    {
        return "$" + value.ToString("0.00", Money);
    }

    private static IEnumerable<string> Center(string text, int width)
    {
        // Divide en trozos del ancho para que cada uno se centre por separado.
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        string remaining = text.Trim();
        while (remaining.Length > width)
        {
            yield return remaining.Substring(0, width);
            remaining = remaining.Substring(width);
        }

        yield return remaining;
    }
}
