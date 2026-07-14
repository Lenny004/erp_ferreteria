using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>CRUD de proveedores con validación y auditoría.</summary>
public interface ISupplierService
{
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(SupplierInput input, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, SupplierInput input, Guid userId, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Datos de entrada para crear o actualizar un proveedor.</summary>
public sealed record SupplierInput(
    string Name,
    string? TradeName,
    string? Nit,
    string? Nrc,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? Municipality,
    string? Department,
    int CreditDays,
    string? Notes);
