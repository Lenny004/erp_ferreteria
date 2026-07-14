using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Valida PIN contra hr.Employees.PinHash (bcrypt) según el módulo operativo elegido.
/// </summary>
public class PinAuthService(IServiceScopeFactory scopeFactory)
{
    public Task<Employee?> ValidatePinAsync(string pin, CancellationToken cancellationToken = default) =>
        ValidatePinAsync(pin, OperationalModule.Caja, cancellationToken);

    public async Task<Employee?> ValidatePinAsync(
        string pin,
        OperationalModule module,
        CancellationToken cancellationToken = default)
    {
        if (pin.Length != 4 || !pin.All(char.IsDigit))
            return null;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var candidatesQuery = db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive && e.PinHash != null);

        candidatesQuery = module switch
        {
            OperationalModule.Caja => candidatesQuery.Where(e => e.CanCashier),
            OperationalModule.Inventario => candidatesQuery.Where(e => e.CanSell),
            _ => candidatesQuery.Where(_ => false)
        };

        var candidates = await candidatesQuery.ToListAsync(cancellationToken);

        foreach (var employee in candidates)
        {
            if (BCrypt.Net.BCrypt.Verify(pin, employee.PinHash!))
                return employee;
        }

        return null;
    }
}
