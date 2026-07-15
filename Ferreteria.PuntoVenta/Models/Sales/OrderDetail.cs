using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Línea de producto en una orden. Tabla <c>sales.OrderDetails</c>.</summary>
public class OrderDetail
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Presentación de venta usada (unidad, docena, caja...). Opcional.</summary>
    public Guid? SaleUnitId { get; set; }

    /// <summary>Cantidad en la presentación vendida (ej: 2 cajas).</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    /// <summary>Unidades base por presentación (UNIDAD=1, DOCENA=12) para descontar stock.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal UnitsPerPackage { get; set; } = 1;

    /// <summary>Precio de venta congelado al registrar la línea.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Costo congelado para margen y kardex.</summary>
    [Column(TypeName = "numeric(12,4)")]
    public decimal UnitCost { get; set; }

    /// <summary>Descuento aplicado a la línea (ej: por volumen).</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    /// <summary>Importe de la línea (Quantity × UnitPrice − DiscountAmount).</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(SaleUnitId))]
    public SaleUnit? SaleUnit { get; set; }
}
