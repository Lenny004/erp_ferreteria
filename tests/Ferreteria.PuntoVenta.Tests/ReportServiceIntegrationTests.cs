using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de integración de reportes e historial en los límites del día local.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class ReportServiceIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa los casos con el fixture PostgreSQL compartido.</summary>
    /// <param name="fixture">Contenedor y servicios compartidos.</param>
    public ReportServiceIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Separa reportes e historial según la medianoche de El Salvador, no según UTC.</summary>
    [Fact]
    public async Task ReportsAndHistory_UseBusinessLocalDateBoundaries()
    {
        var tag = "TZ-REPORT-" + Guid.NewGuid().ToString("N");
        var lateNightUtc = new DateTime(2030, 6, 15, 5, 30, 0, DateTimeKind.Utc);
        var afterMidnightUtc = new DateTime(2030, 6, 15, 6, 5, 0, DateTimeKind.Utc);
        var lateNight = PostgreSqlFixture.NewOrder(
            _fixture.ManagerId,
            lateNightUtc,
            SalesDomainConstants.OrderStatuses.Completed,
            11m);
        lateNight.Notes = tag + "-23-30";
        var afterMidnight = PostgreSqlFixture.NewOrder(
            _fixture.ManagerId,
            afterMidnightUtc,
            SalesDomainConstants.OrderStatuses.Completed,
            22m);
        afterMidnight.Notes = tag + "-00-05";

        var productId = await CreateOwnProductAsync();

        await _fixture.SeedAsync(db =>
        {
            db.Orders.AddRange(lateNight, afterMidnight);
            db.OrderDetails.AddRange(
                NewDetail(lateNight.Id, productId, quantity: 1m, subtotal: 10m),
                NewDetail(afterMidnight.Id, productId, quantity: 3m, subtotal: 20m));
            db.InventoryMovements.AddRange(
                NewPurchaseInflow(productId, lateNightUtc, totalCost: 7m, tag + "-23-30"),
                NewPurchaseInflow(productId, afterMidnightUtc, totalCost: 9m, tag + "-00-05"));
            db.Payments.AddRange(
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = lateNight.Id,
                    Method = SalesDomainConstants.PaymentMethods.Cash,
                    Amount = lateNight.Total,
                    CreatedAt = lateNightUtc
                },
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = afterMidnight.Id,
                    Method = SalesDomainConstants.PaymentMethods.Card,
                    Amount = afterMidnight.Total,
                    CreatedAt = afterMidnightUtc
                });
        });

        var lateNightDay = await _fixture.Reports.GetSalesReportAsync(new DateOnly(2030, 6, 14), new DateOnly(2030, 6, 14));
        var afterMidnightDay = await _fixture.Reports.GetSalesReportAsync(new DateOnly(2030, 6, 15), new DateOnly(2030, 6, 15));
        var twoDays = await _fixture.Reports.GetSalesReportAsync(new DateOnly(2030, 6, 14), new DateOnly(2030, 6, 15));

        Assert.Equal(1, lateNightDay.OrderCount);
        Assert.Equal(11m, lateNightDay.Total);
        Assert.Equal(new[] { new SalesByDayRow(new DateOnly(2030, 6, 14), 1, 11m) }, lateNightDay.ByDay);
        Assert.Equal(1, afterMidnightDay.OrderCount);
        Assert.Equal(22m, afterMidnightDay.Total);
        Assert.Equal(new[] { new SalesByDayRow(new DateOnly(2030, 6, 15), 1, 22m) }, afterMidnightDay.ByDay);
        Assert.Equal(new[]
        {
            new SalesByDayRow(new DateOnly(2030, 6, 14), 1, 11m),
            new SalesByDayRow(new DateOnly(2030, 6, 15), 1, 22m)
        }, twoDays.ByDay);
        Assert.Equal(new[] { new SalesByPaymentRow(SalesDomainConstants.PaymentMethods.Cash, 11m) }, lateNightDay.ByPaymentMethod);
        Assert.Equal(new[] { new SalesByPaymentRow(SalesDomainConstants.PaymentMethods.Card, 22m) }, afterMidnightDay.ByPaymentMethod);

        var lateNightTop = Assert.Single(await _fixture.Reports.GetTopProductsAsync(new DateOnly(2030, 6, 14), new DateOnly(2030, 6, 14)));
        var afterMidnightTop = Assert.Single(await _fixture.Reports.GetTopProductsAsync(new DateOnly(2030, 6, 15), new DateOnly(2030, 6, 15)));
        Assert.Equal((1m, 10m), (lateNightTop.QuantitySold, lateNightTop.Revenue));
        Assert.Equal((3m, 20m), (afterMidnightTop.QuantitySold, afterMidnightTop.Revenue));

        var lateNightPurchases = await _fixture.Reports.GetPurchasesReportAsync(new DateOnly(2030, 6, 14), new DateOnly(2030, 6, 14));
        var afterMidnightPurchases = await _fixture.Reports.GetPurchasesReportAsync(new DateOnly(2030, 6, 15), new DateOnly(2030, 6, 15));
        Assert.Equal((1, 7m), (lateNightPurchases.MovementCount, lateNightPurchases.TotalCost));
        Assert.Equal((1, 9m), (afterMidnightPurchases.MovementCount, afterMidnightPurchases.TotalCost));

        var lateNightFilter = CreateFilter(new DateOnly(2030, 6, 14), tag);
        var afterMidnightFilter = CreateFilter(new DateOnly(2030, 6, 15), tag);
        var lateNightHistory = await _fixture.History.SearchAsync(lateNightFilter, _fixture.ManagerId);
        var afterMidnightHistory = await _fixture.History.SearchAsync(afterMidnightFilter, _fixture.ManagerId);

        Assert.Equal(lateNight.Id, Assert.Single(lateNightHistory.Rows).OrderId);
        Assert.Equal(afterMidnight.Id, Assert.Single(afterMidnightHistory.Rows).OrderId);
    }

    /// <summary>Documenta con timestamps fijos y sesión en UTC la diferencia entre el corte actual de la vista y la expresión propuesta.</summary>
    [Fact]
    public async Task PostgreSqlExpression_ConvertsTimestampBeforeExtractingDate()
    {
        const string sql = """
            SELECT
                ('2026-09-30T05:30:00Z'::timestamptz AT TIME ZONE 'America/El_Salvador')::date,
                '2026-09-30T05:30:00Z'::timestamptz::date;
            """;
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var setUtc = new NpgsqlCommand("SET TIME ZONE 'UTC'", connection))
        {
            await setUtc.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(new DateOnly(2026, 9, 29), reader.GetFieldValue<DateOnly>(0));
        Assert.Equal(new DateOnly(2026, 9, 30), reader.GetFieldValue<DateOnly>(1));
    }

    private static OrderDetail NewDetail(Guid orderId, Guid productId, decimal quantity, decimal subtotal)
    {
        return new OrderDetail
        {
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitsPerPackage = 1m,
            UnitPrice = subtotal / quantity,
            DiscountAmount = 0m,
            Subtotal = subtotal
        };
    }

    private static InventoryMovement NewPurchaseInflow(Guid productId, DateTime createdAtUtc, decimal totalCost, string reason)
    {
        return new InventoryMovement
        {
            ProductId = productId,
            MovementType = SalesDomainConstants.InventoryMovementTypes.PurchaseInflow,
            Quantity = 1m,
            UnitCost = totalCost,
            TotalCost = totalCost,
            StockBefore = 0m,
            StockAfter = 1m,
            Reason = reason,
            CreatedAt = createdAtUtc
        };
    }

    private SalesHistoryFilter CreateFilter(DateOnly day, string tag)
    {
        var range = SalesHistoryFilter.CreateLocalDateRange(day, day, _fixture.Calendar);
        return new SalesHistoryFilter(
            FromUtc: range.FromUtc,
            ToUtc: range.ToUtc,
            Shortcut: SalesHistoryDateShortcut.None,
            SearchText: tag,
            OrderStatus: SalesDomainConstants.OrderStatuses.Completed);
    }

    /// <summary>Crea el producto exclusivo usado por el caso de reportes.</summary>
    /// <returns>Id del producto recién persistido.</returns>
    private async Task<Guid> CreateOwnProductAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Ferreteria.PuntoVenta.Data.FerreteriaDbContext>();
        return (await TestDataFactory.CreateProductsAsync(db, 1, 10m)).Single().Id;
    }
}
