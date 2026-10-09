namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>Entrada inmutable que identifica el contenido de un intento de venta.</summary>
/// <param name="Lines">Producto y cantidad de cada línea.</param>
/// <param name="PaymentMethod">Método de pago normalizado.</param>
/// <param name="PaymentAmount">Monto total del pago.</param>
/// <param name="CustomerId">Cliente seleccionado, si existe.</param>
/// <param name="DocumentType">Tipo de documento seleccionado.</param>
public sealed record SaleAttemptInput(
    IReadOnlyList<SaleAttemptLine> Lines,
    string PaymentMethod,
    decimal PaymentAmount,
    Guid? CustomerId = null,
    string? DocumentType = null);

/// <summary>Línea mínima usada para detectar cambios del carrito.</summary>
/// <param name="ProductId">Identificador del producto.</param>
/// <param name="Quantity">Cantidad solicitada.</param>
public sealed record SaleAttemptLine(Guid ProductId, decimal Quantity);

/// <summary>
/// Conserva un identificador por intento lógico y lo invalida cuando cambia el contenido.
/// </summary>
public sealed class SaleAttemptTracker
{
    private SaleAttemptInput? _lastInput;
    private Guid? _requestId;

    /// <summary>Obtiene el identificador vigente para el contenido proporcionado.</summary>
    /// <param name="input">Carrito y pago actuales.</param>
    /// <returns>Identificador estable hasta un cambio o éxito.</returns>
    public Guid GetOrCreate(SaleAttemptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var normalized = Normalize(input);
        if (_requestId is null || _lastInput is null || !InputsMatch(_lastInput, normalized))
        {
            _requestId = Guid.NewGuid();
            _lastInput = normalized;
        }

        return _requestId.Value;
    }

    /// <summary>Invalida el intento después de una venta confirmada.</summary>
    /// <param name="requestId">Identificador que fue confirmado.</param>
    public void MarkSucceeded(Guid requestId)
    {
        if (_requestId == requestId)
        {
            _requestId = null;
            _lastInput = null;
        }
    }

    private static SaleAttemptInput Normalize(SaleAttemptInput input) =>
        input with
        {
            Lines = input.Lines
                .OrderBy(line => line.ProductId)
                .ThenBy(line => line.Quantity)
                .ToArray(),
            PaymentMethod = input.PaymentMethod.Trim().ToUpperInvariant(),
            DocumentType = input.DocumentType?.Trim()
        };

    private static bool InputsMatch(SaleAttemptInput left, SaleAttemptInput right) =>
        string.Equals(left.PaymentMethod, right.PaymentMethod, StringComparison.Ordinal)
        && left.PaymentAmount == right.PaymentAmount
        && left.CustomerId == right.CustomerId
        && string.Equals(left.DocumentType, right.DocumentType, StringComparison.Ordinal)
        && left.Lines.SequenceEqual(right.Lines);
}
