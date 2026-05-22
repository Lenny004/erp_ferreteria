using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

[Table("subfamilies", Schema = "public")]
public class Subfamily
{
    [Key]
    public int Id { get; set; }

    public int FamilyId { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("FamilyId")]
    public Family Family { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
