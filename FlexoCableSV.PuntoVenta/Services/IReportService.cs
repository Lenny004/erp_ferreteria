namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>Reportes de ventas y compras para el punto de venta de ferretería.</summary>
public interface IReportService
{
    /// <summary>Resumen de ventas completadas en un rango de fechas (inclusive).</summary>
    Task<SalesReport> GetSalesReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Resumen de compras (entradas por compra) en un rango de fechas (inclusive).</summary>
    Task<PurchasesReport> GetPurchasesReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Productos más vendidos por cantidad en un rango de fechas.</summary>
    Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int take = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>Reporte agregado de ventas.</summary>
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

/// <summary>Total de ventas por día.</summary>
public sealed record SalesByDayRow(DateOnly Day, int OrderCount, decimal Total);

/// <summary>Total de ventas por método de pago.</summary>
public sealed record SalesByPaymentRow(string Method, decimal Amount);

/// <summary>Reporte agregado de compras / entradas por compra.</summary>
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
