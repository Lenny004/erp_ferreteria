using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Data;

/// <summary>
/// Método de extensión para registrar el DbContext de FlexoCable
/// con el proveedor Npgsql (PostgreSQL) usando la cadena de conexión
/// definida en appsettings.json.
/// </summary>
public static class DatabaseConfig
{
    /// <summary>
    /// Agrega FlexoDbContext al contenedor DI con PostgreSQL como motor de base de datos.
    /// La cadena de conexión se lee de la sección ConnectionStrings:FlexoCableDB.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <param name="configuration">Interfaz de configuración (appsettings.json, env vars, etc.).</param>
    /// <returns>La misma colección de servicios para encadenar más configuraciones.</returns>
    public static IServiceCollection AddFlexoDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registra FlexoDbContext como servicio scoped (vida por request HTTP).
        // Usa Npgsql como proveedor EF Core para conectarse a PostgreSQL.
        // La cadena de conexión debe estar en appsettings.json bajo ConnectionStrings:FlexoCableDB.
        services.AddDbContext<FlexoDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("FlexoCableDB")));

        return services;
    }
}
