using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Reportes agregados de ventas y compras leyendo <c>sales.Orders</c>, <c>sales.Payments</c>
/// e <c>public.InventoryMovements</c> con días definidos por el negocio.
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BusinessCalendar _calendar;

    /// <summary>Inicializa el servicio de reportes.</summary>
    /// <param name="scopeFactory">Fábrica de ámbitos para resolver el contexto EF.</param>
    /// <param name="calendar">Calendario que convierte fechas locales a rangos UTC.</param>
    public ReportService(IServiceScopeFactory scopeFactory, BusinessCalendar calendar)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    /// <inheritdoc />
    public async Task<SalesReport> GetSalesReportAsync(
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var range = _calendar.RangeUtc(from, toInclusive);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var completedSales = db.Orders.AsNoTracking().Where(o =>
            o.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale &&
            o.Status == SalesDomainConstants.OrderStatuses.Completed &&
            o.CreatedAt >= range.StartUtc && o.CreatedAt < range.EndUtc);

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

        // La zona viaja como un único parámetro: la subconsulta calcula el día local una sola vez
        // para que el GROUP BY agrupe la misma expresión que devuelve el SELECT.
        var byDayRaw = await db.Database.SqlQuery<SalesByDaySqlRow>($"""
            SELECT d."Day",
                   COUNT(*)::int AS "OrderCount",
                   COALESCE(SUM(d."Total"), 0)::numeric AS "Total"
            FROM (
                SELECT (o."CreatedAt" AT TIME ZONE {_calendar.TimeZone.IanaId})::date AS "Day",
                       o."total" AS "Total"
                FROM sales."Orders" o
                WHERE o."OrderType" = {SalesDomainConstants.OrderTypes.CashRegisterSale}
                  AND o."status" = {SalesDomainConstants.OrderStatuses.Completed}
                  AND o."CreatedAt" >= {range.StartUtc}
                  AND o."CreatedAt" < {range.EndUtc}
            ) d
            GROUP BY d."Day"
            ORDER BY d."Day"
            """).ToListAsync(cancellationToken);

        var byDay = byDayRaw
            .Select(row => new SalesByDayRow(row.Day, row.OrderCount, row.Total))
            .ToList();

        var byPaymentRaw = await db.Payments.AsNoTracking()
            .Where(p =>
                p.Order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale &&
                p.Order.Status == SalesDomainConstants.OrderStatuses.Completed &&
                p.CreatedAt >= range.StartUtc && p.CreatedAt < range.EndUtc)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) })
            .OrderByDescending(r => r.Amount)
            .ToListAsync(cancellationToken);
        // EF no traduce el ordenamiento por propiedades de un record construido en la proyección:
        // se ordena sobre el anónimo en SQL y se mapea al contrato en memoria.
        var byPayment = byPaymentRaw
            .Select(r => new SalesByPaymentRow(r.Method, r.Amount))
            .ToList();

        return new SalesReport(
            range.StartUtc,
            range.EndUtc,
            totals?.Count ?? 0,
            totals?.Subtotal ?? 0,
            totals?.Tax ?? 0,
            totals?.Discount ?? 0,
            totals?.Total ?? 0,
            byDay,
            byPayment);
    }

    /// <inheritdoc />
    public async Task<PurchasesReport> GetPurchasesReportAsync(
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var range = _calendar.RangeUtc(from, toInclusive);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var purchases = db.InventoryMovements.AsNoTracking().Where(m =>
            m.MovementType == SalesDomainConstants.InventoryMovementTypes.PurchaseInflow &&
            m.CreatedAt >= range.StartUtc && m.CreatedAt < range.EndUtc);

        var totals = await purchases
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), TotalCost = g.Sum(m => m.TotalCost) })
            .FirstOrDefaultAsync(cancellationToken);

        var byProductRaw = await purchases
            .GroupBy(m => new { m.Product.Code, m.Product.Description })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Description,
                Quantity = g.Sum(m => m.Quantity),
                TotalCost = g.Sum(m => m.TotalCost)
            })
            .OrderByDescending(r => r.TotalCost)
            .Take(200)
            .ToListAsync(cancellationToken);
        var byProduct = byProductRaw
            .Select(r => new PurchasesByProductRow(r.Code, r.Description, r.Quantity, r.TotalCost))
            .ToList();

        return new PurchasesReport(
            range.StartUtc,
            range.EndUtc,
            totals?.Count ?? 0,
            totals?.TotalCost ?? 0,
            byProduct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(
        DateOnly from,
        DateOnly toInclusive,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var range = _calendar.RangeUtc(from, toInclusive);
        take = Math.Clamp(take, 1, 100);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var rows = await db.OrderDetails.AsNoTracking()
            .Where(d =>
                d.Order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale &&
                d.Order.Status == SalesDomainConstants.OrderStatuses.Completed &&
                d.Order.CreatedAt >= range.StartUtc && d.Order.CreatedAt < range.EndUtc)
            .GroupBy(d => new { d.Product.Code, d.Product.Description })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Description,
                QuantitySold = g.Sum(d => d.Quantity * d.UnitsPerPackage),
                Revenue = g.Sum(d => d.Subtotal)
            })
            .OrderByDescending(r => r.QuantitySold)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TopProductRow(r.Code, r.Description, r.QuantitySold, r.Revenue))
            .ToList();
    }

    private sealed class SalesByDaySqlRow
    {
        public DateOnly Day { get; set; }

        public int OrderCount { get; set; }

        public decimal Total { get; set; }
    }
}
