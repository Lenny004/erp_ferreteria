namespace Ferreteria.PuntoVenta.Services.Time;

/// <summary>
/// Reglas puras para convertir instantes y fechas entre UTC y el día de negocio.
/// </summary>
public sealed class BusinessCalendar
{
    private readonly BusinessTimeZone _timeZone;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Inicializa el calendario con una zona validada y un reloj inyectable.
    /// </summary>
    /// <param name="timeZone">Zona horaria del negocio.</param>
    /// <param name="clock">Reloj usado para calcular la fecha actual.</param>
    public BusinessCalendar(BusinessTimeZone timeZone, TimeProvider clock)
    {
        _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Obtiene la fecha actual en la zona del negocio.</summary>
    /// <returns>Fecha local de negocio.</returns>
    public DateOnly Today() => ToLocalDate(_clock.GetUtcNow().UtcDateTime);

    /// <summary>Obtiene el instante UTC actual del reloj de negocio inyectado.</summary>
    /// <returns>Instante UTC usado por operaciones fiscales y recibos.</returns>
    public DateTime UtcNow() => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Obtiene la zona horaria validada usada por el calendario.</summary>
    public BusinessTimeZone TimeZone => _timeZone;

    /// <summary>Convierte un instante UTC a la fecha local del negocio.</summary>
    /// <param name="utc">Instante que se interpretará como UTC.</param>
    /// <returns>Fecha local correspondiente.</returns>
    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    /// <summary>Convierte un instante UTC a la hora local del negocio.</summary>
    /// <param name="utc">Instante que se interpretará como UTC.</param>
    /// <returns>Hora local sin información de zona.</returns>
    public DateTime ToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            _timeZone.ZoneInfo);
    }

    /// <summary>Convierte una hora local sin zona a UTC.</summary>
    /// <param name="localUnspecified">Hora local sin información de zona.</param>
    /// <returns>Instante UTC.</returns>
    public DateTime ToUtc(DateTime localUnspecified)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified),
            _timeZone.ZoneInfo);
    }

    /// <summary>Obtiene el rango UTC semiabierto de un día local.</summary>
    /// <param name="day">Día local del negocio.</param>
    /// <returns>Rango <c>[inicio, fin)</c> con límites UTC.</returns>
    public (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly day)
    {
        return RangeUtc(day, day);
    }

    /// <summary>Obtiene el rango UTC semiabierto de fechas locales inclusivas.</summary>
    /// <param name="from">Primer día local.</param>
    /// <param name="toInclusive">Último día local incluido.</param>
    /// <returns>Rango <c>[inicio, fin)</c> con el inicio del día siguiente como fin.</returns>
    /// <exception cref="ArgumentException">Si <paramref name="from"/> es posterior a <paramref name="toInclusive"/>.</exception>
    public (DateTime StartUtc, DateTime EndUtc) RangeUtc(DateOnly from, DateOnly toInclusive)
    {
        if (from > toInclusive)
        {
            throw new ArgumentException("Desde no puede ser posterior a Hasta.");
        }

        var startUtc = ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var endUtc = ToUtc(toInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return (DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), DateTime.SpecifyKind(endUtc, DateTimeKind.Utc));
    }
}
