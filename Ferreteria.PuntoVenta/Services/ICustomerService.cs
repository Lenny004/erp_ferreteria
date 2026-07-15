using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>CRUD de clientes (<see cref="Customer"/>) con validación y auditoría.</summary>
public interface ICustomerService
{
    /// <summary>Lista clientes activos (o todos) filtrados por nombre, NIT o DUI.</summary>
    Task<IReadOnlyList<Customer>> GetCustomersAsync(
        string? searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un cliente por Id, o null si no existe.</summary>
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea un cliente. Para tipo CCF el NIT es obligatorio (El Salvador).</summary>
    /// <returns>Id del <see cref="Customer"/> creado.</returns>
    Task<Guid> CreateAsync(CustomerInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza datos fiscales y de contacto del cliente.</summary>
    Task UpdateAsync(Guid id, CustomerInput input, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Baja lógica (<c>IsActive = false</c>); no elimina el registro.</summary>
    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Datos de entrada para crear o actualizar un <see cref="Customer"/>.</summary>
/// <param name="CustomerType">CF = consumidor final | CCF = crédito fiscal.</param>
/// <param name="Name">Razón social o nombre comercial.</param>
/// <param name="Dui">Documento Único de Identidad (opcional).</param>
/// <param name="Nit">NIT; obligatorio si CustomerType = CCF.</param>
/// <param name="Nrc">Número de registro de contribuyente.</param>
/// <param name="Phone">Teléfono de contacto.</param>
/// <param name="Email">Correo; si se envía debe contener '@'.</param>
/// <param name="Address">Dirección fiscal.</param>
/// <param name="Municipality">Municipio (El Salvador).</param>
/// <param name="Department">Departamento (El Salvador).</param>
public sealed record CustomerInput(
    string CustomerType,
    string Name,
    string? Dui,
    string? Nit,
    string? Nrc,
    string? Phone,
    string? Email,
    string? Address,
    string? Municipality,
    string? Department);
