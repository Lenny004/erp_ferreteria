using System.Text.Json;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica el desbloqueo administrativo de PIN por terminal.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class PinUnlockIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Un administrador reinicia el lockout, la progresión y deja auditoría completa.</summary>
    [Fact]
    public async Task AdminUnlocksTerminal_ResetsCounterAndWritesAudit()
    {
        var code = $"QA-U-{Guid.NewGuid():N}"[..20];
        try
        {
            await using var provider = BuildProvider(code, fixture.ManagerId);
            var attempts = provider.GetRequiredService<IPinAttemptService>();
            for (var index = 0; index < PinLockoutPolicy.MaxAttempts; index++)
            {
                await attempts.RegisterFailedAttemptAsync();
            }

            Assert.True((await attempts.GetStatusAsync()).IsLocked);

            await provider.GetRequiredService<IPinUnlockService>()
                .UnlockTerminalAsync(code, fixture.ManagerId, "Reinicio QA");

            var reset = await attempts.GetStatusAsync();
            Assert.False(reset.IsLocked);
            Assert.Equal(0, reset.FailedAttempts);
            Assert.Equal(1, (await attempts.RegisterFailedAttemptAsync()).FailedAttempts);

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var audit = await db.AuditLogs.SingleAsync(item =>
                item.TableName == SalesDomainConstants.PinAuditActions.TableName
                && item.RecordId == $"Caja:{code}"
                && item.Action == SalesDomainConstants.PinAuditActions.PinUnlock);
            Assert.Equal(fixture.ManagerId, audit.UserId);
            using var data = JsonDocument.Parse(audit.NewData!);
            Assert.Equal(code, data.RootElement.GetProperty("terminal").GetString());
            Assert.Equal("Reinicio QA", data.RootElement.GetProperty("reason").GetString());
        }
        finally
        {
            await DeleteAuditAsync(code);
        }
    }

    /// <summary>Un cajero no puede desbloquear una terminal ni generar PIN_UNLOCK.</summary>
    [Fact]
    public async Task CashierCannotUnlockTerminal_NoAuditIsWritten()
    {
        var code = $"QA-U-{Guid.NewGuid():N}"[..20];
        try
        {
            await using var provider = BuildProvider(code, fixture.CashierId);

            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                provider.GetRequiredService<IPinUnlockService>()
                    .UnlockTerminalAsync(code, fixture.CashierId, null));

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.False(await db.AuditLogs.AnyAsync(item =>
                item.RecordId == $"Caja:{code}"
                && item.Action == SalesDomainConstants.PinAuditActions.PinUnlock));
        }
        finally
        {
            await DeleteAuditAsync(code);
        }
    }

    /// <summary>Un identificador de actuante suplantado se rechaza antes de escribir auditoría.</summary>
    [Fact]
    public async Task SpoofedActingEmployee_IsRejectedWithoutAudit()
    {
        var code = $"QA-U-{Guid.NewGuid():N}"[..20];
        try
        {
            await using var provider = BuildProvider(code, fixture.ManagerId);

            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                provider.GetRequiredService<IPinUnlockService>()
                    .UnlockTerminalAsync(code, fixture.CashierId, "suplantado"));

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.False(await db.AuditLogs.AnyAsync(item =>
                item.RecordId == $"Caja:{code}"
                && item.Action == SalesDomainConstants.PinAuditActions.PinUnlock));
        }
        finally
        {
            await DeleteAuditAsync(code);
        }
    }

    /// <summary>Un administrador desactivado pierde el permiso aunque la sesión lo conserve.</summary>
    [Fact]
    public async Task DeactivatedEmployee_IsRejectedWithoutAudit()
    {
        var code = $"QA-U-{Guid.NewGuid():N}"[..20];
        await SetEmployeeActiveAsync(fixture.ManagerId, false);
        try
        {
            await using var provider = BuildProvider(code, fixture.ManagerId);

            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                provider.GetRequiredService<IPinUnlockService>()
                    .UnlockTerminalAsync(code, fixture.ManagerId, "inactivo"));
        }
        finally
        {
            await SetEmployeeActiveAsync(fixture.ManagerId, true);
            await DeleteAuditAsync(code);
        }
    }

    private ServiceProvider BuildProvider(string code, Guid sessionEmployeeId)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(PostgreSqlFixture.Now));
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = code);
        services.AddOptions<PinLockoutOptions>();
        services.AddOptions<AuthorizationOptions>().Configure(options =>
            options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddSingleton<ICurrentSessionService>(new SessionDouble(sessionEmployeeId));
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        services.AddSingleton<IPinAttemptService, PinAttemptService>();
        services.AddSingleton<IPinUnlockService, PinUnlockService>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    private async Task SetEmployeeActiveAsync(Guid employeeId, bool isActive)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var employee = await db.Employees.SingleAsync(item => item.Id == employeeId);
        employee.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private async Task DeleteAuditAsync(string code)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var rows = await db.AuditLogs.Where(item =>
            item.TableName == SalesDomainConstants.PinAuditActions.TableName
            && item.RecordId == $"Caja:{code}").ToListAsync();
        db.AuditLogs.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

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
        public OperationalModule? ActiveModule => OperationalModule.Inventario;

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
        public string ResolveInitialSection() => NavSections.Usuarios;

        /// <inheritdoc />
        public void EndSession() => CurrentEmployee = null;
    }
}
