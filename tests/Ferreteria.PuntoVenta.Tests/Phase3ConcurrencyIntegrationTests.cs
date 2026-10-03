using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Returns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Security;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Prueba concurrencia real de ventas, devoluciones y cierre sobre PostgreSQL.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class Phase3ConcurrencyIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Dos ventas con orden inverso de productos terminan sin deadlock y descuentan stock exacto.</summary>
    [Fact]
    public async Task ConcurrentCashSales_InverseProductOrder_KeepExactStock()
    {
        var products = await PrepareProductsAsync(4m);
        var original = products.Select(item => item.CurrentStock).ToArray();
        var code = UniqueCashRegisterCode();
        fixture.SetCashRegisterCode(code);
        var session = await fixture.CashSessions.OpenAsync(fixture.ManagerId, code, 0m, "QA concurrencia");
        try
        {
            var first = BuildSale(products, session.Id, fixture.ManagerId, products.Select(item => item.Id).ToArray());
            var second = BuildSale(products, session.Id, fixture.ManagerId, products.AsEnumerable().Reverse().Select(item => item.Id).ToArray());
            var results = await RunTogetherAsync(
                () => fixture.Orders.CreateCashSaleAsync(first),
                () => fixture.Orders.CreateCashSaleAsync(second));

            Assert.Equal(2, results.Count);
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var finalStock = await db.Products.Where(item => products.Select(product => product.Id).Contains(item.Id)).OrderBy(item => item.Code).Select(item => item.CurrentStock).ToArrayAsync();
            Assert.Equal(original.Select(stock => stock - 2m), finalStock);
        }
        finally
        {
            await CleanupProductsAndSessionAsync(products.Select(item => item.Id).ToArray(), original, session.Id);
        }
    }

    /// <summary>Concurrencia con la misma clave devuelve una sola orden y rechaza payload diferente.</summary>
    [Fact]
    public async Task ConcurrentCashSales_SameClientRequestId_IsIdempotentAndRejectsDifferentPayload()
    {
        var products = await PrepareProductsAsync(3m);
        var original = products.Select(item => item.CurrentStock).ToArray();
        var code = UniqueCashRegisterCode(); fixture.SetCashRegisterCode(code);
        var session = await fixture.CashSessions.OpenAsync(fixture.ManagerId, code, 0m, "QA idempotencia");
        var clientRequestId = Guid.NewGuid();
        try
        {
            var request = BuildSale(products, session.Id, fixture.ManagerId, new[] { products[0].Id, products[1].Id }) with { ClientRequestId = clientRequestId };
            var results = await RunTogetherAsync(
                () => fixture.Orders.CreateCashSaleAsync(request),
                () => fixture.Orders.CreateCashSaleAsync(request));
            Assert.Equal(results[0].OrderId, results[1].OrderId);

            await Assert.ThrowsAsync<InvalidOrderException>(() => fixture.Orders.CreateCashSaleAsync(request with
            {
                Lines = new[] { new CashSaleLineRequest(products[0].Id, 2m), new CashSaleLineRequest(products[1].Id, 1m) }
            }));

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.Equal(1, await db.Orders.CountAsync(item => item.ClientRequestId == clientRequestId));
            var finalStock = await db.Products.Where(item => products.Select(product => product.Id).Contains(item.Id)).OrderBy(item => item.Code).Select(item => item.CurrentStock).ToArrayAsync();
            Assert.Equal(original.Select((stock, index) => stock - (index < 2 ? 1m : 0m)), finalStock);
        }
        finally
        {
            await CleanupProductsAndSessionAsync(products.Select(item => item.Id).ToArray(), original, session.Id);
        }
    }

    /// <summary>El cierre y una venta concurrentes no dejan una venta después de cerrar la sesión.</summary>
    [Fact]
    public async Task ConcurrentCloseAndCashSale_NeverPersistsSaleIntoClosedSession()
    {
        var products = await PrepareProductsAsync(2m);
        var original = products.Select(item => item.CurrentStock).ToArray();
        var code = UniqueCashRegisterCode(); fixture.SetCashRegisterCode(code);
        var session = await fixture.CashSessions.OpenAsync(fixture.ManagerId, code, 0m, "QA cierre concurrente");
        try
        {
            var request = BuildSale(products, session.Id, fixture.ManagerId, new[] { products[0].Id, products[1].Id });
            var observations = await RunTogetherObservationsAsync(
                async () => (object?)await fixture.Orders.CreateCashSaleAsync(request),
                async () =>
                {
                    await fixture.CashSessions.CloseAsync(session.Id, 0m, "QA cierre", fixture.ManagerId);
                    return null;
                });
            Assert.Null(observations[1].Error);

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var persisted = await db.Orders.Where(item => item.ClientRequestId == request.ClientRequestId).SingleOrDefaultAsync();
            var status = await db.CashSessions.Where(item => item.Id == session.Id).Select(item => item.Status).SingleAsync();
            Assert.Equal(SalesDomainConstants.CashSessionStatuses.Closed, status);
            if (persisted is not null) Assert.Equal(session.Id, persisted.CashSessionId);
            if (observations[0].Error is null) Assert.NotNull(persisted);
            else Assert.IsType<InvalidOrderException>(observations[0].Error);
        }
        finally
        {
            await CleanupProductsAndSessionAsync(products.Select(item => item.Id).ToArray(), original, session.Id);
        }
    }

    /// <summary>Dos devoluciones de órdenes distintas y productos invertidos reingresan stock sin deadlock.</summary>
    [Fact]
    public async Task ConcurrentReturns_InverseProductOrder_KeepExactStock()
    {
        var products = await PrepareProductsAsync(4m);
        var original = products.Select(item => item.CurrentStock).ToArray();
        var code = UniqueCashRegisterCode(); fixture.SetCashRegisterCode(code);
        var session = await fixture.CashSessions.OpenAsync(fixture.ManagerId, code, 0m, "QA devoluciones");
        var sales = new List<(CashSaleResult Result, Guid[] Details)>();
        try
        {
            var first = await fixture.Orders.CreateCashSaleAsync(BuildSale(products, session.Id, fixture.ManagerId, new[] { products[0].Id, products[1].Id }));
            var second = await fixture.Orders.CreateCashSaleAsync(BuildSale(products, session.Id, fixture.ManagerId, new[] { products[1].Id, products[0].Id }));
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
                var firstDetails = await db.OrderDetails
                    .Where(item => item.OrderId == first.OrderId)
                    .ToDictionaryAsync(item => item.ProductId, item => item.Id);
                var secondDetails = await db.OrderDetails
                    .Where(item => item.OrderId == second.OrderId)
                    .ToDictionaryAsync(item => item.ProductId, item => item.Id);
                sales.Add((first, new[] { firstDetails[products[0].Id], firstDetails[products[1].Id] }));
                sales.Add((second, new[] { secondDetails[products[1].Id], secondDetails[products[0].Id] }));
            }

            await using var firstProvider = BuildReturnProvider($"QA-RET-{Guid.NewGuid():N}"[..20]);
            var firstRequest = BuildReturnRequest(sales[0], fixture.ManagerId);
            var secondRequest = BuildReturnRequest(sales[1], fixture.ManagerId);
            var results = await RunTogetherAsync(
                () => firstProvider.GetRequiredService<IReturnService>().CreateReturnAsync(firstRequest, "1234"),
                () => firstProvider.GetRequiredService<IReturnService>().CreateReturnAsync(secondRequest, "1234"));
            Assert.Equal(sales[0].Result.OrderId, results[0].OrderId);
            Assert.Equal(sales[1].Result.OrderId, results[1].OrderId);

            await using var verifyScope = fixture.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var finalStock = await verifyDb.Products.Where(item => products.Select(product => product.Id).Contains(item.Id)).OrderBy(item => item.Code).Select(item => item.CurrentStock).ToArrayAsync();
            Assert.Equal(original, finalStock);
        }
        finally
        {
            await CleanupProductsAndSessionAsync(products.Select(item => item.Id).ToArray(), original, session.Id);
        }
    }

    private CreateCashSaleRequest BuildSale(IReadOnlyList<Product> products, Guid sessionId, Guid employeeId, IReadOnlyList<Guid> productOrder)
    {
        var subtotal = products.Sum(product => product.SalePrice);
        return new CreateCashSaleRequest(employeeId, sessionId, null, Guid.NewGuid(), productOrder.Select(id => new CashSaleLineRequest(id, 1m)).ToArray(), new[] { new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, TaxAmountCalculator.CalculateGrandTotal(subtotal)) }, "QA concurrencia");
    }

    private static ReturnRequest BuildReturnRequest((CashSaleResult Result, Guid[] Details) sale, Guid employeeId) => new(Guid.NewGuid(), sale.Result.OrderId, employeeId, Guid.Empty, "CAMBIO", null, ReturnDomainConstants.RefundMethods.None, sale.Details.Select(id => new ReturnLineRequest(id, 1m)).ToArray());

    private async Task<List<T>> RunTogetherAsync<T>(Func<Task<T>> first, Func<Task<T>> second)
    {
        var ready = 0; var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<T> Run(Func<Task<T>> operation) { if (Interlocked.Increment(ref ready) == 2) gate.SetResult(true); await gate.Task; return await operation(); }
        var results = await Task.WhenAll(Run(first), Run(second)); return results.ToList();
    }

    private async Task<Observation<T>[]> RunTogetherObservationsAsync<T>(Func<Task<T>> first, Func<Task<T>> second)
    {
        var results = await RunTogetherAsync(() => ObserveAsync(first), () => ObserveAsync(second));
        return results.ToArray();
    }

    private static async Task<Observation<T>> ObserveAsync<T>(Func<Task<T>> operation)
    {
        try { return new(await operation(), null); }
        catch (Exception exception) { return new(default, exception); }
    }

    private async Task<List<Product>> PrepareProductsAsync(decimal stock)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await TestDataFactory.CreateProductsAsync(db, 2, stock);
    }

    private ServiceProvider BuildReturnProvider(string code)
    {
        fixture.SetCashRegisterCode(code);
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.Configure<ReturnOptions>(ReturnOptions.ApplyDefaults);
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = code);
        services.AddOptions<PinLockoutOptions>();
        services.AddOptions<SalesHistoryOptions>().Configure(options => options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.AddSingleton<IReturnedQuantityReader, ReturnDetailsReturnedQuantityReader>();
        services.AddSingleton<IReturnWriter, EfReturnWriter>();
        services.AddSingleton<IReturnFiscalPolicy, DefaultReturnFiscalPolicy>();
        services.AddSingleton<IPinAttemptService, PinAttemptService>();
        services.AddSingleton<PinAuthService>();
        services.AddSingleton<IAuthorizationGuard, TestAuthorizationGuard>();
        services.AddSingleton<IReturnService, ReturnService>();
        services.AddSingleton<ILogger<PinAttemptService>>(_ => NullLogger<PinAttemptService>.Instance);
        services.AddSingleton<ILogger<ReturnService>>(_ => NullLogger<ReturnService>.Instance);
        return services.BuildServiceProvider();
    }

    private async Task CleanupProductsAndSessionAsync(IReadOnlyList<Guid> productIds, IReadOnlyList<decimal> originalStock, Guid sessionId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var orderIds = await db.Orders.Where(item => item.CashSessionId == sessionId).Select(item => item.Id).ToListAsync();
        var returnIds = await db.Returns.Where(item => orderIds.Contains(item.OrderId)).Select(item => item.Id).ToListAsync();
        db.ReturnDetails.RemoveRange(await db.ReturnDetails.Where(item => returnIds.Contains(item.ReturnId)).ToListAsync());
        db.CashMovements.RemoveRange(await db.CashMovements.Where(item => item.ReturnId.HasValue && returnIds.Contains(item.ReturnId.Value)).ToListAsync());
        db.Returns.RemoveRange(await db.Returns.Where(item => returnIds.Contains(item.Id)).ToListAsync());
        db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => orderIds.Contains(item.OrderId ?? Guid.Empty)).ToListAsync());
        db.Orders.RemoveRange(await db.Orders.Where(item => orderIds.Contains(item.Id)).ToListAsync());
        var session = await db.CashSessions.SingleOrDefaultAsync(item => item.Id == sessionId); if (session is not null) db.CashSessions.Remove(session);
        var products = await db.Products.Where(item => productIds.Contains(item.Id)).ToListAsync();
        db.Products.RemoveRange(products);
        await db.SaveChangesAsync();
    }

    private static string UniqueCashRegisterCode() => $"QA-C-{Guid.NewGuid():N}"[..20];
    private sealed record Observation<T>(T? Result, Exception? Error);
}
