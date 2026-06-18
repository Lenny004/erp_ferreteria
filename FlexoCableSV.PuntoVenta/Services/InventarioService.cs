using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace FlexoCableSV.PuntoVenta.Services;

public sealed class InventarioService(IServiceScopeFactory scopeFactory) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryProductResult>> SearchProductsAsync(
        string? searchText,
        string? stockStatus,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.Family)
            .Include(p => p.Subfamily)
            .Include(p => p.MeasurementType)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var pattern = $"%{searchText.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Code, pattern) ||
                EF.Functions.ILike(p.Description, pattern) ||
                EF.Functions.ILike(p.Family.Name, pattern) ||
                (p.Barcode != null && EF.Functions.ILike(p.Barcode, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(stockStatus) && stockStatus != "TODOS")
        {
            query = stockStatus switch
            {
                "OK" => query.Where(p => p.CurrentStock > p.MinStock),
                "BAJO" => query.Where(p => p.CurrentStock > 0 && p.CurrentStock <= p.MinStock),
                "AGOTADO" => query.Where(p => p.CurrentStock <= 0),
                _ => query
            };
        }

        return await query
            .OrderBy(p => p.Code)
            .Take(take)
            .Select(p => new InventoryProductResult(
                p.Id,
                p.Code,
                p.Description,
                p.Family.Name,
                p.Subfamily != null ? p.Subfamily.Name : null,
                p.MeasurementType.Name,
                p.MeasurementType.UnitLabel,
                p.MeasurementType.Decimals,
                p.CurrentStock,
                p.MinStock,
                p.SalePrice,
                p.IsActive))
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var product = await dbContext.Products
            .Include(p => p.MeasurementType)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException(productId);
        }

        ValidateQuantity(quantity, product.MeasurementType.Decimals);

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
            MovementType = "SALIDA_VENTA",
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

    private static void ValidateQuantity(decimal quantity, short allowedDecimals)
    {
        if (quantity <= 0)
        {
            throw new InvalidInventoryQuantityException("La cantidad debe ser mayor que cero.");
        }

        if (allowedDecimals < 0)
        {
            throw new InvalidInventoryQuantityException("La unidad de medida tiene una configuracion invalida.");
        }

        var rounded = Math.Round(quantity, allowedDecimals, MidpointRounding.AwayFromZero);
        if (quantity != rounded)
        {
            throw new InvalidInventoryQuantityException($"La cantidad permite maximo {allowedDecimals} decimales.");
        }
    }
}
