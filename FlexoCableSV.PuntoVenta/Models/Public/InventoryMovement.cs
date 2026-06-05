using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class InventoryMovement
{
    [Key]
    public long Id { get; set; }
    public int ProductId { get; set; }

    [Required, MaxLength(18)]
    public string MovementType { get; set; } = string.Empty;

    [Column(TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal StockBefore { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal StockAfter { get; set; }

    [MaxLength(100)]
    public string? Reason { get; set; }

    [MaxLength(50)]
    public string? DocumentRef { get; set; }
    public int? SupplierId { get; set; }
    public int? EmployeeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(SupplierId))]
    public Supplier? Supplier { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
}