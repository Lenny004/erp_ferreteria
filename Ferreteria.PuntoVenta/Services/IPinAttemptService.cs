namespace Ferreteria.PuntoVenta.Services;

public interface IPinAttemptService
{
    PinAttemptStatus GetStatus();
    PinAttemptStatus RegisterFailedAttempt();
    void Reset();
}

public sealed record PinAttemptStatus(
    bool IsLocked,
    int FailedAttempts,
    int MaxAttempts,
    DateTime? LockedUntilUtc)
{
    public int RemainingAttempts => Math.Max(0, MaxAttempts - FailedAttempts);

    public TimeSpan RemainingLockout => LockedUntilUtc is null
        ? TimeSpan.Zero
        : LockedUntilUtc.Value - DateTime.UtcNow;
}
