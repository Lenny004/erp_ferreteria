using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Cliente de la ferretería (datos fiscales básicos). Tabla <c>public.Customers</c>.</summary>
public class Customer
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>CF = consumidor final | CCF = crédito fiscal (requiere NIT/NRC).</summary>
    [Required, MaxLength(5)]
    public string CustomerType { get; set; } = "CF";

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? Dui { get; set; }

    [MaxLength(20)]
    public string? Nit { get; set; }

    [MaxLength(20)]
    public string? Nrc { get; set; }

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

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
