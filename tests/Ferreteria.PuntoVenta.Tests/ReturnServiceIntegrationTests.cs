using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Returns;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de devolución contra PostgreSQL con writer y reader autoritativos falsos.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class ReturnServiceIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa los casos con el fixture PostgreSQL compartido.</summary>
    /// <param name="fixture">Fixture con contenedor y empleados semilla.</param>
    public ReturnServiceIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>El bloqueo de la orden impide dos devoluciones concurrentes de la misma cantidad.</summary>
    [Fact]
    public async Task ConcurrentReturns_OnlyOneCanUseAvailableQuantity()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = await CreateSaleAsync(productId);
        var before = await ReadSnapshotAsync(order.Id, productId);
        var ledger = new ReturnLedger();
        using var provider = BuildProvider(ledger.Reader, ledger.Writer);
        var service = provider.GetRequiredService<IReturnService>();
        var first = CreateRequest(order.Id, order.OrderDetails.Single().Id, _fixture.CashierId, Guid.NewGuid());
        var second = first with { ClientRequestId = Guid.NewGuid() };

        var results = await Task.WhenAll(
            ObserveAsync(() => service.CreateReturnAsync(first)),
            ObserveAsync(() => service.CreateReturnAsync(second)));

        Assert.Equal(1, results.Count(result => result.Result is not null));
        var failure = results.Single(result => result.Error is not null).Error;
        var invalidReturn = Assert.IsType<InvalidReturnException>(failure);
        Assert.Contains("supera lo disponible", invalidReturn.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2m, ledger.TotalQuantity(order.Id));
        var after = await ReadSnapshotAsync(order.Id, productId);
        Assert.Equal(before, after);
        Assert.Equal(SalesDomainConstants.OrderStatuses.Completed, after.OrderStatus);
        Assert.Equal(before.InventoryMovements, after.InventoryMovements);
        Assert.Equal(before.AuditLogs, after.AuditLogs);
    }

    /// <summary>Una solicitud repetida devuelve el resultado guardado sin registrar otra línea.</summary>
    [Fact]
    public async Task ExistingClientRequestId_IsIdempotent()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = await CreateSaleAsync(productId);
        var ledger = new ReturnLedger();
        using var provider = BuildProvider(ledger.Reader, ledger.Writer);
        var service = provider.GetRequiredService<IReturnService>();
        var request = CreateRequest(order.Id, order.OrderDetails.Single().Id, _fixture.CashierId, Guid.NewGuid());

        var first = await service.CreateReturnAsync(request);
        var second = await service.CreateReturnAsync(request);

        Assert.Equal(first.ClientRequestId, second.ClientRequestId);
        Assert.Equal(1, ledger.PersistCount(request.ClientRequestId));
    }

    /// <summary>Las implementaciones pendientes rechazan antes de abrir transacción.</summary>
    [Fact]
    public async Task PendingImplementations_RejectWithoutWriting()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = await CreateSaleAsync(productId);
        var before = await ReadSnapshotAsync(order.Id, productId);
        using var provider = BuildProvider(new PendingMigrationReturnedQuantityReader(), new PendingMigrationReturnWriter());
        var service = provider.GetRequiredService<IReturnService>();
        var request = CreateRequest(order.Id, order.OrderDetails.Single().Id, _fixture.CashierId, Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<ReturnsUnavailableException>(() => service.CreateReturnAsync(request));

        Assert.Contains("se habilitará", exception.Message, StringComparison.OrdinalIgnoreCase);
        var after = await ReadSnapshotAsync(order.Id, productId);
        Assert.Equal(before, after);
    }

    /// <summary>El lector falso expone vendido menos ya devuelto en las líneas.</summary>
    [Fact]
    public async Task GetReturnableLines_UsesReturnedQuantityReader()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = await CreateSaleAsync(productId);
        var ledger = new ReturnLedger();
        ledger.SetReturned(order.Id, order.OrderDetails.Single().Id, 1m);
        using var provider = BuildProvider(ledger.Reader, ledger.Writer);
        var service = provider.GetRequiredService<IReturnService>();

        var lines = await service.GetReturnableLinesAsync(order.Id, _fixture.CashierId);

        Assert.Equal(2m, Assert.Single(lines).SoldQuantity);
        Assert.Equal(1m, Assert.Single(lines).AlreadyReturnedQuantity);
        Assert.Equal(1m, Assert.Single(lines).AvailableQuantity);
    }

    /// <summary>El efectivo exige una sesión abierta en el código configurado.</summary>
    [Fact]
    public async Task CashRefundWithoutOpenSession_IsRejected()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = await CreateSaleAsync(productId);
        var ledger = new ReturnLedger();
        using var provider = BuildProvider(ledger.Reader, ledger.Writer);
        var service = provider.GetRequiredService<IReturnService>();
        var request = CreateRequest(order.Id, order.OrderDetails.Single().Id, _fixture.CashierId, Guid.NewGuid()) with
        {
            RefundMethod = ReturnDomainConstants.RefundMethods.Cash
        };

        var exception = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(request));

        Assert.Contains("CAJA-INT", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>El cajero y el administrador ven ventas; un empleado sin alcance no ve ninguna.</summary>
    [Fact]
    public async Task SearchReturnableSales_EnforcesCashierAndFullHistoryAuthorization()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = await CreateSaleAsync(productId);
        var ledger = new ReturnLedger();
        using var provider = BuildProvider(ledger.Reader, ledger.Writer);
        var service = provider.GetRequiredService<IReturnService>();
        var filter = new ReturnableSalesFilter(SearchText: order.Id.ToString());

        var cashierResults = await service.SearchReturnableSalesAsync(filter, _fixture.CashierId);
        var managerResults = await service.SearchReturnableSalesAsync(filter, _fixture.ManagerId);
        var unauthorizedResults = await service.SearchReturnableSalesAsync(filter, _fixture.NonCashierId);

        Assert.Contains(cashierResults, item => item.OrderId == order.Id);
        Assert.Contains(managerResults, item => item.OrderId == order.Id);
        Assert.Empty(unauthorizedResults);
    }

    private async Task<Order> CreateSaleAsync(Guid productId)
    {
        var order = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime, SalesDomainConstants.OrderStatuses.Completed, 20m);
        order.Subtotal = 20m;
        order.TaxAmount = 0m;
        order.DiscountAmount = 0m;
        order.Total = 20m;
        var detail = new OrderDetail
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = productId,
            Quantity = 2m,
            UnitsPerPackage = 1m,
            UnitPrice = 10m,
            UnitCost = 5m,
            Subtotal = 20m
        };
        order.OrderDetails.Add(detail);
        await _fixture.SeedAsync(db =>
        {
            db.Orders.Add(order);
        });
        return order;
    }

    private ServiceProvider BuildProvider(IReturnedQuantityReader reader, IReturnWriter writer)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IOptions<ReturnOptions>>(Options.Create(ReturnOptions.CreateDefault()));
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = "CAJA-INT");
        services.AddOptions<SalesHistoryOptions>().Configure(options => options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.AddSingleton(reader);
        services.AddSingleton(writer);
        services.AddSingleton<IReturnFiscalPolicy, DefaultReturnFiscalPolicy>();
        services.AddSingleton<IReturnService, ReturnService>();
        services.AddSingleton<ILogger<ReturnService>>(NullLogger<ReturnService>.Instance);
        return services.BuildServiceProvider();
    }

    private ReturnRequest CreateRequest(Guid orderId, Guid lineId, Guid employeeId, Guid clientRequestId)
    {
        return new ReturnRequest(clientRequestId, orderId, employeeId, employeeId, "CAMBIO", null, ReturnDomainConstants.RefundMethods.Card, new[] { new ReturnLineRequest(lineId, 2m) }, 20m);
    }

    private async Task<ReturnDatabaseSnapshot> ReadSnapshotAsync(Guid orderId, Guid productId)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return new ReturnDatabaseSnapshot(
            await db.Orders.Where(item => item.Id == orderId).Select(item => item.Status).SingleAsync(),
            await db.Products.Where(item => item.Id == productId).Select(item => item.CurrentStock).SingleAsync(),
            await db.InventoryMovements.CountAsync(),
            await db.AuditLogs.CountAsync(),
            await db.CashSessions.CountAsync());
    }

    private async Task<Observation<ReturnResult>> ObserveAsync(Func<Task<ReturnResult>> operation)
    {
        try
        {
            return new Observation<ReturnResult>(await operation(), null);
        }
        catch (Exception exception)
        {
            return new Observation<ReturnResult>(null, exception);
        }
    }

    private sealed record Observation<T>(T? Result, Exception? Error);

    private sealed record ReturnDatabaseSnapshot(string OrderStatus, decimal ProductStock, int InventoryMovements, int AuditLogs, int CashSessions);

    private sealed class ReturnLedger
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, decimal> _quantities = new();
        private readonly Dictionary<Guid, Guid> _lineIds = new();
        private readonly Dictionary<Guid, ReturnResult> _results = new();
        private int _persistCount;
        public IReturnedQuantityReader Reader { get; }
        public IReturnWriter Writer { get; }

        public ReturnLedger()
        {
            Reader = new LedgerReader(this);
            Writer = new LedgerWriter(this);
        }

        public decimal TotalQuantity(Guid orderId)
        {
            lock (_gate) return _quantities.GetValueOrDefault(orderId);
        }

        public void SetReturned(Guid orderId, Guid lineId, decimal quantity)
        {
            lock (_gate)
            {
                _lineIds[orderId] = lineId;
                _quantities[orderId] = quantity;
            }
        }

        public int PersistCount(Guid requestId)
        {
            lock (_gate) return _results.ContainsKey(requestId) ? 1 : 0;
        }

        private sealed class LedgerReader : IReturnedQuantityReader
        {
            private readonly ReturnLedger _ledger;
            public LedgerReader(ReturnLedger ledger) => _ledger = ledger;
            public bool IsAuthoritative => true;
            public Task<ReturnedQuantityReadResult> GetAsync(FerreteriaDbContext dbContext, Guid orderId, CancellationToken cancellationToken = default)
            {
                lock (_ledger._gate)
                {
                    var quantity = _ledger._quantities.GetValueOrDefault(orderId);
                    var lines = _ledger._lineIds.TryGetValue(orderId, out var lineId)
                        ? new Dictionary<Guid, ReturnedLineCredit> { [lineId] = new ReturnedLineCredit(quantity) }
                        : new Dictionary<Guid, ReturnedLineCredit>();
                    return Task.FromResult(new ReturnedQuantityReadResult(lines, true));
                }
            }
        }

        private sealed class LedgerWriter : IReturnWriter
        {
            private readonly ReturnLedger _ledger;
            public LedgerWriter(ReturnLedger ledger) => _ledger = ledger;
            public bool IsAvailable => true;
            public Task<ReturnResult?> FindByClientRequestIdAsync(FerreteriaDbContext db, Guid clientRequestId, CancellationToken cancellationToken = default)
            {
                lock (_ledger._gate) return Task.FromResult(_ledger._results.GetValueOrDefault(clientRequestId));
            }

            public async Task<Guid> PersistAsync(FerreteriaDbContext db, ReturnPersistenceRecord record, CancellationToken cancellationToken = default)
            {
                await Task.Delay(300, cancellationToken);
                lock (_ledger._gate)
                {
                    _ledger._quantities[record.Header.OrderId] = _ledger._quantities.GetValueOrDefault(record.Header.OrderId) + record.Details.Sum(item => item.Quantity);
                    if (record.Details.Count > 0)
                    {
                        _ledger._lineIds[record.Header.OrderId] = record.Details[0].OrderDetailId;
                    }
                    var result = new ReturnResult(record.Header.ClientRequestId, record.Header.OrderId, new ReturnCalculationResult(Array.Empty<ReturnCreditLine>(), record.Header.Subtotal, record.Header.DiscountAmount, record.Header.TaxAmount, record.Header.Total, 0m, record.Header.ReturnType), new ReturnFiscalDecision(record.Header.FiscalStatus, null, string.Empty));
                    _ledger._results[record.Header.ClientRequestId] = result;
                    _ledger._persistCount++;
                    return Guid.NewGuid();
                }
            }
        }
    }
}
