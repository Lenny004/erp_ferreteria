namespace Ferreteria.PuntoVenta.Services;

/// <summary>Reportes de ventas y compras para el punto de venta de ferretería.</summary>
public interface IReportService
{
    /// <summary>Resumen de ventas completadas en fechas locales inclusivas.</summary>
    /// <param name="from">Primer día local incluido.</param>
    /// <param name="toInclusive">Último día local incluido.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    Task<SalesReport> GetSalesReportAsync(
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);

    /// <summary>Resumen de compras (entradas por compra) en fechas locales inclusivas.</summary>
    /// <param name="from">Primer día local incluido.</param>
    /// <param name="toInclusive">Último día local incluido.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    Task<PurchasesReport> GetPurchasesReportAsync(
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);

    /// <summary>Productos más vendidos por cantidad en fechas locales inclusivas.</summary>
    /// <param name="from">Primer día local incluido.</param>
    /// <param name="toInclusive">Último día local incluido.</param>
    /// <param name="take">Cantidad máxima de productos devueltos.</param>
    /// <param name="cancellationToken">Token para cancelar la consulta.</param>
    Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(
        DateOnly from,
        DateOnly toInclusive,
        int take = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>Reporte agregado de ventas para un rango de fechas locales.</summary>
/// <param name="FromUtc">Inicio UTC inclusivo usado por la consulta.</param>
/// <param name="ToUtc">Fin UTC exclusivo usado por la consulta.</param>
/// <param name="OrderCount">Cantidad de órdenes completadas.</param>
/// <param name="Subtotal">Subtotal acumulado.</param>
/// <param name="TaxAmount">IVA acumulado.</param>
/// <param name="DiscountAmount">Descuento acumulado.</param>
/// <param name="Total">Total acumulado.</param>
/// <param name="ByDay">Totales agrupados por fecha local.</param>
/// <param name="ByPaymentMethod">Totales agrupados por método de pago.</param>
public sealed record SalesReport(
    DateTime FromUtc,
    DateTime ToUtc,
    int OrderCount,
    decimal Subtotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal Total,
    IReadOnlyList<SalesByDayRow> ByDay,
    IReadOnlyList<SalesByPaymentRow> ByPaymentMethod);

/// <summary>Total de ventas por día local.</summary>
/// <param name="Day">Fecha local de negocio.</param>
/// <param name="OrderCount">Cantidad de órdenes del día.</param>
/// <param name="Total">Total vendido en el día.</param>
public sealed record SalesByDayRow(DateOnly Day, int OrderCount, decimal Total);

/// <summary>Total de ventas por método de pago.</summary>
public sealed record SalesByPaymentRow(string Method, decimal Amount);

/// <summary>Reporte agregado de compras / entradas por compra para un rango local.</summary>
/// <param name="FromUtc">Inicio UTC inclusivo usado por la consulta.</param>
/// <param name="ToUtc">Fin UTC exclusivo usado por la consulta.</param>
/// <param name="MovementCount">Cantidad de movimientos incluidos.</param>
/// <param name="TotalCost">Costo acumulado.</param>
/// <param name="ByProduct">Costos agrupados por producto.</param>
public sealed record PurchasesReport(
    DateTime FromUtc,
    DateTime ToUtc,
    int MovementCount,
    decimal TotalCost,
    IReadOnlyList<PurchasesByProductRow> ByProduct);

/// <summary>Compras acumuladas por producto.</summary>
public sealed record PurchasesByProductRow(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal TotalCost);

/// <summary>Producto más vendido.</summary>
public sealed record TopProductRow(
    string ProductCode,
    string ProductDescription,
    decimal QuantitySold,
    decimal Revenue);
