using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Reportes agregados de ventas y compras leyendo <c>sales.Orders</c>, <c>sales.Payments</c>
/// e <c>public.InventoryMovements</c>.
/// </summary>
public sealed class ReportService(IServiceScopeFactory scopeFactory) : IReportService
{
    public async Task<SalesReport> GetSalesReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        (fromUtc, toUtc) = NormalizeRange(fromUtc, toUtc);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var completedSales = db.Orders.AsNoTracking().Where(o =>
            o.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale &&
            o.Status == SalesDomainConstants.OrderStatuses.Completed &&
            o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc);

        var totals = await completedSales
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Subtotal = g.Sum(o => o.Subtotal),
                Tax = g.Sum(o => o.TaxAmount),
                Discount = g.Sum(o => o.DiscountAmount),
                Total = g.Sum(o => o.Total)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var byDayRaw = await completedSales
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total) })
            .OrderBy(r => r.Day)
            .ToListAsync(cancellationToken);

        var byDay = byDayRaw
            .Select(r => new SalesByDayRow(DateOnly.FromDateTime(r.Day), r.Count, r.Total))
            .ToList();

        var byPayment = await db.Payments.AsNoTracking()
            .Where(p =>
                p.Order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale &&
                p.Order.Status == SalesDomainConstants.OrderStatuses.Completed &&
                p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc)
            .GroupBy(p => p.Method)
            .Select(g => new SalesByPaymentRow(g.Key, g.Sum(p => p.Amount)))
            .OrderByDescending(r => r.Amount)
            .ToListAsync(cancellationToken);

        return new SalesReport(
            fromUtc,
            toUtc,
            totals?.Count ?? 0,
            totals?.Subtotal ?? 0,
            totals?.Tax ?? 0,
            totals?.Discount ?? 0,
            totals?.Total ?? 0,
            byDay,
            byPayment);
    }

    public async Task<PurchasesReport> GetPurchasesReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        (fromUtc, toUtc) = NormalizeRange(fromUtc, toUtc);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var purchases = db.InventoryMovements.AsNoTracking().Where(m =>
            m.MovementType == SalesDomainConstants.InventoryMovementTypes.PurchaseInflow &&
            m.CreatedAt >= fromUtc && m.CreatedAt <= toUtc);

        var totals = await purchases
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), TotalCost = g.Sum(m => m.TotalCost) })
            .FirstOrDefaultAsync(cancellationToken);

        var byProduct = await purchases
            .GroupBy(m => new { m.Product.Code, m.Product.Description })
            .Select(g => new PurchasesByProductRow(
                g.Key.Code,
                g.Key.Description,
                g.Sum(m => m.Quantity),
                g.Sum(m => m.TotalCost)))
            .OrderByDescending(r => r.TotalCost)
            .Take(200)
            .ToListAsync(cancellationToken);

        return new PurchasesReport(
            fromUtc,
            toUtc,
            totals?.Count ?? 0,
            totals?.TotalCost ?? 0,
            byProduct);
    }

    public async Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        (fromUtc, toUtc) = NormalizeRange(fromUtc, toUtc);
        take = Math.Clamp(take, 1, 100);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        return await db.OrderDetails.AsNoTracking()
            .Where(d =>
                d.Order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale &&
                d.Order.Status == SalesDomainConstants.OrderStatuses.Completed &&
                d.Order.CreatedAt >= fromUtc && d.Order.CreatedAt <= toUtc)
            .GroupBy(d => new { d.Product.Code, d.Product.Description })
            .Select(g => new TopProductRow(
                g.Key.Code,
                g.Key.Description,
                g.Sum(d => d.Quantity * d.UnitsPerPackage),
                g.Sum(d => d.Subtotal)))
            .OrderByDescending(r => r.QuantitySold)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Asegura que el rango cubra días completos y esté en orden ascendente (UTC).</summary>
    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        var from = DateTime.SpecifyKind(fromUtc.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toUtc.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
        return (from, to);
    }
}
