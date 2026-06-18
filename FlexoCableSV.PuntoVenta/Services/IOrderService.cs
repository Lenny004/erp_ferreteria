namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Operaciones de venta y órdenes de confección sobre <c>sales.Orders</c>.
/// Referencia: FLEXOCABLE_PLAN_FINALIZACION_APP.md — Fase 3.
/// </summary>
public interface IOrderService
{
    /// <summary>Registra una venta de mostrador, descuenta inventario y persiste pagos.</summary>
    Task<CashSaleResult> CreateCashSaleAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Crea una orden de taller en estado pendiente sin descontar inventario.</summary>
    Task<WorkOrderResult> CreateConfectionOrderAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lista órdenes de confección filtradas por estado y texto de búsqueda.</summary>
    Task<IReadOnlyList<ConfectionOrderSummary>> GetConfectionOrdersAsync(
        string? status,
        string? searchText,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Factura una orden pendiente, descuenta inventario y registra pagos.</summary>
    Task<CashSaleResult> CompleteConfectionOrderAsync(
        CompleteConfectionOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lista ventas completadas (caja y taller) para historial.</summary>
    Task<IReadOnlyList<SalesOrderSummary>> GetCompletedSalesAsync(
        string? searchText,
        int take = 100,
        CancellationToken cancellationToken = default);
}
