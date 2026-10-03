namespace Ferreteria.PuntoVenta.Services;

/// <summary>Controla intentos fallidos de PIN mediante eventos persistentes de auditoría.</summary>
public interface IPinAttemptService
{
    /// <summary>Obtiene el estado persistente actual del terminal.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Estado de intentos y bloqueo vigente.</returns>
    Task<PinAttemptStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Registra de forma persistente un fallo y devuelve el estado resultante.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Estado actualizado tras el intento.</returns>
    Task<PinAttemptStatus> RegisterFailedAttemptAsync(CancellationToken cancellationToken = default);

    /// <summary>Registra un PIN correcto y reinicia la racha persistida.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Una tarea que representa la escritura del evento.</returns>
    Task ResetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Estado del bloqueo por intentos fallidos de PIN.</summary>
/// <param name="IsLocked">Indica si el teclado PIN debe permanecer bloqueado.</param>
/// <param name="FailedAttempts">Cantidad de fallos acumulados en la ventana actual.</param>
/// <param name="MaxAttempts">Umbral de fallos antes del bloqueo.</param>
/// <param name="LockedUntilUtc">Fin del bloqueo en UTC, o null si no hay bloqueo.</param>
public sealed record PinAttemptStatus(
    bool IsLocked,
    int FailedAttempts,
    int MaxAttempts,
    DateTime? LockedUntilUtc)
{
    /// <summary>Instante utilizado para calcular el tiempo restante.</summary>
    public DateTime? EvaluatedAtUtc { get; init; }

    /// <summary>Intentos restantes antes del bloqueo.</summary>
    public int RemainingAttempts => Math.Max(0, MaxAttempts - FailedAttempts);

    /// <summary>Tiempo restante de bloqueo, o cero si no está bloqueado.</summary>
    public TimeSpan RemainingLockout => LockedUntilUtc is null
        ? TimeSpan.Zero
        : LockedUntilUtc.Value - (EvaluatedAtUtc ?? DateTime.UtcNow);
}
