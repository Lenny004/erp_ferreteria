using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Sesión local del <see cref="Employee"/> autenticado en el punto de venta (PIN + módulo activo).
/// </summary>
public sealed class CurrentSessionService : ICurrentSessionService
{
    private string? _initialSection;

    /// <inheritdoc />
    public Employee? CurrentEmployee { get; private set; }

    /// <inheritdoc />
    public OperationalModule? ActiveModule { get; private set; }

    /// <inheritdoc />
    public string? CurrentModule { get; private set; }

    /// <inheritdoc />
    public DateTime? StartedAtUtc { get; private set; }

    /// <inheritdoc />
    public bool IsActive => CurrentEmployee is not null && ActiveModule is not null;

    /// <inheritdoc />
    public void StartSession(Employee employee, OperationalModule module, string initialSection)
    {
        CurrentEmployee = employee;
        ActiveModule = module;
        CurrentModule = module.ToString();
        StartedAtUtc = DateTime.UtcNow;
        _initialSection = initialSection;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void EndSession()
    {
        CurrentEmployee = null;
        ActiveModule = null;
        CurrentModule = null;
        StartedAtUtc = null;
        _initialSection = null;
    }
}
