using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Security;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Comprueba el rechazo directo en servicios antes de sus validaciones o escrituras.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class SensitiveServiceAuthorizationIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Crear empleado exige administración de usuarios.</summary>
    [Fact]
    public async Task EmployeeService_Create_SinAdministracion_Rechaza()
    {
        await AssertUnauthorizedAsync< EmployeeService>(
            fixture.CashierId,
            service => service.CreateAsync(new EmployeeInput("", "", null, null, null, DateTime.UtcNow, 0, "PLAZO_FIJO", "MENSUAL", null, null, false, false), null, fixture.CashierId));
    }

    /// <summary>Crear producto exige administración de catálogo.</summary>
    [Fact]
    public async Task ProductCatalogService_Create_SinAdministracion_Rechaza()
    {
        await AssertUnauthorizedAsync<ProductCatalogService>(
            fixture.NonCashierId,
            service => service.CreateProductAsync(new ProductInput("", null, "", Guid.Empty, null, Guid.Empty, null, 0, 0, 0, 0, null, null, null), fixture.NonCashierId));
    }

    /// <summary>Guardar impresora exige administración de configuración.</summary>
    [Fact]
    public async Task PrinterConfigService_Save_SinAdministracion_Rechaza()
    {
        await AssertUnauthorizedAsync<PrinterConfigService>(
            fixture.CashierId,
            service => service.SaveAsync(new PrinterInput("QA", PrinterConfigurationRules.Usb, null, null, PrinterConfigurationRules.PaperWidth80, false)));
    }

    /// <summary>Registrar entradas de inventario exige permiso de inventario y no de caja.</summary>
    [Fact]
    public async Task InventoryService_RegisterEntry_Cajero_Rechaza()
    {
        await AssertUnauthorizedAsync<InventoryService>(
            fixture.CashierId,
            service => service.RegisterEntryAsync(Guid.NewGuid(), 1, 1, fixture.CashierId, "ENTRADA_COMPRA", "QA"));
    }

    /// <summary>Crear proveedor exige operación de inventario.</summary>
    [Fact]
    public async Task SupplierService_Create_Cajero_Rechaza()
    {
        await AssertUnauthorizedAsync<SupplierService>(
            fixture.CashierId,
            service => service.CreateAsync(new SupplierInput("QA", null, null, null, null, null, null, null, null, null, 0, null), fixture.CashierId));
    }

    /// <summary>Crear cliente exige operación de caja.</summary>
    [Fact]
    public async Task CustomerService_Create_Vendedor_Rechaza()
    {
        await AssertUnauthorizedAsync<CustomerService>(
            fixture.NonCashierId,
            service => service.CreateAsync(new CustomerInput("CF", "QA", null, null, null, null, null, null, null, null), fixture.NonCashierId));
    }

    /// <summary>Crear venta exige que el empleado actuante tenga permiso de caja.</summary>
    [Fact]
    public async Task OrderService_CreateCashSale_Vendedor_Rechaza()
    {
        await AssertUnauthorizedAsync<OrderService>(
            fixture.NonCashierId,
            service => service.CreateCashSaleAsync(new CreateCashSaleRequest(
                fixture.NonCashierId, null, null, Guid.NewGuid(), Array.Empty<CashSaleLineRequest>(), Array.Empty<CashSalePaymentRequest>(), null)));
    }

    private async Task AssertUnauthorizedAsync<TService>(Guid sessionEmployeeId, Func<TService, Task> operation)
        where TService : class
    {
        await using var provider = BuildProvider(sessionEmployeeId, typeof(TService));
        var service = provider.GetRequiredService<TService>();
        await Assert.ThrowsAsync<UnauthorizedOperationException>(() => operation(service));
    }

    private ServiceProvider BuildProvider(Guid sessionEmployeeId, Type serviceType)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<ICurrentSessionService>(new SessionDouble(new Employee { Id = sessionEmployeeId }));
        services.AddOptions<AuthorizationOptions>().Configure(options => options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddSingleton<ILogger<AuthorizationGuard>>(_ => NullLogger<AuthorizationGuard>.Instance);
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        services.AddSingleton<IAuditService, NoOpAuditService>();
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = "CAJA-INT");
        services.AddSingleton(serviceType);
        return services.BuildServiceProvider();
    }

    private sealed class SessionDouble(Employee employee) : ICurrentSessionService
    {
        public Employee? CurrentEmployee { get; private set; } = employee;
        public OperationalModule? ActiveModule => OperationalModule.Inventario;
        public string? CurrentModule => ActiveModule?.ToString();
        public DateTime? StartedAtUtc => DateTime.UtcNow;
        public Guid? ActiveCashSessionId => null;
        public bool IsActive => true;
        public void StartSession(Employee employee, OperationalModule module, string initialSection) => CurrentEmployee = employee;
        public void SetActiveCashSession(Guid cashSessionId) { }
        public void ClearActiveCashSession() { }
        public string ResolveInitialSection() => NavSections.Productos;
        public void EndSession() => CurrentEmployee = null;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordChangeAsync(string action, string tableName, string recordId, object? oldData, object? newData, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
