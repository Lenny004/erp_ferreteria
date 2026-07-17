using System.Globalization;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>
/// Convierte montos a su representacion en letras en espanol para el campo
/// <c>totalLetras</c> del DTE y para la representacion grafica del ticket.
/// Formato: "CIENTO TRECE 00/100 DOLARES".
/// </summary>
public static class SpanishNumberToWords
{
    private static readonly string[] Unidades =
    {
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISEIS", "DIECISIETE",
        "DIECIOCHO", "DIECINUEVE", "VEINTE"
    };

    private static readonly string[] Decenas =
    {
        "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    };

    private static readonly string[] Centenas =
    {
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    };

    /// <summary>Convierte un monto en dolares al formato de letras del DTE.</summary>
    /// <param name="amount">Monto a convertir (dos decimales).</param>
    /// <returns>Cadena en mayusculas: "&lt;entero en letras&gt; NN/100 DOLARES".</returns>
    public static string Convert(decimal amount)
    {
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var integerPart = (long)Math.Floor(rounded);
        var cents = (int)Math.Round((rounded - integerPart) * 100m, MidpointRounding.AwayFromZero);

        var words = integerPart == 0 ? "CERO" : ConvertInteger(integerPart);
        var centsText = cents.ToString("00", CultureInfo.InvariantCulture);

        return $"{words} {centsText}/100 DOLARES";
    }

    private static string ConvertInteger(long number)
    {
        if (number == 0)
        {
            return "CERO";
        }

        if (number < 0)
        {
            return "MENOS " + ConvertInteger(-number);
        }

        var result = string.Empty;

        var millones = number / 1_000_000;
        var resto = number % 1_000_000;

        if (millones > 0)
        {
            result += millones == 1 ? "UN MILLON" : ConvertInteger(millones) + " MILLONES";
            if (resto > 0)
            {
                result += " ";
            }
        }

        if (resto > 0)
        {
            result += ConvertBelowMillion(resto);
        }

        return result.Trim();
    }

    private static string ConvertBelowMillion(long number)
    {
        var result = string.Empty;

        var miles = number / 1000;
        var resto = number % 1000;

        if (miles > 0)
        {
            result += miles == 1 ? "MIL" : ConvertBelowThousand(miles) + " MIL";
            if (resto > 0)
            {
                result += " ";
            }
        }

        if (resto > 0)
        {
            result += ConvertBelowThousand(resto);
        }

        return result.Trim();
    }

    private static string ConvertBelowThousand(long number)
    {
        if (number == 100)
        {
            return "CIEN";
        }

        var result = string.Empty;
        var centena = number / 100;
        var resto = number % 100;

        if (centena > 0)
        {
            result += Centenas[centena] + " ";
        }

        result += ConvertBelowHundred(resto);

        return result.Trim();
    }

    private static string ConvertBelowHundred(long number)
    {
        if (number <= 20)
        {
            return Unidades[number];
        }

        if (number < 30)
        {
            return "VEINTI" + Unidades[number - 20].ToLowerInvariant().ToUpperInvariant();
        }

        var decena = number / 10;
        var unidad = number % 10;

        if (unidad == 0)
        {
            return Decenas[decena];
        }

        return Decenas[decena] + " Y " + Unidades[unidad];
    }
}
