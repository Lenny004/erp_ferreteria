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

    /// <summary>
    /// Registra el contexto de datos de FlexoCable usando PostgreSQL.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor de inyección de dependencias.</param>
    /// <param name="configuration">Configuración de aplicación desde la que se obtiene la cadena de conexión.</param>
    /// <returns>La misma colección de servicios para permitir encadenamiento.</returns>
    /// <exception cref="InvalidOperationException">Se lanza cuando no existe la cadena de conexión requerida.</exception>
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