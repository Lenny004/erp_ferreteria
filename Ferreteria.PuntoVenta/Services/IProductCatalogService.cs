using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD del catálogo de productos con validación y auditoría.</summary>
public interface IProductCatalogService
{
    Task<IReadOnlyList<Product>> GetProductsAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateProductAsync(ProductInput input, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateProductAsync(Guid id, ProductInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Baja lógica (IsActive = false). No se borran registros por trazabilidad.</summary>
    Task DeactivateProductAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task ReactivateProductAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Family>> GetFamiliesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subfamily>> GetSubfamiliesAsync(Guid familyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeasurementType>> GetMeasurementTypesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Datos de entrada para crear o actualizar un producto.</summary>
public sealed record ProductInput(
    string Code,
    string? Barcode,
    string Description,
    Guid FamilyId,
    Guid? SubfamilyId,
    Guid MeasurementTypeId,
    Guid? SupplierId,
    decimal SalePrice,
    decimal CostPrice,
    decimal CurrentStock,
    decimal MinStock,
    decimal? MaxStock,
    decimal? ReorderPoint,
    string? Notes);
