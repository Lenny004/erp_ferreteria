using System.Net;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>Datos para crear o actualizar una impresora del POS.</summary>
/// <param name="Name">Nombre de la impresora.</param>
/// <param name="ConnectionType">USB, RED o BLUETOOTH.</param>
/// <param name="IpAddress">IP para impresoras de red.</param>
/// <param name="NetworkPort">Puerto TCP de la impresora.</param>
/// <param name="PaperWidth">Ancho de papel en mm.</param>
/// <param name="IsDefault">Marca la impresora como predeterminada.</param>
public sealed record PrinterInput(
    string Name,
    string ConnectionType,
    string? IpAddress,
    int? NetworkPort,
    short PaperWidth,
    bool IsDefault);

/// <summary>Reglas de validación y normalización de impresoras.</summary>
public static class PrinterConfigurationRules
{
    /// <summary>Conexión por cola de Windows.</summary>
    public const string Usb = "USB";
    /// <summary>Conexión TCP directa.</summary>
    public const string Network = "RED";
    /// <summary>Conexión Bluetooth.</summary>
    public const string Bluetooth = "BLUETOOTH";

    /// <summary>Etiqueta alternativa de la UI para la conexión de red.</summary>
    public const string EthernetAlias = "ETHERNET";

    /// <summary>Ancho de papel de 58 mm (32 columnas).</summary>
    public const short PaperWidth58 = 58;

    /// <summary>Ancho de papel de 80 mm (48 columnas).</summary>
    public const short PaperWidth80 = 80;

    /// <summary>Longitud máxima del nombre de impresora.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Valida una entrada antes de persistirla.</summary>
    /// <param name="input">Entrada de la interfaz.</param>
    /// <exception cref="ArgumentException">Si la entrada es inválida.</exception>
    public static void Validate(PrinterInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"El nombre de la impresora es obligatorio y debe tener hasta {MaxNameLength} caracteres.",
                nameof(input));
        }

        string connection = NormalizeConnectionType(input.ConnectionType);

        if (input.PaperWidth is not PaperWidth58 and not PaperWidth80)
        {
            throw new ArgumentException("El ancho de papel debe ser 58 u 80 mm.", nameof(input));
        }

        if (connection == Network)
        {
            if (!IPAddress.TryParse(input.IpAddress?.Trim(), out _))
            {
                throw new ArgumentException("La dirección IP no es válida para una impresora de red.", nameof(input));
            }

            if (input.NetworkPort is < 1 or > 65535)
            {
                throw new ArgumentException("El puerto debe estar entre 1 y 65535.", nameof(input));
            }
        }
    }

    /// <summary>Convierte etiquetas de la UI al valor canónico.</summary>
    /// <param name="connectionType">Etiqueta de conexión.</param>
    /// <returns>USB, RED o BLUETOOTH.</returns>
    /// <exception cref="ArgumentException">Si la etiqueta no corresponde a ninguna conexión soportada.</exception>
    public static string NormalizeConnectionType(string? connectionType)
    {
        return connectionType?.Trim().ToUpperInvariant() switch
        {
            Usb => Usb,
            Network or EthernetAlias => Network,
            Bluetooth => Bluetooth,
            _ => throw new ArgumentException(
                "Seleccione una conexión válida: USB, red o Bluetooth.",
                nameof(connectionType))
        };
    }
}
