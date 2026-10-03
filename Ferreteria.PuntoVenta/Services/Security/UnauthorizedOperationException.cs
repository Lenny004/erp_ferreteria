namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>
/// Indica que el empleado autenticado no puede ejecutar una operación POS.
/// </summary>
public sealed class UnauthorizedOperationException : Exception
{
    /// <summary>Inicializa una excepción con el mensaje visible para el usuario.</summary>
    /// <param name="permission">Permiso que fue rechazado.</param>
    public UnauthorizedOperationException(PosPermission permission)
        : base($"No tiene permiso para {GetOperationName(permission)}.")
    {
    }

    /// <summary>Inicializa una excepción con un mensaje seguro y específico.</summary>
    /// <param name="message">Mensaje sin datos internos ni secretos.</param>
    public UnauthorizedOperationException(string message)
        : base(message)
    {
    }

    private static string GetOperationName(PosPermission permission) => permission switch
    {
        PosPermission.OperarCaja => "operar la caja",
        PosPermission.OperarInventario => "operar el inventario",
        PosPermission.AdministrarUsuarios => "administrar usuarios y RRHH",
        PosPermission.AdministrarCatalogo => "administrar el catálogo",
        PosPermission.AdministrarConfiguracion => "administrar la configuración",
        _ => "esta operación"
    };
}
