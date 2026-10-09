using System.Globalization;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>Formatea montos de la UI con la cultura monetaria de El Salvador.</summary>
internal static class NumberFormatter
{
    private static readonly CultureInfo EsSv = CultureInfo.GetCultureInfo("es-SV");

    /// <summary>Formatea un monto en dólares estadounidenses con dos decimales.</summary>
    /// <param name="value">Monto que se mostrará.</param>
    /// <returns>Monto con el formato monetario de es-SV.</returns>
    internal static string Currency(decimal value) => value.ToString("C2", EsSv);
}
