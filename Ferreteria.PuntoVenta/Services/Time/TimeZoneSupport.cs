namespace Ferreteria.PuntoVenta.Services.Time;

/// <summary>
/// Fachada estática para formatear horas visibles con la zona configurada del negocio.
/// </summary>
/// <remarks>
/// La fachada existe porque contratos de presentación e impresión son tipos puros que no
/// participan en DI. Se inicializa una sola vez al arrancar con <see cref="BusinessTimeZone"/>
/// y falla de forma explícita si se usa antes, evitando volver a resolver una zona fija o usar UTC-6 como respaldo.
/// </remarks>
public static class TimeZoneSupport
{
    private static readonly object SyncRoot = new();
    private static BusinessTimeZone? _businessTimeZone;

    /// <summary>
    /// Inicializa la fachada con la zona validada del proceso.
    /// </summary>
    /// <param name="businessTimeZone">Zona horaria configurada.</param>
    /// <exception cref="InvalidOperationException">Si se intenta cambiar una zona ya inicializada.</exception>
    public static void Initialize(BusinessTimeZone businessTimeZone)
    {
        ArgumentNullException.ThrowIfNull(businessTimeZone);
        lock (SyncRoot)
        {
            if (_businessTimeZone is null)
            {
                _businessTimeZone = businessTimeZone;
                return;
            }

            if (!string.Equals(_businessTimeZone.IanaId, businessTimeZone.IanaId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TimeZoneSupport ya fue inicializada con otra zona horaria.");
            }
        }
    }

    /// <summary>Convierte un instante UTC a la hora local configurada.</summary>
    /// <param name="utc">Instante que se interpretará como UTC.</param>
    /// <returns>Hora local sin información de zona.</returns>
    /// <exception cref="InvalidOperationException">Si la fachada no fue inicializada.</exception>
    public static DateTime ToLocalTime(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            GetZone().ZoneInfo);
    }

    /// <summary>Convierte una hora local configurada a UTC.</summary>
    /// <param name="localUnspecified">Hora local sin información de zona.</param>
    /// <returns>Instante UTC.</returns>
    /// <exception cref="InvalidOperationException">Si la fachada no fue inicializada.</exception>
    public static DateTime ToUtc(DateTime localUnspecified)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified),
            GetZone().ZoneInfo);
    }

    private static BusinessTimeZone GetZone()
    {
        return _businessTimeZone
            ?? throw new InvalidOperationException(
                "TimeZoneSupport debe inicializarse con la configuración 'Negocio:ZonaHoraria' antes de usarse.");
    }
}
