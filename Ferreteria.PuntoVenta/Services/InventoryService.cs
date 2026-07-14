using System.Data;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Consulta de catálogo y movimientos de inventario sobre <c>public.Products</c>.
/// </summary>
public sealed class InventoryService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService) : IInventoryService
{
    private static readonly string[] ValidEntryTypes =
    [
        SalesDomainConstants.InventoryMovementTypes.PurchaseInflow,
        SalesDomainConstants.InventoryMovementTypes.ReturnInflow
    ];

    public async Task<IReadOnlyList<InventoryProductResult>> SearchProductsAsync(
        string? searchText,
        string? stockStatus,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var productsQuery = dbContext.Products
            .AsNoTracking()
            .Include(product => product.Family)
            .Include(product => product.Subfamily)
            .Include(product => product.MeasurementType)
            .Where(product => product.IsActive);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var likePattern = $"%{searchText.Trim()}%";
            productsQuery = productsQuery.Where(product =>
                EF.Functions.ILike(product.Code, likePattern) ||
                EF.Functions.ILike(product.Description, likePattern) ||
                EF.Functions.ILike(product.Family.Name, likePattern) ||
                (product.Barcode != null && EF.Functions.ILike(product.Barcode, likePattern)));
        }

        if (!string.IsNullOrWhiteSpace(stockStatus) && stockStatus != SalesDomainConstants.StockFilters.All)
        {
            productsQuery = stockStatus switch
            {
                SalesDomainConstants.StockFilters.Sufficient =>
                    productsQuery.Where(product => product.CurrentStock > product.MinStock),
                SalesDomainConstants.StockFilters.Low =>
                    productsQuery.Where(product => product.CurrentStock > 0 && product.CurrentStock <= product.MinStock),
                SalesDomainConstants.StockFilters.Depleted =>
                    productsQuery.Where(product => product.CurrentStock <= 0),
                _ => productsQuery
            };
        }

        return await productsQuery
            .OrderBy(product => product.Code)
            .Take(take)
            .Select(product => new InventoryProductResult(
                product.Id,
                product.Code,
                product.Description,
                product.Family.Name,
                product.Subfamily != null ? product.Subfamily.Name : null,
                product.MeasurementType.Name,
                product.MeasurementType.UnitLabel,
                product.MeasurementType.Decimals,
                product.CurrentStock,
                product.MinStock,
                product.SalePrice,
                product.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<StockDecreaseResult> DecreaseStockAsync(
        Guid productId,
        decimal quantity,
        Guid orderId,
        Guid employeeId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var product = await dbContext.Products
            .Include(product => product.MeasurementType)
            .FirstOrDefaultAsync(
                product => product.Id == productId && product.IsActive,
                cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException(productId);
        }

        InventoryQuantityValidator.ValidatePositiveQuantity(quantity, product.MeasurementType.Decimals);

        var stockBefore = product.CurrentStock;
        if (stockBefore < quantity)
        {
            throw new InsufficientStockException(product.Code, quantity, stockBefore);
        }

        var stockAfter = stockBefore - quantity;
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = SalesDomainConstants.InventoryMovementTypes.SaleOutflow,
            Quantity = quantity,
            UnitCost = product.CostPrice,
            TotalCost = product.CostPrice * quantity,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            OrderId = orderId,
            EmployeeId = employeeId,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Venta" : reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        product.CurrentStock = stockAfter;
        product.UpdatedAt = DateTime.UtcNow;
        dbContext.InventoryMovements.Add(movement);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StockDecreaseResult(product.Id, movement.Id, stockBefore, stockAfter);
    }

    public async Task<StockMovementResult> RegisterEntryAsync(
        Guid productId,
        decimal quantity,
        decimal unitCost,
        Guid employeeId,
        string movementType,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ValidEntryTypes.Contains(movementType))
        {
            throw new ValidationException("El tipo de entrada debe ser una compra o una devolución.");
        }

        if (unitCost < 0)
        {
            throw new ValidationException("El costo unitario no puede ser negativo.");
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var product = await dbContext.Products
            .Include(product => product.MeasurementType)
            .FirstOrDefaultAsync(product => product.Id == productId && product.IsActive, cancellationToken)
            ?? throw new ProductNotFoundException(productId);

        InventoryQuantityValidator.ValidatePositiveQuantity(quantity, product.MeasurementType.Decimals);

        var stockBefore = product.CurrentStock;
        var stockAfter = stockBefore + quantity;

        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = movementType,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = unitCost * quantity,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            EmployeeId = employeeId,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Entrada de inventario" : reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        product.CurrentStock = stockAfter;
        product.UpdatedAt = DateTime.UtcNow;

        // Al reponer stock por encima del mínimo, se resuelven alertas abiertas del producto.
        if (stockAfter > product.MinStock)
        {
            await ResolveOpenAlertsAsync(dbContext, product.Id, cancellationToken);
        }

        dbContext.InventoryMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StockMovementResult(product.Id, movement.Id, movementType, stockBefore, stockAfter);
    }

    public async Task<StockMovementResult> RegisterAdjustmentAsync(
        Guid productId,
        decimal newStock,
        Guid employeeId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (newStock < 0)
        {
            throw new ValidationException("El stock ajustado no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("Todo ajuste de inventario requiere un motivo.");
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var product = await dbContext.Products
            .Include(product => product.MeasurementType)
            .FirstOrDefaultAsync(product => product.Id == productId && product.IsActive, cancellationToken)
            ?? throw new ProductNotFoundException(productId);

        var stockBefore = product.CurrentStock;
        var delta = newStock - stockBefore;
        if (delta == 0)
        {
            throw new ValidationException("El stock ajustado es igual al actual; no hay cambios que registrar.");
        }

        var isInflow = delta > 0;
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = isInflow
                ? SalesDomainConstants.InventoryMovementTypes.AdjustmentIn
                : SalesDomainConstants.InventoryMovementTypes.AdjustmentOut,
            Quantity = Math.Abs(delta),
            UnitCost = product.CostPrice,
            TotalCost = product.CostPrice * Math.Abs(delta),
            StockBefore = stockBefore,
            StockAfter = newStock,
            EmployeeId = employeeId,
            Reason = reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        product.CurrentStock = newStock;
        product.UpdatedAt = DateTime.UtcNow;

        if (newStock > 0 && newStock > product.MinStock)
        {
            await ResolveOpenAlertsAsync(dbContext, product.Id, cancellationToken);
        }
        else
        {
            await RaiseAlertIfNeededAsync(dbContext, product, cancellationToken);
        }

        dbContext.InventoryMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditService.RecordChangeAsync("ADJUSTMENT", "public.Products", product.Id.ToString(),
            new { Stock = stockBefore }, new { Stock = newStock, Reason = reason.Trim() }, employeeId, cancellationToken);

        return new StockMovementResult(product.Id, movement.Id, movement.MovementType, stockBefore, newStock);
    }

    public async Task<IReadOnlyList<StockAlertResult>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        return await dbContext.StockAlerts
            .AsNoTracking()
            .Where(alert => !alert.IsResolved)
            .Include(alert => alert.Product).ThenInclude(product => product.MeasurementType)
            .OrderBy(alert => alert.CurrentStock)
            .Select(alert => new StockAlertResult(
                alert.Id,
                alert.ProductId,
                alert.Product.Code,
                alert.Product.Description,
                alert.CurrentStock <= 0
                    ? SalesDomainConstants.StockFilters.Depleted
                    : SalesDomainConstants.StockFilters.Low,
                alert.CurrentStock,
                alert.MinStock,
                alert.Product.MeasurementType.UnitLabel,
                alert.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryMovementResult>> GetRecentMovementsAsync(
        Guid? productId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var query = dbContext.InventoryMovements.AsNoTracking().Include(m => m.Product).AsQueryable();
        if (productId is { } pid)
        {
            query = query.Where(m => m.ProductId == pid);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .Select(m => new InventoryMovementResult(
                m.Id,
                m.CreatedAt,
                m.Product.Code,
                m.Product.Description,
                m.MovementType,
                m.Quantity,
                m.StockAfter,
                m.Reason))
            .ToListAsync(cancellationToken);
    }

    private static async Task ResolveOpenAlertsAsync(FerreteriaDbContext dbContext, Guid productId, CancellationToken cancellationToken)
    {
        var openAlerts = await dbContext.StockAlerts
            .Where(alert => alert.ProductId == productId && !alert.IsResolved)
            .ToListAsync(cancellationToken);

        foreach (var alert in openAlerts)
        {
            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
        }
    }

    private static async Task RaiseAlertIfNeededAsync(FerreteriaDbContext dbContext, Product product, CancellationToken cancellationToken)
    {
        if (product.CurrentStock > product.MinStock)
        {
            return;
        }

        var alreadyOpen = await dbContext.StockAlerts
            .AnyAsync(alert => alert.ProductId == product.Id && !alert.IsResolved, cancellationToken);

        if (alreadyOpen)
        {
            return;
        }

        dbContext.StockAlerts.Add(new StockAlert
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            CurrentStock = product.CurrentStock,
            MinStock = product.MinStock,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        });
    }
}
