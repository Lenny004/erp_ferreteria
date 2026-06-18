namespace FlexoCableSV.PuntoVenta.Services;

public interface IOrderService
{
    Task<CashSaleResult> CreateCashSaleAsync(
        CreateCashSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkOrderResult> CreateConfectionOrderAsync(
        CreateConfectionOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfectionOrderSummary>> GetConfectionOrdersAsync(
        string? status,
        string? searchText,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<CashSaleResult> CompleteConfectionOrderAsync(
        CompleteConfectionOrderRequest request,
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

public sealed record CompleteConfectionOrderRequest(
    Guid OrderId,
    Guid EmployeeId,
    Guid? CashSessionId,
    IReadOnlyList<CashSalePaymentRequest> Payments);

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

public sealed record ConfectionOrderSummary(
    Guid OrderId,
    DateTime CreatedAt,
    string Customer,
    string Employee,
    string Application,
    string Status,
    int ItemCount,
    decimal Total)
{
    public string OrderNumber => $"#{OrderId.ToString()[..8].ToUpperInvariant()}";
    public string DateText => CreatedAt.ToLocalTime().ToString("dd/MM HH:mm");
    public string TotalText => Total.ToString("C2");
}
