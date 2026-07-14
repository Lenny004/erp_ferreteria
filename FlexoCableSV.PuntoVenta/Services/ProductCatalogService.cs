using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// CRUD del catálogo de productos de ferretería. Valida entradas y registra auditoría
/// en cada operación que modifica datos (CREATE/UPDATE/DELETE lógico).
/// </summary>
public sealed class ProductCatalogService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService) : IProductCatalogService
{
    private const string TableName = "public.Products";

    public async Task<IReadOnlyList<Product>> GetProductsAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var query = db.Products
            .AsNoTracking()
            .Include(p => p.Family)
            .Include(p => p.Subfamily)
            .Include(p => p.MeasurementType)
            .Include(p => p.Supplier)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var like = $"%{searchText.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Code, like) ||
                EF.Functions.ILike(p.Description, like) ||
                EF.Functions.ILike(p.Family.Name, like) ||
                (p.Barcode != null && EF.Functions.ILike(p.Barcode, like)));
        }

        return await query
            .OrderBy(p => p.Code)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        return await db.Products
            .AsNoTracking()
            .Include(p => p.Family)
            .Include(p => p.Subfamily)
            .Include(p => p.MeasurementType)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Guid> CreateProductAsync(ProductInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var normalizedCode = input.Code.Trim().ToUpperInvariant();

        if (await db.Products.AnyAsync(p => p.Code == normalizedCode, cancellationToken))
        {
            throw new ValidationException($"Ya existe un producto con el código '{normalizedCode}'.");
        }

        await EnsureCatalogReferencesAsync(db, input, cancellationToken);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim(),
            Description = input.Description.Trim(),
            FamilyId = input.FamilyId,
            SubfamilyId = input.SubfamilyId,
            MeasurementTypeId = input.MeasurementTypeId,
            SupplierId = input.SupplierId,
            SalePrice = input.SalePrice,
            CostPrice = input.CostPrice,
            CurrentStock = input.CurrentStock,
            MinStock = input.MinStock,
            MaxStock = input.MaxStock,
            ReorderPoint = input.ReorderPoint,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("INSERT", TableName, product.Id.ToString(),
            oldData: null,
            newData: new { product.Code, product.Description, product.SalePrice, product.CurrentStock },
            userId, cancellationToken);

        return product.Id;
    }

    public async Task UpdateProductAsync(Guid id, ProductInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("producto", id);

        var normalizedCode = input.Code.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Code == normalizedCode && p.Id != id, cancellationToken))
        {
            throw new ValidationException($"Ya existe otro producto con el código '{normalizedCode}'.");
        }

        await EnsureCatalogReferencesAsync(db, input, cancellationToken);

        var before = new { product.Code, product.Description, product.SalePrice, product.CostPrice, product.MinStock };

        product.Code = normalizedCode;
        product.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
        product.Description = input.Description.Trim();
        product.FamilyId = input.FamilyId;
        product.SubfamilyId = input.SubfamilyId;
        product.MeasurementTypeId = input.MeasurementTypeId;
        product.SupplierId = input.SupplierId;
        product.SalePrice = input.SalePrice;
        product.CostPrice = input.CostPrice;
        // El stock no se edita directamente aquí: usar movimientos de inventario (entradas/ajustes).
        product.MinStock = input.MinStock;
        product.MaxStock = input.MaxStock;
        product.ReorderPoint = input.ReorderPoint;
        product.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("UPDATE", TableName, product.Id.ToString(),
            oldData: before,
            newData: new { product.Code, product.Description, product.SalePrice, product.CostPrice, product.MinStock },
            userId, cancellationToken);
    }

    public async Task DeactivateProductAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await SetActiveAsync(id, false, userId, cancellationToken);
    }

    public async Task ReactivateProductAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await SetActiveAsync(id, true, userId, cancellationToken);
    }

    private async Task SetActiveAsync(Guid id, bool isActive, Guid userId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("producto", id);

        if (product.IsActive == isActive)
        {
            return;
        }

        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync(isActive ? "REACTIVATE" : "DELETE", TableName, product.Id.ToString(),
            oldData: new { IsActive = !isActive },
            newData: new { IsActive = isActive },
            userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Family>> GetFamiliesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();
        return await db.Families.AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subfamily>> GetSubfamiliesAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();
        return await db.Subfamilies.AsNoTracking()
            .Where(s => s.FamilyId == familyId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MeasurementType>> GetMeasurementTypesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();
        return await db.MeasurementTypes.AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    private static void Validate(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Code))
            throw new ValidationException("El código del producto es obligatorio.");
        if (input.Code.Trim().Length > 30)
            throw new ValidationException("El código no puede superar 30 caracteres.");
        if (string.IsNullOrWhiteSpace(input.Description))
            throw new ValidationException("La descripción es obligatoria.");
        if (input.Description.Trim().Length > 200)
            throw new ValidationException("La descripción no puede superar 200 caracteres.");
        if (input.FamilyId == Guid.Empty)
            throw new ValidationException("Debe seleccionar una categoría.");
        if (input.MeasurementTypeId == Guid.Empty)
            throw new ValidationException("Debe seleccionar una unidad de medida.");
        if (input.SalePrice < 0)
            throw new ValidationException("El precio de venta no puede ser negativo.");
        if (input.CostPrice < 0)
            throw new ValidationException("El costo no puede ser negativo.");
        if (input.CurrentStock < 0)
            throw new ValidationException("El stock no puede ser negativo.");
        if (input.MinStock < 0)
            throw new ValidationException("El stock mínimo no puede ser negativo.");
        if (input.MaxStock is < 0)
            throw new ValidationException("El stock máximo no puede ser negativo.");
        if (input.MaxStock is { } max && max < input.MinStock)
            throw new ValidationException("El stock máximo no puede ser menor que el mínimo.");
    }

    private static async Task EnsureCatalogReferencesAsync(FlexoDbContext db, ProductInput input, CancellationToken cancellationToken)
    {
        if (!await db.Families.AnyAsync(f => f.Id == input.FamilyId, cancellationToken))
            throw new ValidationException("La categoría seleccionada no existe.");

        if (input.SubfamilyId is { } subId
            && !await db.Subfamilies.AnyAsync(s => s.Id == subId && s.FamilyId == input.FamilyId, cancellationToken))
            throw new ValidationException("La subcategoría no pertenece a la categoría seleccionada.");

        if (!await db.MeasurementTypes.AnyAsync(m => m.Id == input.MeasurementTypeId, cancellationToken))
            throw new ValidationException("La unidad de medida seleccionada no existe.");

        if (input.SupplierId is { } supId
            && !await db.Suppliers.AnyAsync(s => s.Id == supId, cancellationToken))
            throw new ValidationException("El proveedor seleccionado no existe.");
    }
}
