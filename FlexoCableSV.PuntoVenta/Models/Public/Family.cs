using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

[Table("families", Schema = "public")]
public class Family
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(5)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Subfamily> Subfamilies { get; set; } = new List<Subfamily>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
