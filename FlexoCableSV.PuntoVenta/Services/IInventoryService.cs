namespace FlexoCableSV.PuntoVenta.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryProductResult>> SearchProductsAsync(
        string? searchText,
        string? stockStatus,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<StockDecreaseResult> DecreaseStockAsync(
        Guid productId,
        decimal quantity,
        Guid orderId,
        Guid employeeId,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryProductResult(
    Guid Id,
    string Code,
    string Description,
    string Family,
    string? Subfamily,
    string Measurement,
    string UnitLabel,
    short Decimals,
    decimal CurrentStock,
    decimal MinStock,
    decimal SalePrice,
    bool IsActive)
{
    public string Status => CurrentStock <= 0
        ? "AGOTADO"
        : CurrentStock <= MinStock ? "BAJO" : "OK";

    public string FormattedStock => $"{Math.Round(CurrentStock, Decimals).ToString($"N{Decimals}")} {UnitLabel}".Trim();
    public string PriceText => SalePrice.ToString("C2");
    public string DetailStatus => IsActive ? $"ACTIVO / STOCK {Status}" : "INACTIVO";
}

public sealed record StockDecreaseResult(
    Guid ProductId,
    Guid InventoryMovementId,
    decimal StockBefore,
    decimal StockAfter);
