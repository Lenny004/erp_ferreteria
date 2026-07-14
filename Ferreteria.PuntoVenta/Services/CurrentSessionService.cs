using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Sesión local del empleado autenticado en el punto de venta (PIN + módulo activo).
/// </summary>
public sealed class CurrentSessionService : ICurrentSessionService
{
    public Employee? CurrentEmployee { get; private set; }
    public OperationalModule? ActiveModule { get; private set; }
    public string? CurrentModule { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public bool IsActive => CurrentEmployee is not null && ActiveModule is not null;

    public void StartSession(Employee employee, OperationalModule module, string initialSection)
    {
        CurrentEmployee = employee;
        ActiveModule = module;
        CurrentModule = module.ToString();
        StartedAtUtc = DateTime.UtcNow;
        _initialSection = initialSection;
    }

    private string? _initialSection;

    public string ResolveInitialSection()
    {
        if (ActiveModule is not OperationalModule module)
        {
            return NavSections.Stock;
        }

        if (!string.IsNullOrWhiteSpace(_initialSection)
            && NavSections.BelongsToModule(_initialSection, module))
        {
            return _initialSection;
        }

        return NavSections.DefaultSection(module);
    }

    public void EndSession()
    {
        CurrentEmployee = null;
        ActiveModule = null;
        CurrentModule = null;
        StartedAtUtc = null;
        _initialSection = null;
    }
}
