using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD del catálogo de <see cref="Product"/> con validación y auditoría.</summary>
public interface IProductCatalogService
{
    /// <summary>Lista productos con familia, subfamilia, unidad y proveedor.</summary>
    Task<IReadOnlyList<Product>> GetProductsAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto por Id, o null si no existe.</summary>
    Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Alta de producto en catálogo. El código debe ser único.</summary>
    /// <returns>Id del <see cref="Product"/> creado.</returns>
    Task<Guid> CreateProductAsync(ProductInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza precios, stock mínimo y datos de catálogo.</summary>
    Task UpdateProductAsync(Guid id, ProductInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Baja lógica (<c>IsActive = false</c>). No se borran registros por trazabilidad.</summary>
    Task DeactivateProductAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Reactiva un producto previamente dado de baja.</summary>
    Task ReactivateProductAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Familias activas para combos de catálogo.</summary>
    Task<IReadOnlyList<Family>> GetFamiliesAsync(CancellationToken cancellationToken = default);

    /// <summary>Subfamilias de una familia.</summary>
    Task<IReadOnlyList<Subfamily>> GetSubfamiliesAsync(Guid familyId, CancellationToken cancellationToken = default);

    /// <summary>Tipos de medida / unidades base del inventario.</summary>
    Task<IReadOnlyList<MeasurementType>> GetMeasurementTypesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Datos de entrada para crear o actualizar un <see cref="Product"/>.</summary>
/// <param name="Code">Código interno único.</param>
/// <param name="Barcode">Código de barras (opcional).</param>
/// <param name="Description">Descripción comercial.</param>
/// <param name="FamilyId">Familia del producto.</param>
/// <param name="SubfamilyId">Subfamilia opcional.</param>
/// <param name="MeasurementTypeId">Unidad de medida base.</param>
/// <param name="SupplierId">Proveedor habitual opcional.</param>
/// <param name="SalePrice">Precio de venta.</param>
/// <param name="CostPrice">Costo unitario.</param>
/// <param name="CurrentStock">Stock actual (solo en alta o según reglas del servicio).</param>
/// <param name="MinStock">Umbral de alerta de stock bajo.</param>
/// <param name="MaxStock">Stock máximo opcional.</param>
/// <param name="ReorderPoint">Punto de reorden opcional.</param>
/// <param name="Notes">Notas internas.</param>
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
