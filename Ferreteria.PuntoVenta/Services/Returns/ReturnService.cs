using System.Data;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Consulta ventas devolvibles y registra devoluciones de forma transaccional.</summary>
/// <remarks>
/// La confirmación se habilita únicamente cuando el lector es autoritativo y el writer está disponible. La operación
/// usa Serializable y bloquea la orden existente con FOR UPDATE antes de leer devoluciones: la BD aún no valida que
/// no se devuelva más de lo vendido, por lo que esa regla debe permanecer en este servicio.
/// </remarks>
public sealed class ReturnService : IReturnService
{
    private const int MaximumSerializationRetries = 2;
    private const string ConfirmationUnavailableMessage = "La confirmación de devoluciones se habilitará cuando el registro de devoluciones exista en la base de datos.";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReturnedQuantityReader _returnedQuantityReader;
    private readonly IReturnWriter _returnWriter;
    private readonly IReturnFiscalPolicy _fiscalPolicy;
    private readonly ReturnOptions _options;
    private readonly CashRegisterOptions _cashRegisterOptions;
    private readonly IReadOnlyList<string> _fullHistoryPositionNames;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReturnService> _logger;

    /// <summary>Inicializa el servicio de devoluciones.</summary>
    /// <param name="scopeFactory">Fábrica de contextos EF por operación.</param>
    /// <param name="returnedQuantityReader">Lector de cantidades históricas.</param>
    /// <param name="returnWriter">Writer transaccional de persistencia.</param>
    /// <param name="fiscalPolicy">Política fiscal intercambiable.</param>
    /// <param name="returnOptions">Opciones de ventana, catálogo y comprobante.</param>
    /// <param name="cashRegisterOptions">Código de caja configurado.</param>
    /// <param name="salesHistoryOptions">Puestos con acceso completo al historial.</param>
    /// <param name="clock">Reloj inyectado para búsquedas deterministas.</param>
    /// <param name="logger">Logger de fallos técnicos sin datos sensibles.</param>
    public ReturnService(
        IServiceScopeFactory scopeFactory,
        IReturnedQuantityReader returnedQuantityReader,
        IReturnWriter returnWriter,
        IReturnFiscalPolicy fiscalPolicy,
        IOptions<ReturnOptions> returnOptions,
        IOptions<CashRegisterOptions> cashRegisterOptions,
        IOptions<SalesHistory.SalesHistoryOptions> salesHistoryOptions,
        TimeProvider clock,
        ILogger<ReturnService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _returnedQuantityReader = returnedQuantityReader ?? throw new ArgumentNullException(nameof(returnedQuantityReader));
        _returnWriter = returnWriter ?? throw new ArgumentNullException(nameof(returnWriter));
        _fiscalPolicy = fiscalPolicy ?? throw new ArgumentNullException(nameof(fiscalPolicy));
        ArgumentNullException.ThrowIfNull(returnOptions);
        _options = returnOptions.Value;
        ArgumentNullException.ThrowIfNull(cashRegisterOptions);
        _cashRegisterOptions = cashRegisterOptions.Value;
        ArgumentNullException.ThrowIfNull(salesHistoryOptions);
        _fullHistoryPositionNames = salesHistoryOptions.Value.FullHistoryPositionNames.ToArray();
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ReturnCapabilities Capabilities => new(
        _returnWriter.IsAvailable && _returnedQuantityReader.IsAuthoritative,
        ConfirmationUnavailableMessage);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReturnableSaleSummary>> SearchReturnableSalesAsync(ReturnableSalesFilter filter, Guid requestedByEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalized = filter.Normalize();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        if (!await IsAuthorizedAsync(db, requestedByEmployeeId, cancellationToken))
        {
            return Array.Empty<ReturnableSaleSummary>();
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var from = normalized.FromUtc ?? now.AddDays(-Math.Max(1, _options.DiasBusquedaMaximos));
        var to = normalized.ToUtc ?? now;
        if (_options.PlazoDias.HasValue)
        {
            from = from < now.AddDays(-Math.Max(0, _options.PlazoDias.Value))
                ? now.AddDays(-Math.Max(0, _options.PlazoDias.Value))
                : from;
        }

        var query = db.Orders.AsNoTracking().Where(order =>
            order.Status == SalesDomainConstants.OrderStatuses.Completed
            && (order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale || order.OrderType == SalesDomainConstants.OrderTypes.ConfectionWorkOrder)
            && order.CreatedAt >= from && order.CreatedAt < to);
        query = ApplySearch(query, normalized.SearchText);
        return await query.OrderByDescending(order => order.CreatedAt).ThenBy(order => order.Id)
            .Skip((normalized.Page - 1) * normalized.PageSize).Take(normalized.PageSize)
            .Select(order => new ReturnableSaleSummary(
                order.Id,
                order.CreatedAt,
                order.Customer == null ? SalesDomainConstants.Customers.DefaultWalkInDisplayName : order.Customer.Name,
                order.Customer == null ? null : order.Customer.Nit,
                order.OrderType,
                order.Status,
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt).Select(dte => dte.DteType).FirstOrDefault(),
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt).Select(dte => dte.ControlNumber).FirstOrDefault(),
                order.Subtotal, order.DiscountAmount, order.TaxAmount, order.Total))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReturnableLine>> GetReturnableLinesAsync(Guid orderId, Guid requestedByEmployeeId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            return Array.Empty<ReturnableLine>();
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        if (!await IsAuthorizedAsync(db, requestedByEmployeeId, cancellationToken))
        {
            return Array.Empty<ReturnableLine>();
        }

        var isReturnable = await db.Orders.AsNoTracking().AnyAsync(order => order.Id == orderId
            && order.Status == SalesDomainConstants.OrderStatuses.Completed
            && (order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale || order.OrderType == SalesDomainConstants.OrderTypes.ConfectionWorkOrder), cancellationToken);
        if (!isReturnable)
        {
            return Array.Empty<ReturnableLine>();
        }

        var returned = await _returnedQuantityReader.GetAsync(db, orderId, cancellationToken);
        return await LoadReturnableLinesAsync(db, orderId, returned.Lines, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReturnResult> CreateReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReturnInputRules.ValidateShape(request, _options);
        if (!Capabilities.CanConfirmReturns)
        {
            throw new ReturnsUnavailableException(ConfirmationUnavailableMessage);
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await CreateReturnOnceAsync(request, cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                if (attempt < MaximumSerializationRetries)
                {
                    _logger.LogWarning(exception, "Conflicto serializable al crear devolución para {OrderId}; reintento {Attempt}", request.OrderId, attempt + 1);
                    continue;
                }

                _logger.LogError(exception, "Se agotaron los reintentos serializables al crear devolución para {OrderId}", request.OrderId);
                throw new ReturnsUnavailableException("No se pudo confirmar la devolución por concurrencia. Intente de nuevo.");
            }
        }
    }

    private async Task<ReturnResult> CreateReturnOnceAsync(ReturnRequest request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var employee = await LoadAuthorizedEmployeeAsync(db, request.EmployeeId, cancellationToken);
        var authorized = await LoadAuthorizedEmployeeAsync(db, request.AuthorizedByEmployeeId, cancellationToken);

        var lockedOrder = await db.Orders.FromSqlInterpolated($"SELECT * FROM sales.\"Orders\" WHERE \"id\" = {request.OrderId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (lockedOrder is null || lockedOrder.Status != SalesDomainConstants.OrderStatuses.Completed
            || (lockedOrder.OrderType != SalesDomainConstants.OrderTypes.CashRegisterSale && lockedOrder.OrderType != SalesDomainConstants.OrderTypes.ConfectionWorkOrder))
        {
            throw new InvalidReturnException("La venta no existe o no está disponible para devolución.");
        }

        var existing = await _returnWriter.FindByClientRequestIdAsync(db, request.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var returned = await _returnedQuantityReader.GetAsync(db, request.OrderId, cancellationToken);
        if (!_returnedQuantityReader.IsAuthoritative || !returned.IsAuthoritative)
        {
            throw new ReturnsUnavailableException(ConfirmationUnavailableMessage);
        }

        var sale = await LoadSaleSummaryAsync(db, request.OrderId, cancellationToken);
        var returnableLines = await LoadReturnableLinesAsync(db, request.OrderId, returned.Lines, cancellationToken);
        var calculation = ReturnCalculator.Calculate(request, sale, returnableLines, returned.Lines, _options);
        var cashRegisterCode = CashRegisterInputRules.ValidateCashRegisterCode(_cashRegisterOptions.Codigo);
        var openSession = await LoadOpenSessionAsync(db, cashRegisterCode, request.RefundMethod.Equals(ReturnDomainConstants.RefundMethods.Cash, StringComparison.OrdinalIgnoreCase), cancellationToken);
        if (request.RefundMethod.Equals(ReturnDomainConstants.RefundMethods.Cash, StringComparison.OrdinalIgnoreCase) && openSession is null)
        {
            throw new InvalidReturnException($"Para reintegrar en efectivo debe haber una sesión de caja abierta en {cashRegisterCode}.");
        }

        var fiscal = _fiscalPolicy.Decide(sale.DteType, calculation.ReturnType);
        var persistence = BuildPersistenceRecord(request, sale, calculation, fiscal, returnableLines, openSession, employee, authorized);
        await _returnWriter.PersistAsync(db, persistence, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReturnResult(request.ClientRequestId, request.OrderId, calculation, fiscal);
    }

    private ReturnPersistenceRecord BuildPersistenceRecord(ReturnRequest request, ReturnableSaleSummary sale, ReturnCalculationResult calculation, ReturnFiscalDecision fiscal, IReadOnlyList<ReturnableLine> lines, CashSession? openSession, Employee employee, Employee authorized)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var sourceLines = lines.ToDictionary(line => line.OrderDetailId);
        var movements = new List<InventoryMovementRecord>();
        var details = calculation.Lines.Select(line =>
        {
            var source = sourceLines[line.OrderDetailId];
            var movementId = line.Restocked ? Guid.NewGuid() : Guid.Empty;
            if (line.Restocked)
            {
                movements.Add(new InventoryMovementRecord(movementId, source.ProductId, "ENTRADA_DEVOLUCION", request.OrderId, employee.Id, line.RestockQuantity, line.UnitCost, "ENTRADA_DEVOLUCION", now));
            }

            return new ReturnDetailRecord(source.OrderDetailId, source.ProductId, line.Quantity, line.UnitsPerPackage, line.UnitPrice, line.UnitCost, line.DiscountAmount, line.Subtotal, line.TaxAmount, line.Restocked, line.RestockQuantity, line.Restocked ? movementId : null, now);
        }).ToArray();
        var cashMovement = request.RefundMethod.Equals(ReturnDomainConstants.RefundMethods.Cash, StringComparison.OrdinalIgnoreCase)
            ? new CashMovementRecord(openSession?.Id ?? throw new InvalidReturnException("La sesión de caja ya no está disponible."), ReturnDomainConstants.CashMovementTypes.CashRefund, employee.Id, authorized.Id, request.ClientRequestId, request.RefundAmount, TruncateReason($"Devolución ORD-{sale.ShortOrderId}"), now)
            : null;
        return new ReturnPersistenceRecord(
            new ReturnHeaderRecord(request.OrderId, openSession?.Id, employee.Id, authorized.Id, request.ClientRequestId, calculation.ReturnType, ReturnDomainConstants.Statuses.Completed, fiscal.FiscalStatus, null, request.ReasonCode.Trim(), request.Notes?.Trim(), calculation.Subtotal, calculation.DiscountAmount, calculation.TaxAmount, calculation.Total, request.RefundMethod.Trim(), request.RefundAmount, now, now),
            details,
            movements,
            cashMovement);
    }

    private async Task<ReturnableSaleSummary> LoadSaleSummaryAsync(FerreteriaDbContext db, Guid orderId, CancellationToken cancellationToken)
    {
        return await db.Orders.AsNoTracking().Where(order => order.Id == orderId)
            .Select(order => new ReturnableSaleSummary(order.Id, order.CreatedAt, order.Customer == null ? SalesDomainConstants.Customers.DefaultWalkInDisplayName : order.Customer.Name, order.Customer == null ? null : order.Customer.Nit, order.OrderType, order.Status, order.DteIssued.OrderByDescending(dte => dte.IssuedAt).Select(dte => dte.DteType).FirstOrDefault(), order.DteIssued.OrderByDescending(dte => dte.IssuedAt).Select(dte => dte.ControlNumber).FirstOrDefault(), order.Subtotal, order.DiscountAmount, order.TaxAmount, order.Total))
            .SingleAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ReturnableLine>> LoadReturnableLinesAsync(FerreteriaDbContext db, Guid orderId, IReadOnlyDictionary<Guid, ReturnedLineCredit> returned, CancellationToken cancellationToken)
    {
        var originalLines = await db.OrderDetails.AsNoTracking().Where(line => line.OrderId == orderId)
            .Select(line => new { line.Id, line.OrderId, line.ProductId, ProductCode = line.Product.Code, ProductDescription = line.Product.Description, line.Quantity, line.UnitPrice, line.Subtotal, line.DiscountAmount, line.UnitsPerPackage, line.UnitCost })
            .ToListAsync(cancellationToken);
        return originalLines.Select(line =>
        {
            var previous = returned.TryGetValue(line.Id, out var credit) ? credit : new ReturnedLineCredit(0m);
            var alreadyReturned = Math.Max(0m, previous.Quantity);
            return new ReturnableLine(line.Id, line.ProductId, line.ProductCode, line.ProductDescription, line.Quantity, alreadyReturned, Math.Max(0m, line.Quantity - alreadyReturned), line.UnitPrice, line.Subtotal, line.DiscountAmount, line.UnitsPerPackage, line.UnitCost, line.OrderId);
        }).ToArray();
    }

    private async Task<CashSession?> LoadOpenSessionAsync(FerreteriaDbContext db, string cashRegisterCode, bool lockSession, CancellationToken cancellationToken)
    {
        if (lockSession)
        {
            return await db.CashSessions.FromSqlInterpolated($"SELECT * FROM sales.\"CashSessions\" WHERE \"CashRegisterCode\" = {cashRegisterCode} AND \"status\" = {SalesDomainConstants.CashSessionStatuses.Open} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        }

        return await db.CashSessions.AsNoTracking().SingleOrDefaultAsync(session => session.CashRegisterCode == cashRegisterCode && session.Status == SalesDomainConstants.CashSessionStatuses.Open, cancellationToken);
    }

    private async Task<bool> IsAuthorizedAsync(FerreteriaDbContext db, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking().Include(item => item.Position).SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        return employee is { IsActive: true } && (employee.CanCashier || IsFullHistoryPosition(employee.Position?.Name));
    }

    private async Task<Employee> LoadAuthorizedEmployeeAsync(FerreteriaDbContext db, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.Include(item => item.Position).SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null || !employee.IsActive || (!employee.CanCashier && !IsFullHistoryPosition(employee.Position?.Name)))
        {
            throw new InvalidReturnException("El empleado no está autorizado para gestionar devoluciones.");
        }

        return employee;
    }

    private bool IsFullHistoryPosition(string? positionName) => _fullHistoryPositionNames.Any(name => string.Equals(name?.Trim(), positionName?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IQueryable<Order> ApplySearch(IQueryable<Order> query, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        var normalized = searchText.Trim().TrimStart('#');
        if (Guid.TryParse(normalized, out var orderId))
        {
            return query.Where(order => order.Id == orderId);
        }

        var escaped = SalesHistory.SalesHistoryFilter.EscapeILikePattern(normalized);
        var pattern = $"%{escaped}%";
        return query.Where(order => EF.Functions.ILike(order.Id.ToString(), $"{escaped}%", "\\")
            || (order.Customer != null && (EF.Functions.ILike(order.Customer.Name, pattern, "\\") || (order.Customer.Nit != null && EF.Functions.ILike(order.Customer.Nit, pattern, "\\"))))
            || order.DteIssued.Any(dte => EF.Functions.ILike(dte.ControlNumber, pattern, "\\")));
    }

    private static string TruncateReason(string reason) => reason.Length <= 300 ? reason : reason[..300];

    private static bool IsSerializationFailure(Exception exception) => FindPostgresException(exception)?.SqlState == PostgresErrorCodes.SerializationFailure;

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
