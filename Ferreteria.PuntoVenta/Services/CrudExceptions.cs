namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Error de validación de negocio en operaciones CRUD. El mensaje es apto para mostrar al usuario.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>Se lanza cuando un registro solicitado no existe.</summary>
public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entity, Guid id)
        : base($"No se encontró {entity} con id {id}.") { }

    public EntityNotFoundException(string message) : base(message) { }
}
