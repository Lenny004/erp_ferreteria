using Ferreteria.PuntoVenta.Data;

namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Lee movimientos de efectivo asociados a una sesión de caja.</summary>
public interface ICashMovementReader
{
    /// <summary>Obtiene el total de devoluciones en efectivo de una sesión.</summary>
    /// <param name="dbContext">Contexto EF de la transacción activa.</param>
    /// <param name="cashSessionId">Sesión de caja.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Monto positivo acumulado de devoluciones.</returns>
    /// <remarks>La lectura debe ejecutarse dentro de la misma transacción Serializable del corte.</remarks>
    Task<decimal> GetCashRefundsAsync(
        FerreteriaDbContext dbContext,
        Guid cashSessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Adaptador temporal hasta que exista la tabla de movimientos de caja.</summary>
public sealed class PendingMigrationCashMovementReader : ICashMovementReader
{
    /// <inheritdoc />
    public Task<decimal> GetCashRefundsAsync(
        FerreteriaDbContext dbContext,
        Guid cashSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        // TODO: sumar sales."CashMovements" de tipo DEVOLUCION_EFECTIVO de la sesión cuando exista la migración (depende de ferreteria_backend).
        return Task.FromResult(0m);
    }
}
