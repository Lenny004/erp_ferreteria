using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica contra PostgreSQL que el guard recarga el estado vigente de la sesión.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class AuthorizationGuardIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Una sesión ausente se rechaza antes de consultar permisos.</summary>
    [Fact]
    public async Task RequireAsync_SinSesion_Rechaza()
    {
        await using var provider = BuildProvider(new SessionDouble());
        var guard = provider.GetRequiredService<IAuthorizationGuard>();

        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            guard.RequireAsync(PosPermission.OperarCaja));
    }

    /// <summary>Un cambio en la BD invalida una foto de sesión que quedó obsoleta.</summary>
    [Fact]
    public async Task RequireAsync_EmpleadoDesactivadoDespuesDeLogin_Rechaza()
    {
        var employee = await CreateTemporaryEmployeeAsync();
        try
        {
            var session = new SessionDouble(employee);
            await using var provider = BuildProvider(session);
            var guard = provider.GetRequiredService<IAuthorizationGuard>();

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
                var current = await db.Employees.SingleAsync(item => item.Id == employee.Id);
                current.IsActive = false;
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                guard.RequireAsync(PosPermission.OperarInventario, employee.Id));
        }
        finally
        {
            await DeleteTemporaryEmployeeAsync(employee.Id);
        }
    }

    /// <summary>Un id actuante diferente del id de sesión se rechaza para evitar suplantación.</summary>
    [Fact]
    public async Task RequireAsync_IdActuanteDiferente_Rechaza()
    {
        var employee = await LoadEmployeeAsync(fixture.ManagerId);
        var session = new SessionDouble(employee);
        await using var provider = BuildProvider(session);
        var guard = provider.GetRequiredService<IAuthorizationGuard>();

        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            guard.RequireAsync(PosPermission.AdministrarUsuarios, Guid.NewGuid()));
    }

    private ServiceProvider BuildProvider(SessionDouble session)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<ICurrentSessionService>(session);
        services.AddOptions<AuthorizationOptions>()
            .Configure(options => options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddSingleton<ILogger<AuthorizationGuard>>(_ => NullLogger<AuthorizationGuard>.Instance);
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        return services.BuildServiceProvider();
    }

    private async Task<Employee> CreateTemporaryEmployeeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var manager = await db.Employees.AsNoTracking().SingleAsync(item => item.Id == fixture.ManagerId);
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "QA",
            LastName = $"Guard-{Guid.NewGuid():N}",
            PositionId = manager.PositionId,
            DepartmentId = manager.DepartmentId,
            HireDate = DateTime.UtcNow.Date,
            ContractType = "PLAZO_FIJO",
            SalaryType = "MENSUAL",
            IsActive = true
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private async Task<Employee> LoadEmployeeAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>()
            .Employees.AsNoTracking().Include(item => item.Position).SingleAsync(item => item.Id == id);
    }

    private async Task DeleteTemporaryEmployeeAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var employee = await db.Employees.SingleOrDefaultAsync(item => item.Id == id);
        if (employee is not null)
        {
            db.Employees.Remove(employee);
            await db.SaveChangesAsync();
        }
    }

    private sealed class SessionDouble(Employee? employee = null) : ICurrentSessionService
    {
        public Employee? CurrentEmployee { get; private set; } = employee;
        public OperationalModule? ActiveModule => OperationalModule.Inventario;
        public string? CurrentModule => ActiveModule?.ToString();
        public DateTime? StartedAtUtc => DateTime.UtcNow;
        public Guid? ActiveCashSessionId => null;
        public bool IsActive => CurrentEmployee is not null;
        public void StartSession(Employee employee, OperationalModule module, string initialSection) => CurrentEmployee = employee;
        public void SetActiveCashSession(Guid cashSessionId) { }
        public void ClearActiveCashSession() { }
        public string ResolveInitialSection() => NavSections.Productos;
        public void EndSession() => CurrentEmployee = null;
    }
}
