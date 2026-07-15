namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Implementación en memoria de <see cref="IPinAttemptService"/>.
/// Política: 5 fallos → bloqueo de 2 minutos (UTC). Thread-safe con lock.
/// </summary>
public sealed class PinAttemptService : IPinAttemptService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(2);

    private readonly object _syncRoot = new();
    private int _failedAttempts;
    private DateTime? _lockedUntilUtc;

    /// <inheritdoc />
    public PinAttemptStatus GetStatus()
    {
        lock (_syncRoot)
        {
            ClearExpiredLockout();
            return BuildStatus();
        }
    }

    /// <inheritdoc />
    public PinAttemptStatus RegisterFailedAttempt()
    {
        lock (_syncRoot)
        {
            ClearExpiredLockout();

            if (_lockedUntilUtc is not null)
            {
                return BuildStatus();
            }

            _failedAttempts++;
            if (_failedAttempts >= MaxAttempts)
            {
                _lockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            }

            return BuildStatus();
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_syncRoot)
        {
            _failedAttempts = 0;
            _lockedUntilUtc = null;
        }
    }

    /// <summary>Si el bloqueo ya venció, reinicia contadores.</summary>
    private void ClearExpiredLockout()
    {
        if (_lockedUntilUtc is null || _lockedUntilUtc > DateTime.UtcNow)
        {
            return;
        }

        _failedAttempts = 0;
        _lockedUntilUtc = null;
    }

    private PinAttemptStatus BuildStatus()
    {
        return new PinAttemptStatus(
            _lockedUntilUtc is not null,
            _failedAttempts,
            MaxAttempts,
            _lockedUntilUtc);
    }
}
