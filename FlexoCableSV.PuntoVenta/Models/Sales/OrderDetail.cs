using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

/// <summary>Línea de producto en una orden. Tabla <c>sales.OrderDetails</c>.</summary>
public class OrderDetail
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    /// <summary>Precio de venta congelado al registrar la línea.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Costo congelado para margen y kardex.</summary>
    [Column(TypeName = "numeric(12,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
