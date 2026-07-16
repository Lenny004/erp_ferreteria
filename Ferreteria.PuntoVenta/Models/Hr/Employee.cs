using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Empleado de taller o caja. Tabla <c>hr.Employees</c>.</summary>
public class Employee
{
    [Key]
    public Guid Id { get; set; }

    // Identidad
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? Dui { get; set; }

    [MaxLength(20)]
    public string? Nit { get; set; }

    /// <summary>Número de afiliación ISSS (planilla El Salvador).</summary>
    [MaxLength(20)]
    public string? IsssNumber { get; set; }

    /// <summary>Número Único Previsional / AFP (planilla El Salvador).</summary>
    [MaxLength(20)]
    public string? Nup { get; set; }

    public DateTime? BirthDate { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    [MaxLength(20)]
    public string? Nationality { get; set; } = "SALVADOREÑA";

    [MaxLength(30)]
    public string? PassportNumber { get; set; }

    public string? DependentsDescription { get; set; }

    // Puesto y contrato
    /// <summary>FK a <see cref="Position"/>.</summary>
    public Guid? PositionId { get; set; }

    /// <summary>FK a <see cref="Department"/> (puede diferir del departamento del cargo).</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>FK al <see cref="Employee"/> supervisor directo.</summary>
    public Guid? DirectSupervisorId { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal BaseSalary { get; set; }

    /// <summary>PLAZO_FIJO, TIEMPO_PARCIAL, HONORARIOS, PASANTE.</summary>
    [Required, MaxLength(20)]
    public string ContractType { get; set; } = "PLAZO_FIJO";

    /// <summary>MENSUAL, QUINCENAL, SEMANAL.</summary>
    [Required, MaxLength(20)]
    public string SalaryType { get; set; } = "MENSUAL";

    [Column(TypeName = "numeric(10,2)")]
    public decimal DefaultBonus { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal DefaultViaticos { get; set; }

    [MaxLength(20)]
    public string? AfpInstitution { get; set; }

    public DateTime? AfpEnrollmentDate { get; set; }
    public bool IsssEnrolled { get; set; } = true;
    public DateTime? IsssEnrollmentDate { get; set; }

    // Contacto
    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? DepartmentSv { get; set; }

    [MaxLength(30)]
    public string? PaymentChannel { get; set; } = "DEPOSITO_BANCARIO";

    [MaxLength(20)]
    public string? MaritalStatus { get; set; }

    [MaxLength(50)]
    public string? AcademicLevel { get; set; }

    // Acceso punto de venta
    /// <summary>
    /// Hash bcrypt del PIN (nunca el PIN en claro). Validado por <see cref="Services.PinAuthService"/>.
    /// Riesgo: no serializar este campo en logs, auditoría ni APIs públicas.
    /// </summary>
    public string? PinHash { get; set; }

    /// <summary>Última vez que se actualizó el PIN (UTC).</summary>
    [Column(TypeName = "timestamptz")]
    public DateTime? PinUpdatedAt { get; set; }

    public bool AttendanceEnabled { get; set; } = true;

    /// <summary>Permiso de módulo Inventario / operaciones de stock y catálogo.</summary>
    public bool CanSell { get; set; } = false;

    /// <summary>Permiso de módulo Caja / facturación en mostrador.</summary>
    public bool CanCashier { get; set; } = false;

    // Baja y período de prueba
    public bool OnProbation { get; set; }
    public DateTime? ProbationEndDate { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime? ProbationCompletedAt { get; set; }

    [MaxLength(40)]
    public string? TerminationReason { get; set; }
    public string? TerminationNotes { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    [ForeignKey(nameof(PositionId))]
    public Position? Position { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public Department? Department { get; set; }

    [ForeignKey(nameof(DirectSupervisorId))]
    public Employee? DirectSupervisor { get; set; }

    public ICollection<Employee> Subordinates { get; set; } = new List<Employee>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<CashSession> CashSessions { get; set; } = new List<CashSession>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
