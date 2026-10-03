namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Desbloquea una terminal mediante una acción administrativa auditada.
/// </summary>
public interface IPinUnlockService
{
    /// <summary>
    /// Reinicia el contador y la progresión de fallos de una terminal.
    /// </summary>
    /// <param name="cashRegisterCode">Código de la terminal que se desea desbloquear.</param>
    /// <param name="actingEmployeeId">Id del administrador autenticado en la sesión actual.</param>
    /// <param name="reason">Motivo opcional del desbloqueo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Una tarea que representa la persistencia de la auditoría.</returns>
    /// <exception cref="Services.Security.UnauthorizedOperationException">
    /// El actuante no tiene permiso para administrar usuarios, está inactivo o no coincide con la sesión.
    /// </exception>
    /// <exception cref="ArgumentException">El código de terminal no es válido.</exception>
    Task UnlockTerminalAsync(
        string cashRegisterCode,
        Guid actingEmployeeId,
        string? reason,
        CancellationToken cancellationToken = default);
}
