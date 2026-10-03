using System.Text.Json;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Servicio administrativo para reiniciar el bloqueo de PIN de una terminal.
/// </summary>
/// <remarks>
/// La autorización se valida contra la sesión y la base de datos. La auditoría y el
/// desbloqueo lógico comparten una transacción explícita para evitar operaciones parciales.
/// </remarks>
public sealed class PinUnlockService : IPinUnlockService
{
    private const int MaximumReasonLength = 300;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthorizationGuard _authorizationGuard;
    private readonly TimeProvider _clock;
    private readonly ILogger<PinUnlockService> _logger;

    /// <summary>
    /// Inicializa el servicio de desbloqueo de terminales.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos para acceder al contexto EF.</param>
    /// <param name="authorizationGuard">Guard que comprueba el actuante y su permiso.</param>
    /// <param name="logger">Logger para incidentes sin datos sensibles.</param>
    /// <param name="clock">Reloj UTC usado para ordenar la auditoría con los intentos.</param>
    public PinUnlockService(
        IServiceScopeFactory scopeFactory,
        IAuthorizationGuard authorizationGuard,
        TimeProvider clock,
        ILogger<PinUnlockService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _authorizationGuard = authorizationGuard ?? throw new ArgumentNullException(nameof(authorizationGuard));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task UnlockTerminalAsync(
        string cashRegisterCode,
        Guid actingEmployeeId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = CashRegisterInputRules.ValidateCashRegisterCode(cashRegisterCode);
        var normalizedReason = NormalizeReason(reason);
        var actor = await _authorizationGuard.RequireAsync(
            PosPermission.AdministrarUsuarios,
            actingEmployeeId,
            cancellationToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({SalesDomainConstants.PinAttemptAdvisoryLockKey})",
                cancellationToken);

            var recordId = $"Caja:{normalizedCode}";
            // Bajo el mismo advisory lock que PinAttemptService: el desbloqueo queda después del último evento
            // de la terminal aunque esta máquina tenga el reloj atrasado o repita el mismo instante.
            var latestEventUtc = await db.AuditLogs.AsNoTracking()
                .Where(item => item.TableName == SalesDomainConstants.PinAuditActions.TableName
                    && item.RecordId == recordId)
                .Select(item => (DateTime?)item.CreatedAt)
                .MaxAsync(cancellationToken);
            var createdAtUtc = PinLockoutPolicy.NextEventTimestamp(
                latestEventUtc is { } latest ? new DateTimeOffset(latest, TimeSpan.Zero) : null,
                _clock.GetUtcNow());

            var auditData = new Dictionary<string, string>
            {
                ["terminal"] = normalizedCode
            };
            if (normalizedReason is not null)
            {
                auditData["reason"] = normalizedReason;
            }

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TableName = SalesDomainConstants.PinAuditActions.TableName,
                RecordId = recordId,
                Action = SalesDomainConstants.PinAuditActions.PinUnlock,
                UserId = actor.Id,
                NewData = JsonSerializer.Serialize(auditData),
                CreatedAt = createdAtUtc.UtcDateTime
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "No se pudo registrar el desbloqueo de la terminal {CashRegisterCode}.",
                normalizedCode);
            throw new PinLockoutUnavailableException();
        }
    }

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > MaximumReasonLength)
        {
            throw new ArgumentException(
                $"El motivo no puede superar {MaximumReasonLength} caracteres.",
                nameof(reason));
        }

        return normalized;
    }
}
