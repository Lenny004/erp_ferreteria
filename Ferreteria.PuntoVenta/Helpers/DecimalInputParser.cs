using System.Globalization;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>
/// Resultado determinista del análisis de un número decimal introducido por el usuario.
/// </summary>
public readonly record struct DecimalInputResult
{
    /// <summary>Inicializa el resultado del análisis decimal.</summary>
    /// <param name="value">Valor analizado o <c>null</c> si hubo un error.</param>
    /// <param name="errorMessage">Mensaje de error o <c>null</c> si el valor es válido.</param>
    public DecimalInputResult(decimal? value, string? errorMessage)
    {
        Value = value;
        ErrorMessage = errorMessage;
    }

    /// <summary>Valor decimal analizado, cuando la entrada es válida.</summary>
    public decimal? Value { get; }

    /// <summary>Mensaje de validación en español, cuando la entrada es inválida.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Indica si el valor fue analizado correctamente.</summary>
    public bool IsValid => Value.HasValue;
}

/// <summary>
/// Analiza entradas decimales de la UI con reglas independientes de la cultura del equipo.
/// </summary>
/// <remarks>
/// Acepta punto o coma como separador decimal, pero solo acepta comas de miles en la forma
/// canónica de es-SV cuando también existe un punto decimal. Un separador final representa
/// un número entero, por ejemplo <c>20.</c> o <c>20,</c>.
/// </remarks>
public static class DecimalInputParser
{
    private const string AmbiguousMessage =
        "Valor ambiguo: escriba los decimales con punto o coma (ej. 1.5 o 1,5) y los miles sin separador o como 1,234.5.";
    private const string InvalidFormatMessage = "Formato numérico no válido.";

    /// <summary>
    /// Analiza un texto decimal y devuelve el valor o un mensaje de validación en español.
    /// </summary>
    /// <param name="input">Texto introducido por el usuario.</param>
    /// <param name="precision">Cantidad total máxima de dígitos permitida.</param>
    /// <param name="scale">Cantidad máxima de dígitos decimales permitida.</param>
    /// <param name="allowNegative">Indica si se permiten valores negativos.</param>
    /// <returns>Resultado con el valor decimal o el mensaje de error correspondiente.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza cuando <paramref name="precision"/> no es positivo o
    /// <paramref name="scale"/> está fuera del rango de cero a <paramref name="precision"/>.
    /// </exception>
    public static DecimalInputResult Parse(string? input, int precision, int scale, bool allowNegative)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(precision);
        if (scale < 0 || scale > precision)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "La escala debe estar entre cero y la precisión.");
        }

        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return Error("Ingrese un valor.");
        }

        var hasNegativeSign = text[0] == '-';
        if (hasNegativeSign)
        {
            if (!allowNegative)
            {
                return Error("El valor no puede ser negativo.");
            }

            text = text[1..];
            if (text.Length == 0)
            {
                return Error(InvalidFormatMessage);
            }
        }

        if (text.Contains('+') || text.Contains('-'))
        {
            return Error(InvalidFormatMessage);
        }

        var hasPoint = text.Contains('.');
        var hasComma = text.Contains(',');
        string normalized;

        if (hasPoint && hasComma)
        {
            if (!TryNormalizeEsSvThousands(text, out normalized))
            {
                return Error(AmbiguousMessage);
            }
        }
        else if (hasPoint)
        {
            if (!IsDecimalFormOrTrailingSeparator(text, '.'))
            {
                return Error(InvalidFormatMessage);
            }

            normalized = text.Length > 0 && text[^1] == '.' ? text[..^1] : text;
        }
        else if (hasComma)
        {
            var commaCount = text.Count(static character => character == ',');
            if (commaCount > 1)
            {
                if (!IsThousandsForm(text))
                {
                    return Error(InvalidFormatMessage);
                }

                normalized = text.Replace(",", string.Empty, StringComparison.Ordinal);
            }
            else
            {
                if (!IsDecimalFormOrTrailingSeparator(text, ','))
                {
                    return Error(InvalidFormatMessage);
                }

                var commaIndex = text.IndexOf(',');
                var decimalDigits = text.Length - commaIndex - 1;
                if (decimalDigits == 0)
                {
                    normalized = text[..commaIndex];
                }
                else
                {
                    if (decimalDigits == 3)
                    {
                        return Error(AmbiguousMessage);
                    }

                    normalized = text.Replace(',', '.');
                }
            }
        }
        else
        {
            if (!IsAsciiDigits(text))
            {
                return Error(InvalidFormatMessage);
            }

            normalized = text;
        }

        var separatorIndex = normalized.IndexOf('.');
        if (separatorIndex >= 0 && normalized.Length - separatorIndex - 1 > scale)
        {
            return Error($"Máximo {scale} decimales.");
        }

        var integerPart = separatorIndex >= 0 ? normalized[..separatorIndex] : normalized;
        var integerDigits = integerPart.TrimStart('0').Length;
        if (integerDigits > precision - scale)
        {
            return Error($"Máximo {precision - scale} dígitos enteros.");
        }

        var parseText = hasNegativeSign ? $"-{normalized}" : normalized;
        if (!decimal.TryParse(
                parseText,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return Error(InvalidFormatMessage);
        }

        return new DecimalInputResult(value, null);
    }

    private static bool TryNormalizeEsSvThousands(string text, out string normalized)
    {
        normalized = string.Empty;
        if (text.Count(static character => character == '.') != 1)
        {
            return false;
        }

        var pointIndex = text.IndexOf('.');
        var integerPart = text[..pointIndex];
        var decimalPart = text[(pointIndex + 1)..];
        if (!IsThousandsForm(integerPart) || !IsAsciiDigits(decimalPart))
        {
            return false;
        }

        normalized = integerPart.Replace(",", string.Empty, StringComparison.Ordinal) + "." + decimalPart;
        return true;
    }

    private static bool IsDecimalFormOrTrailingSeparator(string text, char separator)
    {
        var separatorIndex = text.IndexOf(separator);
        return separatorIndex > 0
            && separatorIndex == text.LastIndexOf(separator)
            && IsAsciiDigits(text[..separatorIndex])
            && (separatorIndex == text.Length - 1 || IsAsciiDigits(text[(separatorIndex + 1)..]));
    }

    private static bool IsThousandsForm(string text)
    {
        var groups = text.Split(',');
        return groups.Length > 1
            && groups[0].Length is >= 1 and <= 3
            && groups.All(static group => IsAsciiDigits(group))
            && groups.Skip(1).All(static group => group.Length == 3);
    }

    private static bool IsAsciiDigits(string text) =>
        text.Length > 0 && text.All(static character => character is >= '0' and <= '9');

    private static DecimalInputResult Error(string message) => new(null, message);
}
