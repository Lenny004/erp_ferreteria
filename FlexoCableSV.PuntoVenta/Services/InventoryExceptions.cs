namespace FlexoCableSV.PuntoVenta.Services;

public sealed class ProductNotFoundException(Guid productId)
    : InvalidOperationException($"Producto no encontrado o inactivo: {productId}");

public sealed class InvalidInventoryQuantityException(string message)
    : InvalidOperationException(message);

public sealed class InsufficientStockException(string productCode, decimal requested, decimal available)
    : InvalidOperationException($"Stock insuficiente para {productCode}. Solicitado: {requested:N3}. Disponible: {available:N3}.");
