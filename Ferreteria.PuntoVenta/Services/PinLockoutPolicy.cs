using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>Evento mínimo utilizado para calcular el lockout sin consultar infraestructura.</summary>
/// <param name="Action">PIN_FAIL, PIN_OK o PIN_UNLOCK.</param>
/// <param name="CreatedAtUtc">Fecha UTC del evento.</param>
public sealed record PinLockoutEvent(string Action, DateTimeOffset CreatedAtUtc);

/// <summary>Política pura de fallos por terminal y bloqueos progresivos.</summary>
public static class PinLockoutPolicy
{
    /// <summary>Umbral predeterminado de fallos que activa el bloqueo.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Duración predeterminada del primer bloqueo temporal.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(2);

    /// <summary>Calcula el estado con los parámetros predeterminados.</summary>
    /// <param name="events">Eventos de PIN del terminal.</param>
    /// <param name="nowUtc">Instante actual inyectado.</param>
    /// <returns>Estado calculado del lockout.</returns>
    public static PinAttemptStatus Evaluate(IEnumerable<PinLockoutEvent> events, DateTimeOffset nowUtc)
    {
        return Evaluate(events, nowUtc, new PinLockoutOptions());
    }

    /// <summary>
    /// Calcula el estado a partir de los fallos recientes del terminal.
    /// </summary>
    /// <param name="events">Eventos de PIN del terminal.</param>
    /// <param name="nowUtc">Instante actual inyectado.</param>
    /// <param name="options">Parámetros de umbral, ventana y progresión.</param>
    /// <returns>Estado calculado del lockout.</returns>
    public static PinAttemptStatus Evaluate(
        IEnumerable<PinLockoutEvent> events,
        DateTimeOffset nowUtc,
        PinLockoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var windowStart = nowUtc - TimeSpan.FromMinutes(options.WindowMinutes);
        var lastUnlock = events
            .Where(item => string.Equals(
                item.Action,
                SalesDomainConstants.PinAuditActions.PinUnlock,
                StringComparison.Ordinal)
                && item.CreatedAtUtc <= nowUtc)
            .Select(item => (DateTimeOffset?)item.CreatedAtUtc)
            .Max();
        var failedAttempts = 0;
        var lockoutCount = 0;
        DateTimeOffset? lockedUntil = null;

        foreach (var pinEvent in events
            .Where(item => string.Equals(
                    item.Action,
                    SalesDomainConstants.PinAuditActions.PinFail,
                    StringComparison.Ordinal)
                && (lastUnlock is null || item.CreatedAtUtc > lastUnlock.Value)
                && item.CreatedAtUtc >= windowStart
                && item.CreatedAtUtc <= nowUtc)
            .OrderBy(item => item.CreatedAtUtc))
        {
            if (lockedUntil is { } previousLock)
            {
                if (previousLock > pinEvent.CreatedAtUtc)
                {
                    continue;
                }

                lockedUntil = null;
            }

            failedAttempts++;
            if (failedAttempts >= options.MaxAttempts)
            {
                lockoutCount++;
                lockedUntil = pinEvent.CreatedAtUtc + CalculateLockoutDuration(options, lockoutCount);
            }
        }

        if (lockedUntil is { } expired && expired <= nowUtc)
        {
            lockedUntil = null;
        }

        return new PinAttemptStatus(
            lockedUntil is not null,
            failedAttempts,
            options.MaxAttempts,
            lockedUntil?.UtcDateTime)
        {
            EvaluatedAtUtc = nowUtc.UtcDateTime
        };
    }

    private static TimeSpan CalculateLockoutDuration(PinLockoutOptions options, int lockoutCount)
    {
        var minutes = options.InitialLockoutMinutes
            * Math.Pow(options.ProgressiveMultiplier, Math.Max(0, lockoutCount - 1));
        return TimeSpan.FromMinutes(Math.Min(options.MaxLockoutMinutes, minutes));
    }

    private static void ValidateOptions(PinLockoutOptions options)
    {
        if (options.MaxAttempts <= 0
            || options.WindowMinutes <= 0
            || options.InitialLockoutMinutes <= 0
            || options.ProgressiveMultiplier < 1d
            || options.MaxLockoutMinutes < options.InitialLockoutMinutes)
        {
            throw new ArgumentException("La configuración de bloqueo de PIN no es válida.", nameof(options));
        }
    }
}
