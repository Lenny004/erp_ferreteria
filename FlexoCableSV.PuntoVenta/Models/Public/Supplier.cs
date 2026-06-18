using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Supplier
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TradeName { get; set; }

    [MaxLength(20)]
    public string? Nit { get; set; }

    [MaxLength(20)]
    public string? Nrc { get; set; }

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

    [Required, MaxLength(5)]
    public string Country { get; set; } = "SV";

    public int CreditDays { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Purchasing is administered by the backend; WPF keeps this read-only shape for lookups.
}
