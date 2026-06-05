using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Position
{
    [Key]
    public int Id { get; set; }
    public int DepartmentId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(DepartmentId))]
    public Department Department { get; set; } = null!;
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}