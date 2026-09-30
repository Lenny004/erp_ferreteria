using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Returns;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de devoluciones contra las tablas reales del fixture PostgreSQL.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class ReturnServiceIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa los casos con el fixture compartido.</summary>
    /// <param name="fixture">Contenedor PostgreSQL y empleados semilla.</param>
    public ReturnServiceIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    /// <summary>Dos devoluciones concurrentes no pueden consumir dos veces la misma cantidad.</summary>
    [Fact]
    public async Task ConcurrentReturns_OnlyOneCanUseAvailableQuantity()
    {
        var order = await CreateSaleAsync(2m);
        var service = BuildService();
        var lineId = order.OrderDetails.Single().Id;
        var first = CreateRequest(order.Id, lineId, Guid.NewGuid(), 2m);
        var second = first with { ClientRequestId = Guid.NewGuid() };

        var results = await Task.WhenAll(ObserveAsync(() => service.CreateReturnAsync(first, "1234")), ObserveAsync(() => service.CreateReturnAsync(second, "1234")));

        Assert.Equal(1, results.Count(result => result.Result is not null));
        var failure = Assert.Single(results.Where(result => result.Error is not null)).Error;
        var invalid = Assert.IsType<InvalidReturnException>(failure);
        Assert.Contains("supera lo disponible", invalid.Message, StringComparison.OrdinalIgnoreCase);
        await AssertDatabaseStateAsync(order.Id, lineId, 2m, 2m);
    }

    /// <summary>Repetir una clave de cliente recupera la misma devolución sin aumentar stock.</summary>
    [Fact]
    public async Task ExistingClientRequestId_IsIdempotent()
    {
        var order = await CreateSaleAsync(2m);
        var service = BuildService();
        var request = CreateRequest(order.Id, order.OrderDetails.Single().Id, Guid.NewGuid(), 1m);

        var first = await service.CreateReturnAsync(request, "1234");
        var second = await service.CreateReturnAsync(request, "1234");

        Assert.NotEqual(Guid.Empty, first.ReturnId);
        Assert.Equal(first.ReturnId, second.ReturnId);
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        Assert.Equal(1, await db.Returns.CountAsync(item => item.ClientRequestId == request.ClientRequestId));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(item => item.OrderId == order.Id && item.MovementType == SalesDomainConstants.InventoryMovementTypes.ReturnInflow));
    }

    /// <summary>La confirmación real queda habilitada y el lector expone la cantidad persistida.</summary>
    [Fact]
    public async Task RealImplementations_AreAuthoritative()
    {
        var order = await CreateSaleAsync(2m);
        var service = BuildService();
        Assert.True(service.Capabilities.CanConfirmReturns);
        var lines = await service.GetReturnableLinesAsync(order.Id, _fixture.CashierId);
        Assert.Equal(2m, Assert.Single(lines).AvailableQuantity);
    }

    /// <summary>El efectivo exige una sesión abierta antes de escribir la devolución.</summary>
    [Fact]
    public async Task CashRefundWithoutOpenSession_IsRejectedWithoutRows()
    {
        var order = await CreateSaleAsync(2m);
        var service = BuildService();
        var request = CreateRequest(order.Id, order.OrderDetails.Single().Id, Guid.NewGuid(), 1m) with
        {
            RefundMethod = ReturnDomainConstants.RefundMethods.Cash,
            RefundAmount = 10m
        };

        var exception = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(request, "1234"));

        Assert.Contains("CAJA-INT", exception.Message, StringComparison.OrdinalIgnoreCase);
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        Assert.False(await db.Returns.AnyAsync(item => item.ClientRequestId == request.ClientRequestId));
    }

    /// <summary>Un cajero puede ejecutar, pero solo el PIN del Administrador autoriza.</summary>
    [Fact]
    public async Task Authorization_RequiresAdministratorPin()
    {
        var order = await CreateSaleAsync(2m);
        var service = BuildService();
        var request = CreateRequest(order.Id, order.OrderDetails.Single().Id, _fixture.CashierId, 1m);

        var cashierPin = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(request, "0000"));
        Assert.Contains("historial", cashierPin.Message, StringComparison.OrdinalIgnoreCase);
        var badPin = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(request with { ClientRequestId = Guid.NewGuid() }, "9999"));
        Assert.Contains("PIN", badPin.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await HasReturnAsync(request.OrderId));
    }

    private IReturnService BuildService()
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IOptions<ReturnOptions>>(Options.Create(ReturnOptions.CreateDefault()));
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = "CAJA-INT");
        services.AddOptions<SalesHistoryOptions>().Configure(options => options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.AddSingleton<IReturnedQuantityReader, ReturnDetailsReturnedQuantityReader>();
        services.AddSingleton<IReturnWriter, EfReturnWriter>();
        services.AddSingleton<IReturnFiscalPolicy, DefaultReturnFiscalPolicy>();
        services.AddSingleton<IPinAttemptService, PinAttemptService>();
        services.AddSingleton<PinAuthService>();
        services.AddSingleton<IReturnService, ReturnService>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ReturnService>>(NullLogger<ReturnService>.Instance);
        return services.BuildServiceProvider().GetRequiredService<IReturnService>();
    }

    private async Task<Order> CreateSaleAsync(decimal quantity)
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime, SalesDomainConstants.OrderStatuses.Completed, quantity * 10m);
        order.Subtotal = quantity * 10m;
        order.TaxAmount = 0m;
        order.Total = quantity * 10m;
        order.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = productId,
            Quantity = quantity,
            UnitsPerPackage = 1m,
            UnitPrice = 10m,
            UnitCost = 5m,
            Subtotal = quantity * 10m
        });
        await _fixture.SeedAsync(db => db.Orders.Add(order));
        return order;
    }

    private static ReturnRequest CreateRequest(Guid orderId, Guid lineId, Guid employeeId, decimal quantity)
    {
        return new ReturnRequest(Guid.NewGuid(), orderId, employeeId, Guid.Empty, "CAMBIO", null, ReturnDomainConstants.RefundMethods.None, new[] { new ReturnLineRequest(lineId, quantity) }, 0m);
    }

    private async Task AssertDatabaseStateAsync(Guid orderId, Guid lineId, decimal quantity, decimal stockIncrease)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        Assert.Equal(quantity, await db.ReturnDetails.Where(item => item.OrderDetailId == lineId).SumAsync(item => item.Quantity));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(item => item.OrderId == orderId && item.MovementType == SalesDomainConstants.InventoryMovementTypes.ReturnInflow));
        Assert.Equal(SalesDomainConstants.OrderStatuses.Completed, await db.Orders.Where(item => item.Id == orderId).Select(item => item.Status).SingleAsync());
        Assert.Equal(stockIncrease, await db.ReturnDetails.Where(item => item.OrderDetailId == lineId).SumAsync(item => item.RestockQuantity));
    }

    private async Task<bool> HasReturnAsync(Guid orderId)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Returns.AnyAsync(item => item.OrderId == orderId);
    }

    private static async Task<Observation<ReturnResult>> ObserveAsync(Func<Task<ReturnResult>> action)
    {
        try
        {
            return new Observation<ReturnResult>(await action(), null);
        }
        catch (Exception exception)
        {
            return new Observation<ReturnResult>(null, exception);
        }
    }

    private sealed record Observation<T>(T? Result, Exception? Error);
}
