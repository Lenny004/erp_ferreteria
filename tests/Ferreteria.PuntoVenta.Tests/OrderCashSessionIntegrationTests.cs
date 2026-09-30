using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de integración de ventas y facturación con sesión de caja.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class OrderCashSessionIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa los casos con el fixture PostgreSQL compartido.</summary>
    /// <param name="fixture">Contenedor y servicios transaccionales compartidos.</param>
    public OrderCashSessionIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Rechaza sesiones inválidas y asocia todos los pagos de una venta válida.</summary>
    /// <remarks>
    /// El mismo ClientRequestId se reintenta después del cierre para verificar que la idempotencia se evalúa
    /// antes de exigir una sesión ABIERTA y no duplica orden ni pagos.
    /// </remarks>
    [Fact]
    public async Task CreateCashSale_ValidatesSessionAndPreservesSessionOnOrderAndPayments()
    {
        var validCode = UniqueCashRegisterCode();
        var otherCode = UniqueCashRegisterCode();
        _fixture.SetCashRegisterCode(validCode);
        var product = await PrepareProductAsync();
        var requestWithoutSession = BuildSaleRequest(product, _fixture.CashierId, null);

        await Assert.ThrowsAsync<InvalidOrderException>(() => _fixture.Orders.CreateCashSaleAsync(requestWithoutSession));

        var closedSession = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, validCode, 0m, null);
        await _fixture.CashSessions.CloseAsync(closedSession.Id, 0m, null, _fixture.CashierId);
        await Assert.ThrowsAsync<InvalidOrderException>(() =>
            _fixture.Orders.CreateCashSaleAsync(requestWithoutSession with { CashSessionId = closedSession.Id }));

        var otherBoxSession = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, otherCode, 0m, null);
        try
        {
            await Assert.ThrowsAsync<InvalidOrderException>(() =>
                _fixture.Orders.CreateCashSaleAsync(requestWithoutSession with { CashSessionId = otherBoxSession.Id }));
        }
        finally
        {
            _fixture.SetCashRegisterCode(otherCode);
            await _fixture.CashSessions.CloseAsync(otherBoxSession.Id, 0m, null, _fixture.CashierId);
            _fixture.SetCashRegisterCode(validCode);
        }

        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, validCode, 0m, null);
        try
        {
            await Assert.ThrowsAsync<InvalidOrderException>(() =>
                _fixture.Orders.CreateCashSaleAsync(requestWithoutSession with { CashSessionId = session.Id, EmployeeId = _fixture.SecondCashierId }));

            var validRequest = BuildSaleRequest(product, _fixture.CashierId, session.Id);
            var result = await _fixture.Orders.CreateCashSaleAsync(validRequest);
            var retry = await _fixture.Orders.CreateCashSaleAsync(validRequest);
            Assert.Equal(result.OrderId, retry.OrderId);

            await _fixture.CashSessions.CloseAsync(session.Id, validRequest.Payments
                .Where(payment => payment.Method == SalesDomainConstants.PaymentMethods.Cash)
                .Sum(payment => payment.Amount), null, _fixture.CashierId);

            var afterCloseRetry = await _fixture.Orders.CreateCashSaleAsync(validRequest);
            Assert.Equal(result.OrderId, afterCloseRetry.OrderId);

            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var persistedOrder = await db.Orders.SingleAsync(order => order.Id == result.OrderId);
            var persistedPayments = await db.Payments.Where(payment => payment.OrderId == result.OrderId).ToListAsync();
            Assert.Equal(session.Id, persistedOrder.CashSessionId);
            Assert.NotEmpty(persistedPayments);
            Assert.All(persistedPayments, payment => Assert.Equal(session.Id, payment.CashSessionId));
        }
        finally
        {
            await CloseIfOpenAsync(session.Id, validCode, _fixture.CashierId);
        }
    }

    /// <summary>Exige sesión al facturar una orden de confección y la propaga a pagos y orden.</summary>
    [Fact]
    public async Task CompleteConfectionOrder_RequiresSessionAndPersistsItOnOrderAndPayments()
    {
        var cashRegisterCode = UniqueCashRegisterCode();
        _fixture.SetCashRegisterCode(cashRegisterCode);
        var product = await PrepareProductAsync();
        var workOrder = await _fixture.Orders.CreateConfectionOrderAsync(new CreateConfectionOrderRequest(
            _fixture.CashierId,
            null,
            Guid.NewGuid(),
            "Cliente de prueba",
            null,
            new[] { new CashSaleLineRequest(product.ProductId, 1m) },
            "Orden de integración"));
        var payment = new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, product.Total);
        var request = new CompleteConfectionOrderRequest(workOrder.OrderId, _fixture.CashierId, null, new[] { payment });

        await Assert.ThrowsAsync<InvalidOrderException>(() => _fixture.Orders.CompleteConfectionOrderAsync(request));

        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 0m, null);
        try
        {
            var result = await _fixture.Orders.CompleteConfectionOrderAsync(request with { CashSessionId = session.Id });
            await _fixture.CashSessions.CloseAsync(session.Id, product.Total, null, _fixture.CashierId);

            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var persistedOrder = await db.Orders.SingleAsync(order => order.Id == result.OrderId);
            var persistedPayments = await db.Payments.Where(item => item.OrderId == result.OrderId).ToListAsync();
            Assert.Equal(SalesDomainConstants.OrderStatuses.Completed, persistedOrder.Status);
            Assert.Equal(session.Id, persistedOrder.CashSessionId);
            Assert.All(persistedPayments, item => Assert.Equal(session.Id, item.CashSessionId));
        }
        finally
        {
            await CloseIfOpenAsync(session.Id, cashRegisterCode, _fixture.CashierId);
        }
    }

    /// <summary>Prepara stock suficiente para que la venta pruebe también la transacción de inventario.</summary>
    /// <returns>Producto y total calculado por las reglas actuales.</returns>
    private async Task<(Guid ProductId, decimal Total)> PrepareProductAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var product = await db.Products.Include(item => item.MeasurementType).OrderBy(item => item.Id).FirstAsync();
        product.CurrentStock = 100m;
        await db.SaveChangesAsync();
        return (product.Id, TaxAmountCalculator.CalculateGrandTotal(product.SalePrice));
    }

    /// <summary>Construye una venta con pagos mixtos que suman el total calculado.</summary>
    /// <param name="product">Producto y total de la venta.</param>
    /// <param name="employeeId">Empleado que cobra.</param>
    /// <param name="cashSessionId">Sesión propuesta, si existe.</param>
    /// <returns>Solicitud de venta idempotente.</returns>
    private static CreateCashSaleRequest BuildSaleRequest(
        (Guid ProductId, decimal Total) product,
        Guid employeeId,
        Guid? cashSessionId)
    {
        var cashAmount = Math.Round(product.Total / 2m, 2, MidpointRounding.AwayFromZero);
        return new CreateCashSaleRequest(
            employeeId,
            cashSessionId,
            null,
            Guid.NewGuid(),
            new[] { new CashSaleLineRequest(product.ProductId, 1m) },
            new[]
            {
                new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, cashAmount),
                new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Card, product.Total - cashAmount)
            },
            "Venta de integración");
    }

    /// <summary>Genera un código de caja único para el caso.</summary>
    /// <returns>Código normalizado de hasta 50 caracteres.</returns>
    private static string UniqueCashRegisterCode()
    {
        return $"CAJA-O-{Guid.NewGuid():N}"[..20];
    }

    /// <summary>Cierra una sesión solo si permanece abierta durante la limpieza.</summary>
    /// <param name="sessionId">Identificador de sesión.</param>
    /// <param name="cashRegisterCode">Código de caja.</param>
    /// <param name="employeeId">Empleado que limpia el caso.</param>
    /// <returns>Tarea de limpieza.</returns>
    private async Task CloseIfOpenAsync(Guid sessionId, string cashRegisterCode, Guid employeeId)
    {
        var openSession = await _fixture.CashSessions.GetOpenSessionAsync(cashRegisterCode);
        if (openSession?.Id == sessionId)
        {
            _fixture.SetCashRegisterCode(cashRegisterCode);
            await _fixture.CashSessions.CloseAsync(sessionId, 0m, "Limpieza de prueba", employeeId);
        }
    }
}
