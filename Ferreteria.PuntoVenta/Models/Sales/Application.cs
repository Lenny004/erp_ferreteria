using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Tipo de aplicación de taller (VT-01, VT-02, etc.). Tabla <c>sales.Applications</c>.</summary>
public class Application
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Código de tipo de aplicación (ej. VT-01, VT-02).</summary>
    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
