using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Kardex de inventario (<see cref="Product"/>). Tabla <c>public.InventoryMovements</c>.</summary>
public class InventoryMovement
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>
    /// Tipo oficial: ENTRADA_COMPRA, ENTRADA_DEVOLUCION, SALIDA_VENTA, AJUSTE_ENTRADA, AJUSTE_SALIDA
    /// (ver <c>SalesDomainConstants.InventoryMovementTypes</c>).
    /// </summary>
    [Required, MaxLength(30)]
    public string MovementType { get; set; } = string.Empty;

    /// <summary>Cantidad movida en unidad base del producto.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    /// <summary>Costo unitario al momento del movimiento.</summary>
    [Column(TypeName = "numeric(12,4)")]
    public decimal UnitCost { get; set; }

    /// <summary>Costo total = Quantity × UnitCost.</summary>
    [Column(TypeName = "numeric(12,4)")]
    public decimal TotalCost { get; set; }

    /// <summary>Stock del producto antes del movimiento.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal StockBefore { get; set; }

    /// <summary>Stock del producto después del movimiento.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal StockAfter { get; set; }

    /// <summary>Orden de venta asociada (salidas por venta).</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Orden de compra asociada (entradas por compra; esquema purchasing).</summary>
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Empleado que registró el movimiento.</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>Motivo legible (venta, ajuste, devolución…).</summary>
    [MaxLength(300)]
    public string? Reason { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}
