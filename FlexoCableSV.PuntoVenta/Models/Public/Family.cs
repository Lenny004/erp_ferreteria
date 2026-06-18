using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

/// <summary>Familia de productos. Tabla <c>public.Families</c>.</summary>
public class Family
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(5)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Subfamily> Subfamilies { get; set; } = new List<Subfamily>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
