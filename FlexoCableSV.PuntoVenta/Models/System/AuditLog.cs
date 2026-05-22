using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class AuditLog
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string TableName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? RecordId { get; set; }

    [Required, MaxLength(6)]
    public string Action { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string? OldData { get; set; }

    [Column(TypeName = "jsonb")]
    public string? NewData { get; set; }
    public string? Description { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}