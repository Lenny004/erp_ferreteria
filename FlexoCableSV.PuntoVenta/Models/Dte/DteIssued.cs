using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class DteIssued
{
    [Key]
    public long Id { get; set; }
    public long OrderId { get; set; }

    [Required, MaxLength(2)]
    public string DteType { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ControlNumber { get; set; } = string.Empty;
    public Guid GenerationCode { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string? ReceptionStamp { get; set; }

    [Required, MaxLength(20)]
    public string MhStatus { get; set; } = "PENDIENTE";

    [Required]
    [Column(TypeName = "jsonb")]
    public string JsonSent { get; set; } = "{}";

    [Column(TypeName = "jsonb")]
    public string? JsonResponse { get; set; }

    [Required, MaxLength(20)]
    public string PaymentMethod { get; set; } = "EFECTIVO";

    [MaxLength(20)]
    public string? ReceiverNit { get; set; }

    [MaxLength(200)]
    public string? ReceiverName { get; set; }

    [Required, MaxLength(2)]
    public string Environment { get; set; } = "01";
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public short Reprints { get; set; } = 0;

    // Navigation
    [ForeignKey("OrderId")]
    public Order Order { get; set; } = null!;
    public DteContingency? Contingency { get; set; }
}