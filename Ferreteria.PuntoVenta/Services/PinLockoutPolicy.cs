namespace Ferreteria.PuntoVenta.Services;

/// <summary>Evento mínimo utilizado para calcular el lockout sin consultar infraestructura.</summary>
/// <param name="Action">PIN_FAIL o PIN_OK.</param>
/// <param name="CreatedAtUtc">Fecha UTC del evento.</param>
public sealed record PinLockoutEvent(string Action, DateTimeOffset CreatedAtUtc);

/// <summary>Política pura de cinco fallos y dos minutos de bloqueo.</summary>
public static class PinLockoutPolicy
{
    /// <summary>Cantidad de fallos consecutivos que activa el bloqueo.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Duración del bloqueo temporal.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(2);

    /// <summary>Calcula el estado a partir de eventos del terminal y un reloj inyectado.</summary>
    /// <param name="events">Eventos de PIN del terminal.</param>
    /// <param name="nowUtc">Instante actual inyectado.</param>
    /// <returns>Estado calculado del lockout.</returns>
    public static PinAttemptStatus Evaluate(IEnumerable<PinLockoutEvent> events, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(events);
        var failedAttempts = 0;
        DateTimeOffset? lockedUntil = null;

        foreach (var pinEvent in events.OrderBy(item => item.CreatedAtUtc))
        {
            if (string.Equals(pinEvent.Action, "PIN_OK", StringComparison.Ordinal))
            {
                failedAttempts = 0;
                lockedUntil = null;
                continue;
            }

            if (!string.Equals(pinEvent.Action, "PIN_FAIL", StringComparison.Ordinal))
            {
                continue;
            }

            if (lockedUntil is { } previousLock && previousLock <= nowUtc)
            {
                failedAttempts = 0;
                lockedUntil = null;
            }

            if (lockedUntil is not null)
            {
                continue;
            }

            failedAttempts++;
            if (failedAttempts >= MaxAttempts)
            {
                lockedUntil = pinEvent.CreatedAtUtc + LockoutDuration;
            }
        }

        if (lockedUntil is { } expired && expired <= nowUtc)
        {
            failedAttempts = 0;
            lockedUntil = null;
        }

        return new PinAttemptStatus(
            lockedUntil is not null,
            failedAttempts,
            MaxAttempts,
            lockedUntil?.UtcDateTime)
        {
            EvaluatedAtUtc = nowUtc.UtcDateTime
        };
    }
}
