using System.Data;
using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Services;

public sealed class OrderService(IServiceScopeFactory scopeFactory) : IOrderService
{
    private const decimal IvaRate = 0.13m;

    public async Task<CashSaleResult> CreateCashSaleAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();
        var clientRequestId = request.ClientRequestId ?? Guid.NewGuid();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingOrder = await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ClientRequestId == clientRequestId, cancellationToken);

        if (existingOrder is not null)
        {
            return new CashSaleResult(
                existingOrder.Id,
                existingOrder.ClientRequestId,
                existingOrder.Subtotal,
                existingOrder.TaxAmount,
                existingOrder.Total);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            CashSessionId = request.CashSessionId,
            CustomerId = request.CustomerId,
            ClientRequestId = clientRequestId,
            OrderType = "VENTA_CAJA",
            Status = "COMPLETADA",
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var line in request.Lines)
        {
            var product = await dbContext.Products
                .Include(p => p.MeasurementType)
                .FirstOrDefaultAsync(p => p.Id == line.ProductId && p.IsActive, cancellationToken);

            if (product is null)
            {
                throw new ProductNotFoundException(line.ProductId);
            }

            ValidateQuantity(line.Quantity, product.MeasurementType.Decimals);

            var stockBefore = product.CurrentStock;
            if (stockBefore < line.Quantity)
            {
                throw new InsufficientStockException(product.Code, line.Quantity, stockBefore);
            }

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

            var stockAfter = stockBefore - line.Quantity;
            product.CurrentStock = stockAfter;
            product.UpdatedAt = DateTime.UtcNow;

            order.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                MovementType = "SALIDA_VENTA",
                Quantity = line.Quantity,
                UnitCost = product.CostPrice,
                TotalCost = product.CostPrice * line.Quantity,
                StockBefore = stockBefore,
                StockAfter = stockAfter,
                EmployeeId = request.EmployeeId,
                Reason = "Venta de caja",
                CreatedAt = DateTime.UtcNow
            });
        }

        order.TaxAmount = Math.Round(order.Subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
        order.Total = order.Subtotal + order.TaxAmount;
        ValidatePayments(request.Payments, order.Total);

        foreach (var payment in request.Payments)
        {
            order.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                CashSessionId = request.CashSessionId,
                Method = payment.Method.Trim().ToUpperInvariant(),
                Amount = payment.Amount,
                Reference = payment.Reference,
                CreatedAt = DateTime.UtcNow
            });
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CashSaleResult(order.Id, order.ClientRequestId, order.Subtotal, order.TaxAmount, order.Total);
    }

    public async Task<WorkOrderResult> CreateConfectionOrderAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOrderException("La orden requiere empleado autenticado.");
        }

        if (request.Lines.Count == 0)
        {
            throw new InvalidOrderException("La orden debe tener al menos un producto.");
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();
        var clientRequestId = request.ClientRequestId ?? Guid.NewGuid();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingOrder = await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ClientRequestId == clientRequestId, cancellationToken);

        if (existingOrder is not null)
        {
            return new WorkOrderResult(
                existingOrder.Id,
                existingOrder.ClientRequestId,
                existingOrder.Subtotal,
                existingOrder.TaxAmount,
                existingOrder.Total);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            CustomerId = request.CustomerId,
            ClientRequestId = clientRequestId,
            OrderType = "ORDEN_CONFECCION",
            Status = "PENDIENTE",
            Notes = BuildConfectionNotes(request),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var line in request.Lines)
        {
            var product = await dbContext.Products
                .Include(p => p.MeasurementType)
                .FirstOrDefaultAsync(p => p.Id == line.ProductId && p.IsActive, cancellationToken);

            if (product is null)
            {
                throw new ProductNotFoundException(line.ProductId);
            }

            ValidateQuantity(line.Quantity, product.MeasurementType.Decimals);

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

        order.TaxAmount = Math.Round(order.Subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
        order.Total = order.Subtotal + order.TaxAmount;

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new WorkOrderResult(order.Id, order.ClientRequestId, order.Subtotal, order.TaxAmount, order.Total);
    }

    private static string? BuildConfectionNotes(CreateConfectionOrderRequest request)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(request.CustomerName) ? null : $"Cliente: {request.CustomerName.Trim()}",
            string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : $"Telefono: {request.CustomerPhone.Trim()}",
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        var notes = string.Join(" | ", parts.Where(p => p is not null));
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
    }

    private static void ValidateRequest(CreateCashSaleRequest request)
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

    private static void ValidatePayments(IReadOnlyList<CashSalePaymentRequest> payments, decimal total)
    {
        var paidTotal = payments.Sum(p => p.Amount);
        if (payments.Any(p => p.Amount <= 0))
        {
            throw new InvalidOrderException("Todos los pagos deben ser mayores que cero.");
        }

        if (Math.Round(paidTotal, 2, MidpointRounding.AwayFromZero) != total)
        {
            throw new InvalidOrderException("La suma de pagos debe coincidir con el total de la venta.");
        }
    }

    private static void ValidateQuantity(decimal quantity, short allowedDecimals)
    {
        if (quantity <= 0)
        {
            throw new InvalidInventoryQuantityException("La cantidad debe ser mayor que cero.");
        }

        var rounded = Math.Round(quantity, allowedDecimals, MidpointRounding.AwayFromZero);
        if (quantity != rounded)
        {
            throw new InvalidInventoryQuantityException($"La cantidad permite maximo {allowedDecimals} decimales.");
        }
    }
}
