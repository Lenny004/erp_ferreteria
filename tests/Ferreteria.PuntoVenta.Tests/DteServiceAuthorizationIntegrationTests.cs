using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Models.Dte.Json;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Dte;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Security;
using Ferreteria.PuntoVenta.Services.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la autorización por sesión de las operaciones DTE sin llamar al MH real.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class DteServiceAuthorizationIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Un empleado sin permiso no puede ejecutar las tres operaciones protegidas.</summary>
    [Fact]
    public async Task DteOperations_InsufficientPermission_AreRejectedWithoutChanges()
    {
        await using var provider = BuildProvider(fixture.NonCashierId, new FakeMhApiClient());
        var dte = provider.GetRequiredService<IDteService>();
        var before = await LoadDteSnapshotAsync();

        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            dte.EmitForOrderAsync(new EmitDteRequest(Guid.NewGuid(), DteConstants.TiposDte.Factura, null)));
        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            dte.EmitCreditNoteAsync("QA-NO-AUTORIZADO", "QA", fixture.NonCashierId));
        await Assert.ThrowsAsync<UnauthorizedOperationException>(() => dte.ProcessPendingContingenciesAsync());

        Assert.Equal(before, await LoadDteSnapshotAsync());
    }

    /// <summary>Una nota de crédito no acepta un empleado actuante suplantado.</summary>
    [Fact]
    public async Task EmitCreditNote_SpoofedActingEmployee_IsRejected()
    {
        await using var provider = BuildProvider(fixture.ManagerId, new FakeMhApiClient());
        var dte = provider.GetRequiredService<IDteService>();

        await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
            dte.EmitCreditNoteAsync("QA-SUPLANTADO", "QA", fixture.CashierId));
    }

    /// <summary>Una nota de crédito autorizada supera el guard y llega a la validación del DTE original.</summary>
    [Fact]
    public async Task EmitCreditNote_Authorized_ReachesBusinessValidation()
    {
        await using var provider = BuildProvider(fixture.ManagerId, new FakeMhApiClient());
        var exception = await Assert.ThrowsAsync<DteException>(() =>
            provider.GetRequiredService<IDteService>().EmitCreditNoteAsync("QA-INEXISTENTE", "QA", fixture.ManagerId));

        Assert.Contains("DTE", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>El reproceso autorizado supera el guard aunque no haya contingencias pendientes.</summary>
    [Fact]
    public async Task ProcessPendingContingencies_Authorized_ReachesService()
    {
        await using var provider = BuildProvider(fixture.ManagerId, new FakeMhApiClient());

        var processed = await provider.GetRequiredService<IDteService>().ProcessPendingContingenciesAsync();

        Assert.True(processed >= 0);
    }

    /// <summary>Un empleado desactivado en la BD no puede emitir aunque la UI conserve su sesión.</summary>
    [Fact]
    public async Task EmitForOrder_DeactivatedSessionEmployee_IsRejected()
    {
        await SetEmployeeActiveAsync(fixture.ManagerId, false);
        try
        {
            await using var provider = BuildProvider(fixture.ManagerId, new FakeMhApiClient());
            await Assert.ThrowsAsync<UnauthorizedOperationException>(() =>
                provider.GetRequiredService<IDteService>().EmitForOrderAsync(
                    new EmitDteRequest(Guid.NewGuid(), DteConstants.TiposDte.Factura, null)));
        }
        finally
        {
            await SetEmployeeActiveAsync(fixture.ManagerId, true);
        }
    }

    /// <summary>La emisión autorizada llega al cliente falso del MH y persiste el resultado.</summary>
    [Fact]
    public async Task EmitForOrder_Authorized_ReachesFakeMhAndPersistsAcceptedDte()
    {
        var orderId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var fakeMh = new FakeMhApiClient();
        try
        {
            await SeedEmissionDataAsync(orderId, configId);
            await using var provider = BuildProvider(fixture.ManagerId, fakeMh);
            var result = await provider.GetRequiredService<IDteService>().EmitForOrderAsync(
                new EmitDteRequest(orderId, DteConstants.TiposDte.Factura, null));

            Assert.True(result.IsAccepted);
            Assert.Equal(1, fakeMh.SendCount);
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.Equal(DteConstants.EstadosMh.Procesado,
                await db.DteIssued.Where(item => item.OrderId == orderId).Select(item => item.MhStatus).SingleAsync());
        }
        finally
        {
            await CleanupEmissionDataAsync(orderId, configId);
        }
    }

    private ServiceProvider BuildProvider(Guid sessionEmployeeId, FakeMhApiClient fakeMh)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(PostgreSqlFixture.Now));
        services.AddOptions<BusinessTimeOptions>().Configure(options => options.ZonaHoraria = "America/El_Salvador");
        services.AddSingleton<BusinessTimeZone>(provider => BusinessTimeZoneFactory.Create(
            provider.GetRequiredService<IOptions<BusinessTimeOptions>>()));
        services.AddSingleton<BusinessCalendar>();
        services.AddOptions<MhOptions>().Configure(options =>
        {
            options.Ambiente = "00";
            options.ContingencyRetryMinutes = 15;
        });
        services.AddOptions<AuthorizationOptions>().Configure(options =>
            options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddSingleton<ICurrentSessionService>(new SessionDouble(sessionEmployeeId));
        services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>();
        services.AddSingleton<IDteNumberingService, DteNumberingService>();
        services.AddSingleton<IDteJsonBuilder, FakeDteJsonBuilder>();
        services.AddSingleton<IDteSigningService, FakeDteSigningService>();
        services.AddSingleton<IMhApiClient>(fakeMh);
        services.AddSingleton<IDteService, DteService>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    private async Task SeedEmissionDataAsync(Guid orderId, Guid configId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var now = PostgreSqlFixture.Now.UtcDateTime;
        db.DteConfigs.Add(new DteConfig
        {
            Id = configId,
            EmisorNit = "00000000000000",
            EmisorNrc = "0000000",
            EmisorName = "Emisor QA",
            ActividadEconomica = "000000",
            AddressLine = "Dirección QA",
            Municipality = "Municipio QA",
            Department = "Departamento QA",
            Ambiente = "00",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Orders.Add(new Order
        {
            Id = orderId,
            EmployeeId = fixture.ManagerId,
            ClientRequestId = Guid.NewGuid(),
            OrderType = SalesDomainConstants.OrderTypes.CashRegisterSale,
            Status = SalesDomainConstants.OrderStatuses.Completed,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    private async Task SetEmployeeActiveAsync(Guid employeeId, bool isActive)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var employee = await db.Employees.SingleAsync(item => item.Id == employeeId);
        employee.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private async Task<DteSnapshot> LoadDteSnapshotAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return new(
            await db.DteIssued.CountAsync(),
            await db.DteContingencies.CountAsync(),
            await db.Orders.CountAsync());
    }

    private async Task CleanupEmissionDataAsync(Guid orderId, Guid configId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        db.DteIssued.RemoveRange(await db.DteIssued.Where(item => item.OrderId == orderId).ToListAsync());
        db.Orders.RemoveRange(await db.Orders.Where(item => item.Id == orderId).ToListAsync());
        db.DteConfigs.RemoveRange(await db.DteConfigs.Where(item => item.Id == configId).ToListAsync());
        await db.SaveChangesAsync();
    }

    private sealed class FakeDteJsonBuilder : IDteJsonBuilder
    {
        public DteDocument BuildInvoice(DteBuildContext context) => Build(context.TipoDte);

        public DteDocument BuildCreditNote(
            DteBuildContext context,
            DteIssued originalDte,
            IReadOnlyList<OrderDetail> returnedLines) => Build(context.TipoDte);

        private static DteDocument Build(string type) => new()
        {
            Identificacion = new DteIdentificacion { Version = 1, TipoDte = type },
            Resumen = new DteResumen { TotalPagar = 0m, TotalGravada = 0m, TotalExenta = 0m, TotalIva = 0m }
        };
    }

    private sealed record DteSnapshot(int Issued, int Contingencies, int Orders);

    private sealed class FakeDteSigningService : IDteSigningService
    {
        public Task<string> SignAsync(DteDocument document, string emisorNit, string certPassword, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("JWS-QA");
        }
    }

    private sealed class FakeMhApiClient : IMhApiClient
    {
        public int SendCount { get; private set; }

        public Task<string> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult("Bearer QA");

        public Task<MhReceptionResponse> SendDteAsync(MhReceptionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCount++;
            return Task.FromResult(new MhReceptionResponse
            {
                Estado = DteConstants.RespuestasMh.Procesado,
                SelloRecibido = "SELLO-QA"
            });
        }
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
