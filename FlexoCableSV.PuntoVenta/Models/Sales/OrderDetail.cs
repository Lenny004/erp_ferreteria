using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class OrderDetail
{
    [Key]
    public long Id { get; set; }
    public long OrderId { get; set; }
    public int ProductId { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}