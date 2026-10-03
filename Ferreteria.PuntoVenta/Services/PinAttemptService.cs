using System.Text.Json;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Lockout persistente por terminal usando eventos append-only en <c>system.AuditLog</c>.
/// </summary>
public sealed class PinAttemptService : IPinAttemptService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly string _cashRegisterCode;
    private readonly ILogger<PinAttemptService> _logger;
    /// <summary>Construye el lockout persistente del terminal configurado.</summary>
    /// <param name="scopeFactory">Fábrica de contextos EF.</param>
    /// <param name="cashRegisterOptions">Configuración de la caja.</param>
    /// <param name="clock">Reloj inyectado para pruebas deterministas.</param>
    /// <param name="logger">Logger sin secretos.</param>
    public PinAttemptService(
        IServiceScopeFactory scopeFactory,
        IOptions<CashRegisterOptions> cashRegisterOptions,
        TimeProvider clock,
        ILogger<PinAttemptService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ArgumentNullException.ThrowIfNull(cashRegisterOptions);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _cashRegisterCode = CashRegisterInputRules.ValidateCashRegisterCode(cashRegisterOptions.Value.Codigo);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PinAttemptStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            return PinLockoutPolicy.Evaluate(await LoadEventsAsync(db, cancellationToken), _clock.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "No se pudo consultar el lockout persistente de PIN.");
            throw new PinLockoutUnavailableException();
        }
    }

    /// <inheritdoc />
    public Task<PinAttemptStatus> RegisterFailedAttemptAsync(CancellationToken cancellationToken = default) =>
        AppendEventAsync(SalesDomainConstants.PinAuditActions.PinFail, cancellationToken);

    /// <inheritdoc />
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await AppendEventAsync(SalesDomainConstants.PinAuditActions.PinOk, cancellationToken);
    }

    /// <summary>Agrega un evento persistente de éxito o fallo al historial de la caja.</summary>
    /// <param name="action">Acción de PIN que se debe registrar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Estado actualizado del lockout.</returns>
    private async Task<PinAttemptStatus> AppendEventAsync(string action, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({SalesDomainConstants.PinAttemptAdvisoryLockKey})",
                cancellationToken);

            var events = await LoadEventsAsync(db, cancellationToken);
            var current = PinLockoutPolicy.Evaluate(events, _clock.GetUtcNow());
            if (action == SalesDomainConstants.PinAuditActions.PinFail && current.IsLocked)
            {
                await transaction.CommitAsync(cancellationToken);
                return current;
            }

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TableName = SalesDomainConstants.PinAuditActions.TableName,
                RecordId = $"Caja:{_cashRegisterCode}",
                Action = action,
                NewData = JsonSerializer.Serialize(new { terminal = _cashRegisterCode }),
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var updatedEvents = events.Append(new PinLockoutEvent(action, _clock.GetUtcNow()));
            return PinLockoutPolicy.Evaluate(updatedEvents, _clock.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "No se pudo registrar el evento de lockout de PIN {Action}.", action);
            throw new PinLockoutUnavailableException();
        }
    }

    private async Task<IReadOnlyList<PinLockoutEvent>> LoadEventsAsync(
        FerreteriaDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(item => item.TableName == SalesDomainConstants.PinAuditActions.TableName
                && item.RecordId == $"Caja:{_cashRegisterCode}"
                && (item.Action == SalesDomainConstants.PinAuditActions.PinFail
                    || item.Action == SalesDomainConstants.PinAuditActions.PinOk))
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new { item.Action, item.CreatedAt })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(item => item.CreatedAt)
            .Select(item => new PinLockoutEvent(item.Action, new DateTimeOffset(item.CreatedAt, TimeSpan.Zero)))
            .ToArray();
    }
}

/// <summary>Indica que no fue posible consultar o persistir el lockout.</summary>
public sealed class PinLockoutUnavailableException : Exception
{
    /// <summary>Inicializa la excepción con un mensaje seguro para el usuario.</summary>
    public PinLockoutUnavailableException()
        : base("No se pudo verificar la protección del PIN. Revise la conexión e intente de nuevo.")
    {
    }
}
