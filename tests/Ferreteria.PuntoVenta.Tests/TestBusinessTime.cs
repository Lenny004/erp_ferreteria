using Ferreteria.PuntoVenta.Services.Time;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Inicializa de forma explícita la fachada <see cref="TimeZoneSupport"/> en pruebas unitarias
/// que formatean fechas, para no depender del orden en que corre el fixture de PostgreSQL.
/// </summary>
internal static class TestBusinessTime
{
    /// <summary>Zona de negocio usada por las pruebas.</summary>
    public const string ZoneId = "America/El_Salvador";

    /// <summary>Inicializa la fachada con la zona de pruebas; es idempotente.</summary>
    public static void EnsureInitialized()
    {
        TimeZoneSupport.Initialize(BusinessTimeZone.Create(new BusinessTimeOptions { ZonaHoraria = ZoneId }));
    }
}
