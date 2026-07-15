using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Pago de una <see cref="Order"/> completada. Tabla <c>sales.Payments</c>.</summary>
public class Payment
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Orden a la que se aplica el pago.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Turno de caja opcional asociado al cobro.</summary>
    public Guid? CashSessionId { get; set; }

    /// <summary>EFECTIVO, TARJETA, TRANSFERENCIA, OTRO (ver <c>SalesDomainConstants.PaymentMethods</c>).</summary>
    [Required, MaxLength(20)]
    public string Method { get; set; } = string.Empty;

    /// <summary>Monto cobrado con este medio.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal Amount { get; set; }

    /// <summary>Referencia de voucher, transferencia o autorización.</summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(CashSessionId))]
    public CashSession? CashSession { get; set; }
}
