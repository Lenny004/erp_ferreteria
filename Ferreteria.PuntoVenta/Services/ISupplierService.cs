using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD de proveedores (<see cref="Supplier"/>) con validación y auditoría.</summary>
public interface ISupplierService
{
    /// <summary>Lista proveedores activos (o todos) filtrados por nombre, nombre comercial, contacto o NIT.</summary>
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un proveedor por Id, o null si no existe.</summary>
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea un proveedor. El NIT, si se indica, debe ser único.</summary>
    /// <returns>Id del <see cref="Supplier"/> creado.</returns>
    Task<Guid> CreateAsync(SupplierInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza datos comerciales y de crédito del proveedor.</summary>
    Task UpdateAsync(Guid id, SupplierInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Baja lógica (<c>IsActive = false</c>); no elimina el registro.</summary>
    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Datos de entrada para crear o actualizar un <see cref="Supplier"/>.</summary>
/// <param name="Name">Razón social (obligatorio).</param>
/// <param name="TradeName">Nombre comercial.</param>
/// <param name="Nit">NIT único si se indica.</param>
/// <param name="Nrc">NRC del proveedor.</param>
/// <param name="ContactName">Persona de contacto.</param>
/// <param name="Phone">Teléfono.</param>
/// <param name="Email">Correo; si se envía debe contener '@'.</param>
/// <param name="Address">Dirección.</param>
/// <param name="Municipality">Municipio.</param>
/// <param name="Department">Departamento.</param>
/// <param name="CreditDays">Días de crédito comercial (≥ 0).</param>
/// <param name="Notes">Notas internas.</param>
public sealed record SupplierInput(
    string Name,
    string? TradeName,
    string? Nit,
    string? Nrc,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? Municipality,
    string? Department,
    int CreditDays,
    string? Notes);
