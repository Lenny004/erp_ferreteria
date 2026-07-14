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

    /// <summary>Registra una entrada de inventario (compra o devolución) que incrementa el stock.</summary>
    Task<StockMovementResult> RegisterEntryAsync(
        Guid productId,
        decimal quantity,
        decimal unitCost,
        Guid employeeId,
        string movementType,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Ajusta el stock a un nuevo valor absoluto, registrando el movimiento de ajuste correspondiente.</summary>
    Task<StockMovementResult> RegisterAdjustmentAsync(
        Guid productId,
        decimal newStock,
        Guid employeeId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Alertas de stock bajo o agotado sin resolver.</summary>
    Task<IReadOnlyList<StockAlertResult>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>Últimos movimientos de inventario (kardex), opcionalmente filtrados por producto.</summary>
    Task<IReadOnlyList<InventoryMovementResult>> GetRecentMovementsAsync(
        Guid? productId,
        int take = 100,
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

/// <summary>Resultado genérico de un movimiento de inventario (entrada o ajuste).</summary>
public sealed record StockMovementResult(
    Guid ProductId,
    Guid InventoryMovementId,
    string MovementType,
    decimal StockBefore,
    decimal StockAfter);

/// <summary>Alerta de stock bajo/agotado para la vista de inventario.</summary>
public sealed record StockAlertResult(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductDescription,
    string AlertType,
    decimal CurrentStock,
    decimal MinStock,
    string UnitLabel,
    DateTime CreatedAt);

/// <summary>Movimiento de inventario proyectado para el kardex.</summary>
public sealed record InventoryMovementResult(
    Guid Id,
    DateTime CreatedAt,
    string ProductCode,
    string ProductDescription,
    string MovementType,
    decimal Quantity,
    decimal StockAfter,
    string? Reason);
