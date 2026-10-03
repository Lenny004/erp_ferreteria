namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>
/// Configuración de autorización del POS.
/// </summary>
public sealed class AuthorizationOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "Autorizacion";

    /// <summary>
    /// Puestos que reciben permisos administrativos. Una lista vacía deniega esos permisos a todos.
    /// </summary>
    public IList<string> PuestosAdministracion { get; set; } = new List<string>();
}
