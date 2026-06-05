using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class DteContingency
{
    [Key]
    public long Id { get; set; }
    public long DteId { get; set; }
    public short Attempts { get; set; } = 0;
    public string? LastError { get; set; }
    [Column(TypeName = "timestamptz")]
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; } = false;
    [Column(TypeName = "timestamptz")]
    public DateTime? ResolvedAt { get; set; }
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(DteId))]
    public DteIssued DteIssued { get; set; } = null!;
}
