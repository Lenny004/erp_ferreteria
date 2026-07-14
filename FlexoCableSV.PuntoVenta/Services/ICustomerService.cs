using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>CRUD de clientes con validación y auditoría.</summary>
public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> GetCustomersAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CustomerInput input, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, CustomerInput input, Guid userId, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Datos de entrada para crear o actualizar un cliente.</summary>
public sealed record CustomerInput(
    string CustomerType,
    string Name,
    string? Dui,
    string? Nit,
    string? Nrc,
    string? Phone,
    string? Email,
    string? Address,
    string? Municipality,
    string? Department);
