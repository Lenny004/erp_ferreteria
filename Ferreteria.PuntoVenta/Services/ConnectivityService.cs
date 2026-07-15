using Ferreteria.PuntoVenta.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Implementación de <see cref="IConnectivityService"/> mediante un ping ligero a PostgreSQL.
/// </summary>
public sealed class ConnectivityService(IServiceScopeFactory scopeFactory) : IConnectivityService
{
    /// <inheritdoc />
    public async Task<ConnectivityStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? new ConnectivityStatus(true, "Base de datos conectada")
                : new ConnectivityStatus(false, "Sin conexion a la base de datos");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // El detalle de la excepción no se muestra al usuario (evita filtrar datos de red/servidor).
            return new ConnectivityStatus(false, "Sin conexion a la base de datos");
        }
    }
}
