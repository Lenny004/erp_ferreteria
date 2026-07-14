using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD de proveedores (esquema <c>purchasing.Suppliers</c>) con validación y auditoría.</summary>
public sealed class SupplierService(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService) : ISupplierService
{
    private const string TableName = "purchasing.Suppliers";

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var query = db.Suppliers.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var like = $"%{searchText.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, like) ||
                (s.TradeName != null && EF.Functions.ILike(s.TradeName, like)) ||
                (s.ContactName != null && EF.Functions.ILike(s.ContactName, like)) ||
                (s.Nit != null && EF.Functions.ILike(s.Nit, like)));
        }

        return await query.OrderBy(s => s.Name).Take(500).ToListAsync(cancellationToken);
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Guid> CreateAsync(SupplierInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var nit = Normalize(input.Nit);
        if (nit is not null && await db.Suppliers.AnyAsync(s => s.Nit == nit, cancellationToken))
        {
            throw new ValidationException($"Ya existe un proveedor con el NIT '{nit}'.");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            TradeName = Normalize(input.TradeName),
            Nit = nit,
            Nrc = Normalize(input.Nrc),
            ContactName = Normalize(input.ContactName),
            Phone = Normalize(input.Phone),
            Email = Normalize(input.Email),
            Address = Normalize(input.Address),
            Municipality = Normalize(input.Municipality),
            Department = Normalize(input.Department),
            Country = "SV",
            CreditDays = Math.Max(0, input.CreditDays),
            Notes = Normalize(input.Notes),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("INSERT", TableName, supplier.Id.ToString(),
            null, new { supplier.Name, supplier.Nit }, userId, cancellationToken);

        return supplier.Id;
    }

    public async Task UpdateAsync(Guid id, SupplierInput input, Guid userId, CancellationToken cancellationToken = default)
    {
        Validate(input);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("proveedor", id);

        var nit = Normalize(input.Nit);
        if (nit is not null && await db.Suppliers.AnyAsync(s => s.Nit == nit && s.Id != id, cancellationToken))
        {
            throw new ValidationException($"Ya existe otro proveedor con el NIT '{nit}'.");
        }

        var before = new { supplier.Name, supplier.Nit, supplier.Phone };

        supplier.Name = input.Name.Trim();
        supplier.TradeName = Normalize(input.TradeName);
        supplier.Nit = nit;
        supplier.Nrc = Normalize(input.Nrc);
        supplier.ContactName = Normalize(input.ContactName);
        supplier.Phone = Normalize(input.Phone);
        supplier.Email = Normalize(input.Email);
        supplier.Address = Normalize(input.Address);
        supplier.Municipality = Normalize(input.Municipality);
        supplier.Department = Normalize(input.Department);
        supplier.CreditDays = Math.Max(0, input.CreditDays);
        supplier.Notes = Normalize(input.Notes);
        supplier.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("UPDATE", TableName, supplier.Id.ToString(),
            before, new { supplier.Name, supplier.Nit, supplier.Phone }, userId, cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("proveedor", id);

        if (!supplier.IsActive)
        {
            return;
        }

        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await auditService.RecordChangeAsync("DELETE", TableName, supplier.Id.ToString(),
            new { supplier.Name, IsActive = true }, new { IsActive = false }, userId, cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(SupplierInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ValidationException("El nombre del proveedor es obligatorio.");
        if (input.Name.Trim().Length > 200)
            throw new ValidationException("El nombre no puede superar 200 caracteres.");
        if (input.CreditDays < 0)
            throw new ValidationException("Los días de crédito no pueden ser negativos.");
        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@'))
            throw new ValidationException("El correo electrónico no es válido.");
    }
}
