namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Compone la representacion imprimible (<see cref="ReceiptDocument"/>) de un DTE
/// a partir de la orden de venta, el DTE emitido y la configuracion del emisor.
/// </summary>
public interface IReceiptCompositionService
{
    /// <summary>Arma el ticket DTE o el comprobante interno de una orden.</summary>
    /// <param name="orderId">Identificador de la orden.</param>
    /// <param name="isReprint">Incluye la leyenda de reimpresión cuando es true.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El documento imprimible o null si la orden no existe.</returns>
    Task<ReceiptDocument?> ComposeForOrderAsync(Guid orderId, bool isReprint = false, CancellationToken cancellationToken = default);
}
