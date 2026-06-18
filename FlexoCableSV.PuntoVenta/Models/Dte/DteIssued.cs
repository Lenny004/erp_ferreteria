using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class DteIssued
{
    [Key]
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }

    [Required, MaxLength(2)]
    public string DteType { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ControlNumber { get; set; } = string.Empty;
    public Guid GenerationCode { get; set; } = Guid.NewGuid();
    public Guid? RelatedDteId { get; set; }

    [Required, MaxLength(20)]
    public string MhStatus { get; set; } = "PENDIENTE";

    public string? MhResponse { get; set; }
    public string? MhSello { get; set; }

    [Required, MaxLength(5)]
    public string Ambiente { get; set; } = "00";

    public string? JsonPayload { get; set; }

    [MaxLength(500)]
    public string? PdfUrl { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalExenta { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalGravada { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalIva { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalPagar { get; set; }

    public int Reprints { get; set; } = 0;

    [Column(TypeName = "timestamptz")]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime? ProcessedAt { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [ForeignKey(nameof(RelatedDteId))]
    public DteIssued? RelatedDte { get; set; }

    public ICollection<DteIssued> CreditNotes { get; set; } = new List<DteIssued>();
    public DteContingency? Contingency { get; set; }
}
