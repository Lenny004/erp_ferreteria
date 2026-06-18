using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class DteConfig
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(20)]
    public string EmisorNit { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string EmisorNrc { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string EmisorName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? EmisorTradeName { get; set; }

    [Required, MaxLength(10)]
    public string ActividadEconomica { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string AddressLine { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Municipality { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Department { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [Required, MaxLength(5)]
    public string Ambiente { get; set; } = "00";

    public bool IsActive { get; set; } = true;
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
