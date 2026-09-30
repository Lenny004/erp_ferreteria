using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Returns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Compara el resumen del servicio con una consulta SQL parametrizada de control.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class CashSessionSummaryIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa el caso con el fixture PostgreSQL compartido.</summary>
    /// <param name="fixture">Contenedor y servicios de integración.</param>
    public CashSessionSummaryIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Verifica campo a campo el resumen para ventas completadas, canceladas y pendientes.</summary>
    [Fact]
    public async Task GetSummaryAsync_MatchesParameterizedSqlControlQuery()
    {
        var cashRegisterCode = $"CAJA-S-{Guid.NewGuid():N}"[..20];
        _fixture.SetCashRegisterCode(cashRegisterCode);
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 5m, null);
        var completedCash = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime, SalesDomainConstants.OrderStatuses.Completed, 10m);
        var completedCard = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime.AddMinutes(1), SalesDomainConstants.OrderStatuses.Completed, 20m);
        var completedTransfer = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime.AddMinutes(2), SalesDomainConstants.OrderStatuses.Completed, 30m);
        var cancelled = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime.AddMinutes(3), SalesDomainConstants.OrderStatuses.Cancelled, 40m);
        var pending = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime.AddMinutes(4), SalesDomainConstants.OrderStatuses.Pending, 50m);
        foreach (var order in new[] { completedCash, completedCard, completedTransfer, cancelled, pending })
        {
            order.CashSessionId = session.Id;
        }

        var dteProcessed = PostgreSqlFixture.NewDte(completedCard.Id, "01", DteConstants.EstadosMh.Procesado, "SUM-PROCESADO");
        var dteContingency = PostgreSqlFixture.NewDte(completedTransfer.Id, "01", DteConstants.EstadosMh.Contingencia, "SUM-CONTINGENCIA");
        await _fixture.SeedAsync(db =>
        {
            db.Orders.AddRange(completedCash, completedCard, completedTransfer, cancelled, pending);
            db.Payments.AddRange(
                new Payment { OrderId = completedCash.Id, CashSessionId = session.Id, Method = SalesDomainConstants.PaymentMethods.Cash, Amount = 10m },
                new Payment { OrderId = completedCard.Id, CashSessionId = session.Id, Method = SalesDomainConstants.PaymentMethods.Card, Amount = 20m },
                new Payment { OrderId = completedTransfer.Id, CashSessionId = session.Id, Method = SalesDomainConstants.PaymentMethods.Transfer, Amount = 30m });
            db.DteIssued.AddRange(dteProcessed, dteContingency);
        });

        try
        {
            var summary = await _fixture.CashSessions.GetSummaryAsync(session.Id, _fixture.CashierId);
            var sql = await ExecuteControlQueryAsync(session.Id);

            Assert.Equal(sql.OpeningAmount, summary.OpeningAmount);
            Assert.Equal(sql.CashPayments, summary.CashPayments);
            Assert.Equal(sql.CardPayments, summary.CardPayments);
            Assert.Equal(sql.TransferPayments, summary.TransferPayments);
            Assert.Equal(sql.OtherPayments, summary.OtherPayments);
            Assert.Equal(sql.TotalSold, summary.TotalSold);
            Assert.Equal(sql.TaxAmount, summary.TaxAmount);
            Assert.Equal(sql.CompletedSales, summary.CompletedSales);
            Assert.Equal(sql.PendingSales, summary.PendingSales);
            Assert.Equal(sql.CancelledSales, summary.CancelledSales);
            Assert.Equal(sql.DteCount, summary.DteCount);
            Assert.Equal(sql.ContingencyDteCount, summary.ContingencyDteCount);
            Assert.Equal(sql.SalesWithoutDte, summary.SalesWithoutDte);
            Assert.Equal(sql.CashRefunds, summary.CashRefunds);
            Assert.Equal(sql.ExpectedCash, summary.ExpectedCash);
        }
        finally
        {
            await _fixture.CashSessions.CloseAsync(session.Id, 15m, "Limpieza de prueba", _fixture.CashierId);
        }
    }

    /// <summary>Verifica que el servicio usa el lector inyectado de devoluciones en efectivo.</summary>
    [Fact]
    public async Task GetSummaryAsync_UsesInjectedCashMovementReader()
    {
        var cashRegisterCode = $"CAJA-R-{Guid.NewGuid():N}"[..20];
        _fixture.SetCashRegisterCode(cashRegisterCode);
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 5m, null);
        try
        {
            var order = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Now.UtcDateTime, SalesDomainConstants.OrderStatuses.Completed, 12.50m);
            var saleReturn = new SaleReturn
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                CashSessionId = session.Id,
                EmployeeId = _fixture.CashierId,
                AuthorizedByEmployeeId = _fixture.ManagerId,
                ClientRequestId = Guid.NewGuid(),
                ReturnType = ReturnDomainConstants.Types.Partial,
                Status = ReturnDomainConstants.Statuses.Completed,
                FiscalStatus = ReturnDomainConstants.FiscalStatuses.RequiresValidation,
                ReasonCode = "CAMBIO",
                Subtotal = 12.50m,
                Total = 12.50m,
                RefundMethod = ReturnDomainConstants.RefundMethods.Cash,
                RefundAmount = 12.50m
            };
            await _fixture.SeedAsync(db =>
            {
                db.Orders.Add(order);
                db.Returns.Add(saleReturn);
                db.CashMovements.Add(new CashMovement
                {
                    Id = Guid.NewGuid(),
                    CashSessionId = session.Id,
                    MovementType = ReturnDomainConstants.CashMovementTypes.CashRefund,
                    Amount = 12.50m,
                    ReturnId = saleReturn.Id,
                    EmployeeId = _fixture.CashierId,
                    AuthorizedByEmployeeId = _fixture.ManagerId,
                    ClientRequestId = Guid.NewGuid(),
                    Reason = "Devolución de prueba"
                });
            });
            var summary = await _fixture.CashSessions.GetSummaryAsync(session.Id, _fixture.CashierId);

            Assert.Equal(12.50m, summary.CashRefunds);
            Assert.Equal(-7.50m, summary.ExpectedCash);
        }
        finally
        {
            await _fixture.CashSessions.CloseAsync(session.Id, 5m, "Limpieza de prueba", _fixture.CashierId);
        }
    }


    /// <summary>Ejecuta la consulta de control con un parámetro UUID sin concatenar valores.</summary>
    /// <param name="sessionId">Sesión cuyo resumen se consulta.</param>
    /// <returns>Campos agregados de control.</returns>
    private async Task<ControlSummary> ExecuteControlQueryAsync(Guid sessionId)
    {
        const string query = """
            WITH session_orders AS (
                SELECT o."id", o."status", o."total", o."TaxAmount"
                FROM sales."Orders" o
                WHERE o."CashSessionId" = @sessionId
            ), completed AS (
                SELECT * FROM session_orders WHERE "status" = 'COMPLETADA'
            ), payment_totals AS (
                SELECT p."method", COALESCE(SUM(p."amount"), 0) AS amount
                FROM sales."Payments" p
                JOIN completed o ON o."id" = p."OrderId"
                WHERE p."CashSessionId" = @sessionId
                GROUP BY p."method"
            ), latest_dte AS (
                SELECT DISTINCT ON (d."OrderId") d."OrderId", d."MhStatus"
                FROM dte."DteIssued" d
                JOIN completed o ON o."id" = d."OrderId"
                ORDER BY d."OrderId", d."IssuedAt" DESC
            )
            SELECT
                s."OpeningAmount",
                COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0),
                COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'TARJETA'), 0),
                COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'TRANSFERENCIA'), 0),
                COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'OTRO'), 0),
                COALESCE((SELECT SUM("total") FROM completed), 0),
                COALESCE((SELECT SUM("TaxAmount") FROM completed), 0),
                (SELECT COUNT(*) FROM completed),
                (SELECT COUNT(*) FROM session_orders WHERE "status" = 'PENDIENTE'),
                (SELECT COUNT(*) FROM session_orders WHERE "status" = 'CANCELADA'),
                (SELECT COUNT(*) FROM latest_dte WHERE "MhStatus" IS NOT NULL),
                (SELECT COUNT(*) FROM latest_dte WHERE "MhStatus" = 'CONTINGENCIA'),
                (SELECT COUNT(*) FROM completed c LEFT JOIN latest_dte d ON d."OrderId" = c."id" WHERE d."OrderId" IS NULL),
                COALESCE((SELECT SUM(m."amount") FROM sales."CashMovements" m WHERE m."CashSessionId" = @sessionId AND m."MovementType" = 'DEVOLUCION_EFECTIVO'), 0),
                s."OpeningAmount" + COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0)
                    - COALESCE((SELECT SUM(m."amount") FROM sales."CashMovements" m WHERE m."CashSessionId" = @sessionId AND m."MovementType" = 'DEVOLUCION_EFECTIVO'), 0)
            FROM sales."CashSessions" s
            WHERE s."id" = @sessionId;
            """;

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.Add(new NpgsqlParameter<Guid>("sessionId", NpgsqlDbType.Uuid) { Value = sessionId });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "La consulta SQL de control debe devolver la sesión.");
        return new ControlSummary(
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            Convert.ToInt32(reader.GetInt64(7)),
            Convert.ToInt32(reader.GetInt64(8)),
            Convert.ToInt32(reader.GetInt64(9)),
            Convert.ToInt32(reader.GetInt64(10)),
            Convert.ToInt32(reader.GetInt64(11)),
            Convert.ToInt32(reader.GetInt64(12)),
            reader.GetDecimal(13),
            reader.GetDecimal(14));
    }

    /// <summary>Campos que devuelve la consulta SQL de control.</summary>
    private sealed record ControlSummary(
        decimal OpeningAmount,
        decimal CashPayments,
        decimal CardPayments,
        decimal TransferPayments,
        decimal OtherPayments,
        decimal TotalSold,
        decimal TaxAmount,
        int CompletedSales,
        int PendingSales,
        int CancelledSales,
        int DteCount,
        int ContingencyDteCount,
        int SalesWithoutDte,
        decimal CashRefunds,
        decimal ExpectedCash);
}
