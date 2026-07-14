using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD de clientes (esquema <c>public.Customers</c>) con validación y auditoría.</summary>
public sealed class CustomerService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService) : ICustomerService
{
    private const string TableName = "public.Customers";

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
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
                (c.Dui != null && EF.Functions.ILike(c.Dui, like)));
        }

        return await query.OrderBy(c => c.Name).Take(500).ToListAsync(cancellationToken);
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Guid> CreateAsync(CustomerInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var nit = Normalize(input.Nit);
        if (nit is not null && await db.Customers.AnyAsync(c => c.Nit == nit, cancellationToken))
        {
            throw new ValidationException($"Ya existe un cliente con el NIT '{nit}'.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CustomerType = input.CustomerType,
            Name = input.Name.Trim(),
            Dui = Normalize(input.Dui),
            Nit = nit,
            Nrc = Normalize(input.Nrc),
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
            null, new { customer.Name, customer.CustomerType, customer.Nit }, userId, cancellationToken);

        return customer.Id;
    }

    public async Task UpdateAsync(Guid id, CustomerInput input, Guid userId, CancellationToken cancellationToken = default)
    {
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
            before, new { customer.Name, customer.CustomerType, customer.Nit }, userId, cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
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
            new { customer.Name, IsActive = true }, new { IsActive = false }, userId, cancellationToken);
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
        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@'))
            throw new ValidationException("El correo electrónico no es válido.");
    }
}
