using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Subfamilia dentro de una familia. Tabla <c>public.Subfamilies</c>.</summary>
public class Subfamily
{
    [Key]
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(FamilyId))]
    public Family Family { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
