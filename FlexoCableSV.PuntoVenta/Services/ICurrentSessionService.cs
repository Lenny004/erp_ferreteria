using FlexoCableSV.PuntoVenta.Models;

namespace FlexoCableSV.PuntoVenta.Services;

public interface ICurrentSessionService
{
    Employee? CurrentEmployee { get; }
    OperationalModule? ActiveModule { get; }
    string? CurrentModule { get; }
    DateTime? StartedAtUtc { get; }
    bool IsActive { get; }

    void StartSession(Employee employee, OperationalModule module, string initialSection);
    string ResolveInitialSection();
    void EndSession();
}
