using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Order
{
    [Key]
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? CashSessionId { get; set; }
    public Guid? CustomerId { get; set; }

    [Required, MaxLength(20)]
    public string OrderType { get; set; } = "VENTA_CAJA";

    public Guid ClientRequestId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(20)]
    public string Status { get; set; } = "PENDIENTE";

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Total { get; set; } = 0;
    public string? Notes { get; set; }
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(CashSessionId))]
    public CashSession? CashSession { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<DteIssued> DteIssued { get; set; } = new List<DteIssued>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
