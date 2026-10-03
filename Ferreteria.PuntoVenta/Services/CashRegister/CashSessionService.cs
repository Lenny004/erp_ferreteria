using System.Data;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Servicio transaccional de apertura, resumen y cierre de sesiones de caja.</summary>
/// <remarks>
/// La base actual tiene un índice parcial por empleado y caja. La exclusión por código de caja se
/// garantiza aquí mediante Serializable; la dependencia de un índice único parcial solo por caja queda documentada.
/// </remarks>
public sealed class CashSessionService : ICashSessionService
{
    private const int MaximumSerializationRetries = 2;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<string> _fullHistoryPositionNames;
    private readonly CashRegisterOptions _options;
    private readonly ICashMovementReader _cashMovementReader;
    private readonly TimeProvider _clock;
    private readonly ILogger<CashSessionService> _logger;
    private readonly IAuthorizationGuard _authorizationGuard;

    /// <summary>Inicializa el servicio de sesiones de caja.</summary>
    /// <param name="scopeFactory">Fábrica de ámbitos para contextos EF independientes.</param>
    /// <param name="options">Configuración de caja y límites monetarios.</param>
    /// <param name="clock">Reloj inyectado para timestamps UTC deterministas.</param>
    /// <param name="logger">Logger de conflictos y fallos técnicos.</param>
    /// <param name="salesHistoryOptions">Puestos de acceso completo compartidos con el historial.</param>
    /// <param name="cashMovementReader">Lector abstracto de devoluciones en efectivo por sesión.</param>
    /// <param name="authorizationGuard">Guard que valida permiso e identidad de la sesión activa.</param>
    public CashSessionService(
        IServiceScopeFactory scopeFactory,
        IOptions<CashRegisterOptions> options,
        IOptions<SalesHistoryOptions> salesHistoryOptions,
        ICashMovementReader cashMovementReader,
        TimeProvider clock,
        ILogger<CashSessionService> logger,
        IAuthorizationGuard authorizationGuard)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        ArgumentNullException.ThrowIfNull(salesHistoryOptions);
        _fullHistoryPositionNames = salesHistoryOptions.Value.FullHistoryPositionNames.ToArray();
        _cashMovementReader = cashMovementReader ?? throw new ArgumentNullException(nameof(cashMovementReader));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authorizationGuard = authorizationGuard ?? throw new ArgumentNullException(nameof(authorizationGuard));
    }

    /// <inheritdoc />
    public async Task<CashSession?> GetOpenSessionAsync(
        string cashRegisterCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = CashRegisterInputRules.ValidateCashRegisterCode(cashRegisterCode);
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await dbContext.CashSessions
            .AsNoTracking()
            .Include(session => session.Employee)
            .FirstOrDefaultAsync(
                session => session.CashRegisterCode == normalizedCode
                    && session.Status == SalesDomainConstants.CashSessionStatuses.Open,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CashSession> OpenAsync(
        Guid employeeId,
        string cashRegisterCode,
        decimal openingAmount,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await _authorizationGuard.RequireAsync(PosPermission.OperarCaja, employeeId, cancellationToken);
        var normalizedCode = CashRegisterInputRules.ValidateCashRegisterCode(cashRegisterCode);
        var normalizedAmount = CashRegisterInputRules.ValidateAmount(
            openingAmount,
            "El fondo inicial",
            _options.MontoMaximo);
        ValidateNotes(notes);

        for (var attempt = 0; attempt <= MaximumSerializationRetries; attempt++)
        {
            try
            {
                return await OpenOnceAsync(
                    employeeId,
                    normalizedCode,
                    normalizedAmount,
                    notes,
                    cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                if (attempt < MaximumSerializationRetries)
                {
                    _logger.LogWarning(
                        exception,
                        "Conflicto serializable al abrir caja {CashRegisterCode}; reintento {Attempt}",
                        normalizedCode,
                        attempt + 1);
                    continue;
                }

                _logger.LogError(exception, "Se agotaron los reintentos serializables al abrir caja {CashRegisterCode}", normalizedCode);
                throw new CashSessionException("No se pudo abrir la caja por concurrencia. Intente de nuevo.");
            }
            catch (Exception exception) when (IsUniqueViolation(exception))
            {
                throw new CashSessionException($"La caja {normalizedCode} ya tiene una sesión abierta. Solo puede haber una sesión abierta por caja.");
            }
        }

        throw new CashSessionException("No se pudo abrir la caja por concurrencia. Intente de nuevo.");
    }

    /// <inheritdoc />
    public async Task<CashRegisterSummary> GetSummaryAsync(
        Guid sessionId,
        Guid requestedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var requester = await LoadAuthorizedEmployeeAsync(
            dbContext,
            requestedByEmployeeId,
            cancellationToken);
        var session = await dbContext.CashSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            throw new CashSessionException("La sesión de caja no existe.");
        }

        if (session.CashRegisterCode != CashRegisterInputRules.ValidateCashRegisterCode(_options.Codigo))
        {
            throw new CashSessionException("La sesión pertenece a otra caja.");
        }

        EnsureCanAccessSession(requester, session);
        var snapshot = await LoadSnapshotAsync(dbContext, session, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CashRegisterCalculator.Calculate(snapshot);
    }

    /// <inheritdoc />
    public async Task<CashSessionCloseResult> CloseAsync(
        Guid sessionId,
        decimal declaredCash,
        string? notes,
        Guid closedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationGuard.RequireAsync(PosPermission.OperarCaja, closedByEmployeeId, cancellationToken);
        var normalizedDeclaredCash = CashRegisterInputRules.ValidateAmount(
            declaredCash,
            "El efectivo contado",
            _options.MontoMaximo);
        ValidateNotes(notes);

        for (var attempt = 0; attempt <= MaximumSerializationRetries; attempt++)
        {
            try
            {
                return await CloseOnceAsync(
                    sessionId,
                    normalizedDeclaredCash,
                    notes,
                    closedByEmployeeId,
                    cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                if (attempt < MaximumSerializationRetries)
                {
                    _logger.LogWarning(
                        exception,
                        "Conflicto serializable al cerrar sesión {SessionId}; reintento {Attempt}",
                        sessionId,
                        attempt + 1);
                    continue;
                }

                _logger.LogError(exception, "Se agotaron los reintentos serializables al cerrar sesión {SessionId}", sessionId);
                throw new CashSessionException("No se pudo cerrar la caja por concurrencia. Verifique el estado e intente de nuevo.");
            }
        }

        throw new CashSessionException("No se pudo cerrar la caja por concurrencia. Verifique el estado e intente de nuevo.");
    }

    /// <summary>Ejecuta un intento de apertura dentro de una transacción Serializable.</summary>
    /// <param name="employeeId">Empleado que abre el turno.</param>
    /// <param name="cashRegisterCode">Código normalizado de la caja.</param>
    /// <param name="openingAmount">Fondo inicial normalizado.</param>
    /// <param name="notes">Observación opcional.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Sesión ABIERTA persistida.</returns>
    /// <remarks>Agrega la auditoría al mismo contexto antes de guardar y confirmar la transacción.</remarks>
    private async Task<CashSession> OpenOnceAsync(
        Guid employeeId,
        string cashRegisterCode,
        decimal openingAmount,
        string? notes,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var employee = await LoadAuthorizedEmployeeAsync(dbContext, employeeId, cancellationToken);
        if (!employee.CanCashier)
        {
            throw new CashSessionException("El empleado no tiene permiso para abrir caja.");
        }

        var openSession = await dbContext.CashSessions.SingleOrDefaultAsync(
            session => session.CashRegisterCode == cashRegisterCode
                && session.Status == SalesDomainConstants.CashSessionStatuses.Open,
            cancellationToken);
        if (openSession is not null)
        {
            if (openSession.EmployeeId == employeeId)
            {
                throw new CashSessionException($"Ya tiene una sesión abierta en la caja {cashRegisterCode}.");
            }

            throw new CashSessionException($"La caja {cashRegisterCode} ya tiene una sesión abierta de otro cajero. Solo puede haber una sesión abierta por caja: ciérrela antes de abrir otra.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var session = new CashSession
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            CashRegisterCode = cashRegisterCode,
            OpenedAt = now,
            OpeningAmount = openingAmount,
            Status = SalesDomainConstants.CashSessionStatuses.Open,
            Notes = notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.CashSessions.Add(session);
        dbContext.AuditLogs.Add(AuditService.CreateChangeEntry(
            CashSessionAuditActions.Open,
            CashSessionAuditActions.TableName,
            session.Id.ToString(),
            null,
            new
            {
                session.EmployeeId,
                session.CashRegisterCode,
                session.OpenedAt,
                session.OpeningAmount,
                session.Status
            },
            employeeId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    /// <summary>Ejecuta un intento de cierre con relectura y autorización dentro de una transacción.</summary>
    /// <param name="sessionId">Sesión que se desea cerrar.</param>
    /// <param name="declaredCash">Efectivo contado normalizado.</param>
    /// <param name="notes">Observación del cierre.</param>
    /// <param name="closedByEmployeeId">Empleado que confirma el cierre.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado calculado y persistido del cierre.</returns>
    /// <remarks>La fila de auditoría se guarda con la sesión antes del commit; un fallo revierte ambos cambios.</remarks>
    private async Task<CashSessionCloseResult> CloseOnceAsync(
        Guid sessionId,
        decimal declaredCash,
        string? notes,
        Guid closedByEmployeeId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var closer = await LoadAuthorizedEmployeeAsync(dbContext, closedByEmployeeId, cancellationToken);

        // El cierre debe tomar el mismo bloqueo de fila padre que las ventas toman en FOR SHARE.
        var session = await dbContext.CashSessions
            .FromSqlInterpolated($"SELECT * FROM sales.\"CashSessions\" WHERE \"id\" = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            throw new CashSessionException("La sesión de caja no existe.");
        }

        if (session.Status != SalesDomainConstants.CashSessionStatuses.Open)
        {
            throw new CashSessionException("La sesión ya no está abierta y no se puede cerrar.");
        }

        if (session.CashRegisterCode != CashRegisterInputRules.ValidateCashRegisterCode(_options.Codigo))
        {
            throw new CashSessionException("La sesión pertenece a otra caja.");
        }

        EnsureCanAccessSession(closer, session);

        var snapshot = await LoadSnapshotAsync(dbContext, session, cancellationToken);
        var summary = CashRegisterCalculator.Calculate(snapshot);
        var difference = CashRegisterCalculator.CalculateDifference(
            summary.ExpectedCash,
            declaredCash,
            _options.UmbralDiferencia);
        if (difference.RequiresObservation && string.IsNullOrWhiteSpace(notes))
        {
            throw new CashSessionException("Registre una observación cuando la diferencia supera el umbral configurado.");
        }

        var oldData = new
        {
            session.Status,
            session.ClosingExpectedAmount,
            session.ClosingDeclaredAmount,
            session.Difference,
            session.ClosedAt
        };
        var closedAt = _clock.GetUtcNow().UtcDateTime;
        session.ClosingExpectedAmount = difference.ExpectedCash;
        session.ClosingDeclaredAmount = difference.DeclaredCash;
        session.Difference = difference.Difference;
        session.Status = SalesDomainConstants.CashSessionStatuses.Closed;
        session.ClosedAt = closedAt;
        session.Notes = notes?.Trim();
        session.UpdatedAt = closedAt;

        dbContext.AuditLogs.Add(AuditService.CreateChangeEntry(
            CashSessionAuditActions.Close,
            CashSessionAuditActions.TableName,
            session.Id.ToString(),
            oldData,
            new
            {
                session.Status,
                session.ClosingExpectedAmount,
                session.ClosingDeclaredAmount,
                session.Difference,
                session.ClosedAt,
                closedByEmployeeId
            },
            closedByEmployeeId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CashSessionCloseResult(
            session.Id,
            difference.ExpectedCash,
            difference.DeclaredCash,
            difference.Difference,
            summary,
            closedAt);
    }

    /// <summary>Lee ventas y pagos asociados a una sesión para construir un cálculo inmutable.</summary>
    /// <param name="dbContext">Contexto de datos de la operación.</param>
    /// <param name="session">Sesión cuyo movimiento se consulta.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Instantánea de ventas, pagos y fondo inicial.</returns>
    private async Task<CashRegisterSnapshot> LoadSnapshotAsync(
        FerreteriaDbContext dbContext,
        CashSession session,
        CancellationToken cancellationToken)
    {
        var orderRows = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CashSessionId == session.Id)
            .Select(order => new
            {
                order.Id,
                order.CreatedAt,
                order.Status,
                order.Total,
                order.TaxAmount,
                DteStatus = order.DteIssued
                    .OrderByDescending(dte => dte.IssuedAt)
                    .Select(dte => dte.MhStatus)
                    .FirstOrDefault(),
                DocumentNumber = order.DteIssued
                    .OrderByDescending(dte => dte.IssuedAt)
                    .Select(dte => dte.ControlNumber)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var paymentRows = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.CashSessionId == session.Id)
            .Select(payment => new
            {
                payment.OrderId,
                payment.Method,
                payment.Amount,
                payment.Reference
            })
            .ToListAsync(cancellationToken);
        var paymentsByOrder = paymentRows
            .GroupBy(payment => payment.OrderId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CashRegisterPaymentSnapshot>)group
                    .Select(payment => new CashRegisterPaymentSnapshot(payment.Method, payment.Amount, payment.Reference))
                    .ToList());
        var sales = orderRows
            .Select(order => new CashRegisterSaleSnapshot(
                order.Id,
                order.CreatedAt,
                order.Status,
                order.Total,
                order.TaxAmount,
                paymentsByOrder.TryGetValue(order.Id, out var payments)
                    ? payments
                    : Array.Empty<CashRegisterPaymentSnapshot>(),
                order.DteStatus,
                order.DocumentNumber))
            .ToList();

        var cashRefunds = await _cashMovementReader.GetCashRefundsAsync(dbContext, session.Id, cancellationToken);
        if (cashRefunds < 0m)
        {
            throw new CashSessionException("La fuente de devoluciones de caja devolvió un monto inválido.");
        }

        return new CashRegisterSnapshot(session.Id, session.OpeningAmount, sales, cashRefunds);
    }

    /// <summary>Obtiene un empleado activo para autorizar una operación de caja.</summary>
    /// <param name="dbContext">Contexto de la transacción activa.</param>
    /// <param name="employeeId">Empleado solicitante.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Empleado activo con su puesto.</returns>
    /// <exception cref="CashSessionException">Si el empleado no existe o está inactivo.</exception>
    /// <remarks>La consulta se ejecuta dentro de la transacción o consulta que protege la operación de caja.</remarks>
    private static async Task<Models.Employee> LoadAuthorizedEmployeeAsync(
        FerreteriaDbContext dbContext,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .Include(item => item.Position)
            .SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            throw new CashSessionException("El empleado no existe o no está activo.");
        }

        if (!employee.IsActive)
        {
            throw new CashSessionException("El empleado está inactivo y no puede operar caja.");
        }

        return employee;
    }

    /// <summary>Aplica la regla de dueño o puesto de acceso completo a una sesión.</summary>
    /// <param name="employee">Empleado activo que solicita la operación.</param>
    /// <param name="session">Sesión que se desea consultar o cerrar.</param>
    /// <exception cref="CashSessionException">Si el empleado no puede acceder.</exception>
    /// <remarks>
    /// El dueño debe conservar <c>CanCashier</c>. Un puesto de acceso completo se toma de la misma lista
    /// <c>SalesHistory:FullHistoryPositionNames</c>; no existe un puesto Supervisor implícito.
    /// </remarks>
    private void EnsureCanAccessSession(Models.Employee employee, CashSession session)
    {
        var isOwner = session.EmployeeId == employee.Id;
        var hasFullAccess = _fullHistoryPositionNames.Any(name =>
            string.Equals(name?.Trim(), employee.Position?.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
        if ((isOwner && employee.CanCashier) || hasFullAccess)
        {
            return;
        }

        throw new CashSessionException("El empleado no está autorizado para consultar o cerrar este turno.");
    }

    private static void ValidateNotes(string? notes)
    {
        if (notes?.Length > 2000)
        {
            throw new ArgumentException("La observación no puede superar 2000 caracteres.", nameof(notes));
        }
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        return FindPostgresException(exception)?.SqlState == PostgresErrorCodes.SerializationFailure;
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        return FindPostgresException(exception)?.SqlState == PostgresErrorCodes.UniqueViolation;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }
}

