using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Doble explícito de lockout para pruebas unitarias que no requieren PostgreSQL.
/// Las pruebas de persistencia usan siempre el servicio real y Testcontainers.
/// </summary>
internal sealed class TestPinAttemptService : IPinAttemptService
{
    private readonly TimeProvider _clock;
    private readonly List<PinLockoutEvent> _events = [];

    /// <summary>Inicializa el doble con el reloj que controla el caso.</summary>
    /// <param name="clock">Reloj determinista de la prueba.</param>
    public TestPinAttemptService(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public Task<PinAttemptStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate());
    }

    /// <inheritdoc />
    public Task<PinAttemptStatus> RegisterFailedAttemptAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = Evaluate();
        if (!status.IsLocked)
        {
            _events.Add(new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, _clock.GetUtcNow()));
        }

        return Task.FromResult(Evaluate());
    }

    /// <inheritdoc />
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _events.Add(new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinOk, _clock.GetUtcNow()));
        return Task.CompletedTask;
    }

    /// <summary>Devuelve el estado del doble para aserciones del caso de prueba.</summary>
    /// <returns>Estado calculado con los eventos registrados.</returns>
    public PinAttemptStatus GetCurrentStatus() => Evaluate();

    private PinAttemptStatus Evaluate() => PinLockoutPolicy.Evaluate(_events, _clock.GetUtcNow());
}
