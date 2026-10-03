using System.Text.RegularExpressions;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Returns;
using Ferreteria.PuntoVenta.Services.Security;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de integración de devoluciones contra las tablas reales del fixture PostgreSQL.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class ReturnServiceIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa los casos con el fixture compartido.</summary>
    /// <param name="fixture">Contenedor PostgreSQL y servicios de producción.</param>
    public ReturnServiceIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    /// <summary>Verifica que dos devoluciones completas concurrentes solo consumen la cantidad vendida.</summary>
    [Fact]
    public async Task ConcurrentReturns_OnlyOneCanUseAvailableQuantity()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 2m);
        try
        {
            var service = BuildService();
            var first = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 2m);
            var second = first with { ClientRequestId = Guid.NewGuid() };
            var results = await Task.WhenAll(
                ObserveAsync(() => service.CreateReturnAsync(first, "1234")),
                ObserveAsync(() => service.CreateReturnAsync(second, "1234")));

            Assert.Equal(1, results.Count(result => result.Result is not null));
            var failure = Assert.Single(results, result => result.Error is not null).Error;
            var invalid = Assert.IsType<InvalidReturnException>(failure);
            Assert.Contains("supera lo disponible", invalid.Message, StringComparison.OrdinalIgnoreCase);
            await AssertReturnStateAsync(sale, 2m, 1, 2m);
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica que dos devoluciones parciales concurrentes tampoco superan la venta.</summary>
    [Fact]
    public async Task ConcurrentPartialReturns_CannotExceedSoldQuantity()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 2m);
        try
        {
            var service = BuildService();
            var first = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1.5m);
            var second = first with { ClientRequestId = Guid.NewGuid() };
            var results = await Task.WhenAll(
                ObserveAsync(() => service.CreateReturnAsync(first, "1234")),
                ObserveAsync(() => service.CreateReturnAsync(second, "1234")));

            Assert.Equal(1, results.Count(result => result.Result is not null));
            var failure = Assert.Single(results, result => result.Error is not null).Error;
            Assert.Contains("supera lo disponible", Assert.IsType<InvalidReturnException>(failure).Message, StringComparison.OrdinalIgnoreCase);
            await AssertReturnStateAsync(sale, 1.5m, 1, 1.5m);
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica idempotencia secuencial, stock y kardex sin duplicación.</summary>
    [Fact]
    public async Task ExistingClientRequestId_IsIdempotent()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 2m);
        try
        {
            var service = BuildService();
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m);
            var first = await service.CreateReturnAsync(request, "1234");
            var second = await service.CreateReturnAsync(request, "1234");

            Assert.NotEqual(Guid.Empty, first.ReturnId);
            Assert.Equal(first.ReturnId, second.ReturnId);
            await AssertReturnStateAsync(sale, 1m, 1, 1m);
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.Equal(1, await db.Returns.CountAsync(item => item.ClientRequestId == request.ClientRequestId));
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica idempotencia cuando el mismo request llega en paralelo.</summary>
    [Fact]
    public async Task SameClientRequestId_InParallel_CreatesSingleReturn()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 2m);
        try
        {
            var service = BuildService();
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m);
            var results = await Task.WhenAll(
                ObserveAsync(() => service.CreateReturnAsync(request, "1234")),
                ObserveAsync(() => service.CreateReturnAsync(request, "1234")));

            Assert.All(results, result => Assert.Null(result.Error));
            Assert.Equal(results[0].Result?.ReturnId, results[1].Result?.ReturnId);
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.Equal(1, await db.Returns.CountAsync(item => item.ClientRequestId == request.ClientRequestId));
            Assert.Equal(sale.StockAfterSale + 1m, await db.Products.Where(item => item.Id == sale.ProductIds[0]).Select(item => item.CurrentStock).SingleAsync());
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica que el lector y writer reales habilitan la confirmación y exponen stock disponible.</summary>
    [Fact]
    public async Task RealImplementations_AreAuthoritative()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 2m);
        try
        {
            var service = BuildService();
            Assert.True(service.Capabilities.CanConfirmReturns);
            var lines = await service.GetReturnableLinesAsync(sale.OrderId, _fixture.CashierId);
            Assert.Equal(2m, Assert.Single(lines).AvailableQuantity);
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica que una devolución parcial y otra total reconstruyen exactamente la venta.</summary>
    [Fact]
    public async Task PartialThenTotal_MatchesOriginalTotalsExactly()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateThreeLineSaleAsync(code);
        try
        {
            var original = await LoadOrderSnapshotAsync(sale.OrderId);
            Assert.NotEqual(0m, original.TaxAmount);
            var service = BuildService();
            var first = new ReturnRequest(
                Guid.NewGuid(), sale.OrderId, _fixture.CashierId, Guid.Empty, "CAMBIO", "Primera parte",
                ReturnDomainConstants.RefundMethods.None,
                new[] { new ReturnLineRequest(sale.LineIds[0], 1m), new ReturnLineRequest(sale.LineIds[2], 2m) });
            var firstResult = await service.CreateReturnAsync(first, "1234");
            Assert.Equal(ReturnDomainConstants.Types.Partial, firstResult.Calculation.ReturnType);

            var second = new ReturnRequest(
                Guid.NewGuid(), sale.OrderId, _fixture.CashierId, Guid.Empty, "CAMBIO", "Resto",
                ReturnDomainConstants.RefundMethods.None,
                new[]
                {
                    new ReturnLineRequest(sale.LineIds[0], 2m),
                    new ReturnLineRequest(sale.LineIds[1], 1m),
                    new ReturnLineRequest(sale.LineIds[2], 5m)
                });
            var secondResult = await service.CreateReturnAsync(second, "1234");
            Assert.Equal(ReturnDomainConstants.Types.Total, secondResult.Calculation.ReturnType);

            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var returns = await db.Returns.Where(item => item.OrderId == sale.OrderId).ToListAsync();
            Assert.Equal(2, returns.Count);
            Assert.Equal(original.Subtotal, returns.Sum(item => item.Subtotal));
            Assert.Equal(original.DiscountAmount, returns.Sum(item => item.DiscountAmount));
            Assert.Equal(original.TaxAmount, returns.Sum(item => item.TaxAmount));
            Assert.Equal(original.Total, returns.Sum(item => item.Total));
            // Cada línea queda devuelta exactamente por lo vendido: 1 + 2 = 3, 0 + 1 = 1 y 2 + 5 = 7.
            var soldByLine = await db.OrderDetails.AsNoTracking().Where(item => item.OrderId == sale.OrderId)
                .ToDictionaryAsync(item => item.Id, item => item.Quantity);
            var returnIds = returns.Select(item => item.Id).ToArray();
            var returnedByLine = (await db.ReturnDetails.AsNoTracking().Where(item => returnIds.Contains(item.ReturnId)).ToListAsync())
                .GroupBy(item => item.OrderDetailId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            Assert.Equal(new[] { 3m, 1m, 7m }, sale.LineIds.Select(id => soldByLine[id]).ToArray());
            Assert.Equal(new[] { 3m, 1m, 7m }, sale.LineIds.Select(id => returnedByLine[id]).ToArray());
            Assert.Equal(original, await LoadOrderSnapshotAsync(sale.OrderId));
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica devolución en efectivo punta a punta y su impacto en el corte.</summary>
    [Fact]
    public async Task CashReturn_ReducesExpectedCashInSummaryAndClose()
    {
        var code = UniqueCashRegisterCode();
        _fixture.SetCashRegisterCode(code);
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, code, 20m, "Prueba devolución en efectivo");
        var sale = await CreateRealCashSaleAsync(code, 1m, session);
        try
        {
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m) with
            {
                RefundMethod = ReturnDomainConstants.RefundMethods.Cash,
                RefundAmount = sale.Total
            };
            var result = await BuildService().CreateReturnAsync(request, "1234");
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var movement = await db.CashMovements.SingleAsync(item => item.ReturnId == result.ReturnId);
            var persistedReturn = await db.Returns.SingleAsync(item => item.Id == result.ReturnId);
            Assert.Equal(ReturnDomainConstants.CashMovementTypes.CashRefund, movement.MovementType);
            Assert.Equal(request.RefundAmount, movement.Amount);
            Assert.Equal(session.Id, movement.CashSessionId);
            Assert.Equal(session.Id, persistedReturn.CashSessionId);
            Assert.Equal(_fixture.CashierId, movement.EmployeeId);
            Assert.Equal(_fixture.ManagerId, movement.AuthorizedByEmployeeId);

            var summary = await _fixture.CashSessions.GetSummaryAsync(session.Id, _fixture.CashierId);
            Assert.Equal(sale.Total, summary.CashPayments);
            Assert.Equal(request.RefundAmount, summary.CashRefunds);
            Assert.Equal(session.OpeningAmount + sale.Total - request.RefundAmount, summary.ExpectedCash);
            var close = await _fixture.CashSessions.CloseAsync(session.Id, summary.ExpectedCash, "Cierre de prueba", _fixture.CashierId);
            Assert.Equal(summary.ExpectedCash, close.ExpectedCash);
            Assert.Equal(summary.ExpectedCash, await db.CashSessions.Where(item => item.Id == session.Id).Select(item => item.ClosingExpectedAmount).SingleAsync());

            var audit = await db.AuditLogs.Where(item => item.RecordId == result.ReturnId.ToString()).ToListAsync();
            Assert.Contains(audit, item => item.Action == ReturnAuditActions.Return && item.UserId == _fixture.CashierId);
            Assert.Contains(audit, item => item.Action == ReturnAuditActions.Refund && item.UserId == _fixture.CashierId);
            Assert.All(audit.Where(item => item.Action is ReturnAuditActions.Return or ReturnAuditActions.Refund), item =>
                Assert.Contains(_fixture.ManagerId.ToString(), item.NewData ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount);
        }
    }

    /// <summary>Verifica por SQL que una caja no admite dos sesiones abiertas aunque cambien los cajeros.</summary>
    [Fact]
    public async Task OpenByRegisterIndex_RejectsSecondOpenSessionFromAnotherCashier()
    {
        var code = UniqueCashRegisterCode();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        const string insert = """
            INSERT INTO sales."CashSessions"
                ("id", "EmployeeId", "CashRegisterCode", "OpenedAt", "OpeningAmount", "status", "CreatedAt", "UpdatedAt")
            VALUES (@id, @employeeId, @code, CURRENT_TIMESTAMP, 0, 'ABIERTA', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """;
        try
        {
            await ExecuteSessionInsertAsync(connection, insert, firstId, _fixture.CashierId, code);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSessionInsertAsync(connection, insert, secondId, _fixture.SecondCashierId, code));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
            Assert.Equal("IdxCashSessionOpenByRegister", exception.ConstraintName);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("DELETE FROM sales.\"CashSessions\" WHERE \"id\" IN (@firstId, @secondId)", connection);
            cleanup.Parameters.Add(new NpgsqlParameter<Guid>("firstId", firstId));
            cleanup.Parameters.Add(new NpgsqlParameter<Guid>("secondId", secondId));
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Compara columnas, tipos, precisiones, longitudes y nulabilidad de las tres tablas nuevas.</summary>
    [Fact]
    public async Task EfMapping_MatchesSquemaForReturnTables()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var tables = new[] { (typeof(SaleReturn), "Returns"), (typeof(SaleReturnDetail), "ReturnDetails"), (typeof(CashMovement), "CashMovements") };
        foreach (var (clrType, tableName) in tables)
        {
            var entity = db.Model.FindEntityType(clrType) ?? throw new InvalidOperationException($"No hay mapeo EF para {clrType.Name}.");
            var store = StoreObjectIdentifier.Table(tableName, "sales");
            var modelColumns = entity.GetProperties().ToDictionary(
                property => property.GetColumnName(store) ?? throw new InvalidOperationException($"Propiedad sin columna: {property.Name}"),
                property => property);
            var schemaColumns = await LoadSchemaColumnsAsync(tableName);
            Assert.Equal(modelColumns.Keys.OrderBy(item => item), schemaColumns.Keys.OrderBy(item => item));
            foreach (var (columnName, property) in modelColumns)
            {
                var actual = schemaColumns[columnName];
                Assert.Equal(NormalizeEfDataType(property.GetColumnType()), actual.DataType);
                Assert.Equal(property.GetPrecision(), actual.NumericPrecision);
                Assert.Equal(property.GetScale(), actual.NumericScale);
                Assert.Equal(property.GetMaxLength(), actual.CharacterMaximumLength);
                Assert.Equal(property.IsNullable, string.Equals(actual.IsNullable, "YES", StringComparison.Ordinal));
            }
        }

        var checks = await LoadCheckDefinitionsAsync("Returns");
        Assert.Equal(new[] { ReturnDomainConstants.Types.Partial, ReturnDomainConstants.Types.Total }.OrderBy(item => item), ExtractLiterals(checks["ChkReturnsType"]));
        Assert.Equal(new[] { ReturnDomainConstants.Statuses.Voided, ReturnDomainConstants.Statuses.Completed }.OrderBy(item => item), ExtractLiterals(checks["ChkReturnsStatus"]));
        Assert.Equal(new[] { ReturnDomainConstants.FiscalStatuses.Issued, ReturnDomainConstants.FiscalStatuses.NotApplicable, ReturnDomainConstants.FiscalStatuses.Pending, ReturnDomainConstants.FiscalStatuses.RequiresValidation }.OrderBy(item => item), ExtractLiterals(checks["ChkReturnsFiscalStatus"]));
        Assert.Equal(new[] { ReturnDomainConstants.RefundMethods.Cash, ReturnDomainConstants.RefundMethods.None, ReturnDomainConstants.RefundMethods.Card, ReturnDomainConstants.RefundMethods.Transfer }.OrderBy(item => item), ExtractLiterals(checks["ChkReturnsRefundMethod"]));
        var movementChecks = await LoadCheckDefinitionsAsync("CashMovements");
        Assert.Equal(new[] { ReturnDomainConstants.CashMovementTypes.CashRefund, ReturnDomainConstants.CashMovementTypes.Deposit, ReturnDomainConstants.CashMovementTypes.Withdrawal }.OrderBy(item => item), ExtractLiterals(movementChecks["ChkCashMovementsType"]));
    }

    /// <summary>Verifica permisos del ejecutor, autorización por PIN, intentos y ausencia de escrituras rechazadas.</summary>
    [Fact]
    public async Task Authorization_RequiresAdministratorPin()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 2m);
        try
        {
            var attempts = new TestPinAttemptService(TimeProvider.System);
            using var provider = BuildProvider(new EfReturnWriter(), attempts);
            var service = provider.GetRequiredService<IReturnService>();
            var before = await LoadReturnStateSnapshotAsync(sale);

            var cashierPin = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m);
            var cashierException = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(cashierPin, "0000"));
            Assert.Contains("PIN", cashierException.Message, StringComparison.OrdinalIgnoreCase);
            await AssertUnchangedAsync(sale, before);

            var badPin = cashierPin with { ClientRequestId = Guid.NewGuid() };
            var badPinException = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(badPin, "9999"));
            Assert.Contains("PIN", badPinException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(cashierException.Message, badPinException.Message);
            Assert.Equal(2, attempts.GetCurrentStatus().FailedAttempts);
            await AssertUnchangedAsync(sale, before);

            var maria = cashierPin with { ClientRequestId = Guid.NewGuid(), EmployeeId = _fixture.NonCashierId };
            var mariaException = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(maria, "1234"));
            Assert.Contains("empleado", mariaException.Message, StringComparison.OrdinalIgnoreCase);
            await AssertUnchangedAsync(sale, before);

            var mismatch = cashierPin with { ClientRequestId = Guid.NewGuid(), AuthorizedByEmployeeId = _fixture.CashierId };
            var mismatchException = await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(mismatch, "1234"));
            Assert.Contains("no coincide", mismatchException.Message, StringComparison.OrdinalIgnoreCase);
            await AssertUnchangedAsync(sale, before);

            var accepted = cashierPin with { ClientRequestId = Guid.NewGuid() };
            var result = await service.CreateReturnAsync(accepted, "1234");
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var persisted = await db.Returns.SingleAsync(item => item.Id == result.ReturnId);
            Assert.Equal(_fixture.CashierId, persisted.EmployeeId);
            Assert.Equal(_fixture.ManagerId, persisted.AuthorizedByEmployeeId);
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica que reintegros no monetarios nunca crean movimientos de efectivo.</summary>
    /// <param name="refundMethod">Método no efectivo bajo prueba.</param>
    [Theory]
    [InlineData("TARJETA")]
    [InlineData("TRANSFERENCIA")]
    [InlineData("NINGUNO")]
    public async Task NonCashMethods_DoNotCreateCashMovements(string refundMethod)
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 1m);
        try
        {
            var service = BuildService();
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m) with
            {
                RefundMethod = refundMethod,
                RefundAmount = refundMethod == ReturnDomainConstants.RefundMethods.None ? 0m : sale.Total
            };
            var result = await service.CreateReturnAsync(request, "1234");
            Assert.Equal(request.RefundAmount, refundMethod == ReturnDomainConstants.RefundMethods.None ? 0m : result.Calculation.Total);
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            Assert.Equal(0, await db.CashMovements.CountAsync(item => item.ReturnId == result.ReturnId));

            if (refundMethod == ReturnDomainConstants.RefundMethods.None)
            {
                var invalidRequest = request with { ClientRequestId = Guid.NewGuid(), RefundAmount = 1m };
                await Assert.ThrowsAsync<InvalidReturnException>(() => service.CreateReturnAsync(invalidRequest, "1234"));
                Assert.Equal(1, await db.Returns.CountAsync(item => item.OrderId == sale.OrderId));
            }
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica que el efectivo sin sesión abierta se rechaza antes de escribir.</summary>
    [Fact]
    public async Task CashRefundWithoutOpenSession_IsRejectedWithoutRows()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 1m);
        try
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m) with
            {
                RefundMethod = ReturnDomainConstants.RefundMethods.Cash,
                RefundAmount = sale.Total
            };
            var before = await LoadReturnStateSnapshotAsync(sale);
            var pinOkBefore = await CountPinOkEventsAsync();
            var exception = await Assert.ThrowsAsync<InvalidReturnException>(() => BuildService().CreateReturnAsync(request, "1234"));
            Assert.Contains(code, exception.Message, StringComparison.OrdinalIgnoreCase);
            await AssertUnchangedAsync(sale, before);

            // El PIN del autorizador era válido: el lockout persistente registra exactamente un PIN_OK,
            // fuera de la transacción de la devolución (que no dejó filas).
            Assert.Equal(pinOkBefore + 1, await CountPinOkEventsAsync());
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica que una devolución sin reingreso no toca stock ni kardex.</summary>
    [Fact]
    public async Task NoRestock_KeepsStockAndCreatesNoMovement()
    {
        var code = UniqueCashRegisterCode();
        var sale = await CreateRealCashSaleAsync(code, 1m);
        try
        {
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m) with
            {
                Lines = new[] { new ReturnLineRequest(sale.LineIds[0], 1m, false) }
            };
            var result = await BuildService().CreateReturnAsync(request, "1234");
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var detail = await db.ReturnDetails.SingleAsync(item => item.ReturnId == result.ReturnId);
            Assert.False(detail.Restocked);
            Assert.Equal(0m, detail.RestockQuantity);
            Assert.Null(detail.InventoryMovementId);
            Assert.Equal(sale.StockAfterSale, await db.Products.Where(item => item.Id == sale.ProductIds[0]).Select(item => item.CurrentStock).SingleAsync());
            Assert.Equal(0, await db.InventoryMovements.CountAsync(item => item.OrderId == sale.OrderId && item.MovementType == SalesDomainConstants.InventoryMovementTypes.ReturnInflow));
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Verifica rollback completo cuando el writer falla después de persistir en el contexto.</summary>
    [Fact]
    public async Task WriterFailure_RollsBackEverything()
    {
        var code = UniqueCashRegisterCode();
        _fixture.SetCashRegisterCode(code);
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, code, 10m, "Prueba rollback");
        var sale = await CreateRealCashSaleAsync(code, 1m, session);
        try
        {
            var before = await LoadReturnStateSnapshotAsync(sale);
            using var provider = BuildProvider(new ThrowingReturnWriter(new EfReturnWriter()));
            var request = CreateRequest(sale.OrderId, sale.LineIds[0], _fixture.CashierId, 1m) with
            {
                RefundMethod = ReturnDomainConstants.RefundMethods.Cash,
                RefundAmount = sale.Total
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<IReturnService>().CreateReturnAsync(request, "1234"));
            await AssertUnchangedAsync(sale, before);
        }
        finally
        {
            await CloseIfOpenAsync(sale, sale.OpeningAmount + sale.Total);
        }
    }

    /// <summary>Cuenta los eventos PIN_OK del lockout persistente en <c>system.AuditLog</c>.</summary>
    /// <returns>Cantidad de eventos de PIN correcto registrados.</returns>
    private async Task<int> CountPinOkEventsAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.AuditLogs.CountAsync(item =>
            item.TableName == SalesDomainConstants.PinAuditActions.TableName
            && item.Action == SalesDomainConstants.PinAuditActions.PinOk);
    }
    /// <summary>Construye el servicio compartido por el fixture, con las implementaciones reales.</summary>
    /// <returns>Servicio de devoluciones configurado contra PostgreSQL.</returns>
    private IReturnService BuildService() => _fixture.Services.GetRequiredService<IReturnService>();

    /// <summary>Crea un proveedor aislado para sustituir el writer o el estado de intentos de PIN.</summary>
    /// <param name="writer">Writer que usará el servicio.</param>
    /// <param name="pinAttempts">Estado de intentos que se desea observar.</param>
    /// <returns>Proveedor desechable con la composición de producción.</returns>
    private ServiceProvider BuildProvider(IReturnWriter writer, IPinAttemptService? pinAttempts = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IOptions<ReturnOptions>>(Options.Create(ReturnOptions.CreateDefault()));
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = CurrentCashRegisterCode());
        services.AddOptions<PinLockoutOptions>();
        services.AddOptions<SalesHistoryOptions>().Configure(options => options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.AddSingleton<IReturnedQuantityReader, ReturnDetailsReturnedQuantityReader>();
        services.AddSingleton(writer);
        services.AddSingleton<IReturnFiscalPolicy, DefaultReturnFiscalPolicy>();
        services.AddSingleton<IPinAttemptService>(pinAttempts ?? new TestPinAttemptService(TimeProvider.System));
        services.AddSingleton<PinAuthService>();
        services.AddSingleton<IAuthorizationGuard, TestAuthorizationGuard>();
        services.AddSingleton<IReturnService, ReturnService>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ReturnService>>(NullLogger<ReturnService>.Instance);
        return services.BuildServiceProvider();
    }

    /// <summary>Crea una venta real cobrada en efectivo y conserva el delta de stock posterior a la venta.</summary>
    /// <param name="cashRegisterCode">Caja única del caso.</param>
    /// <param name="quantity">Cantidad vendida.</param>
    /// <param name="session">Sesión ya abierta o null para crearla.</param>
    /// <returns>Datos congelados de la venta y sus líneas.</returns>
    private async Task<TestSale> CreateRealCashSaleAsync(string cashRegisterCode, decimal quantity, CashSession? session = null)
    {
        _fixture.SetCashRegisterCode(cashRegisterCode);
        var ownSession = session ?? await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 10m, "Venta de devolución");
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var product = (await TestDataFactory.CreateProductsAsync(db, 1, 20m)).Single();
        var stockBeforeSale = product.CurrentStock;
        var subtotal = Math.Round(product.SalePrice * quantity, 2, MidpointRounding.AwayFromZero);
        var result = await _fixture.Orders.CreateCashSaleAsync(new CreateCashSaleRequest(
            _fixture.CashierId, ownSession.Id, null, Guid.NewGuid(),
            new[] { new CashSaleLineRequest(product.Id, quantity) },
            new[] { new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, TaxAmountCalculator.CalculateGrandTotal(subtotal)) },
            "Venta real para devolución"));
        var order = await db.Orders.AsNoTracking().Include(item => item.OrderDetails).SingleAsync(item => item.Id == result.OrderId);
        var stockAfterSale = await db.Products.Where(item => item.Id == product.Id).Select(item => item.CurrentStock).SingleAsync();
        return new TestSale(result.OrderId, new[] { order.OrderDetails.Single().Id }, new[] { product.Id }, new[] { product.CostPrice }, new[] { stockAfterSale }, ownSession.Id, cashRegisterCode, ownSession.OpeningAmount, result.Total);
    }

    /// <summary>Crea una venta real con tres precios y cantidades que fuerzan redondeos de IVA.</summary>
    /// <param name="cashRegisterCode">Caja única del caso.</param>
    /// <returns>Venta real con sus tres líneas.</returns>
    private async Task<TestSale> CreateThreeLineSaleAsync(string cashRegisterCode)
    {
        _fixture.SetCashRegisterCode(cashRegisterCode);
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 10m, "Venta de redondeo");
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var products = await TestDataFactory.CreateProductsAsync(
            db,
            3,
            20m,
            new[] { 3.33m, 7.77m, 1.01m });
        var originalPrices = products.Select(item => item.SalePrice).ToArray();
        // Las cantidades se aplican por índice de creación; LineIds y ProductIds conservan ese mismo índice.
        var quantities = new[] { 3m, 1m, 7m };
        var subtotal = quantities.Select((quantity, index) => Math.Round(quantity * originalPrices[index], 2, MidpointRounding.AwayFromZero)).Sum();
        var result = await _fixture.Orders.CreateCashSaleAsync(new CreateCashSaleRequest(
            _fixture.CashierId, session.Id, null, Guid.NewGuid(),
            products.Select((product, index) => new CashSaleLineRequest(product.Id, quantities[index])).ToArray(),
            new[] { new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, TaxAmountCalculator.CalculateGrandTotal(subtotal)) },
            "Venta con redondeo para devolución"));
        var order = await db.Orders.AsNoTracking().Include(item => item.OrderDetails).SingleAsync(item => item.Id == result.OrderId);
        var detailsByProduct = order.OrderDetails.ToDictionary(item => item.ProductId);
        var productIds = products.Select(product => product.Id).ToArray();
        var stockById = await db.Products.AsNoTracking().Where(item => productIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.CurrentStock);
        return new TestSale(
            result.OrderId,
            products.Select(product => detailsByProduct[product.Id].Id).ToArray(),
            productIds,
            products.Select(item => item.CostPrice).ToArray(),
            products.Select(product => stockById[product.Id]).ToArray(),
            session.Id,
            cashRegisterCode,
            session.OpeningAmount,
            result.Total);
    }

    /// <summary>Construye una solicitud de devolución de una sola línea.</summary>
    /// <param name="orderId">Orden original.</param>
    /// <param name="lineId">Línea original.</param>
    /// <param name="employeeId">Empleado ejecutor.</param>
    /// <param name="quantity">Cantidad solicitada.</param>
    /// <returns>Solicitud con reintegro NINGUNO.</returns>
    private static ReturnRequest CreateRequest(Guid orderId, Guid lineId, Guid employeeId, decimal quantity) => new(
        Guid.NewGuid(), orderId, employeeId, Guid.Empty, "CAMBIO", null,
        ReturnDomainConstants.RefundMethods.None, new[] { new ReturnLineRequest(lineId, quantity) });

    /// <summary>Compara cantidad devuelta, stock, kardex y estado de la orden.</summary>
    /// <param name="sale">Venta bajo prueba.</param>
    /// <param name="quantity">Cantidad que debe haberse devuelto.</param>
    /// <param name="movementCount">Cantidad esperada de movimientos de entrada de devolución.</param>
    /// <param name="stockIncrease">Delta esperado de stock.</param>
    private async Task AssertReturnStateAsync(TestSale sale, decimal quantity, int movementCount, decimal stockIncrease)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        Assert.Equal(quantity, await db.ReturnDetails.Where(item => item.OrderDetailId == sale.LineIds[0]).SumAsync(item => item.Quantity));
        var movements = await db.InventoryMovements.Where(item => item.OrderId == sale.OrderId && item.MovementType == SalesDomainConstants.InventoryMovementTypes.ReturnInflow).ToListAsync();
        Assert.Equal(movementCount, movements.Count);
        Assert.All(movements, movement => Assert.Equal(sale.UnitCosts[0], movement.UnitCost));
        Assert.Equal(quantity, movements.Sum(movement => movement.Quantity));
        Assert.Equal(sale.StockAfterSale + stockIncrease, await db.Products.Where(item => item.Id == sale.ProductIds[0]).Select(item => item.CurrentStock).SingleAsync());
        Assert.Equal(SalesDomainConstants.OrderStatuses.Completed, await db.Orders.Where(item => item.Id == sale.OrderId).Select(item => item.Status).SingleAsync());
    }

    /// <summary>Carga el estado de orden usado para comprobar que una devolución no la modifica.</summary>
    /// <param name="orderId">Orden original.</param>
    /// <returns>Campos inmutables de la orden.</returns>
    private async Task<OrderSnapshot> LoadOrderSnapshotAsync(Guid orderId)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Orders.Where(item => item.Id == orderId).Select(item => new OrderSnapshot(item.Status, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.UpdatedAt)).SingleAsync();
    }

    /// <summary>Cuenta filas y captura stock para verificar ausencia de escritura.</summary>
    /// <param name="sale">Venta bajo prueba.</param>
    /// <returns>Conteo de entidades afectables y stock actual.</returns>
    private async Task<ReturnStateSnapshot> LoadReturnStateSnapshotAsync(TestSale sale)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return new ReturnStateSnapshot(
            await db.Returns.CountAsync(item => item.OrderId == sale.OrderId),
            await db.ReturnDetails.CountAsync(item => item.OrderDetailId == sale.LineIds[0]),
            await db.CashMovements.CountAsync(item => item.CashSessionId == sale.SessionId),
            await db.AuditLogs.CountAsync(item => item.TableName != SalesDomainConstants.PinAuditActions.TableName),
            await db.InventoryMovements.CountAsync(item => item.OrderId == sale.OrderId && item.MovementType == SalesDomainConstants.InventoryMovementTypes.ReturnInflow),
            await db.Products.Where(item => item.Id == sale.ProductIds[0]).Select(item => item.CurrentStock).SingleAsync());
    }

    /// <summary>Comprueba que ninguna entidad ni el stock cambiaron.</summary>
    /// <param name="sale">Venta bajo prueba.</param>
    /// <param name="before">Estado capturado antes de la operación.</param>
    private async Task AssertUnchangedAsync(TestSale sale, ReturnStateSnapshot before)
    {
        Assert.Equal(before, await LoadReturnStateSnapshotAsync(sale));
    }

    /// <summary>Cierra una sesión que siga abierta después de un caso.</summary>
    /// <param name="sale">Venta y sesión del caso.</param>
    /// <param name="declaredCash">Efectivo esperado que se declara para evitar diferencias artificiales.</param>
    private async Task CloseIfOpenAsync(TestSale sale, decimal declaredCash)
    {
        var openSession = await _fixture.CashSessions.GetOpenSessionAsync(sale.CashRegisterCode);
        if (openSession?.Id != sale.SessionId)
        {
            return;
        }

        _fixture.SetCashRegisterCode(sale.CashRegisterCode);
        await _fixture.CashSessions.CloseAsync(sale.SessionId, declaredCash, "Limpieza de prueba", _fixture.CashierId);
    }

    /// <summary>Ejecuta una inserción SQL parametrizada de sesión.</summary>
    /// <param name="connection">Conexión PostgreSQL abierta.</param>
    /// <param name="sql">Sentencia de inserción.</param>
    /// <param name="sessionId">Id de sesión.</param>
    /// <param name="employeeId">Cajero propietario.</param>
    /// <param name="cashRegisterCode">Código único de caja.</param>
    private static async Task ExecuteSessionInsertAsync(NpgsqlConnection connection, string sql, Guid sessionId, Guid employeeId, string cashRegisterCode)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter<Guid>("id", sessionId));
        command.Parameters.Add(new NpgsqlParameter<Guid>("employeeId", employeeId));
        command.Parameters.Add(new NpgsqlParameter<string>("code", cashRegisterCode));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Lee la definición de los CHECK de una tabla.</summary>
    /// <param name="tableName">Tabla del esquema sales.</param>
    /// <returns>Definiciones indexadas por nombre.</returns>
    private async Task<Dictionary<string, string>> LoadCheckDefinitionsAsync(string tableName)
    {
        const string sql = """
            SELECT c.conname, pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'sales' AND t.relname = @tableName AND c.contype = 'c'
            """;
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter<string>("tableName", tableName));
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }

    /// <summary>Lee columnas de information_schema para una tabla.</summary>
    /// <param name="tableName">Tabla del esquema sales.</param>
    /// <returns>Columnas indexadas por nombre.</returns>
    private async Task<Dictionary<string, SchemaColumn>> LoadSchemaColumnsAsync(string tableName)
    {
        const string sql = """
            SELECT column_name, data_type, numeric_precision, numeric_scale,
                   character_maximum_length, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'sales' AND table_name = @tableName
            """;
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter<string>("tableName", tableName));
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<string, SchemaColumn>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = new SchemaColumn(
                reader.GetString(1),
                reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2)),
                reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                reader.GetString(5));
        }

        return result;
    }

    /// <summary>Normaliza el tipo EF para compararlo con information_schema.</summary>
    /// <param name="columnType">Tipo declarado en el modelo EF.</param>
    /// <returns>Tipo base de PostgreSQL.</returns>
    private static string NormalizeEfDataType(string? columnType)
    {
        var normalized = columnType?.Trim().ToLowerInvariant() ?? string.Empty;
        var baseType = normalized.Split('(', 2)[0].Trim();
        if (baseType == "numeric")
        {
            return "numeric";
        }

        return baseType switch
        {
            "timestamptz" => "timestamp with time zone",
            "varchar" => "character varying",
            _ => baseType
        };
    }

    /// <summary>Extrae y ordena literales de una definición CHECK.</summary>
    /// <param name="definition">Definición PostgreSQL del CHECK.</param>
    /// <returns>Literales únicos ordenados.</returns>
    private static string[] ExtractLiterals(string definition) => Regex.Matches(definition, "'([^']*)'")
        .Select(match => match.Groups[1].Value)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Genera un código de caja válido y único para un caso.</summary>
    /// <returns>Código de caja de longitud menor a 50.</returns>
    private static string UniqueCashRegisterCode() => $"CAJA-R-{Guid.NewGuid():N}"[..24];

    /// <summary>Devuelve el código actualmente configurado en el fixture.</summary>
    /// <returns>Código vigente de caja.</returns>
    private string CurrentCashRegisterCode() => _fixture.Services.GetRequiredService<IOptions<CashRegisterOptions>>().Value.Codigo;

    /// <summary>Observa una tarea conservando el resultado o la excepción para asserts de concurrencia.</summary>
    /// <param name="action">Operación a observar.</param>
    /// <returns>Resultado de la operación.</returns>
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

    private sealed record TestSale(
        Guid OrderId,
        IReadOnlyList<Guid> LineIds,
        IReadOnlyList<Guid> ProductIds,
        IReadOnlyList<decimal> UnitCosts,
        IReadOnlyList<decimal> StockAfterSaleByProduct,
        Guid SessionId,
        string CashRegisterCode,
        decimal OpeningAmount,
        decimal Total)
    {
        /// <summary>Stock del primer producto después de la venta.</summary>
        public decimal StockAfterSale => StockAfterSaleByProduct[0];
    }

    private sealed record OrderSnapshot(string Status, decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal Total, DateTime UpdatedAt);

    private sealed record ReturnStateSnapshot(int Returns, int Details, int CashMovements, int Audits, int InventoryMovements, decimal Stock);

    private sealed record SchemaColumn(string DataType, int? NumericPrecision, int? NumericScale, int? CharacterMaximumLength, string IsNullable);

    /// <summary>Writer de prueba que falla después del writer real para forzar rollback.</summary>
    private sealed class ThrowingReturnWriter : IReturnWriter
    {
        private readonly IReturnWriter _inner;

        /// <summary>Inicializa el decorador con un writer real.</summary>
        /// <param name="inner">Writer real que ejecutará la persistencia.</param>
        public ThrowingReturnWriter(IReturnWriter inner) => _inner = inner;

        /// <inheritdoc />
        public bool IsAvailable => _inner.IsAvailable;

        /// <inheritdoc />
        public Task<ReturnResult?> FindByClientRequestIdAsync(FerreteriaDbContext db, Guid clientRequestId, CancellationToken cancellationToken = default) => _inner.FindByClientRequestIdAsync(db, clientRequestId, cancellationToken);

        /// <inheritdoc />
        public async Task<Guid> PersistAsync(FerreteriaDbContext db, ReturnPersistenceRecord record, CancellationToken cancellationToken = default)
        {
            var returnId = await _inner.PersistAsync(db, record, cancellationToken);
            throw new InvalidOperationException($"Fallo controlado después de persistir {returnId}.");
        }
    }
}
