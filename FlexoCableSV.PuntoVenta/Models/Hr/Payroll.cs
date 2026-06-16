using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

public class Payroll
{
    [Key]
    public Guid Id { get; set; }
    public short PeriodMonth { get; set; }
    public short PeriodYear { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "BORRADOR";

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalSalaries { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalIsssEmployee { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalAfpEmployee { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalIsr { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalDeductions { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalNet { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalIsssEmployer { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalAfpEmployer { get; set; } = 0;
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime? ApprovedAt { get; set; }
    [Column(TypeName = "timestamptz")]
    public DateTime? PaidAt { get; set; }

    // Navigation
    public ICollection<PayrollDetail> PayrollDetails { get; set; } = new List<PayrollDetail>();
}
