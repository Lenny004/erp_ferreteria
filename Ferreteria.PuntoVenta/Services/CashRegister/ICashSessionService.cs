namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Abre, consulta y cierra turnos de caja con validación transaccional.</summary>
public interface ICashSessionService
{
    /// <summary>Obtiene la sesión ABIERTA de una caja física.</summary>
    /// <param name="cashRegisterCode">Código de la caja.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La sesión abierta o <c>null</c> si no existe.</returns>
    Task<Models.CashSession?> GetOpenSessionAsync(
        string cashRegisterCode,
        CancellationToken cancellationToken = default);

    /// <summary>Abre un turno si el empleado puede operar caja y no hay otro turno en la caja.</summary>
    /// <param name="employeeId">Empleado autenticado que abre el turno.</param>
    /// <param name="cashRegisterCode">Código de la caja física.</param>
    /// <param name="openingAmount">Fondo inicial en efectivo.</param>
    /// <param name="notes">Observación opcional de apertura.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La sesión ABIERTA creada.</returns>
    /// <exception cref="CashSessionException">Si el empleado no está autorizado o la caja ya está abierta.</exception>
    /// <remarks>
    /// Ejecuta una transacción <see cref="System.Data.IsolationLevel.Serializable"/> y vuelve a consultar
    /// las sesiones ABIERTAS por caja. Reintenta de forma acotada un conflicto PostgreSQL <c>40001</c>
    /// y traduce una violación única <c>23505</c> a un mensaje operativo claro.
    /// </remarks>
    Task<Models.CashSession> OpenAsync(
        Guid employeeId,
        string cashRegisterCode,
        decimal openingAmount,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>Calcula el resumen de una sesión sin modificarla.</summary>
    /// <param name="sessionId">Identificador de la sesión.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resumen de pagos, ventas, DTE y efectivo esperado.</returns>
    /// <param name="requestedByEmployeeId">Empleado activo que solicita el resumen.</param>
    /// <remarks>La firma se amplía respecto de la especificación inicial para aplicar autorización en servidor.</remarks>
    Task<CashRegisterSummary> GetSummaryAsync(
        Guid sessionId,
        Guid requestedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra una sesión ABIERTA con el efectivo contado por un cajero autorizado.</summary>
    /// <param name="sessionId">Identificador de la sesión.</param>
    /// <param name="declaredCash">Efectivo contado.</param>
    /// <param name="notes">Observación del cierre.</param>
    /// <param name="closedByEmployeeId">Empleado que confirma el cierre.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Totales y valores persistidos del cierre.</returns>
    /// <exception cref="CashSessionException">Si la sesión no está abierta, no existe o el empleado no puede cerrar.</exception>
    /// <remarks>
    /// Usa una transacción <see cref="System.Data.IsolationLevel.Serializable"/>, vuelve a leer la sesión
    /// y sus pagos dentro de ella, calcula el esperado y solo después guarda declarado, esperado, diferencia,
    /// estado CERRADA y hora UTC. Una segunda confirmación observa la sesión ya cerrada y no la edita.
    /// </remarks>
    Task<CashSessionCloseResult> CloseAsync(
        Guid sessionId,
        decimal declaredCash,
        string? notes,
        Guid closedByEmployeeId,
        CancellationToken cancellationToken = default);
}

