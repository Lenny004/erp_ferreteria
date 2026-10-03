using Npgsql;
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

/// <summary>Comprueba en el PostgreSQL desechable que el rol del POS no recibe privilegios peligrosos.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class DatabaseRoleIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Aplica el script al contenedor efímero y comprueba lectura POS, WebUsers y DDL.</summary>
    [Fact]
    public async Task PosAppRole_AllowsPosReadAndRejectsWebUsersAndDdl()
    {
        // El script de docs/pos se enlaza en el csproj y se copia a Data/, igual que Squema.sql.
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Data", "pos_app_rol_minimo.sql");
        var script = await File.ReadAllTextAsync(scriptPath);
        var adminBuilder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        script = script.Replace(
            "GRANT CONNECT ON DATABASE ferreteria TO pos_app;",
            $"GRANT CONNECT ON DATABASE \"{adminBuilder.Database}\" TO pos_app;",
            StringComparison.Ordinal);

        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand(script, admin);
            await command.ExecuteNonQueryAsync();
            await using var password = new NpgsqlCommand("ALTER ROLE pos_app PASSWORD 'qa-only-password'", admin);
            await password.ExecuteNonQueryAsync();
            await using var webUsers = new NpgsqlCommand(
                "CREATE TABLE IF NOT EXISTS system.\"WebUsers\" (\"id\" uuid PRIMARY KEY)",
                admin);
            await webUsers.ExecuteNonQueryAsync();
        }

        adminBuilder.Username = "pos_app";
        adminBuilder.Password = "qa-only-password";
        await using var restricted = new NpgsqlConnection(adminBuilder.ConnectionString);
        await restricted.OpenAsync();

        await using (var currentUser = new NpgsqlCommand("SELECT current_user", restricted))
        {
            Assert.Equal("pos_app", (string?)await currentUser.ExecuteScalarAsync());
        }

        await using (var readable = new NpgsqlCommand("SELECT COUNT(*) FROM sales.\"Orders\"", restricted))
        {
            Assert.True(Convert.ToInt64(await readable.ExecuteScalarAsync()) >= 0);
        }

        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("SELECT COUNT(*) FROM system.\"WebUsers\"", restricted).ExecuteScalarAsync());
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("CREATE TABLE public.pos_app_ddl_forbidden (id integer)", restricted).ExecuteNonQueryAsync());
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("UPDATE system.\"AuditLog\" SET \"Action\" = \"Action\" WHERE FALSE", restricted).ExecuteNonQueryAsync());
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("UPDATE hr.\"Positions\" SET \"Name\" = \"Name\" WHERE FALSE", restricted).ExecuteNonQueryAsync());
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("UPDATE hr.\"Departments\" SET \"Name\" = \"Name\" WHERE FALSE", restricted).ExecuteNonQueryAsync());
    }

    /// <summary>Ejecuta apertura, venta, devolución, cierre y lockout usando exclusivamente la conexión <c>pos_app</c>.</summary>
    /// <remarks>La decisión fiscal de la devolución permanece pendiente de verificación con contador/MH.</remarks>
    [Fact]
    public async Task PosAppRole_ExecutesCompletePosFlowAndPersistsPinAudit()
    {
        var code = $"QA-R-{Guid.NewGuid():N}"[..20];
        var productId = Guid.Empty;
        var originalStock = 0m;
        var sessionId = Guid.Empty;
        var orderId = Guid.Empty;
        try
        {
            var roleConnectionString = await PrepareRoleAsync();
            await using var provider = BuildRestrictedProvider(roleConnectionString, code);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
                var product = (await TestDataFactory.CreateProductsAsync(db, 1, 3m)).Single();
                productId = product.Id;
                originalStock = product.CurrentStock;
            }

            var cash = provider.GetRequiredService<ICashSessionService>();
            var opened = await cash.OpenAsync(fixture.ManagerId, code, 0m, "QA rol pos_app");
            sessionId = opened.Id;
            var productSnapshot = await ReadProductAsync(provider, productId);
            var total = TaxAmountCalculator.CalculateGrandTotal(productSnapshot.SalePrice);
            var order = await provider.GetRequiredService<OrderService>().CreateCashSaleAsync(new CreateCashSaleRequest(
                fixture.ManagerId, sessionId, null, Guid.NewGuid(), new[] { new CashSaleLineRequest(productId, 1m) },
                new[] { new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, total) }, "QA rol pos_app"));
            orderId = order.OrderId;

            var orderDetailId = await ReadOrderDetailIdAsync(provider, orderId);
            var returnResult = await provider.GetRequiredService<IReturnService>().CreateReturnAsync(new ReturnRequest(
                Guid.NewGuid(), orderId, fixture.ManagerId, Guid.Empty, "CAMBIO", null,
                ReturnDomainConstants.RefundMethods.None, new[] { new ReturnLineRequest(orderDetailId, 1m) }), "1234");
            Assert.Equal(orderId, returnResult.OrderId);

            var pinAttempts = provider.GetRequiredService<IPinAttemptService>();
            for (var index = 0; index < PinLockoutPolicy.MaxAttempts; index++)
            {
                await pinAttempts.RegisterFailedAttemptAsync();
            }
            Assert.True((await pinAttempts.GetStatusAsync()).IsLocked);
            // Un PIN_OK queda auditado, pero ya no borra los fallos de la terminal: el bloqueo sigue vigente.
            await pinAttempts.ResetAsync();
            Assert.True((await pinAttempts.GetStatusAsync()).IsLocked);

            await provider.GetRequiredService<IPinUnlockService>()
                .UnlockTerminalAsync(code, fixture.ManagerId, "Desbloqueo QA pos_app");
            Assert.False((await pinAttempts.GetStatusAsync()).IsLocked);

            await cash.CloseAsync(sessionId, total, "QA cierre pos_app", fixture.ManagerId);

            await using var verifyScope = provider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.Equal(SalesDomainConstants.CashSessionStatuses.Closed, await verifyDb.CashSessions.Where(item => item.Id == sessionId).Select(item => item.Status).SingleAsync());
            Assert.Equal(1, await verifyDb.Returns.CountAsync(item => item.Id == returnResult.ReturnId));
            Assert.True(await verifyDb.AuditLogs.CountAsync(item => item.TableName == SalesDomainConstants.PinAuditActions.TableName && item.Action == SalesDomainConstants.PinAuditActions.PinFail) >= PinLockoutPolicy.MaxAttempts);
            Assert.True(await verifyDb.AuditLogs.AnyAsync(item => item.TableName == SalesDomainConstants.PinAuditActions.TableName && item.Action == SalesDomainConstants.PinAuditActions.PinOk));
            Assert.True(await verifyDb.AuditLogs.AnyAsync(item => item.TableName == SalesDomainConstants.PinAuditActions.TableName && item.Action == SalesDomainConstants.PinAuditActions.PinUnlock && item.RecordId == $"Caja:{code}" && item.UserId == fixture.ManagerId));
        }
        finally
        {
            await CleanupAdminAsync(productId, originalStock, sessionId, orderId);
        }
    }

    private async Task<string> PrepareRoleAsync()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Data", "pos_app_rol_minimo.sql");
        var script = await File.ReadAllTextAsync(scriptPath);
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        script = script.Replace("GRANT CONNECT ON DATABASE ferreteria TO pos_app;", $"GRANT CONNECT ON DATABASE \"{builder.Database}\" TO pos_app;", StringComparison.Ordinal);
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand(script, admin)) await command.ExecuteNonQueryAsync();
        await using (var password = new NpgsqlCommand("ALTER ROLE pos_app PASSWORD 'qa-only-password'", admin)) await password.ExecuteNonQueryAsync();
        builder.Username = "pos_app"; builder.Password = "qa-only-password";
        return builder.ConnectionString;
    }

    private ServiceProvider BuildRestrictedProvider(string connectionString, string code)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(PostgreSqlFixture.Now));
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = code);
        services.AddOptions<PinLockoutOptions>();
        services.AddOptions<SalesHistoryOptions>().Configure(options => options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.Configure<ReturnOptions>(ReturnOptions.ApplyDefaults);
        services.AddOptions<AuthorizationOptions>().Configure(options => options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddSingleton<ICurrentSessionService>(new RoleSessionDouble(fixture.ManagerId));
        services.AddSingleton<ILogger<AuthorizationGuard>>(_ => NullLogger<AuthorizationGuard>.Instance);
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        services.AddSingleton<ICashMovementReader, CashMovementsCashMovementReader>();
        services.AddSingleton<IReturnedQuantityReader, ReturnDetailsReturnedQuantityReader>();
        services.AddSingleton<IReturnWriter, EfReturnWriter>();
        services.AddSingleton<IReturnFiscalPolicy, DefaultReturnFiscalPolicy>();
        services.AddSingleton<ILogger<PinAttemptService>>(_ => NullLogger<PinAttemptService>.Instance);
        services.AddSingleton<IPinAttemptService, PinAttemptService>();
        services.AddSingleton<ILogger<PinUnlockService>>(_ => NullLogger<PinUnlockService>.Instance);
        services.AddSingleton<IPinUnlockService, PinUnlockService>();
        services.AddSingleton<PinAuthService>();
        services.AddSingleton<ICashSessionService, CashSessionService>();
        services.AddSingleton<OrderService>();
        services.AddSingleton<IReturnService, ReturnService>();
        services.AddSingleton<ILogger<CashSessionService>>(_ => NullLogger<CashSessionService>.Instance);
        services.AddSingleton<ILogger<OrderService>>(_ => NullLogger<OrderService>.Instance);
        services.AddSingleton<ILogger<ReturnService>>(_ => NullLogger<ReturnService>.Instance);
        return services.BuildServiceProvider();
    }

    private async Task<Product> ReadProductAsync(ServiceProvider provider, Guid id)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().Products.AsNoTracking().SingleAsync(item => item.Id == id);
    }

    private async Task<Guid> ReadOrderDetailIdAsync(ServiceProvider provider, Guid orderId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().OrderDetails.Where(item => item.OrderId == orderId).Select(item => item.Id).SingleAsync();
    }

    private async Task CleanupAdminAsync(Guid productId, decimal originalStock, Guid sessionId, Guid orderId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        if (orderId != Guid.Empty)
        {
            var orderDetails = await db.OrderDetails.Where(item => item.OrderId == orderId).ToListAsync();
            var detailIds = orderDetails.Select(item => item.Id).ToArray();
            db.ReturnDetails.RemoveRange(await db.ReturnDetails.Where(item => detailIds.Contains(item.OrderDetailId)).ToListAsync());
            var returns = await db.Returns.Where(item => item.OrderId == orderId).ToListAsync();
            var returnIds = returns.Select(item => item.Id).ToArray();
            db.CashMovements.RemoveRange(await db.CashMovements.Where(item => item.ReturnId.HasValue && returnIds.Contains(item.ReturnId.Value)).ToListAsync());
            db.Returns.RemoveRange(returns);
            db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => item.OrderId == orderId).ToListAsync());
            var orders = await db.Orders.Where(item => item.Id == orderId).ToListAsync();
            db.Orders.RemoveRange(orders);
        }
        if (sessionId != Guid.Empty)
        {
            var sessions = await db.CashSessions.Where(item => item.Id == sessionId).ToListAsync();
            db.CashSessions.RemoveRange(sessions);
        }
        if (productId != Guid.Empty)
        {
            var product = await db.Products.SingleOrDefaultAsync(item => item.Id == productId);
            if (product is not null) db.Products.Remove(product);
        }
        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RoleSessionDouble(Guid employeeId) : ICurrentSessionService
    {
        public Employee? CurrentEmployee { get; private set; } = new() { Id = employeeId };
        public OperationalModule? ActiveModule => OperationalModule.Caja;
        public string? CurrentModule => ActiveModule?.ToString();
        public DateTime? StartedAtUtc => DateTime.UtcNow;
        public Guid? ActiveCashSessionId => null;
        public bool IsActive => true;
        public void StartSession(Employee employee, OperationalModule module, string initialSection) => CurrentEmployee = employee;
        public void SetActiveCashSession(Guid cashSessionId) { }
        public void ClearActiveCashSession() { }
        public string ResolveInitialSection() => NavSections.Facturacion;
        public void EndSession() => CurrentEmployee = null;
    }
}
