using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Consulta ventas devolvibles y bloquea confirmaciones sin persistencia autoritativa.</summary>
/// <remarks>
/// Solo consulta tablas existentes con AsNoTracking. La confirmación futura deberá usar Serializable,
/// bloqueo FOR UPDATE de la orden, idempotencia por ClientRequestId y escritura atómica de devolución,
/// kardex, caja y auditoría. La orden conservará COMPLETADA incluso en una devolución total.
/// </remarks>
public sealed class ReturnService : IReturnService
{
    private const string ConfirmationUnavailableMessage =
        "La confirmación de devoluciones se habilitará cuando el registro de devoluciones exista en la base de datos.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReturnedQuantityReader _returnedQuantityReader;
    private readonly IReturnFiscalPolicy _fiscalPolicy;
    private readonly ReturnOptions _options;
    private readonly IReadOnlyList<string> _fullHistoryPositionNames;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReturnService> _logger;

    /// <summary>Inicializa el servicio de devoluciones.</summary>
    /// <param name="scopeFactory">Fábrica de contextos EF por operación.</param>
    /// <param name="returnedQuantityReader">Lector de cantidades históricas.</param>
    /// <param name="fiscalPolicy">Política fiscal intercambiable.</param>
    /// <param name="returnOptions">Opciones de ventana, catálogo y comprobante.</param>
    /// <param name="salesHistoryOptions">Puestos con acceso completo al historial.</param>
    /// <param name="clock">Reloj inyectado para búsquedas deterministas.</param>
    /// <param name="logger">Logger de fallos técnicos sin datos sensibles.</param>
    public ReturnService(
        IServiceScopeFactory scopeFactory,
        IReturnedQuantityReader returnedQuantityReader,
        IReturnFiscalPolicy fiscalPolicy,
        IOptions<ReturnOptions> returnOptions,
        IOptions<SalesHistory.SalesHistoryOptions> salesHistoryOptions,
        TimeProvider clock,
        ILogger<ReturnService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _returnedQuantityReader = returnedQuantityReader ?? throw new ArgumentNullException(nameof(returnedQuantityReader));
        _fiscalPolicy = fiscalPolicy ?? throw new ArgumentNullException(nameof(fiscalPolicy));
        ArgumentNullException.ThrowIfNull(returnOptions);
        _options = returnOptions.Value;
        ArgumentNullException.ThrowIfNull(salesHistoryOptions);
        _fullHistoryPositionNames = salesHistoryOptions.Value.FullHistoryPositionNames.ToArray();
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ReturnCapabilities Capabilities => new(false, ConfirmationUnavailableMessage);

    /// <inheritdoc />
    public ReturnCapabilities ReturnCapabilities => Capabilities;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReturnableSaleSummary>> SearchReturnableSalesAsync(
        ReturnableSalesFilter filter,
        Guid requestedByEmployeeId,
        CancellationToken cancellationToken = default)
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
        var operationalFrom = now.AddDays(-Math.Max(1, _options.DiasBusquedaMaximos));
        var from = normalized.FromUtc ?? operationalFrom;
        var to = normalized.ToUtc ?? now;
        if (_options.PlazoDias.HasValue)
        {
            var configuredFrom = now.AddDays(-Math.Max(0, _options.PlazoDias.Value));
            if (from < configuredFrom)
            {
                from = configuredFrom;
            }
        }

        var query = db.Orders
            .AsNoTracking()
            .Where(order => order.Status == SalesDomainConstants.OrderStatuses.Completed
                && (order.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale
                    || order.OrderType == SalesDomainConstants.OrderTypes.ConfectionWorkOrder)
                && order.CreatedAt >= from
                && order.CreatedAt < to);
        query = ApplySearch(query, normalized.SearchText);

        return await query
            .OrderByDescending(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(order => new ReturnableSaleSummary(
                order.Id,
                order.CreatedAt,
                order.Customer == null ? SalesDomainConstants.Customers.DefaultWalkInDisplayName : order.Customer.Name,
                order.Customer == null ? null : order.Customer.Nit,
                order.OrderType,
                order.Status,
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt).Select(dte => dte.DteType).FirstOrDefault(),
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt).Select(dte => dte.ControlNumber).FirstOrDefault(),
                order.Subtotal,
                order.DiscountAmount,
                order.TaxAmount,
                order.Total))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReturnableLine>> GetReturnableLinesAsync(
        Guid orderId,
        Guid requestedByEmployeeId,
        CancellationToken cancellationToken = default)
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

        var order = await db.Orders
            .AsNoTracking()
            .Where(item => item.Id == orderId
                && item.Status == SalesDomainConstants.OrderStatuses.Completed
                && (item.OrderType == SalesDomainConstants.OrderTypes.CashRegisterSale
                    || item.OrderType == SalesDomainConstants.OrderTypes.ConfectionWorkOrder))
            .Select(item => item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (order == Guid.Empty)
        {
            return Array.Empty<ReturnableLine>();
        }

        var returned = await _returnedQuantityReader.GetAsync(db, orderId, cancellationToken);
        var originalLines = await db.OrderDetails
            .AsNoTracking()
            .Where(line => line.OrderId == orderId)
            .Select(line => new
            {
                line.Id,
                line.OrderId,
                line.ProductId,
                ProductCode = line.Product.Code,
                ProductDescription = line.Product.Description,
                line.Quantity,
                line.UnitPrice,
                line.Subtotal,
                line.DiscountAmount,
                line.UnitsPerPackage,
                line.UnitCost
            })
            .ToListAsync(cancellationToken);

        return originalLines.Select(line => ToReturnableLine(
            line.Id,
            line.OrderId,
            line.ProductId,
            line.ProductCode,
            line.ProductDescription,
            line.Quantity,
            line.UnitPrice,
            line.Subtotal,
            line.DiscountAmount,
            line.UnitsPerPackage,
            line.UnitCost,
            returned.Lines)).ToArray();
    }

    /// <inheritdoc />
    public async Task<ReturnResult> CreateReturnAsync(
        ReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var employee = await LoadAuthorizedEmployeeAsync(db, request.EmployeeId, cancellationToken);
        var authorized = await LoadAuthorizedEmployeeAsync(db, request.AuthorizedByEmployeeId, cancellationToken);
        _ = employee;
        _ = authorized;

        var order = await db.Orders
            .AsNoTracking()
            .Include(item => item.DteIssued)
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Id == request.OrderId, cancellationToken);
        if (order is null
            || order.Status != SalesDomainConstants.OrderStatuses.Completed
            || (order.OrderType != SalesDomainConstants.OrderTypes.CashRegisterSale
                && order.OrderType != SalesDomainConstants.OrderTypes.ConfectionWorkOrder))
        {
            throw new InvalidReturnException("La venta no existe o no está disponible para devolución.");
        }

        var returned = await _returnedQuantityReader.GetAsync(db, order.Id, cancellationToken);
        var lines = await db.OrderDetails
            .AsNoTracking()
            .Where(line => line.OrderId == order.Id)
            .Select(line => new
            {
                line.Id,
                line.OrderId,
                line.ProductId,
                ProductCode = line.Product.Code,
                ProductDescription = line.Product.Description,
                line.Quantity,
                line.UnitPrice,
                line.Subtotal,
                line.DiscountAmount,
                line.UnitsPerPackage,
                line.UnitCost
            })
            .ToListAsync(cancellationToken);
        var returnableLines = lines.Select(line => ToReturnableLine(
            line.Id,
            line.OrderId,
            line.ProductId,
            line.ProductCode,
            line.ProductDescription,
            line.Quantity,
            line.UnitPrice,
            line.Subtotal,
            line.DiscountAmount,
            line.UnitsPerPackage,
            line.UnitCost,
            returned.Lines)).ToArray();
        ReturnInputRules.Validate(request, returnableLines, _options);
        var sale = new ReturnableSaleSummary(
            order.Id,
            order.CreatedAt,
            order.Customer?.Name ?? SalesDomainConstants.Customers.DefaultWalkInDisplayName,
            order.Customer?.Nit,
            order.OrderType,
            order.Status,
            order.DteIssued.OrderByDescending(item => item.IssuedAt).Select(item => item.DteType).FirstOrDefault(),
            order.DteIssued.OrderByDescending(item => item.IssuedAt).Select(item => item.ControlNumber).FirstOrDefault(),
            order.Subtotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.Total);
        var calculation = ReturnCalculator.Calculate(request, sale, returnableLines, returned.Lines, _options);
        var fiscal = _fiscalPolicy.Decide(sale.DteType, calculation.ReturnType);
        _logger.LogInformation(
            "Solicitud de devolución validada pero pendiente de migración para orden {OrderId}",
            order.Id);

        if (!returned.IsAuthoritative || !Capabilities.CanConfirmReturns)
        {
            throw new ReturnsUnavailableException(ConfirmationUnavailableMessage);
        }

        throw new ReturnsUnavailableException(ConfirmationUnavailableMessage);
    }

    private async Task<bool> IsAuthorizedAsync(
        FerreteriaDbContext db,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees
            .AsNoTracking()
            .Include(item => item.Position)
            .SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return false;
        }

        return employee.CanCashier || _fullHistoryPositionNames.Any(name =>
            string.Equals(name?.Trim(), employee.Position?.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Employee> LoadAuthorizedEmployeeAsync(
        FerreteriaDbContext db,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees
            .AsNoTracking()
            .Include(item => item.Position)
            .SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null || !employee.IsActive || (!employee.CanCashier && !_fullHistoryPositionNames.Any(name =>
                string.Equals(name?.Trim(), employee.Position?.Name?.Trim(), StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidReturnException("El empleado no está autorizado para gestionar devoluciones.");
        }

        return employee;
    }

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
        var prefix = $"{escaped}%";
        return query.Where(order =>
            EF.Functions.ILike(order.Id.ToString(), prefix, "\\")
            || (order.Customer != null && (EF.Functions.ILike(order.Customer.Name, pattern, "\\")
                || (order.Customer.Nit != null && EF.Functions.ILike(order.Customer.Nit, pattern, "\\"))))
            || order.DteIssued.Any(dte => EF.Functions.ILike(dte.ControlNumber, pattern, "\\")));
    }

    private static ReturnableLine ToReturnableLine(
        Guid id,
        Guid orderId,
        Guid productId,
        string productCode,
        string productDescription,
        decimal quantity,
        decimal unitPrice,
        decimal subtotal,
        decimal discountAmount,
        decimal unitsPerPackage,
        decimal unitCost,
        IReadOnlyDictionary<Guid, ReturnedLineCredit> returned)
    {
        var previous = returned.TryGetValue(id, out var value) ? value : new ReturnedLineCredit(0m);
        var alreadyReturned = Math.Max(0m, previous.Quantity);
        return new ReturnableLine(
            id,
            productId,
            productCode,
            productDescription,
            quantity,
            alreadyReturned,
            Math.Max(0m, quantity - alreadyReturned),
            unitPrice,
            subtotal,
            discountAmount,
            unitsPerPackage,
            unitCost,
            orderId);
    }
}
