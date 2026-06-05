using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Employee
{
    [Key]
    public int Id { get; set; }

    // Identity
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? Dui { get; set; }

    [MaxLength(20)]
    public string? Nit { get; set; }

    [MaxLength(20)]
    public string? IsssNumber { get; set; }

    [MaxLength(20)]
    public string? Nup { get; set; }

    // Job
    public int? PositionId { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal BaseSalary { get; set; }

    [Required, MaxLength(20)]
    public string ContractType { get; set; } = "PLANILLA";

    [MaxLength(50)]
    public string? Afp { get; set; }

    // Contact
    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? AltPhone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? Municipality { get; set; }

    // Personal
    [MaxLength(20)]
    public string? MaritalStatus { get; set; }

    [MaxLength(50)]
    public string? AcademicLevel { get; set; }

    // Emergency contact
    [MaxLength(100)]
    public string? EmergencyName { get; set; }

    [MaxLength(20)]
    public string? EmergencyPhone { get; set; }

    [MaxLength(50)]
    public string? EmergencyRelationship { get; set; }

    // POS access (replaces technicians table)
    public string? PinHash { get; set; }
    public bool CanSell { get; set; } = false;
    public bool CanCashier { get; set; } = false;

    // Status
    public bool IsActive { get; set; } = true;
    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(PositionId))]
    public Position? Position { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
    public ICollection<PayrollDetail> PayrollDetails { get; set; } = new List<PayrollDetail>();
    public ICollection<WebUser> WebUsers { get; set; } = new List<WebUser>();
}
