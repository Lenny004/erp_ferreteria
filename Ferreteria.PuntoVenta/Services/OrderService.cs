using System.Data;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Implementación transaccional de ventas y órdenes de confección.
/// Usa aislamiento <see cref="IsolationLevel.Serializable"/> en operaciones que modifican stock.
/// </summary>
public sealed class OrderService(IServiceScopeFactory scopeFactory) : IOrderService
{
    /// <inheritdoc />
    public async Task<CashSaleResult> CreateCashSaleAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCashSaleRequest(request);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var clientRequestId = request.ClientRequestId ?? Guid.NewGuid();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingOrder = await FindOrderByClientRequestIdAsync(dbContext, clientRequestId, cancellationToken);
        if (existingOrder is not null)
        {
            return MapToCashSaleResult(existingOrder);
        }

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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var line in request.Lines)
        {
            await AddSaleLineAsync(
                dbContext,
                order,
                line,
                request.EmployeeId,
                inventoryReason: "Venta de caja",
                cancellationToken);
        }

        ApplyTaxTotals(order);
        ValidatePaymentTotals(request.Payments, order.Total);
        AddPayments(order, request.Payments, request.CashSessionId);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapToCashSaleResult(order);
    }

    /// <inheritdoc />
    public async Task<WorkOrderResult> CreateConfectionOrderAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfectionOrderRequest(request);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var clientRequestId = request.ClientRequestId ?? Guid.NewGuid();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingOrder = await FindOrderByClientRequestIdAsync(dbContext, clientRequestId, cancellationToken);
        if (existingOrder is not null)
        {
            return MapToWorkOrderResult(existingOrder);
        }

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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var line in request.Lines)
        {
            await AddPendingWorkOrderLineAsync(dbContext, order, line, cancellationToken);
        }

        ApplyTaxTotals(order);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
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

        using var scope = scopeFactory.CreateScope();
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
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOrderException("La facturacion requiere empleado autenticado.");
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
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

        foreach (var detail in order.OrderDetails)
        {
            DeductInventoryForCompletedSale(
                order,
                detail.Product,
                detail.Quantity,
                request.EmployeeId,
                inventoryReason: "Facturacion de orden de confeccion");
        }

        AddPayments(order, request.Payments, request.CashSessionId);
        order.Status = SalesDomainConstants.OrderStatuses.Completed;
        order.UpdatedAt = DateTime.UtcNow;

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

        using var scope = scopeFactory.CreateScope();
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
            .FirstOrDefaultAsync(order => order.ClientRequestId == clientRequestId, cancellationToken);
    }

    private static async Task AddSaleLineAsync(
        FerreteriaDbContext dbContext,
        Order order,
        CashSaleLineRequest line,
        Guid employeeId,
        string inventoryReason,
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

        DeductInventoryForCompletedSale(order, product, line.Quantity, employeeId, inventoryReason);
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
        string inventoryReason)
    {
        var stockBefore = product.CurrentStock;
        if (stockBefore < quantity)
        {
            throw new InsufficientStockException(product.Code, quantity, stockBefore);
        }

        var stockAfter = stockBefore - quantity;
        product.CurrentStock = stockAfter;
        product.UpdatedAt = DateTime.UtcNow;

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
            CreatedAt = DateTime.UtcNow
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
        Guid? cashSessionId)
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
                CreatedAt = DateTime.UtcNow
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
}
