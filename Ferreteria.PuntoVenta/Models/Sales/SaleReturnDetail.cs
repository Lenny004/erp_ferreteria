using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Línea de una devolución POS. Tabla <c>sales.ReturnDetails</c>.</summary>
/// <remarks>La cantidad conserva la misma unidad que descontó la venta; hoy no se multiplica por UnitsPerPackage.</remarks>
public class SaleReturnDetail
{
    /// <summary>Identificador de la linea.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Identificador de la devolucion.</summary>
    public Guid ReturnId { get; set; }
    /// <summary>Identificador del detalle original.</summary>
    public Guid OrderDetailId { get; set; }
    /// <summary>Identificador del producto.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Cantidad devuelta en la unidad de la venta.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    /// <summary>Unidades por empaque de la linea original.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal UnitsPerPackage { get; set; } = 1m;

    /// <summary>Precio unitario original.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Costo original de la línea, provisional y a verificar con contador.</summary>
    [Column(TypeName = "numeric(12,4)")]
    public decimal UnitCost { get; set; }

    /// <summary>Descuento de la linea devuelta.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>Subtotal de la linea devuelta.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    /// <summary>Impuesto de la linea devuelta.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal TaxAmount { get; set; }

    /// <summary>Indica si la cantidad fue reingresada al inventario.</summary>
    public bool Restocked { get; set; }

    /// <summary>Cantidad reingresada en la unidad descontada por la venta.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal RestockQuantity { get; set; }

    /// <summary>Identificador del movimiento de kardex generado.</summary>
    public Guid? InventoryMovementId { get; set; }

    /// <summary>Fecha UTC de creacion.</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Cabecera de devolucion relacionada.</summary>
    [ForeignKey(nameof(ReturnId))]
    public SaleReturn? Return { get; set; }

    /// <summary>Detalle original relacionado.</summary>
    [ForeignKey(nameof(OrderDetailId))]
    public OrderDetail? OrderDetail { get; set; }

    /// <summary>Producto relacionado.</summary>
    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }

    /// <summary>Movimiento de kardex relacionado.</summary>
    [ForeignKey(nameof(InventoryMovementId))]
    public InventoryMovement? InventoryMovement { get; set; }
}
