namespace Ferreteria.PuntoVenta.Services;

public sealed class PinAttemptService : IPinAttemptService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(2);

    private readonly object _lock = new();
    private int _failedAttempts;
    private DateTime? _lockedUntilUtc;

    public PinAttemptStatus GetStatus()
    {
        lock (_lock)
        {
            ClearExpiredLockout();
            return BuildStatus();
        }
    }

    public PinAttemptStatus RegisterFailedAttempt()
    {
        lock (_lock)
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

    public void Reset()
    {
        lock (_lock)
        {
            _failedAttempts = 0;
            _lockedUntilUtc = null;
        }
    }

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
