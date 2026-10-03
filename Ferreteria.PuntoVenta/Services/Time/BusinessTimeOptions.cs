namespace Ferreteria.PuntoVenta.Services.Time;

/// <summary>
/// Configuración de la zona horaria que define el día de negocio del POS.
/// </summary>
public sealed class BusinessTimeOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "Negocio";

    /// <summary>
    /// Identificador IANA o Windows de la zona horaria del negocio.
    /// </summary>
    public string? ZonaHoraria { get; set; }
}
