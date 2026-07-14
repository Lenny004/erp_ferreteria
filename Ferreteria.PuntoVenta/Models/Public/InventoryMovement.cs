using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Kardex de inventario. Tabla <c>public.InventoryMovements</c>.</summary>
public class InventoryMovement
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>ENTRADA, SALIDA, AJUSTE, VENTA, etc.</summary>
    [Required, MaxLength(30)]
    public string MovementType { get; set; } = string.Empty;

    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "numeric(12,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "numeric(12,4)")]
    public decimal TotalCost { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal StockBefore { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal StockAfter { get; set; }

    public Guid? OrderId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? EmployeeId { get; set; }

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
