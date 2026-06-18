using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

/// <summary>Cabecera de venta u orden de confección. Tabla <c>sales.Orders</c>.</summary>
public class Order
{
    [Key]
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid? CashSessionId { get; set; }
    public Guid? CustomerId { get; set; }

    /// <summary>VENTA_CAJA | ORDEN_CONFECCION.</summary>
    [Required, MaxLength(20)]
    public string OrderType { get; set; } = "VENTA_CAJA";

    /// <summary>Idempotencia del cliente WPF ante reintentos offline.</summary>
    public Guid ClientRequestId { get; set; } = Guid.NewGuid();

    /// <summary>PENDIENTE | COMPLETADA | CANCELADA.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "PENDIENTE";

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Total { get; set; } = 0;

    /// <summary>En confección puede incluir cliente/teléfono hasta tener campos dedicados.</summary>
    public string? Notes { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(CashSessionId))]
    public CashSession? CashSession { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<DteIssued> DteIssued { get; set; } = new List<DteIssued>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
