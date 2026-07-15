using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Valida el PIN de 4 dígitos contra <c>hr.Employees.PinHash</c> (bcrypt) según el módulo operativo.
/// Riesgo: el PIN llega en claro solo en memoria durante la verificación; nunca se persiste ni se registra en auditoría.
/// </summary>
public class PinAuthService(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Valida el PIN para el módulo de caja (requiere <see cref="Employee.CanCashier"/>).
    /// </summary>
    /// <param name="pin">PIN de 4 dígitos en claro (solo tránsito en memoria).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El <see cref="Employee"/> autenticado, o null si el PIN es inválido.</returns>
    public Task<Employee?> ValidatePinAsync(string pin, CancellationToken cancellationToken = default) =>
        ValidatePinAsync(pin, OperationalModule.Caja, cancellationToken);

    /// <summary>
    /// Valida el PIN filtrando candidatos activos con permiso del módulo (caja o inventario).
    /// </summary>
    /// <param name="pin">PIN de 4 dígitos; cualquier otro formato se rechaza sin consultar BD.</param>
    /// <param name="module">Módulo operativo: Caja → CanCashier; Inventario → CanSell.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Empleado autenticado o null.</returns>
    public async Task<Employee?> ValidatePinAsync(
        string pin,
        OperationalModule module,
        CancellationToken cancellationToken = default)
    {
        if (pin.Length != 4 || !pin.All(char.IsDigit))
            return null;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

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
