using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Reintentos cuando falla el envío a Hacienda. Tabla <c>dte.DteContingency</c>.</summary>
public class DteContingency
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>DTE en contingencia (<see cref="DteIssued"/>).</summary>
    public Guid DteId { get; set; }

    /// <summary>Cantidad de reintentos de envío al Ministerio de Hacienda.</summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>Último error reportado por el transmisor o la API del MH.</summary>
    public string? LastError { get; set; }

    /// <summary>Próximo intento programado (UTC).</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime? NextRetryAt { get; set; }

    /// <summary>Momento en que se resolvió la contingencia; null si sigue abierta.</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime? ResolvedAt { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(DteId))]
    public DteIssued DteIssued { get; set; } = null!;
}
