using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ferreteria.PuntoVenta.Services.Security;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD de clientes (esquema <c>public.Customers</c>) con validación y auditoría.</summary>
/// <param name="scopeFactory">Fábrica de ámbitos de datos.</param>
/// <param name="auditService">Servicio de auditoría.</param>
/// <param name="authorizationGuard">Guard obligatorio de autorización.</param>
public sealed class CustomerService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService,
    IAuthorizationGuard authorizationGuard) : ICustomerService
{
    private readonly IAuthorizationGuard _authorizationGuard = authorizationGuard
        ?? throw new ArgumentNullException(nameof(authorizationGuard));
    private const string TableName = "public.Customers";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(
        string? searchText,
        bool includeInactive = false,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await RequireCashOperationAsync(Guid.Empty, cancellationToken, allowMissingActingEmployee: true);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var query = db.Customers.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var like = $"%{searchText.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, like) ||
                (c.Nit != null && EF.Functions.ILike(c.Nit, like)) ||
                (c.Dui != null && EF.Functions.ILike(c.Dui, like)) ||
                (c.Nrc != null && EF.Functions.ILike(c.Nrc, like)));
        }

        take = Math.Clamp(take, 1, 100);
        return await query.OrderBy(c => c.Name).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await RequireCashOperationAsync(Guid.Empty, cancellationToken, allowMissingActingEmployee: true);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(CustomerInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireCashOperationAsync(userId, cancellationToken);
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var nit = Normalize(input.Nit);
        if (nit is not null && await db.Customers.AnyAsync(c => c.Nit == nit, cancellationToken))
        {
            throw new ValidationException($"Ya existe un cliente con el NIT '{nit}'.");
        }

        var dui = Normalize(input.Dui);
        if (dui is not null && await db.Customers.AnyAsync(c => c.Dui == dui, cancellationToken))
        {
            throw new ValidationException($"Ya existe un cliente con el DUI '{dui}'.");
        }

        var nrc = Normalize(input.Nrc);
        if (nrc is not null && await db.Customers.AnyAsync(c => c.Nrc == nrc, cancellationToken))
        {
            throw new ValidationException($"Ya existe un cliente con el NRC '{nrc}'.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CustomerType = input.CustomerType,
            Name = input.Name.Trim(),
            Dui = dui,
            Nit = nit,
            Nrc = nrc,
            Phone = Normalize(input.Phone),
            Email = Normalize(input.Email),
            Address = Normalize(input.Address),
            Municipality = Normalize(input.Municipality),
            Department = Normalize(input.Department),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("INSERT", TableName, customer.Id.ToString(),
            null, new { customer.Name, customer.CustomerType, customer.Nit }, actor, cancellationToken);

        return customer.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Guid id, CustomerInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireCashOperationAsync(userId, cancellationToken);
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("cliente", id);

        var nit = Normalize(input.Nit);
        if (nit is not null && await db.Customers.AnyAsync(c => c.Nit == nit && c.Id != id, cancellationToken))
        {
            throw new ValidationException($"Ya existe otro cliente con el NIT '{nit}'.");
        }

        var before = new { customer.Name, customer.CustomerType, customer.Nit };

        customer.CustomerType = input.CustomerType;
        customer.Name = input.Name.Trim();
        customer.Dui = Normalize(input.Dui);
        customer.Nit = nit;
        customer.Nrc = Normalize(input.Nrc);
        customer.Phone = Normalize(input.Phone);
        customer.Email = Normalize(input.Email);
        customer.Address = Normalize(input.Address);
        customer.Municipality = Normalize(input.Municipality);
        customer.Department = Normalize(input.Department);
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("UPDATE", TableName, customer.Id.ToString(),
            before, new { customer.Name, customer.CustomerType, customer.Nit }, actor, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequireCashOperationAsync(userId, cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("cliente", id);

        if (!customer.IsActive)
        {
            return;
        }

        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("DELETE", TableName, customer.Id.ToString(),
            new { customer.Name, IsActive = true }, new { IsActive = false }, actor, cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(CustomerInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ValidationException("El nombre del cliente es obligatorio.");
        if (input.CustomerType is not ("CF" or "CCF"))
            throw new ValidationException("El tipo de cliente debe ser CF o CCF.");
        if (input.CustomerType == "CCF" && string.IsNullOrWhiteSpace(input.Nit))
            throw new ValidationException("Un cliente de crédito fiscal (CCF) requiere NIT.");
        if (input.CustomerType == "CCF" && string.IsNullOrWhiteSpace(input.Nrc))
            throw new ValidationException("Un cliente de crédito fiscal (CCF) requiere NRC.");
        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@'))
            throw new ValidationException("El correo electrónico no es válido.");

        ValidateLength(input.Name, 200, "nombre");
        ValidateLength(input.Dui, 15, "DUI");
        ValidateLength(input.Nit, 20, "NIT");
        ValidateLength(input.Nrc, 20, "NRC");
        ValidateLength(input.Phone, 20, "teléfono");
        ValidateLength(input.Email, 100, "correo");
        ValidateLength(input.Address, 300, "dirección");
        ValidateLength(input.Municipality, 100, "municipio");
        ValidateLength(input.Department, 50, "departamento");
    }

    private static void ValidateLength(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Trim().Length > maxLength)
        {
            throw new ValidationException($"El campo {fieldName} no puede superar {maxLength} caracteres.");
        }
    }

    private async Task<Guid> RequireCashOperationAsync(Guid actingEmployeeId, CancellationToken cancellationToken, bool allowMissingActingEmployee = false)
    {
        return (await _authorizationGuard.RequireAsync(
            PosPermission.OperarCaja,
            allowMissingActingEmployee ? null : actingEmployeeId,
            cancellationToken)).Id;
    }
}
