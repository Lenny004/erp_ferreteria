using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Sesión local del empleado autenticado en el punto de venta (PIN + módulo activo).
/// </summary>
public sealed class CurrentSessionService : ICurrentSessionService
{
    public Employee? CurrentEmployee { get; private set; }
    public string? CurrentModule { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public bool IsActive => CurrentEmployee is not null;

    public void StartSession(Employee employee, string moduleName)
    {
        CurrentEmployee = employee;
        CurrentModule = moduleName;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void EndSession()
    {
        CurrentEmployee = null;
        CurrentModule = null;
        StartedAtUtc = null;
    }
}
