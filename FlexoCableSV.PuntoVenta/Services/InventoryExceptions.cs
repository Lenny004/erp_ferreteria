namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>Producto inexistente o inactivo en catálogo.</summary>
public sealed class ProductNotFoundException(Guid productId)
    : InvalidOperationException($"Producto no encontrado o inactivo: {productId}");

/// <summary>Cantidad inválida para la unidad de medida del producto.</summary>
public sealed class InvalidInventoryQuantityException(string message)
    : InvalidOperationException(message);

/// <summary>Stock insuficiente para completar la operación solicitada.</summary>
public sealed class InsufficientStockException(string productCode, decimal requestedQuantity, decimal availableQuantity)
    : InvalidOperationException(
        $"Stock insuficiente para {productCode}. Solicitado: {requestedQuantity:N3}. Disponible: {availableQuantity:N3}.");
