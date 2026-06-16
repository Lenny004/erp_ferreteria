using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class StockAlert
{
    [Key]
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal CurrentStock { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal MinStock { get; set; }
    public bool IsResolved { get; set; } = false;
    [Column(TypeName = "timestamptz")]
    public DateTime? ResolvedAt { get; set; }
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
