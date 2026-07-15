using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Proveedor. Tabla <c>purchasing.Suppliers</c>.</summary>
public class Supplier
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Razón social.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Nombre comercial.</summary>
    [MaxLength(200)]
    public string? TradeName { get; set; }

    /// <summary>NIT (único si se indica).</summary>
    [MaxLength(20)]
    public string? Nit { get; set; }

    /// <summary>NRC del contribuyente.</summary>
    [MaxLength(20)]
    public string? Nrc { get; set; }

    /// <summary>Persona de contacto.</summary>
    [MaxLength(150)]
    public string? ContactName { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? Municipality { get; set; }

    [MaxLength(50)]
    public string? Department { get; set; }

    /// <summary>Código ISO de país; por defecto SV (El Salvador).</summary>
    [Required, MaxLength(5)]
    public string Country { get; set; } = "SV";

    /// <summary>Días de crédito comercial otorgados al negocio.</summary>
    public int CreditDays { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
