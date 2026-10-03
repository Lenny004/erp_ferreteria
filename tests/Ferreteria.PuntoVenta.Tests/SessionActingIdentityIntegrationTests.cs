using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Returns;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Verifica que las operaciones de caja usen exclusivamente la identidad de la sesión activa.
/// </summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class SessionActingIdentityIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Una apertura con un empleado distinto a la sesión no escribe una sesión.</summary>
    [Fact]
    public async Task OpenCashSession_SpoofedActingEmployee_IsRejectedWithoutChanges()
    {
        var code = UniqueCashRegisterCode();
        await using var provider = BuildProvider(fixture.CashierId, code);
        var cash = provider.GetRequiredService<ICashSessionService>();
        var before = await CountSessionsAsync(code);

        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            cash.OpenAsync(fixture.SecondCashierId, code, 10m, "QA"));

        Assert.Equal(before, await CountSessionsAsync(code));
    }

    /// <summary>Un cierre con un empleado distinto a la sesión deja abierta la caja.</summary>
    [Fact]
    public async Task CloseCashSession_SpoofedActingEmployee_IsRejectedWithoutChanges()
    {
        var code = UniqueCashRegisterCode();
        await using var provider = BuildProvider(fixture.CashierId, code);
        var cash = provider.GetRequiredService<ICashSessionService>();
        var opened = await cash.OpenAsync(fixture.CashierId, code, 10m, "QA");

        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            cash.CloseAsync(opened.Id, 10m, null, fixture.SecondCashierId));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        Assert.Equal(SalesDomainConstants.CashSessionStatuses.Open,
            await db.CashSessions.Where(item => item.Id == opened.Id).Select(item => item.Status).SingleAsync());

        await cash.CloseAsync(opened.Id, 10m, "Limpieza de prueba", fixture.CashierId);
    }

    /// <summary>Una devolución con un empleado distinto a la sesión no registra filas ni cambia stock.</summary>
    [Fact]
    public async Task CreateReturn_SpoofedActingEmployee_IsRejectedWithoutChanges()
    {
        var code = UniqueCashRegisterCode();
        fixture.SetCashRegisterCode(code);
        var session = await fixture.CashSessions.OpenAsync(fixture.CashierId, code, 10m, "Venta QA");
        Guid productId = Guid.Empty;
        Guid orderId = Guid.Empty;
        try
        {
            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
                var product = await db.Products.OrderBy(item => item.Code).FirstAsync();
                productId = product.Id;
                var sale = await fixture.Orders.CreateCashSaleAsync(new CreateCashSaleRequest(
                    fixture.CashierId,
                    session.Id,
                    null,
                    Guid.NewGuid(),
                    new[] { new CashSaleLineRequest(product.Id, 1m) },
                    new[] { new CashSalePaymentRequest(
                        SalesDomainConstants.PaymentMethods.Cash,
                        TaxAmountCalculator.CalculateGrandTotal(product.SalePrice)) },
                    "Venta QA"));
                orderId = sale.OrderId;
            }

            var lineId = await LoadOrderDetailIdAsync(orderId);
            var before = await LoadReturnSnapshotAsync(orderId, productId);
            await using var provider = BuildProvider(fixture.CashierId, code);
            var request = new ReturnRequest(
                Guid.NewGuid(), orderId, fixture.SecondCashierId, Guid.Empty, "CAMBIO", null,
                ReturnDomainConstants.RefundMethods.None, new[] { new ReturnLineRequest(lineId, 1m) });

            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                provider.GetRequiredService<IReturnService>().CreateReturnAsync(request, "1234"));

            Assert.Equal(before, await LoadReturnSnapshotAsync(orderId, productId));
        }
        finally
        {
            await CleanupSaleAsync(orderId, productId, session.Id, code);
        }
    }

    private ServiceProvider BuildProvider(Guid sessionEmployeeId, string code)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(PostgreSqlFixture.Now));
        services.AddOptions<CashRegisterOptions>().Configure(options =>
        {
            options.Codigo = code;
            options.MontoMaximo = 100000m;
            options.UmbralDiferencia = 1m;
        });
        services.AddOptions<SalesHistoryOptions>().Configure(options =>
            options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.Configure<ReturnOptions>(ReturnOptions.ApplyDefaults);
        services.AddOptions<AuthorizationOptions>().Configure(options =>
            options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddOptions<PinLockoutOptions>();
        services.AddSingleton<ICurrentSessionService>(new SessionDouble(sessionEmployeeId));
        services.AddSingleton<ILogger<AuthorizationGuard>>(_ => NullLogger<AuthorizationGuard>.Instance);
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        services.AddSingleton<ICashMovementReader, CashMovementsCashMovementReader>();
        services.AddSingleton<IReturnedQuantityReader, ReturnDetailsReturnedQuantityReader>();
        services.AddSingleton<IReturnWriter, EfReturnWriter>();
        services.AddSingleton<IReturnFiscalPolicy, DefaultReturnFiscalPolicy>();
        services.AddSingleton<IPinAttemptService, TestPinAttemptService>();
        services.AddSingleton<PinAuthService>();
        services.AddSingleton<ICashSessionService, CashSessionService>();
        services.AddSingleton<IReturnService, ReturnService>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    private async Task<int> CountSessionsAsync(string code)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>()
            .CashSessions.CountAsync(item => item.CashRegisterCode == code);
    }

    private async Task<Guid> LoadOrderDetailIdAsync(Guid orderId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>()
            .OrderDetails.Where(item => item.OrderId == orderId).Select(item => item.Id).SingleAsync();
    }

    private async Task<ReturnSnapshot> LoadReturnSnapshotAsync(Guid orderId, Guid productId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return new(
            await db.Returns.CountAsync(item => item.OrderId == orderId),
            await db.ReturnDetails.CountAsync(item => item.OrderDetail != null && item.OrderDetail.OrderId == orderId),
            await db.InventoryMovements.CountAsync(item => item.OrderId == orderId),
            await db.Products.Where(item => item.Id == productId).Select(item => item.CurrentStock).SingleAsync());
    }

    private async Task CleanupSaleAsync(Guid orderId, Guid productId, Guid sessionId, string code)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        if (orderId != Guid.Empty)
        {
            var detailIds = await db.OrderDetails.Where(item => item.OrderId == orderId).Select(item => item.Id).ToArrayAsync();
            db.ReturnDetails.RemoveRange(await db.ReturnDetails.Where(item => detailIds.Contains(item.OrderDetailId)).ToListAsync());
            var returns = await db.Returns.Where(item => item.OrderId == orderId).ToListAsync();
            db.CashMovements.RemoveRange(await db.CashMovements.Where(item => item.ReturnId.HasValue && returns.Select(value => value.Id).Contains(item.ReturnId.Value)).ToListAsync());
            db.Returns.RemoveRange(returns);
            db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => item.OrderId == orderId).ToListAsync());
            db.Orders.RemoveRange(await db.Orders.Where(item => item.Id == orderId).ToListAsync());
        }

        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == productId);
        if (product is not null)
        {
            product.CurrentStock += 1m;
        }

        db.CashSessions.RemoveRange(await db.CashSessions.Where(item => item.Id == sessionId).ToListAsync());
        await db.SaveChangesAsync();
        fixture.SetCashRegisterCode(code);
    }

    private static string UniqueCashRegisterCode() => $"QA-S-{Guid.NewGuid():N}"[..20];

    private sealed record ReturnSnapshot(int Returns, int Details, int InventoryMovements, decimal Stock);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class SessionDouble(Guid employeeId) : ICurrentSessionService
    {
        /// <inheritdoc />
        public Employee? CurrentEmployee { get; private set; } = new() { Id = employeeId };

        /// <inheritdoc />
        public OperationalModule? ActiveModule => OperationalModule.Caja;

        /// <inheritdoc />
        public string? CurrentModule => ActiveModule?.ToString();

        /// <inheritdoc />
        public DateTime? StartedAtUtc => PostgreSqlFixture.Now.UtcDateTime;

        /// <inheritdoc />
        public Guid? ActiveCashSessionId => null;

        /// <inheritdoc />
        public bool IsActive => CurrentEmployee is not null;

        /// <inheritdoc />
        public void StartSession(Employee employee, OperationalModule module, string initialSection) => CurrentEmployee = employee;

        /// <inheritdoc />
        public void SetActiveCashSession(Guid cashSessionId) { }

        /// <inheritdoc />
        public void ClearActiveCashSession() { }

        /// <inheritdoc />
        public string ResolveInitialSection() => NavSections.Facturacion;

        /// <inheritdoc />
        public void EndSession() => CurrentEmployee = null;
    }
}
