using Ferreteria.PuntoVenta.Data;

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
/// <param name="EmployeeId">Columna <c>EmployeeId</c>.</param>
/// <param name="AuthorizedByEmployeeId">Columna <c>AuthorizedByEmployeeId</c>, nullable.</param>
/// <param name="ClientRequestId">Columna <c>ClientRequestId</c> única.</param>
/// <param name="Amount">Columna <c>amount</c>, estrictamente positiva.</param>
/// <param name="Reason">Columna <c>reason</c>, máximo 300 caracteres.</param>
/// <param name="CreatedAt">Columna <c>CreatedAt</c>.</param>
/// <param name="ReturnId">Columna <c>ReturnId</c>; el writer la liga a la devolución que genere.</param>
public sealed record CashMovementRecord(
    Guid CashSessionId,
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
/// <param name="OrderId">Orden original asociada.</param>
/// <param name="EmployeeId">Empleado que ejecuta.</param>
/// <param name="Quantity">Cantidad en la unidad descontada por la venta.</param>
/// <param name="UnitCost">Costo original de <c>OrderDetails.UnitCost</c>.</param>
/// <param name="Reason">Motivo legible del movimiento.</param>
/// <param name="CreatedAt">Fecha UTC del movimiento.</param>
public sealed record InventoryMovementRecord(Guid Id, Guid ProductId, Guid OrderId, Guid EmployeeId, decimal Quantity, decimal UnitCost, string Reason, DateTime CreatedAt);

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

/// <summary>Persistencia transaccional desacoplada de las tablas que aún no existen en desarrollo.</summary>
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
    /// <exception cref="ReturnsUnavailableException">Si la migración aún no está disponible.</exception>
    /// <remarks>La implementación futura debe conservar la misma transacción y ligar CashMovement a ReturnId, nunca a OrderId.</remarks>
    Task<Guid> PersistAsync(FerreteriaDbContext db, ReturnPersistenceRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Writer inerte hasta que la migración de devoluciones llegue al entorno.</summary>
public sealed class PendingMigrationReturnWriter : IReturnWriter
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public Task<ReturnResult?> FindByClientRequestIdAsync(FerreteriaDbContext db, Guid clientRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        return Task.FromResult<ReturnResult?>(null);
    }

    /// <inheritdoc />
    public Task<Guid> PersistAsync(FerreteriaDbContext db, ReturnPersistenceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        // TODO: persistir Returns, ReturnDetails, kardex, stock, CashMovements y AuditLog (depende de ferreteria_backend).
        throw new ReturnsUnavailableException("La persistencia de devoluciones todavía no está disponible.");
    }
}
