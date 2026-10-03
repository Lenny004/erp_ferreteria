using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Valores de la cabecera que se insertará en <c>sales."Returns"</c>.</summary>
/// <param name="OrderId">Columna <c>OrderId</c>.</param>
/// <param name="CashSessionId">Columna <c>CashSessionId</c>, nullable.</param>
/// <param name="EmployeeId">Columna <c>EmployeeId</c>.</param>
/// <param name="AuthorizedByEmployeeId">Columna <c>AuthorizedByEmployeeId</c>.</param>
/// <param name="ClientRequestId">Columna <c>ClientRequestId</c> única.</param>
/// <param name="ReturnType">Columna <c>ReturnType</c>.</param>
/// <param name="Status">Columna <c>status</c>; siempre COMPLETADA en esta fase.</param>
/// <param name="FiscalStatus">Columna <c>FiscalStatus</c>.</param>
/// <param name="CreditNoteDteId">Columna <c>CreditNoteDteId</c>; null en esta fase.</param>
/// <param name="ReasonCode">Columna <c>ReasonCode</c>.</param>
/// <param name="Notes">Columna <c>notes</c>.</param>
/// <param name="Subtotal">Columna <c>subtotal</c>.</param>
/// <param name="DiscountAmount">Columna <c>DiscountAmount</c>.</param>
/// <param name="TaxAmount">Columna <c>TaxAmount</c>.</param>
/// <param name="Total">Columna <c>total</c>.</param>
/// <param name="RefundMethod">Columna <c>RefundMethod</c>.</param>
/// <param name="RefundAmount">Columna <c>RefundAmount</c>.</param>
/// <param name="CreatedAt">Columna <c>CreatedAt</c>.</param>
/// <param name="UpdatedAt">Columna <c>UpdatedAt</c>.</param>
public sealed record ReturnHeaderRecord(
    Guid OrderId,
    Guid? CashSessionId,
    Guid EmployeeId,
    Guid AuthorizedByEmployeeId,
    Guid ClientRequestId,
    string ReturnType,
    string Status,
    string FiscalStatus,
    Guid? CreditNoteDteId,
    string ReasonCode,
    string? Notes,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    string RefundMethod,
    decimal RefundAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Valores de detalle que se insertarán en <c>sales."ReturnDetails"</c>.</summary>
/// <param name="OrderDetailId">Columna <c>OrderDetailId</c>.</param>
/// <param name="ProductId">Columna <c>ProductId</c>.</param>
/// <param name="Quantity">Columna <c>quantity</c>, en la unidad de la venta.</param>
/// <param name="UnitsPerPackage">Columna <c>UnitsPerPackage</c>.</param>
/// <param name="UnitPrice">Columna <c>UnitPrice</c>.</param>
/// <param name="UnitCost">Columna <c>UnitCost</c>.</param>
/// <param name="DiscountAmount">Columna <c>DiscountAmount</c>.</param>
/// <param name="Subtotal">Columna <c>subtotal</c>.</param>
/// <param name="TaxAmount">Columna <c>TaxAmount</c>.</param>
/// <param name="Restocked">Columna <c>Restocked</c>.</param>
/// <param name="RestockQuantity">Columna <c>RestockQuantity</c>.</param>
/// <param name="InventoryMovementId">Columna <c>InventoryMovementId</c>, nullable.</param>
/// <param name="CreatedAt">Columna <c>CreatedAt</c>.</param>
public sealed record ReturnDetailRecord(
    Guid OrderDetailId,
    Guid ProductId,
    decimal Quantity,
    decimal UnitsPerPackage,
    decimal UnitPrice,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal Subtotal,
    decimal TaxAmount,
    bool Restocked,
    decimal RestockQuantity,
    Guid? InventoryMovementId,
    DateTime CreatedAt);

/// <summary>Valores de movimiento que se insertarán en <c>sales."CashMovements"</c>.</summary>
/// <param name="CashSessionId">Columna <c>CashSessionId</c>.</param>
/// <param name="MovementType">Columna <c>MovementType</c>; siempre DEVOLUCION_EFECTIVO.</param>
/// <param name="EmployeeId">Columna <c>EmployeeId</c>.</param>
/// <param name="AuthorizedByEmployeeId">Columna <c>AuthorizedByEmployeeId</c>, nullable.</param>
/// <param name="ClientRequestId">Columna <c>ClientRequestId</c> única.</param>
/// <param name="Amount">Columna <c>amount</c>, estrictamente positiva.</param>
/// <param name="Reason">Columna <c>reason</c>, máximo 300 caracteres.</param>
/// <param name="CreatedAt">Columna <c>CreatedAt</c>.</param>
/// <param name="ReturnId">Columna <c>ReturnId</c>; el writer la liga a la devolución que genere.</param>
public sealed record CashMovementRecord(
    Guid CashSessionId,
    string MovementType,
    Guid EmployeeId,
    Guid? AuthorizedByEmployeeId,
    Guid ClientRequestId,
    decimal Amount,
    string Reason,
    DateTime CreatedAt,
    Guid? ReturnId = null);

/// <summary>Movimiento de kardex previsto para <c>public."InventoryMovements"</c>.</summary>
/// <remarks>El costo original y la unidad de cantidad son provisionales, a verificar con contador.</remarks>
/// <param name="Id">Identificador que el writer usará para ligar el detalle.</param>
/// <param name="ProductId">Producto afectado.</param>
/// <param name="MovementType">Columna <c>MovementType</c>; siempre ENTRADA_DEVOLUCION.</param>
/// <param name="OrderId">Orden original asociada.</param>
/// <param name="EmployeeId">Empleado que ejecuta.</param>
/// <param name="Quantity">Cantidad en la unidad descontada por la venta.</param>
/// <param name="UnitCost">Costo original de <c>OrderDetails.UnitCost</c>.</param>
/// <param name="Reason">Motivo legible del movimiento.</param>
/// <param name="CreatedAt">Fecha UTC del movimiento.</param>
public sealed record InventoryMovementRecord(Guid Id, Guid ProductId, string MovementType, Guid OrderId, Guid EmployeeId, decimal Quantity, decimal UnitCost, string Reason, DateTime CreatedAt);

/// <summary>Conjunto inmutable de inserciones atómicas de una devolución.</summary>
/// <param name="Header">Cabecera de <c>sales."Returns"</c>.</param>
/// <param name="Details">Detalles de <c>sales."ReturnDetails"</c>.</param>
/// <param name="InventoryMovements">Líneas de kardex de reingreso.</param>
/// <param name="CashMovement">Movimiento de efectivo o <c>null</c> si no aplica.</param>
public sealed record ReturnPersistenceRecord(
    ReturnHeaderRecord Header,
    IReadOnlyList<ReturnDetailRecord> Details,
    IReadOnlyList<InventoryMovementRecord> InventoryMovements,
    CashMovementRecord? CashMovement);

/// <summary>Persistencia transaccional de devoluciones, inventario, caja y auditoría.</summary>
public interface IReturnWriter
{
    /// <summary>Indica si la implementación puede escribir las tablas de devoluciones.</summary>
    bool IsAvailable { get; }

    /// <summary>Busca una devolución ya confirmada por su idempotency key.</summary>
    /// <param name="db">Contexto que participa en la transacción activa.</param>
    /// <param name="clientRequestId">Identificador único de la solicitud.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado existente o <c>null</c>.</returns>
    Task<ReturnResult?> FindByClientRequestIdAsync(FerreteriaDbContext db, Guid clientRequestId, CancellationToken cancellationToken = default);

    /// <summary>Inserta devolución, kardex, stock, caja y auditoría atómicamente.</summary>
    /// <param name="db">Contexto dentro de la transacción Serializable del servicio.</param>
    /// <param name="record">Valores preparados para insertar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Identificador generado para la devolución.</returns>
    /// <exception cref="ReturnsUnavailableException">Si la persistencia configurada no está disponible.</exception>
    /// <remarks>La implementación conserva la misma transacción y liga CashMovement a ReturnId, nunca a OrderId.</remarks>
    Task<Guid> PersistAsync(FerreteriaDbContext db, ReturnPersistenceRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Writer EF Core para la persistencia atómica de una devolución.</summary>
public sealed class EfReturnWriter : IReturnWriter
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public async Task<ReturnResult?> FindByClientRequestIdAsync(FerreteriaDbContext db, Guid clientRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saleReturn = await db.Returns.AsNoTracking()
            .Include(item => item.ReturnDetails)
            .SingleOrDefaultAsync(item => item.ClientRequestId == clientRequestId, cancellationToken);
        return saleReturn is null ? null : ToResult(saleReturn);
    }

    /// <inheritdoc />
    public async Task<Guid> PersistAsync(FerreteriaDbContext db, ReturnPersistenceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(record);
        var returnId = Guid.NewGuid();
        var saleReturn = new SaleReturn
        {
            Id = returnId,
            OrderId = record.Header.OrderId,
            CashSessionId = record.Header.CashSessionId,
            EmployeeId = record.Header.EmployeeId,
            AuthorizedByEmployeeId = record.Header.AuthorizedByEmployeeId,
            ClientRequestId = record.Header.ClientRequestId,
            ReturnType = record.Header.ReturnType,
            Status = record.Header.Status,
            FiscalStatus = record.Header.FiscalStatus,
            CreditNoteDteId = record.Header.CreditNoteDteId,
            ReasonCode = record.Header.ReasonCode,
            Notes = record.Header.Notes,
            Subtotal = record.Header.Subtotal,
            DiscountAmount = record.Header.DiscountAmount,
            TaxAmount = record.Header.TaxAmount,
            Total = record.Header.Total,
            RefundMethod = record.Header.RefundMethod,
            RefundAmount = record.Header.RefundAmount,
            CreatedAt = record.Header.CreatedAt,
            UpdatedAt = record.Header.UpdatedAt
        };

        var productIds = record.InventoryMovements.Select(movement => movement.ProductId).Distinct().ToArray(); // Orden de bloqueo: ORDER BY "id" en PostgreSQL, no en C#.
        var lockedProducts = await db.Products
            .FromSqlInterpolated($"SELECT * FROM public.\"Products\" WHERE \"id\" = ANY({productIds}) ORDER BY \"id\" FOR UPDATE")
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var movement in record.InventoryMovements)
        {
            var product = lockedProducts[movement.ProductId];
            var stockBefore = product.CurrentStock;
            product.CurrentStock += movement.Quantity;
            product.UpdatedAt = movement.CreatedAt;
            var inventoryMovement = new InventoryMovement
            {
                Id = movement.Id,
                ProductId = movement.ProductId,
                MovementType = movement.MovementType,
                OrderId = movement.OrderId,
                EmployeeId = movement.EmployeeId,
                Quantity = movement.Quantity,
                UnitCost = movement.UnitCost,
                TotalCost = Math.Round(movement.Quantity * movement.UnitCost, 4, MidpointRounding.AwayFromZero),
                StockBefore = stockBefore,
                StockAfter = product.CurrentStock,
                Reason = movement.Reason,
                CreatedAt = movement.CreatedAt
            };
            db.InventoryMovements.Add(inventoryMovement);
        }

        foreach (var detail in record.Details)
        {
            saleReturn.ReturnDetails.Add(new SaleReturnDetail
            {
                Id = Guid.NewGuid(),
                ReturnId = returnId,
                OrderDetailId = detail.OrderDetailId,
                ProductId = detail.ProductId,
                Quantity = detail.Quantity,
                UnitsPerPackage = detail.UnitsPerPackage,
                UnitPrice = detail.UnitPrice,
                UnitCost = detail.UnitCost,
                DiscountAmount = detail.DiscountAmount,
                Subtotal = detail.Subtotal,
                TaxAmount = detail.TaxAmount,
                Restocked = detail.Restocked,
                RestockQuantity = detail.RestockQuantity,
                InventoryMovementId = detail.InventoryMovementId,
                CreatedAt = detail.CreatedAt
            });
        }

        db.Returns.Add(saleReturn);
        if (record.CashMovement is not null)
        {
            var cash = record.CashMovement;
            db.CashMovements.Add(new CashMovement
            {
                Id = Guid.NewGuid(),
                CashSessionId = cash.CashSessionId,
                MovementType = cash.MovementType,
                Amount = cash.Amount,
                ReturnId = returnId,
                EmployeeId = cash.EmployeeId,
                AuthorizedByEmployeeId = cash.AuthorizedByEmployeeId,
                ClientRequestId = cash.ClientRequestId,
                Reason = cash.Reason,
                CreatedAt = cash.CreatedAt
            });
        }

        db.AuditLogs.Add(AuditService.CreateChangeEntry(
            ReturnAuditActions.Return,
            ReturnAuditActions.ReturnsTableName,
            returnId.ToString(),
            null,
            new
            {
                Evento = ReturnAuditActions.ReturnEvent,
                saleReturn.EmployeeId,
                saleReturn.AuthorizedByEmployeeId,
                saleReturn.Total,
                saleReturn.RefundMethod,
                Lineas = record.Details.Select(detail => new { detail.OrderDetailId, detail.Quantity, Total = detail.Subtotal - detail.DiscountAmount + detail.TaxAmount }).ToArray()
            },
            saleReturn.EmployeeId));

        if (record.CashMovement is not null)
        {
            db.AuditLogs.Add(AuditService.CreateChangeEntry(
                ReturnAuditActions.Refund,
                ReturnAuditActions.CashMovementsTableName,
                returnId.ToString(),
                null,
                new
                {
                    Evento = ReturnAuditActions.RefundEvent,
                    saleReturn.EmployeeId,
                    saleReturn.AuthorizedByEmployeeId,
                    Amount = record.CashMovement.Amount,
                    ReturnId = returnId
                },
                saleReturn.EmployeeId));
        }

        await db.SaveChangesAsync(cancellationToken);
        return returnId;
    }

    private static ReturnResult ToResult(SaleReturn saleReturn)
    {
        var lines = saleReturn.ReturnDetails.Select(detail => new ReturnCreditLine(
            detail.OrderDetailId,
            detail.Quantity,
            detail.Subtotal,
            detail.DiscountAmount,
            detail.TaxAmount,
            detail.Subtotal - detail.DiscountAmount + detail.TaxAmount,
            detail.Restocked ? Math.Round(detail.UnitCost * detail.RestockQuantity, 4, MidpointRounding.AwayFromZero) : 0m,
            detail.RestockQuantity,
            detail.UnitPrice,
            detail.UnitsPerPackage,
            detail.UnitCost,
            detail.Restocked)).ToArray();
        var calculation = new ReturnCalculationResult(lines, saleReturn.Subtotal, saleReturn.DiscountAmount, saleReturn.TaxAmount, saleReturn.Total, lines.Sum(line => line.RestockCost), saleReturn.ReturnType);
        var fiscal = new ReturnFiscalDecision(saleReturn.FiscalStatus, null, "Devolución recuperada desde la base de datos.");
        return new ReturnResult(
            saleReturn.ClientRequestId,
            saleReturn.OrderId,
            calculation,
            fiscal,
            saleReturn.Id,
            saleReturn.AuthorizedByEmployeeId,
            saleReturn.RefundMethod,
            saleReturn.RefundAmount,
            saleReturn.EmployeeId);
    }
}
