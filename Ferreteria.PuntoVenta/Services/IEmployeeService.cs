using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>Gestión de empleados / usuarios del punto de venta (<c>hr.Employees</c>).</summary>
public interface IEmployeeService
{
    /// <summary>Lista empleados con cargo y departamento, filtrados por nombre o DUI.</summary>
    Task<IReadOnlyList<Employee>> GetEmployeesAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un empleado por Id (incluye Position y Department).</summary>
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un empleado. Si se envía PIN, se hashea con bcrypt (12 rounds) antes de persistir.
    /// Riesgo: el parámetro <paramref name="pin"/> llega en claro; no debe registrarse en logs ni auditoría.
    /// </summary>
    /// <returns>Id del <see cref="Employee"/> creado.</returns>
    Task<Guid> CreateAsync(EmployeeInput input, string? pin, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza datos laborales y permisos POS (CanCashier / CanSell). No cambia el PIN.</summary>
    Task UpdateAsync(Guid id, EmployeeInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna o cambia el PIN (4 dígitos). Se guarda solo el hash bcrypt en <c>PinHash</c>.
    /// La auditoría registra PIN_CHANGE sin el valor del PIN.
    /// </summary>
    Task SetPinAsync(Guid id, string pin, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Baja lógica del empleado (<c>IsActive = false</c>).</summary>
    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Catálogo de departamentos activos para combos de RRHH.</summary>
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Cargos activos, opcionalmente filtrados por departamento.</summary>
    Task<IReadOnlyList<Position>> GetPositionsAsync(Guid? departmentId, CancellationToken cancellationToken = default);
}

/// <summary>Datos básicos de un <see cref="Employee"/> / usuario del POS.</summary>
/// <param name="FirstName">Nombres.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Dui">DUI único si se indica.</param>
/// <param name="PositionId">Cargo (<see cref="Position"/>).</param>
/// <param name="DepartmentId">Departamento (<see cref="Department"/>).</param>
/// <param name="HireDate">Fecha de ingreso.</param>
/// <param name="BaseSalary">Salario base (≥ 0).</param>
/// <param name="ContractType">PLAZO_FIJO | TIEMPO_PARCIAL | HONORARIOS | PASANTE.</param>
/// <param name="SalaryType">MENSUAL | QUINCENAL | SEMANAL.</param>
/// <param name="Phone">Teléfono.</param>
/// <param name="Email">Correo.</param>
/// <param name="CanCashier">Permiso de módulo Caja.</param>
/// <param name="CanSell">Permiso de módulo Inventario / venta.</param>
public sealed record EmployeeInput(
    string FirstName,
    string LastName,
    string? Dui,
    Guid? PositionId,
    Guid? DepartmentId,
    DateTime HireDate,
    decimal BaseSalary,
    string ContractType,
    string SalaryType,
    string? Phone,
    string? Email,
    bool CanCashier,
    bool CanSell);
