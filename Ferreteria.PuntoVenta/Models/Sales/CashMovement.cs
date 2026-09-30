using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Movimiento de efectivo independiente de los pagos de una venta. Tabla <c>sales.CashMovements</c>.</summary>
public class CashMovement
{
    /// <summary>Identificador del movimiento.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Identificador de la sesion de caja.</summary>
    public Guid CashSessionId { get; set; }

    /// <summary>Tipo de movimiento de efectivo.</summary>
    [Required, MaxLength(30)]
    public string MovementType { get; set; } = string.Empty;

    /// <summary>Importe del movimiento.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal Amount { get; set; }

    /// <summary>Identificador de la devolucion relacionada.</summary>
    public Guid? ReturnId { get; set; }
    /// <summary>Empleado que ejecuto el movimiento.</summary>
    public Guid EmployeeId { get; set; }
    /// <summary>Empleado que autorizo el movimiento.</summary>
    public Guid? AuthorizedByEmployeeId { get; set; }
    /// <summary>Identificador unico de solicitud.</summary>
    public Guid ClientRequestId { get; set; }

    /// <summary>Motivo del movimiento.</summary>
    [MaxLength(300)]
    public string? Reason { get; set; }

    /// <summary>Fecha UTC de creacion.</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Sesion de caja relacionada.</summary>
    [ForeignKey(nameof(CashSessionId))]
    public CashSession? CashSession { get; set; }

    /// <summary>Devolucion relacionada.</summary>
    [ForeignKey(nameof(ReturnId))]
    public SaleReturn? Return { get; set; }

    /// <summary>Empleado ejecutor relacionado.</summary>
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    /// <summary>Empleado autorizador relacionado.</summary>
    [ForeignKey(nameof(AuthorizedByEmployeeId))]
    public Employee? AuthorizedByEmployee { get; set; }
}
