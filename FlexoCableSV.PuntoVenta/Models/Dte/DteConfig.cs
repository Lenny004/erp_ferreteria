using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class DteConfig
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(2)]
    public string Environment { get; set; } = "00";

    [Required, MaxLength(200)]
    public string ApiUrl { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string IssuerNit { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string IssuerName { get; set; } = string.Empty;

    [MaxLength(9)]
    public string? IssuerNrc { get; set; }

    [MaxLength(10)]
    public string? ActivityCode { get; set; }

    [MaxLength(200)]
    public string? ActivityDescription { get; set; }
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? CertificatePath { get; set; }
    public string? CertificateKey { get; set; }
    public bool IsActive { get; set; } = true;
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
