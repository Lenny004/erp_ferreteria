using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Catálogo de productos. Tabla <c>public.Products</c>.</summary>
public class Product
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Barcode { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>FK a <see cref="Family"/>.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>FK opcional a <see cref="Subfamily"/> dentro de la familia.</summary>
    public Guid? SubfamilyId { get; set; }

    /// <summary>FK a <see cref="MeasurementType"/> (unidad base de inventario).</summary>
    public Guid MeasurementTypeId { get; set; }

    /// <summary>FK opcional al <see cref="Supplier"/> habitual.</summary>
    public Guid? SupplierId { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal SalePrice { get; set; } = 0;

    [Column(TypeName = "numeric(12,4)")]
    public decimal CostPrice { get; set; } = 0;

    /// <summary>Existencias actuales en unidad base.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal CurrentStock { get; set; } = 0;

    /// <summary>Umbral de alerta de stock bajo.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal MinStock { get; set; } = 0;

    /// <summary>Tope de stock opcional.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal? MaxStock { get; set; }

    /// <summary>Nivel de reorden sugerido para compras.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal? ReorderPoint { get; set; }

    /// <summary>Clasificación de rotación ABC: ALTA | MEDIA | BAJA | NULA.</summary>
    [MaxLength(10)]
    public string? RotationClass { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(FamilyId))]
    public Family Family { get; set; } = null!;

    [ForeignKey(nameof(SubfamilyId))]
    public Subfamily? Subfamily { get; set; }

    [ForeignKey(nameof(MeasurementTypeId))]
    public MeasurementType MeasurementType { get; set; } = null!;

    [ForeignKey(nameof(SupplierId))]
    public Supplier? Supplier { get; set; }

    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<StockAlert> StockAlerts { get; set; } = new List<StockAlert>();
    public ICollection<ProductSaleUnit> ProductSaleUnits { get; set; } = new List<ProductSaleUnit>();
    public ICollection<VolumeDiscount> VolumeDiscounts { get; set; } = new List<VolumeDiscount>();
}
