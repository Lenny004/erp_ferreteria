using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

public class Payment
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Guid? CashSessionId { get; set; }

    [Required, MaxLength(20)]
    public string Method { get; set; } = string.Empty;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(CashSessionId))]
    public CashSession? CashSession { get; set; }
}
