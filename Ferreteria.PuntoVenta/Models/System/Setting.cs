using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Parámetro global clave/valor. Tabla <c>system.Settings</c>.</summary>
public class Setting
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Valor del parámetro. Riesgo: puede contener secretos de integración;
    /// no registrar en logs ni en <see cref="AuditLog"/> en claro.
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
