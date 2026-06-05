using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class PayrollDetail
{
    [Key]
    public long Id { get; set; }
    public int PayrollId { get; set; }
    public int EmployeeId { get; set; }

    // Income
    [Column(TypeName = "numeric(10,2)")]
    public decimal BaseSalary { get; set; }

    [Column(TypeName = "numeric(5,2)")]
    public decimal OvertimeHours { get; set; } = 0;

    [Column(TypeName = "numeric(10,2)")]
    public decimal OvertimeAmount { get; set; } = 0;

    [Column(TypeName = "numeric(10,2)")]
    public decimal Bonuses { get; set; } = 0;

    [Column(TypeName = "numeric(10,2)")]
    public decimal TotalIncome { get; set; }

    // Deductions
    [Column(TypeName = "numeric(10,2)")]
    public decimal IsssEmployee { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal AfpEmployee { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal Isr { get; set; } = 0;

    [Column(TypeName = "numeric(10,2)")]
    public decimal OtherDeductions { get; set; } = 0;

    [Column(TypeName = "numeric(10,2)")]
    public decimal TotalDeductions { get; set; }

    // Net
    [Column(TypeName = "numeric(10,2)")]
    public decimal NetSalary { get; set; }

    // Employer cost
    [Column(TypeName = "numeric(10,2)")]
    public decimal IsssEmployer { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal AfpEmployer { get; set; }
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(PayrollId))]
    public Payroll Payroll { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}
