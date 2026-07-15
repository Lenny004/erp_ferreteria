using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Turno de caja del cajero. Tabla <c>sales.CashSessions</c>.</summary>
public class CashSession
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Cajero responsable del turno (<see cref="Employee"/>).</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Código de la caja física (ej. CAJA-01).</summary>
    [Required, MaxLength(50)]
    public string CashRegisterCode { get; set; } = "CAJA-01";

    /// <summary>Apertura del turno (UTC).</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Cierre del turno; null mientras esté abierta.</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime? ClosedAt { get; set; }

    /// <summary>Fondo inicial en efectivo al abrir.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal OpeningAmount { get; set; }

    /// <summary>Efectivo declarado por el cajero al cerrar.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal? ClosingDeclaredAmount { get; set; }

    /// <summary>Efectivo esperado según ventas del sistema.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal? ClosingExpectedAmount { get; set; }

    /// <summary>Diferencia = declarado − esperado (sobrante/faltante).</summary>
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
