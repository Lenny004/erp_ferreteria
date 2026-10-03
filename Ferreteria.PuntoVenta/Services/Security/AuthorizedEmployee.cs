namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>
/// Snapshot vigente del empleado usado para una decisión de autorización.
/// </summary>
/// <param name="Id">Identificador del empleado.</param>
/// <param name="IsActive">Indica si el empleado sigue activo.</param>
/// <param name="CanSell">Permiso operativo de inventario.</param>
/// <param name="CanCashier">Permiso operativo de caja.</param>
/// <param name="PositionName">Nombre vigente del puesto.</param>
public sealed record AuthorizedEmployee(
    Guid Id,
    bool IsActive,
    bool CanSell,
    bool CanCashier,
    string? PositionName);
