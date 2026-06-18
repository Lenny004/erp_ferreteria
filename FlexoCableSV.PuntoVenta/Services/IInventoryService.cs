namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>Consulta de inventario y movimientos de stock.</summary>
public interface IInventoryService
{
    /// <summary>Busca productos activos por código, descripción, familia o código de barras.</summary>
    Task<IReadOnlyList<InventoryProductResult>> SearchProductsAsync(
        string? searchText,
        string? stockStatus,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Registra una salida de inventario asociada a una orden.</summary>
    Task<StockDecreaseResult> DecreaseStockAsync(
        Guid productId,
        decimal quantity,
        Guid orderId,
        Guid employeeId,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>Proyección de producto para vistas de inventario y confección.</summary>
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
        ? Domain.SalesDomainConstants.StockFilters.Depleted
        : CurrentStock <= MinStock
            ? Domain.SalesDomainConstants.StockFilters.Low
            : Domain.SalesDomainConstants.StockFilters.Sufficient;

    public string FormattedStock =>
        $"{Math.Round(CurrentStock, Decimals).ToString($"N{Decimals}")} {UnitLabel}".Trim();

    public string PriceText => SalePrice.ToString("C2");

    public string DetailStatus => IsActive ? $"ACTIVO / STOCK {Status}" : "INACTIVO";
}

/// <summary>Resultado de una salida de inventario persistida.</summary>
public sealed record StockDecreaseResult(
    Guid ProductId,
    Guid InventoryMovementId,
    decimal StockBefore,
    decimal StockAfter);
