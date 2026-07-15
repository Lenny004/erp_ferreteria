namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Comprueba si el punto de venta puede alcanzar la base de datos PostgreSQL.
/// </summary>
public interface IConnectivityService
{
    /// <summary>
    /// Evalúa la conectividad actual contra <see cref="Data.FerreteriaDbContext"/>.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Estado online/offline con un mensaje legible para la UI.</returns>
    Task<ConnectivityStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resultado de una comprobación de conectividad a la base de datos.</summary>
/// <param name="IsOnline">True si <c>CanConnectAsync</c> responde afirmativamente.</param>
/// <param name="Message">Texto corto para mostrar al cajero o al operador de inventario.</param>
public sealed record ConnectivityStatus(bool IsOnline, string Message);
