using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.SalesHistory;

/// <summary>Fila resumida del historial de ventas.</summary>
/// <param name="OrderId">Identificador de la orden.</param>
/// <param name="CreatedAtUtc">Fecha de creación de la orden en UTC.</param>
/// <param name="CustomerDisplayName">Nombre del cliente para mostrar.</param>
/// <param name="EmployeeDisplayName">Nombre del empleado que registró la orden.</param>
/// <param name="OrderType">Tipo persistido de orden.</param>
/// <param name="PaymentMethod">Primer método de pago registrado.</param>
/// <param name="Status">Estado persistido de la orden.</param>
/// <param name="DteType">Tipo del DTE más reciente, si existe.</param>
/// <param name="MhStatus">Estado del Ministerio de Hacienda más reciente, si existe.</param>
/// <param name="ControlNumber">Número de control real del DTE más reciente, si existe.</param>
/// <param name="Total">Total persistido de la orden.</param>
/// <param name="Reprints">Cantidad de eventos de reimpresión auditados.</param>
public sealed record SalesHistoryRow(
    Guid OrderId,
    DateTime CreatedAtUtc,
    string CustomerDisplayName,
    string EmployeeDisplayName,
    string OrderType,
    string PaymentMethod,
    string Status,
    string? DteType,
    string? MhStatus,
    string? ControlNumber,
    decimal Total,
    int Reprints)
{
    /// <summary>Fecha de la venta en la zona local del POS.</summary>
    public string DateText => TimeZoneSupport.ToElSalvadorTime(CreatedAtUtc).ToString("dd/MM HH:mm");

    /// <summary>Total formateado para la vista.</summary>
    public string TotalText => Total.ToString("C2");

    /// <summary>Número de control real o identificador interno claramente marcado.</summary>
    public string ControlNumberText => SalesHistoryNumberFormatter.FormatControlNumber(ControlNumber, OrderId);

    /// <summary>Canal de origen de la orden para la lista del historial.</summary>
    public string ChannelLabel => OrderType == SalesDomainConstants.OrderTypes.ConfectionWorkOrder
        ? SalesDomainConstants.OrderChannelLabels.ConfectionShop
        : SalesDomainConstants.OrderChannelLabels.CashRegister;

    /// <summary>Indica si la fila tiene un DTE emitido.</summary>
    public bool HasDte => !string.IsNullOrWhiteSpace(ControlNumber);
}

/// <summary>Totales agregados en base de datos para el filtro completo.</summary>
/// <remarks>Total e IVA solo suman órdenes COMPLETADAS, aunque el filtro incluya otros estados.</remarks>
/// <param name="Sales">Cantidad de órdenes que cumplen el filtro.</param>
/// <param name="Total">Suma de totales de órdenes completadas.</param>
/// <param name="Tax">Suma de IVA de órdenes completadas.</param>
/// <param name="Contingencies">Cantidad de órdenes con DTE en contingencia.</param>
/// <param name="Reprints">Cantidad de reimpresiones auditadas.</param>
public sealed record SalesHistorySummary(int Sales, decimal Total, decimal Tax, int Contingencies, int Reprints);

/// <summary>Configuración del alcance de consulta del historial.</summary>
public sealed class SalesHistoryOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "SalesHistory";

    /// <summary>Nombres de puesto que pueden consultar el historial completo.</summary>
    /// <remarks>Decisión pendiente del dueño; la lista vacía trata a todos como cajeros.</remarks>
    public IList<string> FullHistoryPositionNames { get; set; } = new List<string>();
}

/// <summary>Resultado paginado del historial.</summary>
/// <param name="Rows">Filas de la página solicitada.</param>
/// <param name="Summary">Resumen agregado sobre el filtro completo.</param>
/// <param name="Page">Número de página normalizado.</param>
/// <param name="PageSize">Tamaño de página normalizado.</param>
/// <param name="HasPreviousPage">Indica si existe una página anterior.</param>
/// <param name="HasNextPage">Indica si existe una página siguiente.</param>
public sealed record SalesHistoryPage(
    IReadOnlyList<SalesHistoryRow> Rows,
    SalesHistorySummary Summary,
    int Page,
    int PageSize,
    bool HasPreviousPage,
    bool HasNextPage);

/// <summary>Detalle completo de una venta.</summary>
/// <param name="OrderId">Identificador de la orden.</param>
/// <param name="CreatedAtUtc">Fecha de creación de la orden en UTC.</param>
/// <param name="Status">Estado persistido de la orden.</param>
/// <param name="OrderType">Tipo persistido de orden.</param>
/// <param name="CustomerDisplayName">Nombre del cliente para mostrar.</param>
/// <param name="EmployeeDisplayName">Nombre del empleado que registró la orden.</param>
/// <param name="Subtotal">Subtotal persistido de la orden.</param>
/// <param name="Tax">IVA persistido de la orden.</param>
/// <param name="Discount">Descuento persistido de la orden.</param>
/// <param name="Total">Total persistido de la orden.</param>
/// <param name="Notes">Notas formateadas para mostrar.</param>
/// <param name="Lines">Líneas de productos de la orden.</param>
/// <param name="Payments">Pagos asociados a la orden.</param>
/// <param name="Dtes">DTE emitidos y sus notas de crédito relacionadas.</param>
/// <param name="Movements">Movimientos de inventario asociados.</param>
public sealed record SalesHistoryDetail(
    Guid OrderId,
    DateTime CreatedAtUtc,
    string Status,
    string OrderType,
    string CustomerDisplayName,
    string EmployeeDisplayName,
    decimal Subtotal,
    decimal Tax,
    decimal Discount,
    decimal Total,
    string Notes,
    IReadOnlyList<SalesHistoryLine> Lines,
    IReadOnlyList<SalesHistoryPayment> Payments,
    IReadOnlyList<SalesHistoryDte> Dtes,
    IReadOnlyList<SalesHistoryMovement> Movements);

/// <summary>Línea congelada de una venta.</summary>
/// <param name="Product">Descripción del producto.</param>
/// <param name="Quantity">Cantidad vendida.</param>
/// <param name="Unit">Unidad de venta.</param>
/// <param name="UnitsPerPackage">Unidades base por presentación.</param>
/// <param name="UnitPrice">Precio unitario.</param>
/// <param name="Discount">Descuento de la línea.</param>
/// <param name="Subtotal">Subtotal de la línea.</param>
public sealed record SalesHistoryLine(
    string Product,
    decimal Quantity,
    string Unit,
    decimal UnitsPerPackage,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal);

/// <summary>Pago de una venta.</summary>
/// <param name="Method">Método de pago.</param>
/// <param name="Amount">Monto pagado.</param>
/// <param name="Reference">Referencia del pago, si existe.</param>
public sealed record SalesHistoryPayment(string Method, decimal Amount, string? Reference);

/// <summary>DTE y sus notas de crédito relacionadas.</summary>
/// <param name="Type">Tipo de DTE.</param>
/// <param name="ControlNumber">Número de control del DTE.</param>
/// <param name="GenerationCode">Código de generación del DTE.</param>
/// <param name="Status">Estado del DTE ante el Ministerio de Hacienda.</param>
/// <param name="Seal">Sello de recepción, si existe.</param>
/// <param name="RelatedDteId">Identificador del DTE relacionado, si existe.</param>
/// <param name="Reprints">Contador de reimpresiones del DTE.</param>
/// <param name="CreditNotes">Notas de crédito relacionadas.</param>
public sealed record SalesHistoryDte(
    string Type,
    string ControlNumber,
    Guid GenerationCode,
    string Status,
    string? Seal,
    Guid? RelatedDteId,
    int Reprints,
    IReadOnlyList<SalesHistoryCreditNote> CreditNotes);

/// <summary>Nota de crédito relacionada con un DTE.</summary>
/// <param name="Type">Tipo de documento relacionado.</param>
/// <param name="ControlNumber">Número de control de la nota de crédito.</param>
/// <param name="Status">Estado de la nota de crédito.</param>
public sealed record SalesHistoryCreditNote(string Type, string ControlNumber, string Status);

/// <summary>Movimiento de inventario asociado a una venta.</summary>
/// <param name="Product">Descripción del producto.</param>
/// <param name="Type">Tipo de movimiento.</param>
/// <param name="Quantity">Cantidad movida.</param>
/// <param name="Reason">Motivo registrado, si existe.</param>
public sealed record SalesHistoryMovement(string Product, string Type, decimal Quantity, string? Reason);

/// <summary>Alcance de datos autorizado para una sesión.</summary>
/// <param name="RestrictToEmployeeId">Empleado al que se limita el alcance, si aplica.</param>
/// <param name="MinCreatedAtUtc">Límite inferior UTC, si aplica.</param>
/// <param name="MaxCreatedAtUtc">Límite superior UTC exclusivo, si aplica.</param>
public sealed record SalesHistoryScope(Guid? RestrictToEmployeeId, DateTime? MinCreatedAtUtc, DateTime? MaxCreatedAtUtc);

/// <summary>Contrato de consulta y detalle del historial.</summary>
public interface ISalesHistoryService
{
    /// <summary>Consulta ventas aplicando el filtro y resolviendo el alcance en el servidor.</summary>
    /// <param name="filter">Filtro de búsqueda y paginación.</param>
    /// <param name="employeeId">Empleado que solicita la consulta.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    /// <returns>Una página con sus totales agregados.</returns>
    Task<SalesHistoryPage> SearchAsync(SalesHistoryFilter filter, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene el detalle si el empleado tiene acceso a la venta.</summary>
    /// <param name="orderId">Identificador de la orden.</param>
    /// <param name="employeeId">Empleado que solicita el detalle.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    /// <returns>El detalle o <c>null</c> cuando no existe o no está autorizado.</returns>
    Task<SalesHistoryDetail?> GetDetailAsync(Guid orderId, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Incrementa de manera atómica el contador de reimpresiones de los DTE de una orden.</summary>
    /// <remarks>No modifica nada cuando la orden no tiene DTE.</remarks>
    /// <param name="orderId">Identificador de la orden.</param>
    /// <param name="cancellationToken">Token para cancelar la actualización.</param>
    /// <returns><c>true</c> si se actualizó al menos un DTE; de lo contrario, <c>false</c>.</returns>
    Task<bool> IncrementReprintsAsync(Guid orderId, CancellationToken cancellationToken = default);
}
