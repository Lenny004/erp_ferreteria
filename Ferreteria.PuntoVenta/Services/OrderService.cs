using System.Data;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Ferreteria.PuntoVenta.Services.Security;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Implementación transaccional de ventas y órdenes de confección.
/// Usa aislamiento <see cref="IsolationLevel.Serializable"/> en operaciones que modifican stock.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CashRegisterOptions _cashRegisterOptions;
    private readonly IAuthorizationGuard _authorizationGuard;
    private readonly TimeProvider _clock;
    private readonly ILogger<OrderService> _logger;

    /// <summary>Inicializa el servicio de órdenes con el alcance de datos y la caja configurada.</summary>
    /// <param name="scopeFactory">Fábrica de ámbitos para crear contextos EF por operación.</param>
    /// <param name="cashRegisterOptions">Configuración del código de caja activa.</param>
    /// <param name="authorizationGuard">Guard que valida el permiso vigente del empleado.</param>
    /// <param name="clock">Reloj inyectado para timestamps deterministas.</param>
    /// <param name="logger">Logger de reintentos sin datos sensibles.</param>
    public OrderService(
        IServiceScopeFactory scopeFactory,
        IOptions<CashRegisterOptions> cashRegisterOptions,
        IAuthorizationGuard authorizationGuard,
        TimeProvider clock,
        ILogger<OrderService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ArgumentNullException.ThrowIfNull(cashRegisterOptions);
        _cashRegisterOptions = cashRegisterOptions.Value;
        _authorizationGuard = authorizationGuard ?? throw new ArgumentNullException(nameof(authorizationGuard));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// La búsqueda por <c>ClientRequestId</c> ocurre dentro de la transacción Serializable y antes de
    /// exigir una sesión abierta. Así, un reintento de una venta ya persistida devuelve el resultado
    /// idempotente aunque la sesión haya cambiado; una orden nueva valida la sesión antes de crear orden,
    /// detalle o pago.
    /// </remarks>
    public async Task<CashSaleResult> CreateCashSaleAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(PosPermission.OperarCaja, request.EmployeeId, cancellationToken);
        ValidateCashSaleRequest(request);
        var clientRequestId = request.ClientRequestId ?? Guid.NewGuid();
        var normalizedRequest = request with { ClientRequestId = clientRequestId };

        try
        {
            return await PostgresTransientRetry.ExecuteAsync(
                retryToken => CreateCashSaleOnceAsync(normalizedRequest, retryToken),
                (retry, _) => _logger.LogWarning("Reintento de venta POS por conflicto transitorio. Intento {Retry}", retry),
                cancellationToken);
        }
        catch (PostgresUniqueRequestException)
        {
            var existing = await FindOrderByClientRequestIdInNewScopeAsync(clientRequestId, cancellationToken)
                ?? throw new InvalidOrderException("No se pudo recuperar la venta idempotente. Intente de nuevo.");
            EnsureCashSaleMatches(existing, normalizedRequest);
            return MapToCashSaleResult(existing);
        }
        catch (PostgresTransientOperationException)
        {
            throw new InvalidOrderException("Otra caja está vendiendo los mismos productos. Intente de nuevo.");
        }
    }

    private async Task<CashSaleResult> CreateCashSaleOnceAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken)
    {

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var clientRequestId = request.ClientRequestId!.Value;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingOrder = await FindOrderByClientRequestIdAsync(dbContext, clientRequestId, cancellationToken);
        if (existingOrder is not null)
        {
            EnsureCashSaleMatches(existingOrder, request);
            return MapToCashSaleResult(existingOrder);
        }

        await EnsureOpenCashSessionAsync(
            dbContext,
            request.CashSessionId,
            request.EmployeeId,
            cancellationToken);

        var now = _clock.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            CashSessionId = request.CashSessionId,
            CustomerId = request.CustomerId,
            ClientRequestId = clientRequestId,
            OrderType = SalesDomainConstants.OrderTypes.CashRegisterSale,
            Status = SalesDomainConstants.OrderStatuses.Completed,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        var products = await LockProductsAsync(dbContext, request.Lines.Select(line => line.ProductId), cancellationToken);
        foreach (var line in request.Lines)
        {
            await AddSaleLineAsync(
                dbContext,
                order,
                line,
                products,
                request.EmployeeId,
                inventoryReason: "Venta de caja",
                cancellationToken,
                now);
        }

        ApplyTaxTotals(order);
        ValidatePaymentTotals(request.Payments, order.Total);
        AddPayments(order, request.Payments, request.CashSessionId, now);

        dbContext.Orders.Add(order);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOrderClientRequestUniqueViolation(exception))
        {
            throw new PostgresUniqueRequestException(exception);
        }
        await transaction.CommitAsync(cancellationToken);

        return MapToCashSaleResult(order);
    }

    /// <inheritdoc />
    public async Task<WorkOrderResult> CreateConfectionOrderAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(PosPermission.OperarInventario, request.EmployeeId, cancellationToken);
        ValidateConfectionOrderRequest(request);
        var clientRequestId = request.ClientRequestId ?? Guid.NewGuid();
        var normalizedRequest = request with { ClientRequestId = clientRequestId };

        try
        {
            return await PostgresTransientRetry.ExecuteAsync(
                retryToken => CreateConfectionOrderOnceAsync(normalizedRequest, retryToken),
                (retry, _) => _logger.LogWarning("Reintento de orden de confección por conflicto transitorio. Intento {Retry}", retry),
                cancellationToken);
        }
        catch (PostgresUniqueRequestException)
        {
            var existing = await FindOrderByClientRequestIdInNewScopeAsync(clientRequestId, cancellationToken)
                ?? throw new InvalidOrderException("No se pudo recuperar la orden idempotente. Intente de nuevo.");
            EnsureConfectionOrderMatches(existing, normalizedRequest);
            return MapToWorkOrderResult(existing);
        }
        catch (PostgresTransientOperationException)
        {
            throw new InvalidOrderException("No se pudo registrar la orden por concurrencia. Intente de nuevo.");
        }
    }

    private async Task<WorkOrderResult> CreateConfectionOrderOnceAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken)
    {

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var clientRequestId = request.ClientRequestId!.Value;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingOrder = await FindOrderByClientRequestIdAsync(dbContext, clientRequestId, cancellationToken);
        if (existingOrder is not null)
        {
            EnsureConfectionOrderMatches(existingOrder, request);
            return MapToWorkOrderResult(existingOrder);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            CustomerId = request.CustomerId,
            ClientRequestId = clientRequestId,
            OrderType = SalesDomainConstants.OrderTypes.ConfectionWorkOrder,
            Status = SalesDomainConstants.OrderStatuses.Pending,
            Notes = OrderNotesFormatter.BuildConfectionOrderNotes(
                request.CustomerName,
                request.CustomerPhone,
                request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var line in request.Lines)
        {
            await AddPendingWorkOrderLineAsync(dbContext, order, line, cancellationToken);
        }

        ApplyTaxTotals(order);

        dbContext.Orders.Add(order);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOrderClientRequestUniqueViolation(exception))
        {
            throw new PostgresUniqueRequestException(exception);
        }
        await transaction.CommitAsync(cancellationToken);

        return MapToWorkOrderResult(order);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfectionOrderSummary>> GetConfectionOrdersAsync(
        string? status,
        string? searchText,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Employee)
            .Include(order => order.OrderDetails)
            .Where(order => order.OrderType == SalesDomainConstants.OrderTypes.ConfectionWorkOrder);

        if (!string.IsNullOrWhiteSpace(status) && status != SalesDomainConstants.OrderStatuses.All)
        {
            ordersQuery = ordersQuery.Where(order => order.Status == status);
        }

        ordersQuery = ordersQuery.ApplySearchTextFilter(searchText);

        var orders = await ordersQuery
            .OrderByDescending(order => order.CreatedAt)
            .Take(take)
            .Select(order => new
            {
                order.Id,
                order.CreatedAt,
                order.Notes,
                order.Status,
                order.Total,
                EmployeeDisplayName = order.Employee.FirstName + " " + order.Employee.LastName,
                ItemCount = order.OrderDetails.Count
            })
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => new ConfectionOrderSummary(
                order.Id,
                order.CreatedAt,
                OrderNotesFormatter.ExtractCustomerDisplayName(order.Notes),
                order.EmployeeDisplayName,
                SalesDomainConstants.OrderChannelLabels.WorkshopApplication,
                order.Status,
                order.ItemCount,
                order.Total))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CashSaleResult> CompleteConfectionOrderAsync(
        CompleteConfectionOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync(PosPermission.OperarCaja, request.EmployeeId, cancellationToken);
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOrderException("La facturacion requiere empleado autenticado.");
        }

        try
        {
            return await PostgresTransientRetry.ExecuteAsync(
                retryToken => CompleteConfectionOrderOnceAsync(request, retryToken),
                (retry, _) => _logger.LogWarning(
                    "Conflicto transitorio al facturar confección para {OrderId}; reintento {Attempt}",
                    request.OrderId,
                    retry),
                cancellationToken);
        }
        catch (PostgresTransientOperationException exception)
        {
            _logger.LogError(exception, "Se agotaron los reintentos transitorios al facturar confección para {OrderId}", request.OrderId);
            throw new InvalidOrderException("Otra caja está vendiendo los mismos productos. Intente de nuevo.");
        }
    }

    private async Task<CashSaleResult> CompleteConfectionOrderOnceAsync(
        CompleteConfectionOrderRequest request,
        CancellationToken cancellationToken)
    {

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        await EnsureOpenCashSessionAsync(
            dbContext,
            request.CashSessionId,
            request.EmployeeId,
            cancellationToken);

        var order = await dbContext.Orders
            .Include(order => order.OrderDetails)
            .ThenInclude(detail => detail.Product)
            .ThenInclude(product => product.MeasurementType)
            .Include(order => order.InventoryMovements)
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(order => order.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidOrderException("Orden no encontrada.");
        }

        if (order.OrderType != SalesDomainConstants.OrderTypes.ConfectionWorkOrder)
        {
            throw new InvalidOrderException("La orden no es de confeccion.");
        }

        if (order.Status != SalesDomainConstants.OrderStatuses.Pending)
        {
            throw new InvalidOrderException("Solo se pueden facturar ordenes pendientes.");
        }

        ValidatePaymentTotals(request.Payments, order.Total);
        var lockedProducts = await LockProductsAsync(
            dbContext,
            order.OrderDetails.Select(detail => detail.ProductId),
            cancellationToken);
        var now = _clock.GetUtcNow().UtcDateTime;

        var existingPaymentIds = order.Payments.Select(payment => payment.Id).ToHashSet();
        var existingMovementIds = order.InventoryMovements.Select(movement => movement.Id).ToHashSet();

        foreach (var detail in order.OrderDetails)
        {
            DeductInventoryForCompletedSale(
                order,
                lockedProducts[detail.ProductId],
                detail.Quantity,
                request.EmployeeId,
                inventoryReason: "Facturacion de orden de confeccion",
                now);
        }

        AddPayments(order, request.Payments, request.CashSessionId, now);

        // La orden ya existe y está rastreada: los hijos nuevos traen Id asignado y EF los
        // trataría como filas existentes (UPDATE sin filas afectadas). Se registran como altas.
        dbContext.Payments.AddRange(
            order.Payments.Where(payment => !existingPaymentIds.Contains(payment.Id)).ToList());
        dbContext.InventoryMovements.AddRange(
            order.InventoryMovements.Where(movement => !existingMovementIds.Contains(movement.Id)).ToList());

        order.CashSessionId = request.CashSessionId;
        order.Status = SalesDomainConstants.OrderStatuses.Completed;
        order.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapToCashSaleResult(order);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalesOrderSummary>> GetCompletedSalesAsync(
        string? searchText,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var ordersQuery = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Employee)
            .Include(order => order.Payments)
            .Where(order => order.Status == SalesDomainConstants.OrderStatuses.Completed)
            .ApplySearchTextFilter(searchText);

        var orders = await ordersQuery
            .OrderByDescending(order => order.CreatedAt)
            .Take(take)
            .Select(order => new
            {
                order.Id,
                order.CreatedAt,
                order.Notes,
                order.OrderType,
                order.Status,
                order.Total,
                PaymentMethod = order.Payments
                    .OrderBy(payment => payment.CreatedAt)
                    .Select(payment => payment.Method)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => new SalesOrderSummary(
                order.Id,
                order.CreatedAt,
                OrderNotesFormatter.ExtractCustomerDisplayName(order.Notes),
                order.OrderType,
                string.IsNullOrWhiteSpace(order.PaymentMethod)
                    ? SalesDomainConstants.OrderChannelLabels.PaymentMethodNotAvailable
                    : order.PaymentMethod,
                order.Status,
                order.Total))
            .ToList();
    }

    private static async Task<Order?> FindOrderByClientRequestIdAsync(
        FerreteriaDbContext dbContext,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.OrderDetails)
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(order => order.ClientRequestId == clientRequestId, cancellationToken);
    }

    private async Task<Order?> FindOrderByClientRequestIdInNewScopeAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await FindOrderByClientRequestIdAsync(db, clientRequestId, cancellationToken);
    }

    private static void EnsureCashSaleMatches(Order existing, CreateCashSaleRequest request)
    {
        var linesMatch = existing.OrderDetails
            .OrderBy(line => line.ProductId)
            .ThenBy(line => line.Quantity)
            .Select(line => (line.ProductId, line.Quantity, line.Notes?.Trim()))
            .SequenceEqual(request.Lines
                .OrderBy(line => line.ProductId)
                .ThenBy(line => line.Quantity)
                .Select(line => (line.ProductId, line.Quantity, line.Notes?.Trim())));
        var paymentsMatch = existing.Payments
            .OrderBy(payment => payment.Method)
            .ThenBy(payment => payment.Amount)
            .Select(payment => (payment.Method, payment.Amount, payment.Reference?.Trim()))
            .SequenceEqual(request.Payments
                .OrderBy(payment => payment.Method.Trim().ToUpperInvariant())
                .ThenBy(payment => payment.Amount)
                .Select(payment => (payment.Method.Trim().ToUpperInvariant(), payment.Amount, payment.Reference?.Trim())));

        if (existing.OrderType != SalesDomainConstants.OrderTypes.CashRegisterSale
            || existing.EmployeeId != request.EmployeeId
            || existing.CashSessionId != request.CashSessionId
            || existing.CustomerId != request.CustomerId
            || !string.Equals(existing.Notes?.Trim(), request.Notes?.Trim(), StringComparison.Ordinal)
            || !linesMatch
            || !paymentsMatch)
        {
            throw new InvalidOrderException("Esta solicitud ya se registró con otro contenido. Inicie una venta nueva.");
        }
    }

    private static void EnsureConfectionOrderMatches(Order existing, CreateConfectionOrderRequest request)
    {
        var expectedNotes = OrderNotesFormatter.BuildConfectionOrderNotes(request.CustomerName, request.CustomerPhone, request.Notes);
        var linesMatch = existing.OrderDetails
            .OrderBy(line => line.ProductId)
            .ThenBy(line => line.Quantity)
            .Select(line => (line.ProductId, line.Quantity, line.Notes?.Trim()))
            .SequenceEqual(request.Lines
                .OrderBy(line => line.ProductId)
                .ThenBy(line => line.Quantity)
                .Select(line => (line.ProductId, line.Quantity, line.Notes?.Trim())));

        if (existing.OrderType != SalesDomainConstants.OrderTypes.ConfectionWorkOrder
            || existing.EmployeeId != request.EmployeeId
            || existing.CustomerId != request.CustomerId
            || !string.Equals(existing.Notes, expectedNotes, StringComparison.Ordinal)
            || !linesMatch)
        {
            throw new InvalidOrderException("Esta solicitud ya se registró con otro contenido. Inicie una orden nueva.");
        }
    }

    private static async Task<Dictionary<Guid, Product>> LockProductsAsync(
        FerreteriaDbContext dbContext,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Where(id => id != Guid.Empty).Distinct().ToArray(); // El orden de bloqueo lo define PostgreSQL (ORDER BY "id"), no C#: Guid y uuid ordenan distinto.
        var products = await dbContext.Products
            .FromSqlInterpolated($"SELECT * FROM public.\"Products\" WHERE \"id\" = ANY({ids}) ORDER BY \"id\" FOR UPDATE")
            .Include(product => product.MeasurementType)
            .ToListAsync(cancellationToken);
        return products.ToDictionary(product => product.Id);
    }

    /// <summary>
    /// Comprueba dentro de la transacción que el cobro usa una sesión abierta de la caja configurada.
    /// </summary>
    /// <param name="dbContext">Contexto de datos de la operación.</param>
    /// <param name="cashSessionId">Sesión propuesta por la UI.</param>
    /// <param name="employeeId">Empleado que intenta cobrar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <exception cref="InvalidOrderException">Si falta, no existe, está cerrada o pertenece a otra caja.</exception>
    /// <remarks>
    /// La consulta ocurre después de abrir la transacción Serializable y antes de crear orden, detalle o pago.
    /// Así una UI con estado antiguo no puede persistir un cobro en una sesión cerrada o de otra caja.
    /// </remarks>
    private async Task EnsureOpenCashSessionAsync(
        FerreteriaDbContext dbContext,
        Guid? cashSessionId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        if (cashSessionId is not Guid sessionId || sessionId == Guid.Empty)
        {
            throw new InvalidOrderException("No se puede cobrar sin una sesión de caja abierta.");
        }

        // Fila padre primero: FOR SHARE permite ventas concurrentes en la sesión, pero impide que un cierre
        // (FOR UPDATE) cambie su estado hasta el commit. Después se bloquean los productos.
        var session = await dbContext.CashSessions
            .FromSqlInterpolated($"SELECT * FROM sales.\"CashSessions\" WHERE \"id\" = {sessionId} FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            throw new InvalidOrderException("La sesión de caja no existe. Abra la caja antes de cobrar.");
        }

        if (session.Status != SalesDomainConstants.CashSessionStatuses.Open)
        {
            throw new InvalidOrderException("La sesión de caja ya está cerrada. Abra una nueva sesión antes de cobrar.");
        }

        var configuredCode = CashRegisterInputRules.ValidateCashRegisterCode(_cashRegisterOptions.Codigo);
        if (session.CashRegisterCode != configuredCode)
        {
            throw new InvalidOrderException("La sesión pertenece a otra caja.");
        }

        if (session.EmployeeId != employeeId)
        {
            throw new InvalidOrderException("La sesión de caja pertenece a otro cajero autenticado.");
        }
    }

    private static async Task AddSaleLineAsync(
        FerreteriaDbContext dbContext,
        Order order,
        CashSaleLineRequest line,
        IReadOnlyDictionary<Guid, Product> products,
        Guid employeeId,
        string inventoryReason,
        CancellationToken cancellationToken,
        DateTime now)
    {
        if (!products.TryGetValue(line.ProductId, out var product))
        {
            throw new ProductNotFoundException(line.ProductId);
        }
        InventoryQuantityValidator.ValidatePositiveQuantity(line.Quantity, product.MeasurementType.Decimals);

        var lineSubtotal = Math.Round(product.SalePrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
        order.Subtotal += lineSubtotal;

        order.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = line.Quantity,
            UnitPrice = product.SalePrice,
            UnitCost = product.CostPrice,
            Subtotal = lineSubtotal,
            Notes = line.Notes
        });

        DeductInventoryForCompletedSale(order, product, line.Quantity, employeeId, inventoryReason, now);
    }

    private static async Task AddPendingWorkOrderLineAsync(
        FerreteriaDbContext dbContext,
        Order order,
        CashSaleLineRequest line,
        CancellationToken cancellationToken)
    {
        var product = await LoadActiveProductAsync(dbContext, line.ProductId, cancellationToken);
        InventoryQuantityValidator.ValidatePositiveQuantity(line.Quantity, product.MeasurementType.Decimals);

        var lineSubtotal = Math.Round(product.SalePrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
        order.Subtotal += lineSubtotal;

        order.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = line.Quantity,
            UnitPrice = product.SalePrice,
            UnitCost = product.CostPrice,
            Subtotal = lineSubtotal,
            Notes = line.Notes
        });
    }

    private static async Task<Product> LoadActiveProductAsync(
        FerreteriaDbContext dbContext,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(product => product.MeasurementType)
            .FirstOrDefaultAsync(
                product => product.Id == productId && product.IsActive,
                cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException(productId);
        }

        return product;
    }

    private static void DeductInventoryForCompletedSale(
        Order order,
        Product product,
        decimal quantity,
        Guid employeeId,
        string inventoryReason,
        DateTime now)
    {
        var stockBefore = product.CurrentStock;
        if (stockBefore < quantity)
        {
            throw new InsufficientStockException(product.Code, quantity, stockBefore);
        }

        var stockAfter = stockBefore - quantity;
        product.CurrentStock = stockAfter;
        product.UpdatedAt = now;

        order.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = SalesDomainConstants.InventoryMovementTypes.SaleOutflow,
            Quantity = quantity,
            UnitCost = product.CostPrice,
            TotalCost = product.CostPrice * quantity,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            EmployeeId = employeeId,
            Reason = inventoryReason,
            CreatedAt = now
        });
    }

    private static void ApplyTaxTotals(Order order)
    {
        order.TaxAmount = TaxAmountCalculator.CalculateTaxAmount(order.Subtotal);
        order.Total = TaxAmountCalculator.CalculateGrandTotal(order.Subtotal);
    }

    private static void AddPayments(
        Order order,
        IReadOnlyList<CashSalePaymentRequest> payments,
        Guid? cashSessionId,
        DateTime now)
    {
        foreach (var payment in payments)
        {
            order.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                CashSessionId = cashSessionId,
                Method = payment.Method.Trim().ToUpperInvariant(),
                Amount = payment.Amount,
                Reference = payment.Reference,
                CreatedAt = now
            });
        }
    }

    private static CashSaleResult MapToCashSaleResult(Order order)
    {
        return new CashSaleResult(
            order.Id,
            order.ClientRequestId,
            order.Subtotal,
            order.TaxAmount,
            order.Total);
    }

    private static WorkOrderResult MapToWorkOrderResult(Order order)
    {
        return new WorkOrderResult(
            order.Id,
            order.ClientRequestId,
            order.Subtotal,
            order.TaxAmount,
            order.Total);
    }

    private static void ValidateCashSaleRequest(CreateCashSaleRequest request)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOrderException("La venta requiere empleado autenticado.");
        }

        if (request.Lines.Count == 0)
        {
            throw new InvalidOrderException("La venta debe tener al menos un producto.");
        }

        if (request.Payments.Count == 0)
        {
            throw new InvalidOrderException("La venta requiere al menos un pago.");
        }
    }

    private static void ValidateConfectionOrderRequest(CreateConfectionOrderRequest request)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOrderException("La orden requiere empleado autenticado.");
        }

        if (request.Lines.Count == 0)
        {
            throw new InvalidOrderException("La orden debe tener al menos un producto.");
        }
    }

    private static void ValidatePaymentTotals(IReadOnlyList<CashSalePaymentRequest> payments, decimal expectedTotal)
    {
        if (payments.Any(payment => payment.Amount <= 0))
        {
            throw new InvalidOrderException("Todos los pagos deben ser mayores que cero.");
        }

        var paidTotal = payments.Sum(payment => payment.Amount);
        if (Math.Round(paidTotal, 2, MidpointRounding.AwayFromZero) != expectedTotal)
        {
            throw new InvalidOrderException("La suma de pagos debe coincidir con el total de la venta.");
        }
    }

    private async Task RequirePermissionAsync(
        PosPermission permission,
        Guid actingEmployeeId,
        CancellationToken cancellationToken)
    {
        await _authorizationGuard.RequireAsync(permission, actingEmployeeId, cancellationToken);
    }

    private static bool IsOrderClientRequestUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(postgres.ConstraintName, "IdxOrdersClientRequest", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class PostgresUniqueRequestException(Exception innerException) : Exception(
        "La solicitud de venta ya fue insertada por otra operación.", innerException);
}
