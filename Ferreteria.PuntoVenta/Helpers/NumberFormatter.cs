using System.Globalization;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>Formatea montos de la UI con la cultura monetaria de El Salvador.</summary>
public static class NumberFormatter
{
    private static readonly CultureInfo EsSv = CultureInfo.GetCultureInfo("es-SV");

    /// <summary>Formatea un monto en dólares estadounidenses con dos decimales.</summary>
    /// <param name="value">Monto que se mostrará.</param>
    /// <returns>Monto con el formato monetario de es-SV.</returns>
    public static string Currency(decimal value) => value.ToString("C2", EsSv);

    /// <summary>Formatea una cantidad con hasta tres decimales.</summary>
    /// <param name="value">Cantidad que se mostrará.</param>
    /// <returns>Cantidad con separadores de <c>es-SV</c>.</returns>
    public static string Quantity(decimal value) => value.ToString("0.###", EsSv);

    /// <summary>Formatea una fecha de negocio como día, mes y año.</summary>
    /// <param name="value">Fecha que se mostrará.</param>
    /// <returns>Fecha con formato <c>dd/MM/yyyy</c>.</returns>
    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", EsSv);

    /// <summary>Convierte un instante UTC y lo formatea en la zona horaria del negocio.</summary>
    /// <param name="utc">Instante UTC que se mostrará.</param>
    /// <param name="businessTimeZone">Zona horaria configurada para el negocio.</param>
    /// <returns>Fecha y hora local con formato <c>dd/MM/yyyy HH:mm</c>.</returns>
    public static string DateTimeUtc(DateTime utc, Services.Time.BusinessTimeZone businessTimeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), businessTimeZone.ZoneInfo)
            .ToString("dd/MM/yyyy HH:mm", EsSv);

    /// <summary>Formatea un instante UTC usando la zona de negocio inicializada.</summary>
    /// <param name="utc">Instante UTC que se mostrará.</param>
    /// <returns>Fecha y hora local del negocio.</returns>
    public static string DateTimeUtc(DateTime utc) =>
        Services.Time.TimeZoneSupport.ToLocalTime(utc).ToString("dd/MM/yyyy HH:mm", EsSv);
}
