using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

public interface IAuditService
{
    Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default);
    Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default);
}
