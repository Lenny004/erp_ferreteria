using Ferreteria.PuntoVenta.Services.Security;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la política pura de autorización por permiso y estado vigente.</summary>
public sealed class PosAuthorizationPolicyTests
{
    /// <summary>Comprueba los permisos operativos y administrativos por tipo de empleado.</summary>
    [Theory]
    [InlineData(PosPermission.OperarCaja, true, false, false, true, "Cajero")]
    [InlineData(PosPermission.OperarInventario, false, true, false, true, "Vendedor")]
    [InlineData(PosPermission.AdministrarUsuarios, false, false, true, true, "Administrador")]
    [InlineData(PosPermission.AdministrarCatalogo, false, false, true, true, "Administrador")]
    [InlineData(PosPermission.AdministrarConfiguracion, false, false, true, true, "Administrador")]
    public void IsAllowed_ResuelvePermisoPorMinimoPrivilegio(
        PosPermission permission,
        bool canCashier,
        bool canSell,
        bool expectedForAdmin,
        bool isActive,
        string position)
    {
        var employee = new AuthorizedEmployee(Guid.NewGuid(), isActive, canSell, canCashier, position);
        var positions = expectedForAdmin ? new[] { " administrador " } : new[] { "Administrador" };

        var allowed = PosAuthorizationPolicy.IsAllowed(employee, permission, positions);

        Assert.Equal(permission switch
        {
            PosPermission.OperarCaja => canCashier,
            PosPermission.OperarInventario => canSell,
            _ => expectedForAdmin
        }, allowed);
    }

    /// <summary>Comprueba que un empleado inactivo no conserve ningún permiso.</summary>
    [Fact]
    public void IsAllowed_EmpleadoInactivo_NoTienePermisos()
    {
        var employee = new AuthorizedEmployee(Guid.NewGuid(), false, true, true, "Administrador");

        Assert.Empty(PosAuthorizationPolicy.GetGrantedPermissions(employee, new[] { "Administrador" }));
    }

    /// <summary>Comprueba la comparación de puestos sin sensibilidad a mayúsculas ni espacios.</summary>
    [Fact]
    public void IsAllowed_PuestoNormalizado_ConcedeAdministracion()
    {
        var employee = new AuthorizedEmployee(Guid.NewGuid(), true, false, false, "  aDmInIsTrAdOr  ");

        Assert.True(PosAuthorizationPolicy.IsAllowed(
            employee,
            PosPermission.AdministrarUsuarios,
            new[] { " administrador " }));
    }

    /// <summary>Comprueba que una configuración administrativa vacía falla cerrada.</summary>
    [Fact]
    public void IsAllowed_ListaAdministracionVacia_DeniegaAdministracion()
    {
        var employee = new AuthorizedEmployee(Guid.NewGuid(), true, false, false, "Administrador");

        Assert.False(PosAuthorizationPolicy.IsAllowed(
            employee,
            PosPermission.AdministrarCatalogo,
            Array.Empty<string>()));
    }
}
