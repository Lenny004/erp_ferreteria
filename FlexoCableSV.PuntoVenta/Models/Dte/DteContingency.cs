using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

/// <summary>Reintentos cuando falla el envío a Hacienda. Tabla <c>dte.DteContingency</c>.</summary>
public class DteContingency
{
    [Key]
    public Guid Id { get; set; }

    public Guid DteId { get; set; }
    public int AttemptCount { get; set; } = 0;
    public string? LastError { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime? NextRetryAt { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime? ResolvedAt { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(DteId))]
    public DteIssued DteIssued { get; set; } = null!;
}
