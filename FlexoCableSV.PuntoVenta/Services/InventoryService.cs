using System.Data;
using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using FlexoCableSV.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Consulta de catálogo y movimientos de inventario sobre <c>public.Products</c>.
/// </summary>
public sealed class InventoryService(IServiceScopeFactory scopeFactory) : IInventoryService
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

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
}
