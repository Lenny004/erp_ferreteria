namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Módulo operativo elegido en la pantalla de inicio de la ferretería.
/// </summary>
public enum OperationalModule
{
    /// <summary>Punto de venta / facturación (requiere <see cref="Models.Employee.CanCashier"/>).</summary>
    Caja,

    /// <summary>Gestión de stock, catálogo, proveedores y usuarios (requiere <see cref="Models.Employee.CanSell"/>).</summary>
    Inventario
}
