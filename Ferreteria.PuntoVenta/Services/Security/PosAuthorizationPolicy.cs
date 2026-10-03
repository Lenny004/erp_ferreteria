using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>
/// Política pura y determinista de permisos del punto de venta.
/// </summary>
public static class PosAuthorizationPolicy
{
    /// <summary>
    /// Evalúa un snapshot de empleado sin consultar servicios externos ni la base de datos.
    /// </summary>
    /// <param name="employee">Snapshot del empleado.</param>
    /// <param name="permission">Permiso que se desea evaluar.</param>
    /// <param name="administrationPositions">Puestos configurados como administrativos.</param>
    /// <returns><c>true</c> si el empleado puede ejecutar el permiso.</returns>
    public static bool IsAllowed(
        AuthorizedEmployee employee,
        PosPermission permission,
        IEnumerable<string>? administrationPositions)
    {
        ArgumentNullException.ThrowIfNull(employee);

        if (!employee.IsActive)
        {
            return false;
        }

        var isAdministrator = administrationPositions?.Any(position =>
            string.Equals(position?.Trim(), employee.PositionName?.Trim(), StringComparison.OrdinalIgnoreCase)) == true;

        return permission switch
        {
            PosPermission.OperarCaja => employee.CanCashier,
            PosPermission.OperarInventario => employee.CanSell,
            PosPermission.AdministrarUsuarios => isAdministrator,
            PosPermission.AdministrarCatalogo => isAdministrator,
            PosPermission.AdministrarConfiguracion => isAdministrator,
            _ => false
        };
    }

    /// <summary>Convierte una entidad recargada en un snapshot seguro para la política.</summary>
    /// <param name="employee">Entidad de empleado recargada de la base de datos.</param>
    /// <returns>Snapshot sin hash de PIN ni datos personales innecesarios.</returns>
    public static AuthorizedEmployee Snapshot(Employee employee) =>
        new(employee.Id, employee.IsActive, employee.CanSell, employee.CanCashier, employee.Position?.Name);

    /// <summary>Obtiene todos los permisos concedidos para un snapshot.</summary>
    /// <param name="employee">Snapshot del empleado.</param>
    /// <param name="administrationPositions">Puestos administrativos configurados.</param>
    /// <returns>Conjunto inmutable de permisos efectivos.</returns>
    public static IReadOnlySet<PosPermission> GetGrantedPermissions(
        AuthorizedEmployee employee,
        IEnumerable<string>? administrationPositions)
    {
        var permissions = Enum.GetValues<PosPermission>()
            .Where(permission => IsAllowed(employee, permission, administrationPositions))
            .ToHashSet();
        return permissions;
    }
}
