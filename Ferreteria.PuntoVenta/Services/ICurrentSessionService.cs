using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Sesión en memoria del <see cref="Employee"/> autenticado por PIN y del módulo operativo activo.
/// No persiste credenciales; solo el estado de la UI del punto de venta.
/// </summary>
public interface ICurrentSessionService
{
    /// <summary>Empleado autenticado, o null si no hay sesión.</summary>
    Employee? CurrentEmployee { get; }

    /// <summary>Módulo activo (<see cref="OperationalModule.Caja"/> o <see cref="OperationalModule.Inventario"/>).</summary>
    OperationalModule? ActiveModule { get; }

    /// <summary>Nombre del módulo activo como texto (compatibilidad con vistas).</summary>
    string? CurrentModule { get; }

    /// <summary>Marca de tiempo UTC del inicio de sesión.</summary>
    DateTime? StartedAtUtc { get; }

    /// <summary>True cuando hay empleado y módulo activos.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Inicia la sesión local tras validar el PIN.
    /// </summary>
    /// <param name="employee">Empleado autenticado (sin PIN en claro).</param>
    /// <param name="module">Módulo operativo elegido en la pantalla de inicio.</param>
    /// <param name="initialSection">Clave de sección del panel lateral (ver <see cref="NavSections"/>).</param>
    void StartSession(Employee employee, OperationalModule module, string initialSection);

    /// <summary>
    /// Resuelve la sección inicial válida para el módulo activo.
    /// </summary>
    /// <returns>Clave de sección de <see cref="NavSections"/>.</returns>
    string ResolveInitialSection();

    /// <summary>Limpia empleado, módulo y sección; no escribe en base de datos.</summary>
    void EndSession();
}
