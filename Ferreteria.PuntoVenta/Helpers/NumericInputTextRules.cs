namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>
/// Resultado de validar una edición de texto decimal.
/// </summary>
public readonly record struct NumericInputTextRuleResult
{
    /// <summary>Inicializa un resultado de validación.</summary>
    /// <param name="isAllowed">Indica si la edición es permitida.</param>
    /// <param name="errorMessage">Mensaje en español cuando la edición es rechazada.</param>
    public NumericInputTextRuleResult(bool isAllowed, string? errorMessage)
    {
        IsAllowed = isAllowed;
        ErrorMessage = errorMessage;
    }

    /// <summary>Indica si la edición conserva un valor permitido.</summary>
    public bool IsAllowed { get; }

    /// <summary>Mensaje visible asociado al rechazo; es <see langword="null"/> si se permite.</summary>
    public string? ErrorMessage { get; }
}

/// <summary>
/// Decisión pura para filtrar la edición de un campo decimal.
/// </summary>
public static class NumericInputTextRules
{
    /// <summary>
    /// Evalúa una inserción o reemplazo de texto sin depender de controles WPF.
    /// </summary>
    /// <param name="currentText">Texto actual del control.</param>
    /// <param name="selectionStart">Índice inicial de la selección.</param>
    /// <param name="selectionLength">Longitud de la selección reemplazada.</param>
    /// <param name="insertedText">Texto que se pretende insertar.</param>
    /// <param name="precision">Cantidad total máxima de dígitos.</param>
    /// <param name="scale">Cantidad máxima de dígitos decimales.</param>
    /// <param name="allowNegative">Indica si se permite el signo negativo.</param>
    /// <param name="allowIntermediate">Permite prefijos que pueden completarse mientras se escribe.</param>
    /// <returns>Resultado con la decisión y un mensaje en español cuando se rechaza la edición.</returns>
    /// <remarks>
    /// La escritura permite prefijos de miles como <c>1,234</c> y <c>1,234.</c> para que
    /// puedan completarse como <c>1,234.50</c>. El pegado se valida con el parser completo
    /// usando <paramref name="allowIntermediate"/> en <see langword="false"/>.
    /// </remarks>
    public static NumericInputTextRuleResult Validate(
        string? currentText,
        int selectionStart,
        int selectionLength,
        string? insertedText,
        int precision,
        int scale,
        bool allowNegative,
        bool allowIntermediate = true)
    {
        if (precision <= 0 || scale < 0 || scale > precision)
        {
            return Reject("La precisión y la escala configuradas no son válidas.");
        }

        var current = currentText ?? string.Empty;
        var insertion = insertedText ?? string.Empty;
        if (selectionStart < 0 || selectionLength < 0 || selectionStart > current.Length
            || selectionStart + selectionLength > current.Length)
        {
            return Reject("La selección del texto no es válida.");
        }

        var candidate = current.Remove(selectionStart, selectionLength)
            .Insert(selectionStart, insertion);

        if (candidate.Length == 0)
        {
            return Allowed();
        }

        if (!candidate.All(static character => character is >= '0' and <= '9' or '.' or ',' or '-'))
        {
            return Reject("Solo se permiten dígitos, separadores decimales y el signo negativo.");
        }

        if (candidate.Contains('-') && (!allowNegative || candidate[0] != '-'
            || candidate.Count(static character => character == '-') > 1))
        {
            return Reject(allowNegative
                ? "El signo negativo solo puede aparecer al inicio."
                : "El valor no puede ser negativo.");
        }

        if (allowIntermediate && allowNegative && candidate == "-")
        {
            return Allowed();
        }

        var parsed = DecimalInputParser.Parse(candidate, precision, scale, allowNegative);
        if (parsed.IsValid)
        {
            return Allowed();
        }

        if (allowIntermediate && IsCanonicalThousandsPrefix(candidate, precision, scale))
        {
            return Allowed();
        }

        return Reject(parsed.ErrorMessage ?? "Formato numérico no válido.");
    }

    private static bool IsCanonicalThousandsPrefix(string candidate, int precision, int scale)
    {
        var unsignedCandidate = candidate.StartsWith("-", StringComparison.Ordinal)
            ? candidate[1..]
            : candidate;
        var pointIndex = unsignedCandidate.IndexOf('.');
        var integerPart = pointIndex >= 0 ? unsignedCandidate[..pointIndex] : unsignedCandidate;
        var decimalPart = pointIndex >= 0 ? unsignedCandidate[(pointIndex + 1)..] : string.Empty;

        if (pointIndex >= 0 && pointIndex != unsignedCandidate.LastIndexOf('.')
            || pointIndex >= 0 && !decimalPart.All(static character => character is >= '0' and <= '9'))
        {
            return false;
        }

        if (pointIndex >= 0 && decimalPart.Length > scale)
        {
            return false;
        }

        var integerDigits = integerPart.Replace(",", string.Empty, StringComparison.Ordinal)
            .TrimStart('0')
            .Length;
        if (integerDigits > precision - scale)
        {
            return false;
        }

        var groups = integerPart.Split(',');
        if (groups.Length < 2 || groups[0].Length is < 1 or > 3
            || !groups[0].All(static character => character is >= '0' and <= '9'))
        {
            return false;
        }

        if (groups.Skip(1).Any(static group => group.Length > 3
            || !group.All(static character => character is >= '0' and <= '9')))
        {
            return false;
        }

        if (pointIndex >= 0 && groups[^1].Length != 3)
        {
            return false;
        }

        return groups.Skip(1).Take(groups.Length - 2).All(static group => group.Length == 3)
            && groups[^1].Length <= 3;
    }

    private static NumericInputTextRuleResult Allowed() => new(true, null);

    private static NumericInputTextRuleResult Reject(string message) => new(false, message);
}
