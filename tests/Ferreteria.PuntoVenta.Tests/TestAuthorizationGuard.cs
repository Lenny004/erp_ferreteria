using Ferreteria.PuntoVenta.Services.Security;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Doble explícito de autorización para flujos de integración que no prueban RBAC.
/// Los casos de permisos usan <see cref="AuthorizationGuard"/> contra la BD del fixture.
/// </summary>
internal sealed class TestAuthorizationGuard : IAuthorizationGuard
{
    /// <inheritdoc />
    public Task<AuthorizedEmployee> RequireAsync(
        PosPermission permission,
        Guid? actingEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actingEmployeeId is not Guid employeeId || employeeId == Guid.Empty)
        {
            throw new UnauthorizedOperationException(permission);
        }

        return Task.FromResult(new AuthorizedEmployee(employeeId, true, true, true, "Prueba"));
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<PosPermission>> GetGrantedPermissionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlySet<PosPermission>>(Enum.GetValues<PosPermission>().ToHashSet());
    }
}
