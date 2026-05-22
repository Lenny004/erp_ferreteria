using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Product
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;
    public int FamilyId { get; set; }
    public int? SubfamilyId { get; set; }
    public int MeasurementTypeId { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal SalePrice { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal CostPrice { get; set; } = 0;

    [Column(TypeName = "numeric(12,3)")]
    public decimal CurrentStock { get; set; } = 0;

    [Column(TypeName = "numeric(12,3)")]
    public decimal MinStock { get; set; } = 0;
    public int? SupplierId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("FamilyId")]
    public Family Family { get; set; } = null!;

    [ForeignKey("SubfamilyId")]
    public Subfamily? Subfamily { get; set; }

    [ForeignKey("MeasurementTypeId")]
    public MeasurementType MeasurementType { get; set; } = null!;

    [ForeignKey("SupplierId")]
    public Supplier? Supplier { get; set; }

    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<StockAlert> StockAlerts { get; set; } = new List<StockAlert>();
}