using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ferreteria.PuntoVenta.Services.Security;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Gestión básica de empleados / usuarios (nombre, cargo, PIN, permisos) sobre <c>hr.Employees</c>.
/// El PIN se guarda como hash bcrypt (12 rounds) y nunca en texto plano.
/// <param name="scopeFactory">Fábrica de ámbitos de datos.</param>
/// <param name="auditService">Servicio de auditoría.</param>
/// <param name="authorizationGuard">Guard obligatorio de autorización.</param>
/// <param name="authorizationOptions">Configuración de puestos administrativos.</param>
/// </summary>
public sealed class EmployeeService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService,
    IAuthorizationGuard authorizationGuard,
    IOptions<AuthorizationOptions> authorizationOptions) : IEmployeeService
{
    private readonly IAuthorizationGuard _authorizationGuard = authorizationGuard
        ?? throw new ArgumentNullException(nameof(authorizationGuard));
    private readonly HashSet<string> _administrationPositions = CreateAdministrationPositions(authorizationOptions);
    private const string TableName = "hr.Employees";
    private static readonly string[] ValidContractTypes = ["PLAZO_FIJO", "TIEMPO_PARCIAL", "HONORARIOS", "PASANTE"];
    private static readonly string[] ValidSalaryTypes = ["MENSUAL", "QUINCENAL", "SEMANAL"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministrationAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var query = db.Employees.AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.Department)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var like = $"%{searchText.Trim()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.FirstName, like) ||
                EF.Functions.ILike(e.LastName, like) ||
                (e.Dui != null && EF.Functions.ILike(e.Dui, like)));
        }

        return await query
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await RequireAdministrationAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Employees.AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(EmployeeInput input, string? pin, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAdministrationAsync(cancellationToken, userId);
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var dui = Normalize(input.Dui);
        if (dui is not null && await db.Employees.AnyAsync(e => e.Dui == dui, cancellationToken))
        {
            throw new ValidationException($"Ya existe un empleado con el DUI '{dui}'.");
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Dui = dui,
            PositionId = input.PositionId,
            DepartmentId = input.DepartmentId,
            HireDate = input.HireDate,
            BaseSalary = input.BaseSalary,
            ContractType = input.ContractType,
            SalaryType = input.SalaryType,
            Phone = Normalize(input.Phone),
            Email = Normalize(input.Email),
            CanCashier = input.CanCashier,
            CanSell = input.CanSell,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(pin))
        {
            ValidatePin(pin);
            await EnsurePinAvailableAsync(db, pin, null, cancellationToken);
            employee.PinHash = BCrypt.Net.BCrypt.HashPassword(pin, 12);
            employee.PinUpdatedAt = DateTime.UtcNow;
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);
        db.AuditLogs.Add(AuditService.CreateChangeEntry(
            "INSERT", TableName, employee.Id.ToString(), null,
            new { employee.FirstName, employee.LastName, employee.CanCashier, employee.CanSell }, actor.Id));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return employee.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Guid id, EmployeeInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAdministrationAsync(cancellationToken, userId);
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAdministratorStateAsync(db, cancellationToken);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("empleado", id);

        var dui = Normalize(input.Dui);
        if (dui is not null && await db.Employees.AnyAsync(e => e.Dui == dui && e.Id != id, cancellationToken))
        {
            throw new ValidationException($"Ya existe otro empleado con el DUI '{dui}'.");
        }

        var before = new { employee.FirstName, employee.LastName, employee.CanCashier, employee.CanSell };
        var currentPositionId = employee.PositionId;

        employee.FirstName = input.FirstName.Trim();
        employee.LastName = input.LastName.Trim();
        employee.Dui = dui;
        employee.PositionId = input.PositionId;
        employee.DepartmentId = input.DepartmentId;
        employee.HireDate = input.HireDate;
        employee.BaseSalary = input.BaseSalary;
        employee.ContractType = input.ContractType;
        employee.SalaryType = input.SalaryType;
        employee.Phone = Normalize(input.Phone);
        employee.Email = Normalize(input.Email);
        employee.CanCashier = input.CanCashier;
        employee.CanSell = input.CanSell;
        employee.UpdatedAt = DateTime.UtcNow;

        if (await WouldRemoveLastActiveAdministratorAsync(
                db,
                currentPositionId,
                input.PositionId,
                employee.IsActive,
                deactivating: false,
                cancellationToken: cancellationToken))
        {
            throw new ValidationException("No se puede quitar el último administrador activo del POS.");
        }

        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("UPDATE", TableName, employee.Id.ToString(),
            before, new { employee.FirstName, employee.LastName, employee.CanCashier, employee.CanSell }, actor.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetPinAsync(Guid id, string pin, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAdministrationAsync(cancellationToken, userId);
        ValidatePin(pin);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("empleado", id);

        await EnsurePinAvailableAsync(db, pin, id, cancellationToken);
        employee.PinHash = BCrypt.Net.BCrypt.HashPassword(pin, 12);
        employee.PinUpdatedAt = DateTime.UtcNow;
        employee.UpdatedAt = DateTime.UtcNow;
        // El cambio y su auditoría comparten contexto y transacción; nunca se agrega el PIN ni su hash.
        db.AuditLogs.Add(AuditService.CreateChangeEntry(
            "PIN_CHANGE", TableName, employee.Id.ToString(), null,
            new { PinChanged = true }, actor.Id));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAdministrationAsync(cancellationToken, userId);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAdministratorStateAsync(db, cancellationToken);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("empleado", id);

        if (!employee.IsActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (await WouldRemoveLastActiveAdministratorAsync(
                db,
                employee.PositionId,
                employee.PositionId,
                employee.IsActive,
                deactivating: true,
                cancellationToken: cancellationToken))
        {
            throw new ValidationException("No se puede desactivar el último administrador activo del POS.");
        }

        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("DELETE", TableName, employee.Id.ToString(),
            new { employee.FirstName, employee.LastName, IsActive = true }, new { IsActive = false }, actor.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        await RequireAdministrationAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Position>> GetPositionsAsync(Guid? departmentId, CancellationToken cancellationToken = default)
    {
        await RequireAdministrationAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var query = db.Positions.AsNoTracking().Where(p => p.IsActive);
        if (departmentId is { } depId)
        {
            query = query.Where(p => p.DepartmentId == depId);
        }

        return await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(EmployeeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.FirstName))
            throw new ValidationException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(input.LastName))
            throw new ValidationException("El apellido es obligatorio.");
        if (input.BaseSalary < 0)
            throw new ValidationException("El salario base no puede ser negativo.");
        if (!ValidContractTypes.Contains(input.ContractType))
            throw new ValidationException("Tipo de contrato inválido.");
        if (!ValidSalaryTypes.Contains(input.SalaryType))
            throw new ValidationException("Tipo de salario inválido.");
        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@'))
            throw new ValidationException("El correo electrónico no es válido.");
    }

    private static void ValidatePin(string pin)
    {
        if (pin.Length != 4 || !pin.All(char.IsDigit))
            throw new ValidationException("El PIN debe tener exactamente 4 dígitos.");
    }

    private async Task<AuthorizedEmployee> RequireAdministrationAsync(
        CancellationToken cancellationToken,
        Guid? actingEmployeeId = null)
    {
        return await _authorizationGuard.RequireAsync(
            PosPermission.AdministrarUsuarios,
            actingEmployeeId,
            cancellationToken);
    }

    private static async Task EnsurePinAvailableAsync(
        FerreteriaDbContext db,
        string pin,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        // El lock de transacción serializa altas y cambios de PIN entre procesos POS concurrentes.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({SalesDomainConstants.PinUniquenessAdvisoryLockKey})",
            cancellationToken);
        var hashes = await db.Employees.AsNoTracking()
            .Where(employee => employee.PinHash != null && (employeeId == null || employee.Id != employeeId.Value))
            .Select(employee => employee.PinHash!)
            .ToListAsync(cancellationToken);

        if (hashes.Any(hash => BCrypt.Net.BCrypt.Verify(pin, hash)))
        {
            throw new ValidationException("Ese PIN no está disponible. Elija otro.");
        }
    }

    private static HashSet<string> CreateAdministrationPositions(IOptions<AuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value.PuestosAdministracion
            .Where(position => !string.IsNullOrWhiteSpace(position))
            .Select(position => position.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAdministrationPosition(string? positionName) =>
        positionName is not null && _administrationPositions.Contains(positionName.Trim());

    private async Task LockAdministratorStateAsync(FerreteriaDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({SalesDomainConstants.ActiveAdministratorAdvisoryLockKey})",
            cancellationToken);
    }

    private async Task<bool> WouldRemoveLastActiveAdministratorAsync(
        FerreteriaDbContext db,
        Guid? currentPositionId,
        Guid? resultingPositionId,
        bool employeeIsActive,
        bool deactivating,
        CancellationToken cancellationToken)
    {
        if (!employeeIsActive)
        {
            return false;
        }

        if (currentPositionId is not Guid currentPosition || resultingPositionId is not Guid resultingPosition)
        {
            return false;
        }

        var currentPositionName = await db.Positions
            .Where(position => position.Id == currentPosition)
            .Select(position => position.Name)
            .SingleAsync(cancellationToken);
        var resultingPositionName = await db.Positions
            .Where(position => position.Id == resultingPosition)
            .Select(position => position.Name)
            .SingleAsync(cancellationToken);
        if (!IsAdministrationPosition(currentPositionName)
            || (!deactivating && IsAdministrationPosition(resultingPositionName)))
        {
            return false;
        }

        var activeAdministrators = await db.Employees
            .Where(item => item.IsActive)
            .Join(db.Positions, item => item.PositionId, position => position.Id, (item, position) => position.Name)
            .CountAsync(name => _administrationPositions.Contains(name), cancellationToken);
        return activeAdministrators <= 1;
    }
}
