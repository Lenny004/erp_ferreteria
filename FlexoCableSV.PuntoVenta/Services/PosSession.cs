using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Empleado autenticado en el punto de venta (sesión local tras PIN).
/// </summary>
public static class PosSession
{
    public static Employee? CurrentEmployee { get; private set; }

    public static void Set(Employee employee) => CurrentEmployee = employee;

    public static void Clear() => CurrentEmployee = null;
}
