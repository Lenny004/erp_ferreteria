namespace FlexoCableSV.PuntoVenta.Services;

public interface IOrderService
{
    Task<CashSaleResult> CreateCashSaleAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkOrderResult> CreateConfectionOrderAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateCashSaleRequest(
    Guid EmployeeId,
    Guid? CashSessionId,
    Guid? CustomerId,
    Guid? ClientRequestId,
    IReadOnlyList<CashSaleLineRequest> Lines,
    IReadOnlyList<CashSalePaymentRequest> Payments,
    string? Notes);

public sealed record CashSaleLineRequest(Guid ProductId, decimal Quantity, string? Notes = null);

public sealed record CashSalePaymentRequest(string Method, decimal Amount, string? Reference = null);

public sealed record CashSaleResult(
    Guid OrderId,
    Guid ClientRequestId,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total);

public sealed record CreateConfectionOrderRequest(
    Guid EmployeeId,
    Guid? CustomerId,
    Guid? ClientRequestId,
    string? CustomerName,
    string? CustomerPhone,
    IReadOnlyList<CashSaleLineRequest> Lines,
    string? Notes);

public sealed record WorkOrderResult(
    Guid OrderId,
    Guid ClientRequestId,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total);
