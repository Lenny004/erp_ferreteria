using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Alerta de stock bajo mínimo. Tabla <c>public.StockAlerts</c>.</summary>
public class StockAlert
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>FK al <see cref="Product"/> en alerta.</summary>
    public Guid ProductId { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal CurrentStock { get; set; }

    [Column(TypeName = "numeric(12,3)")]
    public decimal MinStock { get; set; }

    /// <summary>True cuando la alerta ya fue atendida o el stock se recuperó.</summary>
    public bool IsResolved { get; set; } = false;

    [Column(TypeName = "timestamptz")]
    public DateTime? ResolvedAt { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
