using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Valida PIN de caja contra hr.Employees.PinHash (bcrypt).
/// </summary>
public class PinAuthService(IServiceScopeFactory scopeFactory)
{
    public async Task<Employee?> ValidatePinAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (pin.Length != 4 || !pin.All(char.IsDigit))
            return null;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();

        var candidates = await db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive && e.PinHash != null && (e.CanSell || e.CanCashier))
            .ToListAsync(cancellationToken);

        foreach (var employee in candidates)
        {
            if (BCrypt.Net.BCrypt.Verify(pin, employee.PinHash!))
                return employee;
        }

        return null;
    }
}
