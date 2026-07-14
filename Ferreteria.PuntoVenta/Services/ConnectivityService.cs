using Ferreteria.PuntoVenta.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

public sealed class ConnectivityService(IServiceScopeFactory scopeFactory) : IConnectivityService
{
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ConnectivityStatus(false, "Sin conexion a la base de datos");
        }
    }
}
