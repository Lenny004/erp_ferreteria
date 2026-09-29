namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Formato y lectura de <c>sales.Orders.Notes</c> para órdenes de confección.
/// Hasta que exista <c>CustomerName</c> dedicado en el esquema, el taller guarda datos del cliente en notas.
/// </summary>
public static class OrderNotesFormatter
{
    private const string CustomerNamePrefix = "Cliente: ";
    private const string CustomerPhonePrefix = "Telefono: ";
    private const string NotesSegmentSeparator = " | ";

    /// <summary>Construye notas estructuradas a partir de los datos opcionales del cliente.</summary>
    public static string? BuildConfectionOrderNotes(
        string? customerName,
        string? customerPhone,
        string? additionalNotes)
    {
        var segments = new[]
        {
            FormatSegment(CustomerNamePrefix, customerName),
            FormatSegment(CustomerPhonePrefix, customerPhone),
            string.IsNullOrWhiteSpace(additionalNotes) ? null : additionalNotes.Trim()
        };

        var composedNotes = string.Join(NotesSegmentSeparator, segments.Where(segment => segment is not null));
        return string.IsNullOrWhiteSpace(composedNotes) ? null : composedNotes;
    }

    /// <summary>Extrae el nombre del cliente para mostrar en listados e historial.</summary>
    public static string ExtractCustomerDisplayName(string? orderNotes)
    {
        if (string.IsNullOrWhiteSpace(orderNotes))
        {
            return SalesDomainConstants.Customers.DefaultWalkInDisplayName;
        }

        var customerSegment = orderNotes
            .Split(NotesSegmentSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(segment => segment.StartsWith(CustomerNamePrefix, StringComparison.OrdinalIgnoreCase));

        return customerSegment is null
            ? SalesDomainConstants.Customers.DefaultWalkInDisplayName
            : customerSegment[CustomerNamePrefix.Length..].Trim();
    }

    /// <summary>Normaliza las notas de una orden para mostrarlas en el detalle.</summary>
    /// <param name="orderNotes">Notas persistidas de la orden.</param>
    /// <returns>Notas separadas por líneas o una etiqueta de ausencia.</returns>
    /// <remarks>La separación conserva los segmentos estructurados sin exponer el formato persistido al usuario.</remarks>
    public static string FormatForDisplay(string? orderNotes)
    {
        if (string.IsNullOrWhiteSpace(orderNotes))
        {
            return "Sin notas.";
        }

        return string.Join(Environment.NewLine, orderNotes
            .Split(NotesSegmentSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? FormatSegment(string prefix, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{prefix}{value.Trim()}";
    }
}
