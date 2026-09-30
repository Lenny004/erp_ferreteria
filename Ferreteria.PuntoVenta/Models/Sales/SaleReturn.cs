using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Cabecera de una devolución POS. Tabla <c>sales.Returns</c>.</summary>
/// <remarks>La orden original permanece COMPLETADA; el estado de devolución se deriva de esta tabla.</remarks>
public class SaleReturn
{
    /// <summary>Identificador de la devolucion.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Identificador de la orden original.</summary>
    public Guid OrderId { get; set; }
    /// <summary>Identificador de la sesion de caja, si aplica.</summary>
    public Guid? CashSessionId { get; set; }
    /// <summary>Empleado que ejecuto la devolucion.</summary>
    public Guid EmployeeId { get; set; }
    /// <summary>Empleado cuya autorizacion por PIN habilito la devolucion.</summary>
    public Guid AuthorizedByEmployeeId { get; set; }
    /// <summary>Identificador unico generado por el cliente para idempotencia.</summary>
    public Guid ClientRequestId { get; set; }

    /// <summary>Tipo de devolucion definido por el dominio.</summary>
    [Required, MaxLength(10)]
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>Estado persistido de la devolucion.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Estado fiscal de la devolucion.</summary>
    [Required, MaxLength(20)]
    public string FiscalStatus { get; set; } = string.Empty;

    /// <summary>Identificador de la nota de credito fiscal, cuando exista.</summary>
    public Guid? CreditNoteDteId { get; set; }

    /// <summary>Codigo del motivo de la devolucion.</summary>
    [Required, MaxLength(30)]
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Observaciones opcionales de la devolucion.</summary>
    public string? Notes { get; set; }

    /// <summary>Subtotal bruto de las lineas devueltas.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    /// <summary>Descuento total de las lineas devueltas.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>Impuesto total de las lineas devueltas.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal TaxAmount { get; set; }

    /// <summary>Total de la devolucion.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal Total { get; set; }

    /// <summary>Metodo del reintegro.</summary>
    [Required, MaxLength(20)]
    public string RefundMethod { get; set; } = string.Empty;

    /// <summary>Importe reintegrado al cliente.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal RefundAmount { get; set; }

    /// <summary>Fecha UTC de creacion.</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha UTC de ultima actualizacion.</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Orden original relacionada.</summary>
    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    /// <summary>Sesion de caja relacionada.</summary>
    [ForeignKey(nameof(CashSessionId))]
    public CashSession? CashSession { get; set; }

    /// <summary>Empleado ejecutor relacionado.</summary>
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    /// <summary>Empleado autorizador relacionado.</summary>
    [ForeignKey(nameof(AuthorizedByEmployeeId))]
    public Employee? AuthorizedByEmployee { get; set; }

    /// <summary>Nota de credito DTE relacionada.</summary>
    [ForeignKey(nameof(CreditNoteDteId))]
    public DteIssued? CreditNoteDte { get; set; }

    /// <summary>Lineas de la devolucion.</summary>
    public ICollection<SaleReturnDetail> ReturnDetails { get; set; } = new List<SaleReturnDetail>();
    /// <summary>Movimientos de efectivo generados por la devolucion.</summary>
    public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
}
