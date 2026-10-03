using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Ferreteria.PuntoVenta.Data;

/// <summary>Resuelve y valida la cadena de conexión del POS sin almacenar secretos.</summary>
public sealed class DatabaseConnectionResolver
{
    /// <summary>Nombre de configuración usado por variables de entorno y secretos.</summary>
    public const string ConnectionName = "FerreteriaDB";

    /// <summary>Resuelve la cadena efectiva y exige usuario y contraseña.</summary>
    /// <param name="configuration">Configuración combinada del host.</param>
    /// <returns>Cadena de conexión completa para PostgreSQL.</returns>
    /// <exception cref="DatabaseConnectionConfigurationException">Si falta o no tiene credenciales.</exception>
    public string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DatabaseConnectionConfigurationException(
                "Falta la cadena de conexión. Configure ConnectionStrings__FerreteriaDB en el entorno o el secreto de usuario 'ConnectionStrings:FerreteriaDB'.");
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Username) || string.IsNullOrWhiteSpace(builder.Password))
            {
                throw new DatabaseConnectionConfigurationException(
                    "La cadena de conexión no tiene credenciales. Configure usuario y contraseña en ConnectionStrings__FerreteriaDB o en User Secrets.");
            }
        }
        catch (FormatException exception)
        {
            throw new DatabaseConnectionConfigurationException(
                "La cadena de conexión de FerreteriaDB no tiene un formato válido.", exception);
        }

        return connectionString;
    }
}

/// <summary>Configuración inválida de conexión de base de datos del POS.</summary>
public sealed class DatabaseConnectionConfigurationException : InvalidOperationException
{
    /// <summary>Inicializa el error con un mensaje operativo seguro.</summary>
    /// <param name="message">Mensaje sin secretos.</param>
    public DatabaseConnectionConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa el error con una causa técnica interna.</summary>
    /// <param name="message">Mensaje sin secretos.</param>
    /// <param name="innerException">Causa original.</param>
    public DatabaseConnectionConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
