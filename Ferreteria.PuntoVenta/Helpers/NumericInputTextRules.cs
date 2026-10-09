namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>Decisión pura para filtrar la edición de un campo decimal.</summary>
public static class NumericInputTextRules
{
    /// <summary>
    /// Determina si insertar o pegar texto conserva un decimal permitido.
    /// </summary>
    /// <param name="currentText">Texto actual del control.</param>
    /// <param name="selectionStart">Índice inicial de la selección.</param>
    /// <param name="selectionLength">Longitud de la selección reemplazada.</param>
    /// <param name="insertedText">Texto que se pretende insertar.</param>
    /// <param name="precision">Cantidad total máxima de dígitos.</param>
    /// <param name="scale">Cantidad máxima de dígitos decimales.</param>
    /// <param name="allowNegative">Indica si se permite el signo negativo.</param>
    /// <returns><see langword="true"/> cuando el texto resultante es permitido.</returns>
    public static bool IsAllowed(
        string? currentText,
        int selectionStart,
        int selectionLength,
        string? insertedText,
        int precision,
        int scale,
        bool allowNegative)
    {
        if (precision <= 0 || scale < 0 || scale > precision)
        {
            return false;
        }

        var current = currentText ?? string.Empty;
        var insertion = insertedText ?? string.Empty;
        if (selectionStart < 0 || selectionLength < 0 || selectionStart > current.Length
            || selectionStart + selectionLength > current.Length)
        {
            return false;
        }

        var candidate = current.Remove(selectionStart, selectionLength)
            .Insert(selectionStart, insertion);

        if (candidate.Length == 0 || (allowNegative && candidate == "-"))
        {
            return true;
        }

        if (candidate.Count(static character => character is '.' or ',') > 1
            || candidate.Count(static character => character == '-') > (allowNegative ? 1 : 0)
            || (candidate.Contains('-') && candidate[0] != '-'))
        {
            return false;
        }

        if (!candidate.All(static character => character is >= '0' and <= '9' or '.' or ',' or '-'))
        {
            return false;
        }

        return DecimalInputParser.Parse(candidate, precision, scale, allowNegative).IsValid;
    }
}
