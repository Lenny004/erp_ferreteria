namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>
/// Permisos de alto nivel que protegen las operaciones sensibles del POS.
/// </summary>
public enum PosPermission
{
    /// <summary>Permite operar la caja y registrar ventas.</summary>
    OperarCaja,

    /// <summary>Permite operar existencias, entradas, ajustes y proveedores.</summary>
    OperarInventario,

    /// <summary>Permite administrar empleados, PIN, permisos y datos de RRHH.</summary>
    AdministrarUsuarios,

    /// <summary>Permite crear, modificar, activar y desactivar productos.</summary>
    AdministrarCatalogo,

    /// <summary>Permite cambiar la configuración de impresoras y del negocio.</summary>
    AdministrarConfiguracion
}
