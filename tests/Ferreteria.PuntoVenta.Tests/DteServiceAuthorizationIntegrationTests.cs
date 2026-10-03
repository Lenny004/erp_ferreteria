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

    /// <summary>Una nota de crédito propia se emite contra un DTE propio y queda enlazada al original.</summary>
    [Fact]
    public async Task EmitCreditNote_Authorized_UsesOwnAcceptedOriginal()
    {
        var orderId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var previousActiveConfigIds = await PrepareCreditNoteDataAsync(orderId, configId, customerId, productId);
        try
        {
            var fakeMh = new FakeMhApiClient();
            await using var provider = BuildProvider(fixture.ManagerId, fakeMh);
            var dte = provider.GetRequiredService<IDteService>();
            var original = await dte.EmitForOrderAsync(
                new EmitDteRequest(orderId, DteConstants.TiposDte.CreditoFiscal, customerId));

            Assert.True(original.IsAccepted);
            Assert.Equal(DteConstants.TiposDte.CreditoFiscal, original.DteType);

            var note = await dte.EmitCreditNoteAsync(original.NumeroControl, "QA nota autorizada", fixture.ManagerId);

            Assert.True(note.IsAccepted);
            Assert.Equal(DteConstants.TiposDte.NotaCredito, note.DteType);
            Assert.Equal(orderId, note.OrderId);
            Assert.Equal(2, fakeMh.SendCount);

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var rows = await db.DteIssued.Where(item => item.OrderId == orderId).ToListAsync();
            var persistedOriginal = Assert.Single(rows, item => item.Id == original.DteIssuedId);
            var persistedNote = Assert.Single(rows, item => item.Id == note.DteIssuedId);
            Assert.Equal(DteConstants.TiposDte.CreditoFiscal, persistedOriginal.DteType);
            Assert.Equal(DteConstants.TiposDte.NotaCredito, persistedNote.DteType);
            Assert.Equal(persistedOriginal.Id, persistedNote.RelatedDteId);
            Assert.Equal(SalesDomainConstants.OrderStatuses.Cancelled,
                await db.Orders.Where(item => item.Id == orderId).Select(item => item.Status).SingleAsync());
        }
        finally
        {
            await CleanupCreditNoteDataAsync(orderId, configId, customerId, productId, previousActiveConfigIds);
        }
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
        IReadOnlyList<Guid> previousActiveConfigIds = Array.Empty<Guid>();
        try
        {
            previousActiveConfigIds = await SeedEmissionDataAsync(orderId, configId);
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
            await CleanupEmissionDataAsync(orderId, configId, previousActiveConfigIds);
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

    private async Task<IReadOnlyList<Guid>> SeedEmissionDataAsync(Guid orderId, Guid configId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var now = PostgreSqlFixture.Now.UtcDateTime;
        var previousActiveConfigIds = await db.DteConfigs
            .Where(item => item.IsActive)
            .Select(item => item.Id)
            .ToListAsync();
        var previousActiveConfigs = await db.DteConfigs
            .Where(item => previousActiveConfigIds.Contains(item.Id))
            .ToListAsync();
        foreach (var previousActiveConfig in previousActiveConfigs)
        {
            previousActiveConfig.IsActive = false;
        }

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
        return previousActiveConfigIds;
    }

    private async Task<IReadOnlyList<Guid>> PrepareCreditNoteDataAsync(
        Guid orderId,
        Guid configId,
        Guid customerId,
        Guid productId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var now = PostgreSqlFixture.Now.UtcDateTime;
        var activeConfigIds = await db.DteConfigs.Where(item => item.IsActive).Select(item => item.Id).ToListAsync();
        var activeConfigs = await db.DteConfigs.Where(item => activeConfigIds.Contains(item.Id)).ToListAsync();
        foreach (var activeConfig in activeConfigs)
        {
            activeConfig.IsActive = false;
        }

        var familyId = await db.Families.Select(item => item.Id).FirstAsync();
        var measurementTypeId = await db.MeasurementTypes.Select(item => item.Id).FirstAsync();
        db.DteConfigs.Add(new DteConfig
        {
            Id = configId,
            EmisorNit = "00000000000000",
            EmisorNrc = "0000000",
            EmisorName = "Emisor QA Nota",
            ActividadEconomica = "000000",
            AddressLine = "Dirección QA",
            Municipality = "Municipio QA",
            Department = "Departamento QA",
            Ambiente = "00",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            CustomerType = "CCF",
            Name = "Cliente QA Nota",
            Nit = "06140000000000",
            Nrc = "0000000",
            IsActive = true
        });
        db.Products.Add(new Product
        {
            Id = productId,
            Code = $"QA-DTE-{Guid.NewGuid():N}"[..30],
            Description = "Producto propio DTE QA",
            FamilyId = familyId,
            MeasurementTypeId = measurementTypeId,
            SalePrice = 10m,
            CostPrice = 5m,
            CurrentStock = 10m,
            IsActive = true
        });
        db.Orders.Add(new Order
        {
            Id = orderId,
            EmployeeId = fixture.ManagerId,
            CustomerId = customerId,
            ClientRequestId = Guid.NewGuid(),
            OrderType = SalesDomainConstants.OrderTypes.CashRegisterSale,
            Status = SalesDomainConstants.OrderStatuses.Completed,
            Subtotal = 10m,
            TaxAmount = 1.30m,
            Total = 11.30m,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = 1m,
            UnitsPerPackage = 1m,
            UnitPrice = 10m,
            UnitCost = 5m,
            Subtotal = 10m
        });
        await db.SaveChangesAsync();
        return activeConfigIds;
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

    private async Task CleanupEmissionDataAsync(
        Guid orderId,
        Guid configId,
        IReadOnlyList<Guid> previousActiveConfigIds)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        db.DteIssued.RemoveRange(await db.DteIssued.Where(item => item.OrderId == orderId).ToListAsync());
        db.Orders.RemoveRange(await db.Orders.Where(item => item.Id == orderId).ToListAsync());
        db.DteConfigs.RemoveRange(await db.DteConfigs.Where(item => item.Id == configId).ToListAsync());
        var previousActiveConfigs = await db.DteConfigs
            .Where(item => previousActiveConfigIds.Contains(item.Id))
            .ToListAsync();
        foreach (var previousActiveConfig in previousActiveConfigs)
        {
            previousActiveConfig.IsActive = true;
        }
        await db.SaveChangesAsync();
    }

    private async Task CleanupCreditNoteDataAsync(
        Guid orderId,
        Guid configId,
        Guid customerId,
        Guid productId,
        IReadOnlyList<Guid> previousActiveConfigIds)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        db.DteIssued.RemoveRange(await db.DteIssued.Where(item => item.OrderId == orderId).ToListAsync());
        // La nota de crédito reingresa el stock con un movimiento de kardex que referencia la orden y el producto propios.
        db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => item.OrderId == orderId || item.ProductId == productId).ToListAsync());
        db.StockAlerts.RemoveRange(await db.StockAlerts.Where(item => item.ProductId == productId).ToListAsync());
        db.OrderDetails.RemoveRange(await db.OrderDetails.Where(item => item.OrderId == orderId).ToListAsync());
        db.Orders.RemoveRange(await db.Orders.Where(item => item.Id == orderId).ToListAsync());
        db.Products.RemoveRange(await db.Products.Where(item => item.Id == productId).ToListAsync());
        db.Customers.RemoveRange(await db.Customers.Where(item => item.Id == customerId).ToListAsync());
        db.DteConfigs.RemoveRange(await db.DteConfigs.Where(item => item.Id == configId).ToListAsync());
        var previousConfigs = await db.DteConfigs.Where(item => previousActiveConfigIds.Contains(item.Id)).ToListAsync();
        foreach (var previousConfig in previousConfigs)
        {
            previousConfig.IsActive = true;
        }

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
