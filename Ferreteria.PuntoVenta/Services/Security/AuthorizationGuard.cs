using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.Security;

/// <summary>
/// Guarda de autorización que no confía en la copia de empleado mantenida por la UI.
/// </summary>
public sealed class AuthorizationGuard : IAuthorizationGuard
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentSessionService _currentSession;
    private readonly AuthorizationOptions _options;
    private readonly ILogger<AuthorizationGuard> _logger;

    /// <summary>Inicializa el guard con acceso a sesión, configuración y datos.</summary>
    public AuthorizationGuard(
        IServiceScopeFactory scopeFactory,
        ICurrentSessionService currentSession,
        IOptions<AuthorizationOptions> options,
        ILogger<AuthorizationGuard> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AuthorizedEmployee> RequireAsync(
        PosPermission permission,
        Guid? actingEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        var sessionEmployeeId = _currentSession.CurrentEmployee?.Id;
        if (sessionEmployeeId is null)
        {
            return Reject(permission, null, "sin sesión");
        }

        if (actingEmployeeId is not null && actingEmployeeId != sessionEmployeeId)
        {
            return Reject(permission, sessionEmployeeId, "identificador actuante no coincide");
        }

        var employee = await LoadCurrentEmployeeAsync(sessionEmployeeId.Value, cancellationToken);
        if (employee is null)
        {
            return Reject(permission, sessionEmployeeId, "empleado inexistente");
        }

        var snapshot = PosAuthorizationPolicy.Snapshot(employee);
        if (!PosAuthorizationPolicy.IsAllowed(snapshot, permission, _options.PuestosAdministracion))
        {
            return Reject(permission, snapshot.Id, "permiso insuficiente o empleado inactivo");
        }

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<PosPermission>> GetGrantedPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var sessionEmployeeId = _currentSession.CurrentEmployee?.Id;
        if (sessionEmployeeId is null)
        {
            return new HashSet<PosPermission>();
        }

        var employee = await LoadCurrentEmployeeAsync(sessionEmployeeId.Value, cancellationToken);
        if (employee is null)
        {
            return new HashSet<PosPermission>();
        }

        return PosAuthorizationPolicy.GetGrantedPermissions(
            PosAuthorizationPolicy.Snapshot(employee),
            _options.PuestosAdministracion);
    }

    private async Task<Models.Employee?> LoadCurrentEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Employees.AsNoTracking()
            .Include(employee => employee.Position)
            .SingleOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
    }

    private AuthorizedEmployee Reject(PosPermission permission, Guid? employeeId, string reason)
    {
        _logger.LogWarning(
            "Operación POS rechazada. Permiso: {Permission}; Empleado: {EmployeeId}; Motivo: {Reason}",
            permission,
            employeeId,
            reason);
        throw new UnauthorizedOperationException(permission);
    }
}
