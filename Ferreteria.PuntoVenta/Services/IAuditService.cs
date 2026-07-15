using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Escritura en <c>system.AuditLogs</c> para login/logout y cambios críticos del POS.
/// Los fallos de auditoría se registran en log y no interrumpen la operación de negocio.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Registra un LOGIN del <see cref="Employee"/> en el módulo indicado (Caja o Inventario).
    /// </summary>
    /// <param name="employee">Empleado autenticado.</param>
    /// <param name="module">Nombre del módulo operativo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un LOGOUT del empleado. El módulo puede ser null si la sesión ya estaba limpia.
    /// </summary>
    Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un cambio crítico (INSERT/UPDATE/DELETE/PIN_CHANGE) en la bitácora.
    /// No debe incluir secretos (PIN en claro, hashes, tokens).
    /// </summary>
    /// <param name="action">Acción auditada (p. ej. INSERT, UPDATE, DELETE, PIN_CHANGE).</param>
    /// <param name="tableName">Tabla lógica afectada (p. ej. <c>public.Products</c>).</param>
    /// <param name="recordId">Identificador del registro afectado.</param>
    /// <param name="oldData">Estado anterior serializable, o null en altas.</param>
    /// <param name="newData">Estado nuevo serializable, o null en bajas totales.</param>
    /// <param name="userId">Id del <see cref="Employee"/> que ejecutó el cambio.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RecordChangeAsync(
        string action,
        string tableName,
        string recordId,
        object? oldData,
        object? newData,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
