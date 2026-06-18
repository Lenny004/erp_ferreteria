using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class OrderDetail
{
    [Key]
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "numeric(12,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
