using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Comprueba la unicidad transaccional de PIN y el rechazo de duplicados heredados.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class PinUniquenessIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Un cambio a un PIN ya usado rechaza la operación sin revelar al propietario.</summary>
    [Fact]
    public async Task SetPinAsync_PinRepetido_RechazaSinRevelarPropietario()
    {
        var firstId = await CreateEmployeeAsync();
        var secondId = await CreateEmployeeAsync();
        const string pin = "7461";
        try
        {
            await using var provider = BuildEmployeeProvider(fixture.ManagerId);
            var service = provider.GetRequiredService<EmployeeService>();
            await service.SetPinAsync(firstId, pin, fixture.ManagerId);

            var exception = await Assert.ThrowsAsync<ValidationException>(() =>
                service.SetPinAsync(secondId, pin, fixture.ManagerId));

            Assert.Equal("Ese PIN no está disponible. Elija otro.", exception.Message);
        }
        finally
        {
            await DeleteEmployeesAsync(firstId, secondId);
        }
    }

    /// <summary>Dos cambios concurrentes al mismo PIN dejan un único hash ganador.</summary>
    [Fact]
    public async Task SetPinAsync_ConcurrenteMismoPin_SoloUnaOperacionGana()
    {
        var firstId = await CreateEmployeeAsync();
        var secondId = await CreateEmployeeAsync();
        const string pin = "8352";
        try
        {
            await using var firstProvider = BuildEmployeeProvider(fixture.ManagerId);
            await using var secondProvider = BuildEmployeeProvider(fixture.ManagerId);
            var firstTask = firstProvider.GetRequiredService<EmployeeService>()
                .SetPinAsync(firstId, pin, fixture.ManagerId);
            var secondTask = secondProvider.GetRequiredService<EmployeeService>()
                .SetPinAsync(secondId, pin, fixture.ManagerId);

            var firstExceptionTask = Record.ExceptionAsync(() => firstTask);
            var secondExceptionTask = Record.ExceptionAsync(() => secondTask);
            var exceptions = await Task.WhenAll(firstExceptionTask, secondExceptionTask);

            Assert.Equal(1, exceptions.Count(exception => exception is null));
            Assert.All(exceptions.Where(exception => exception is not null),
                exception => Assert.IsType<ValidationException>(exception));

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var hashes = await db.Employees.AsNoTracking()
                .Where(employee => employee.Id == firstId || employee.Id == secondId)
                .Select(employee => employee.PinHash)
                .ToListAsync();
            Assert.Equal(1, hashes.Count(hash => hash is not null && BCrypt.Net.BCrypt.Verify(pin, hash)));
        }
        finally
        {
            await DeleteEmployeesAsync(firstId, secondId);
        }
    }

    /// <summary>Un PIN duplicado heredado no selecciona arbitrariamente un empleado.</summary>
    [Fact]
    public async Task PinAuthService_PinDuplicadoHeredado_Rechaza()
    {
        var firstId = await CreateEmployeeAsync();
        var secondId = await CreateEmployeeAsync();
        const string pin = "2947";
        try
        {
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
                var employees = await db.Employees
                    .Where(employee => employee.Id == firstId || employee.Id == secondId)
                    .ToListAsync();
                var hash = BCrypt.Net.BCrypt.HashPassword(pin, 4);
                foreach (var employee in employees)
                {
                    employee.PinHash = hash;
                    employee.CanCashier = true;
                }

                await db.SaveChangesAsync();
            }

            await using var provider = BuildPinProvider();
            var auth = provider.GetRequiredService<PinAuthService>();
            var result = await auth.ValidateActiveEmployeePinAsync(pin);

            Assert.Null(result);
        }
        finally
        {
            await DeleteEmployeesAsync(firstId, secondId);
        }
    }

    private async Task<Guid> CreateEmployeeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var manager = await db.Employees.AsNoTracking().SingleAsync(employee => employee.Id == fixture.ManagerId);
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "QA",
            LastName = $"Pin-{Guid.NewGuid():N}",
            PositionId = manager.PositionId,
            DepartmentId = manager.DepartmentId,
            HireDate = DateTime.UtcNow.Date,
            ContractType = "PLAZO_FIJO",
            SalaryType = "MENSUAL",
            IsActive = true
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task DeleteEmployeesAsync(params Guid[] ids)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var employees = await db.Employees.Where(employee => ids.Contains(employee.Id)).ToListAsync();
        db.Employees.RemoveRange(employees);
        await db.SaveChangesAsync();
    }

    private ServiceProvider BuildEmployeeProvider(Guid sessionEmployeeId)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<ICurrentSessionService>(new SessionDouble(sessionEmployeeId));
        services.AddOptions<AuthorizationOptions>()
            .Configure(options => options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddSingleton<ILogger<AuthorizationGuard>>(_ => NullLogger<AuthorizationGuard>.Instance);
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        services.AddSingleton<IAuditService, NoOpAuditService>();
        services.AddSingleton<EmployeeService>();
        return services.BuildServiceProvider();
    }

    private ServiceProvider BuildPinProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<ILogger<PinAuthService>>(_ => NullLogger<PinAuthService>.Instance);
        services.AddSingleton<PinAuthService>();
        return services.BuildServiceProvider();
    }

    private sealed class SessionDouble(Guid employeeId) : ICurrentSessionService
    {
        public Employee? CurrentEmployee { get; private set; } = new() { Id = employeeId };
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
