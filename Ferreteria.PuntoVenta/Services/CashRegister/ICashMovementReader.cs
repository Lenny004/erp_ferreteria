using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services.Returns;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Lee movimientos de efectivo asociados a una sesión de caja.</summary>
public interface ICashMovementReader
{
    /// <summary>Obtiene el total de devoluciones en efectivo de una sesión.</summary>
    /// <param name="dbContext">Contexto EF de la transacción activa.</param>
    /// <param name="cashSessionId">Identificador de la sesión de caja.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Monto positivo acumulado de devoluciones.</returns>
    Task<decimal> GetCashRefundsAsync(FerreteriaDbContext dbContext, Guid cashSessionId, CancellationToken cancellationToken = default);
}

/// <summary>Lee devoluciones de efectivo persistidas en <c>sales.CashMovements</c>.</summary>
public sealed class CashMovementsCashMovementReader : ICashMovementReader
{
    /// <inheritdoc />
    public async Task<decimal> GetCashRefundsAsync(FerreteriaDbContext dbContext, Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return await dbContext.CashMovements
            .Where(movement => movement.CashSessionId == cashSessionId && movement.MovementType == ReturnDomainConstants.CashMovementTypes.CashRefund)
            .Select(movement => (decimal?)movement.Amount)
            .SumAsync(cancellationToken) ?? 0m;
    }
}
