namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>Datos requeridos para registrar una venta de mostrador.</summary>
public sealed record CreateCashSaleRequest(
    Guid EmployeeId,
    Guid? CashSessionId,
    Guid? CustomerId,
    Guid? ClientRequestId,
    IReadOnlyList<CashSaleLineRequest> Lines,
    IReadOnlyList<CashSalePaymentRequest> Payments,
    string? Notes);

/// <summary>Línea de producto en una venta u orden de confección.</summary>
public sealed record CashSaleLineRequest(Guid ProductId, decimal Quantity, string? Notes = null);

/// <summary>Pago asociado a una venta u orden completada.</summary>
public sealed record CashSalePaymentRequest(string Method, decimal Amount, string? Reference = null);

/// <summary>Datos para facturar una orden de confección previamente pendiente.</summary>
public sealed record CompleteConfectionOrderRequest(
    Guid OrderId,
    Guid EmployeeId,
    Guid? CashSessionId,
    IReadOnlyList<CashSalePaymentRequest> Payments);

/// <summary>Resultado de una venta registrada o facturada.</summary>
public sealed record CashSaleResult(
    Guid OrderId,
    Guid ClientRequestId,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total);

/// <summary>Datos para crear una orden de taller que queda pendiente hasta facturación.</summary>
public sealed record CreateConfectionOrderRequest(
    Guid EmployeeId,
    Guid? CustomerId,
    Guid? ClientRequestId,
    string? CustomerName,
    string? CustomerPhone,
    IReadOnlyList<CashSaleLineRequest> Lines,
    string? Notes);

/// <summary>Resultado de la creación de una orden de confección.</summary>
public sealed record WorkOrderResult(
    Guid OrderId,
    Guid ClientRequestId,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total);

/// <summary>Proyección de listado para la bandeja de órdenes de confección.</summary>
public sealed record ConfectionOrderSummary(
    Guid OrderId,
    DateTime CreatedAt,
    string CustomerDisplayName,
    string EmployeeDisplayName,
    string ApplicationLabel,
    string Status,
    int ItemCount,
    decimal Total)
{
    public string OrderNumber => $"#{OrderId.ToString()[..8].ToUpperInvariant()}";
    public string DateText => CreatedAt.ToLocalTime().ToString("dd/MM HH:mm");
    public string TotalText => Total.ToString("C2");
}

/// <summary>Proyección de ventas completadas para historial de caja.</summary>
public sealed record SalesOrderSummary(
    Guid OrderId,
    DateTime CreatedAt,
    string CustomerDisplayName,
    string OrderType,
    string PaymentMethod,
    string Status,
    decimal Total)
{
    public string DateText => CreatedAt.ToLocalTime().ToString("dd/MM HH:mm");
    public string ControlNumberText => $"ORD-{OrderId.ToString()[..8].ToUpperInvariant()}";
    public string ChannelLabel => OrderType == Domain.SalesDomainConstants.OrderTypes.ConfectionWorkOrder
        ? Domain.SalesDomainConstants.OrderChannelLabels.ConfectionShop
        : Domain.SalesDomainConstants.OrderChannelLabels.CashRegister;
    public string TotalText => Total.ToString("C2");
}
