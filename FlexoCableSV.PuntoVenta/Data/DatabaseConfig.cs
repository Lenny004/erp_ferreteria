using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Data;

/// <summary>
/// Registra la conexión a PostgreSQL en el contenedor de DI.
/// La cadena de conexión se lee desde appsettings.json → "FlexoCableDB".
/// </summary>
public static class DatabaseConfig
{
    private const string ConnectionStringKey = "FlexoCableDB";

    public static IServiceCollection AddFlexoDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringKey)
            ?? throw new InvalidOperationException(
                $"No se encontró la cadena de conexión '{ConnectionStringKey}' en appsettings.json.");

        services.AddDbContext<FlexoDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}