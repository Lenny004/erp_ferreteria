namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Controla intentos fallidos de PIN en el cliente WPF (bloqueo temporal anti fuerza bruta).
/// El estado vive solo en memoria del proceso; no sustituye el hash bcrypt en <c>hr.Employees.PinHash</c>.
/// </summary>
public interface IPinAttemptService
{
    /// <summary>Obtiene el estado actual (intentos, bloqueo y tiempo restante).</summary>
    PinAttemptStatus GetStatus();

    /// <summary>
    /// Registra un intento fallido. Tras alcanzar el máximo, aplica bloqueo temporal.
    /// </summary>
    /// <returns>Estado actualizado tras el intento.</returns>
    PinAttemptStatus RegisterFailedAttempt();

    /// <summary>Reinicia contadores tras un PIN correcto o al cerrar el diálogo.</summary>
    void Reset();
}

/// <summary>Estado del bloqueo por intentos fallidos de PIN.</summary>
/// <param name="IsLocked">True si el teclado PIN debe permanecer bloqueado.</param>
/// <param name="FailedAttempts">Cantidad de fallos acumulados en la ventana actual.</param>
/// <param name="MaxAttempts">Umbral de fallos antes del bloqueo.</param>
/// <param name="LockedUntilUtc">Fin del bloqueo en UTC, o null si no hay bloqueo.</param>
public sealed record PinAttemptStatus(
    bool IsLocked,
    int FailedAttempts,
    int MaxAttempts,
    DateTime? LockedUntilUtc)
{
    /// <summary>Intentos restantes antes del bloqueo.</summary>
    public int RemainingAttempts => Math.Max(0, MaxAttempts - FailedAttempts);

    /// <summary>Tiempo restante de bloqueo (cero si ya expiró o no hay bloqueo).</summary>
    public TimeSpan RemainingLockout => LockedUntilUtc is null
        ? TimeSpan.Zero
        : LockedUntilUtc.Value - DateTime.UtcNow;
}
