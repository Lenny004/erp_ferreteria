using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

public interface ICurrentSessionService
{
    Employee? CurrentEmployee { get; }
    string? CurrentModule { get; }
    DateTime? StartedAtUtc { get; }
    bool IsActive { get; }

    void StartSession(Employee employee, string module);
    void EndSession();
}
