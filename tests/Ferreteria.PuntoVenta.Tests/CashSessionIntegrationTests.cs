using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de integración del ciclo transaccional de apertura, resumen y cierre.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class CashSessionIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    /// <summary>Inicializa los casos con el fixture PostgreSQL compartido.</summary>
    /// <param name="fixture">Contenedor y servicios transaccionales compartidos.</param>
    public CashSessionIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Verifica autorización, resumen, concurrencia de cierre y auditoría atómica.</summary>
    /// <remarks>
    /// El administrador cierra un turno perteneciente al cajero porque su puesto está en la misma lista
    /// de acceso completo del historial. La apertura y el cierre escriben la auditoría antes del commit.
    /// </remarks>
    [Fact]
    public async Task CashSessionLifecycle_EnforcesAccessAndAuditsTransitions()
    {
        var cashSessions = _fixture.CashSessions;
        var cashRegisterCode = UniqueCashRegisterCode();
        CashSession? session = null;
        try
        {
            session = await cashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 25m, "Prueba de integración");

            var duplicate = await Assert.ThrowsAsync<CashSessionException>(() =>
                cashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 25m, null));
            Assert.Contains("abierta", duplicate.Message, StringComparison.OrdinalIgnoreCase);

            var unauthorizedSummary = await Assert.ThrowsAsync<CashSessionException>(() =>
                cashSessions.GetSummaryAsync(session.Id, _fixture.NonCashierId));
            Assert.Contains("autorizado", unauthorizedSummary.Message, StringComparison.OrdinalIgnoreCase);

            var summary = await cashSessions.GetSummaryAsync(session.Id, _fixture.CashierId);
            Assert.Equal(25m, summary.OpeningAmount);
            Assert.Equal(25m, summary.ExpectedCash);

            var closeResults = await Task.WhenAll(
                ObserveCompletionAsync(cashSessions.CloseAsync(session.Id, 25m, null, _fixture.ManagerId)),
                ObserveCompletionAsync(cashSessions.CloseAsync(session.Id, 25m, null, _fixture.ManagerId)));
            Assert.Equal(1, closeResults.Count(result => result));
            Assert.Equal(1, closeResults.Count(result => !result));

            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var persisted = await db.CashSessions.SingleAsync(item => item.Id == session.Id);
            Assert.Equal(SalesDomainConstants.CashSessionStatuses.Closed, persisted.Status);
            Assert.Equal(25m, persisted.ClosingExpectedAmount);
            Assert.Equal(25m, persisted.ClosingDeclaredAmount);

            var auditActions = await db.AuditLogs
                .Where(item => item.RecordId == session.Id.ToString())
                .OrderBy(item => item.CreatedAt)
                .Select(item => item.Action)
                .ToListAsync();
            Assert.Contains(CashSessionAuditActions.Open, auditActions);
            Assert.Contains(CashSessionAuditActions.Close, auditActions);
            Assert.All(auditActions, action => Assert.InRange(action.Length, 1, 10));
        }
        finally
        {
            if (session is not null)
            {
                var openSession = await cashSessions.GetOpenSessionAsync(cashRegisterCode);
                if (openSession?.Id == session.Id)
                {
                    try
                    {
                        await cashSessions.CloseAsync(session.Id, 25m, "Limpieza de prueba", _fixture.ManagerId);
                    }
                    catch (CashSessionException)
                    {
                    }
                }
            }
        }
    }

    /// <summary>Verifica que un empleado inactivo no pueda abrir, consultar ni cerrar un turno.</summary>
    [Fact]
    public async Task InactiveEmployee_IsRejectedByAllCashSessionOperations()
    {
        var cashSessions = _fixture.CashSessions;
        var cashRegisterCode = UniqueCashRegisterCode();
        CashSession? session = null;
        try
        {
            session = await cashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 10m, null);
            await SetEmployeeActiveAsync(_fixture.CashierId, false);

            await Assert.ThrowsAsync<CashSessionException>(() =>
                cashSessions.GetSummaryAsync(session.Id, _fixture.CashierId));
            await Assert.ThrowsAsync<CashSessionException>(() =>
                cashSessions.CloseAsync(session.Id, 10m, null, _fixture.CashierId));
        }
        finally
        {
            await SetEmployeeActiveAsync(_fixture.CashierId, true);
            if (session is not null)
            {
                var openSession = await cashSessions.GetOpenSessionAsync(cashRegisterCode);
                if (openSession?.Id == session.Id)
                {
                    await cashSessions.CloseAsync(session.Id, 10m, "Limpieza de prueba", _fixture.ManagerId);
                }
            }
        }

        await SetEmployeeActiveAsync(_fixture.CashierId, false);
        try
        {
            await Assert.ThrowsAsync<CashSessionException>(() =>
                cashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 10m, null));
        }
        finally
        {
            await SetEmployeeActiveAsync(_fixture.CashierId, true);
        }
    }

    /// <summary>Convierte una operación de cierre en un resultado booleano para probar la carrera.</summary>
    /// <param name="operation">Operación que debe completar sin propagar su error esperado.</param>
    /// <returns><c>true</c> si la operación ganó el cierre; de lo contrario, <c>false</c>.</returns>
    private static async Task<bool> ObserveCompletionAsync(Task<CashSessionCloseResult> operation)
    {
        try
        {
            await operation;
            return true;
        }
        catch (CashSessionException)
        {
            return false;
        }
    }

    /// <summary>Actualiza únicamente el estado activo de un empleado semilla para un caso de integración.</summary>
    /// <param name="employeeId">Empleado que se modifica.</param>
    /// <param name="isActive">Nuevo estado operativo.</param>
    /// <returns>Tarea de persistencia.</returns>
    private async Task SetEmployeeActiveAsync(Guid employeeId, bool isActive)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var employee = await db.Employees.SingleAsync(item => item.Id == employeeId);
        employee.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    /// <summary>Otro cajero no puede abrir una caja mientras existe un turno abierto ajeno.</summary>
    [Fact]
    public async Task DifferentCashier_CannotOpenSameRegisterWhileItIsOpen()
    {
        var cashRegisterCode = UniqueCashRegisterCode();
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 15m, null);
        try
        {
            var exception = await Assert.ThrowsAsync<CashSessionException>(() =>
                _fixture.CashSessions.OpenAsync(_fixture.SecondCashierId, cashRegisterCode, 15m, null));
            Assert.Contains("Ya hay una caja abierta", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CloseIfOpenAsync(session.Id, cashRegisterCode, _fixture.CashierId);
        }
    }

    /// <summary>Dos aperturas simultáneas del mismo empleado dejan exactamente un ganador controlado.</summary>
    [Fact]
    public async Task ConcurrentOpenings_BySameCashier_HaveOneControlledWinner()
    {
        var cashRegisterCode = UniqueCashRegisterCode();
        var results = await Task.WhenAll(
            ObserveOpenAsync(_fixture.CashierId, cashRegisterCode),
            ObserveOpenAsync(_fixture.CashierId, cashRegisterCode));

        Assert.Equal(1, results.Count(result => result is not null));
        Assert.Equal(1, results.Count(result => result is null));

        var winner = results.Single(result => result is not null);
        if (winner is not null)
        {
            await CloseIfOpenAsync(winner.Id, cashRegisterCode, _fixture.CashierId);
        }
    }

    /// <summary>Un cajero ajeno no puede cerrar; Administrador sí puede cerrar el turno ajeno.</summary>
    [Fact]
    public async Task ForeignCashierCannotClose_ButManagerCanClose()
    {
        var cashRegisterCode = UniqueCashRegisterCode();
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 15m, null);
        try
        {
            var exception = await Assert.ThrowsAsync<CashSessionException>(() =>
                _fixture.CashSessions.CloseAsync(session.Id, 15m, null, _fixture.SecondCashierId));
            Assert.Contains("autorizado", exception.Message, StringComparison.OrdinalIgnoreCase);
            var result = await _fixture.CashSessions.CloseAsync(session.Id, 15m, null, _fixture.ManagerId);
            Assert.Equal(session.Id, result.SessionId);
        }
        finally
        {
            await CloseIfOpenAsync(session.Id, cashRegisterCode, _fixture.ManagerId);
        }
    }

    /// <summary>Exige observación sobre el umbral y persiste la diferencia con signo declarado menos esperado.</summary>
    [Fact]
    public async Task CloseOverThreshold_RequiresObservationAndPersistsSignedDifference()
    {
        var cashRegisterCode = UniqueCashRegisterCode();
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, cashRegisterCode, 10m, null);
        try
        {
            await Assert.ThrowsAsync<CashSessionException>(() =>
                _fixture.CashSessions.CloseAsync(session.Id, 20m, null, _fixture.CashierId));

            var result = await _fixture.CashSessions.CloseAsync(
                session.Id,
                20m,
                "Sobrante de prueba",
                _fixture.CashierId);
            Assert.Equal(10m, result.ExpectedCash);
            Assert.Equal(10m, result.Difference);
            Assert.Equal(FixtureNow(), result.ClosedAtUtc);
        }
        finally
        {
            await CloseIfOpenAsync(session.Id, cashRegisterCode, _fixture.ManagerId);
        }
    }

    /// <summary>Una sesión de otra caja no se consulta ni se cierra desde la caja configurada.</summary>
    [Fact]
    public async Task SessionFromAnotherRegister_CannotBeSummarizedOrClosed()
    {
        var foreignCode = UniqueCashRegisterCode();
        var session = await _fixture.CashSessions.OpenAsync(_fixture.CashierId, foreignCode, 10m, null);
        try
        {
            var configuredCode = UniqueCashRegisterCode();
            Assert.NotEqual(foreignCode, configuredCode);

            var summaryError = await Assert.ThrowsAsync<CashSessionException>(() =>
                _fixture.CashSessions.GetSummaryAsync(session.Id, _fixture.ManagerId));
            Assert.Contains("otra caja", summaryError.Message, StringComparison.OrdinalIgnoreCase);

            var closeError = await Assert.ThrowsAsync<CashSessionException>(() =>
                _fixture.CashSessions.CloseAsync(session.Id, 10m, null, _fixture.ManagerId));
            Assert.Contains("otra caja", closeError.Message, StringComparison.OrdinalIgnoreCase);

            var stillOpen = await _fixture.CashSessions.GetOpenSessionAsync(foreignCode);
            Assert.Equal(session.Id, stillOpen?.Id);
        }
        finally
        {
            await CloseIfOpenAsync(session.Id, foreignCode, _fixture.ManagerId);
        }
    }

    /// <summary>Genera un código aislado y lo deja como caja configurada del fixture.</summary>
    /// <remarks>
    /// El servicio solo consulta y cierra sesiones de la caja configurada, por eso cada caso
    /// configura el mismo código que usa al abrir y evita depender del orden de ejecución.
    /// </remarks>
    /// <returns>Código de caja válido y único para el caso.</returns>
    private string UniqueCashRegisterCode()
    {
        var cashRegisterCode = $"CAJA-T-{Guid.NewGuid():N}"[..20];
        _fixture.SetCashRegisterCode(cashRegisterCode);
        return cashRegisterCode;
    }

    /// <summary>Obtiene el instante fijo usado por el proveedor de tiempo del fixture.</summary>
    /// <returns>Fecha UTC de la semilla.</returns>
    private static DateTime FixtureNow()
    {
        return PostgreSqlFixture.Now.UtcDateTime;
    }

    /// <summary>Ejecuta una apertura y convierte el conflicto esperado en resultado nulo.</summary>
    /// <param name="employeeId">Empleado que intenta abrir.</param>
    /// <param name="cashRegisterCode">Código aislado de caja.</param>
    /// <returns>Sesión ganadora o <c>null</c> si perdió el conflicto controlado.</returns>
    private async Task<CashSession?> ObserveOpenAsync(Guid employeeId, string cashRegisterCode)
    {
        try
        {
            return await _fixture.CashSessions.OpenAsync(employeeId, cashRegisterCode, 10m, null);
        }
        catch (CashSessionException)
        {
            return null;
        }
    }

    /// <summary>Cierra una sesión solo si continúa abierta para limpiar el caso.</summary>
    /// <param name="sessionId">Sesión creada por el caso.</param>
    /// <param name="cashRegisterCode">Código de caja del caso.</param>
    /// <param name="employeeId">Empleado autorizado para limpiar.</param>
    /// <returns>Tarea de limpieza.</returns>
    private async Task CloseIfOpenAsync(Guid sessionId, string cashRegisterCode, Guid employeeId)
    {
        var openSession = await _fixture.CashSessions.GetOpenSessionAsync(cashRegisterCode);
        if (openSession?.Id == sessionId)
        {
            _fixture.SetCashRegisterCode(cashRegisterCode);
            await _fixture.CashSessions.CloseAsync(sessionId, 15m, "Limpieza de prueba", employeeId);
        }
    }
}
