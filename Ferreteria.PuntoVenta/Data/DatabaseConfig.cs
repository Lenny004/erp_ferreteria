using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Data;

/// <summary>
/// Método de extensión para registrar el DbContext de Ferreteria
/// con el proveedor Npgsql (PostgreSQL) usando la cadena de conexión
/// definida en appsettings.json.
/// </summary>
public static class DatabaseConfig
{
    /// <summary>
    /// Agrega FerreteriaDbContext al contenedor DI con PostgreSQL como motor de base de datos.
    /// La cadena de conexión se lee de la sección ConnectionStrings:FerreteriaDB.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <param name="configuration">Interfaz de configuración (appsettings.json, env vars, etc.).</param>
    /// <returns>La misma colección de servicios para encadenar más configuraciones.</returns>
    public static IServiceCollection AddFerreteriaDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registra FerreteriaDbContext como servicio scoped (un contexto por operación/scope en WPF).
        // Usa Npgsql como proveedor EF Core para conectarse a PostgreSQL.
        // La cadena de conexión debe estar en appsettings.json bajo ConnectionStrings:FerreteriaDB.
        services.AddDbContext<FerreteriaDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("FerreteriaDB")));

        return services;
    }
}
