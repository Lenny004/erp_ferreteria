using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>Gestión básica de empleados / usuarios del punto de venta (RRHH).</summary>
public interface IEmployeeService
{
    Task<IReadOnlyList<Employee>> GetEmployeesAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(EmployeeInput input, string? pin, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, EmployeeInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Asigna o cambia el PIN (4 dígitos) de un empleado. Se guarda como hash bcrypt.</summary>
    Task SetPinAsync(Guid id, string pin, Guid userId, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Position>> GetPositionsAsync(Guid? departmentId, CancellationToken cancellationToken = default);
}

/// <summary>Datos básicos de un empleado / usuario.</summary>
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
