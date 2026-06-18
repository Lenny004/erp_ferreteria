using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

/// <summary>Departamento organizacional. Tabla <c>hr.Departments</c>.</summary>
public class Department
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ParentId))]
    public Department? Parent { get; set; }

    public ICollection<Department> Children { get; set; } = new List<Department>();
    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
