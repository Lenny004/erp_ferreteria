using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.Time;

/// <summary>
/// Zona horaria validada que utiliza el calendario de negocio y PostgreSQL.
/// </summary>
/// <remarks>
/// La instancia es inmutable para que todos los servicios del proceso compartan el mismo
/// identificador y no dependan de la zona horaria de la sesión de la base de datos.
/// </remarks>
public sealed class BusinessTimeZone
{
    private BusinessTimeZone(TimeZoneInfo zoneInfo, string ianaId)
    {
        ZoneInfo = zoneInfo;
        IanaId = ianaId;
    }

    /// <summary>Zona resuelta por .NET para conversiones locales y UTC.</summary>
    public TimeZoneInfo ZoneInfo { get; }

    /// <summary>
    /// Identificador IANA que se envía a PostgreSQL en expresiones <c>AT TIME ZONE</c>.
    /// </summary>
    public string IanaId { get; }

    /// <summary>
    /// Crea una zona validada a partir de las opciones de negocio.
    /// </summary>
    /// <param name="options">Opciones que contienen la zona configurada.</param>
    /// <returns>Zona horaria inmutable y lista para usar.</returns>
    /// <exception cref="InvalidOperationException">
    /// La clave <c>Negocio:ZonaHoraria</c> es nula, vacía, inválida o no puede representarse como IANA.
    /// </exception>
    public static BusinessTimeZone Create(BusinessTimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuredId = options.ZonaHoraria?.Trim();
        if (string.IsNullOrWhiteSpace(configuredId))
        {
            throw InvalidConfiguration("está vacía o no fue configurada");
        }

        var zoneInfo = FindZone(configuredId);
        if (zoneInfo is null)
        {
            throw InvalidConfiguration($"'{configuredId}' no existe en este sistema");
        }

        var ianaId = ResolveIanaId(configuredId);
        if (ianaId is null)
        {
            throw InvalidConfiguration($"'{configuredId}' no tiene un identificador IANA equivalente");
        }

        return new BusinessTimeZone(zoneInfo, ianaId);
    }

    /// <summary>
    /// Indica si las opciones contienen una zona que puede resolverse en este sistema.
    /// </summary>
    /// <param name="options">Opciones que se desean validar.</param>
    /// <returns><c>true</c> si la zona es válida; de lo contrario, <c>false</c>.</returns>
    public static bool IsValid(BusinessTimeOptions options)
    {
        try
        {
            Create(options);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static TimeZoneInfo? FindZone(string configuredId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configuredId);
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(configuredId, out var ianaId)
            && !string.IsNullOrWhiteSpace(ianaId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(configuredId, out var windowsId)
            && !string.IsNullOrWhiteSpace(windowsId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return null;
    }

    private static string? ResolveIanaId(string configuredId)
    {
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(configuredId, out var ianaId)
            && !string.IsNullOrWhiteSpace(ianaId))
        {
            return ianaId;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(configuredId, out _))
        {
            return configuredId;
        }

        return null;
    }

    private static InvalidOperationException InvalidConfiguration(string reason)
    {
        return new InvalidOperationException(
            $"La clave de configuración 'Negocio:ZonaHoraria' {reason}.");
    }
}

/// <summary>
/// Fábrica explícita de zonas de negocio a partir de opciones validadas.
/// </summary>
public static class BusinessTimeZoneFactory
{
    /// <summary>
    /// Construye la zona de negocio desde las opciones enlazadas.
    /// </summary>
    /// <param name="options">Opciones de zona horaria.</param>
    /// <returns>Zona horaria validada.</returns>
    public static BusinessTimeZone Create(IOptions<BusinessTimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return BusinessTimeZone.Create(options.Value);
    }
}
