using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

[Table("stock_alerts", Schema = "public")]
public class StockAlert
{
    [Key]
    public long Id { get; set; }

    public int ProductId { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal CurrentStock { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal MinStock { get; set; }

    public bool IsResolved { get; set; } = false;

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("ProductId")]
    public Product Product { get; set; } = null!;
}
