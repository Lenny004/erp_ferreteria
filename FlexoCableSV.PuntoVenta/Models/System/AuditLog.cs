using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

/// <summary>Bitácora de cambios. Tabla <c>system.AuditLog</c>.</summary>
public class AuditLog
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string TableName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>INSERT, UPDATE, DELETE.</summary>
    [Required, MaxLength(10)]
    public string Action { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string? OldData { get; set; }

    [Column(TypeName = "jsonb")]
    public string? NewData { get; set; }

    public Guid? UserId { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
