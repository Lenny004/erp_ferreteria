using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Turno de caja. Tabla <c>sales.CashSessions</c>.</summary>
public class CashSession
{
    [Key]
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string CashRegisterCode { get; set; } = "CAJA-01";

    [Column(TypeName = "timestamptz")]
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime? ClosedAt { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal OpeningAmount { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal? ClosingDeclaredAmount { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal? ClosingExpectedAmount { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal? Difference { get; set; }

    /// <summary>ABIERTA | CERRADA | CANCELADA.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "ABIERTA";

    public string? Notes { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
