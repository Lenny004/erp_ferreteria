namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>Contrato de autorización zero trust para operaciones del POS.</summary>
public interface IAuthorizationGuard
{
    /// <summary>
    /// Recarga el empleado actual y exige el permiso indicado.
    /// </summary>
    /// <param name="permission">Permiso requerido.</param>
    /// <param name="actingEmployeeId">Id declarado por la operación, si aplica.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Snapshot vigente del empleado autorizado.</returns>
    /// <exception cref="UnauthorizedOperationException">No existe sesión o el permiso fue rechazado.</exception>
    Task<AuthorizedEmployee> RequireAsync(
        PosPermission permission,
        Guid? actingEmployeeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene los permisos vigentes del empleado de la sesión.</summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Permisos concedidos; vacío si no hay sesión autorizable.</returns>
    Task<IReadOnlySet<PosPermission>> GetGrantedPermissionsAsync(CancellationToken cancellationToken = default);
}
