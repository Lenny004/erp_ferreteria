using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

public interface IAuditService
{
    Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default);
    Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default);

    /// <summary>Registra un cambio crítico (INSERT/UPDATE/DELETE) en la bitácora de auditoría.</summary>
    Task RecordChangeAsync(
        string action,
        string tableName,
        string recordId,
        object? oldData,
        object? newData,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
