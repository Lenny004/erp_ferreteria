using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Impresora de tiquets/facturas. Tabla <c>system.Printers</c>.</summary>
public class Printer
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>USB, RED, BLUETOOTH.</summary>
    [Required, MaxLength(10)]
    public string ConnectionType { get; set; } = "USB";

    [MaxLength(15)]
    public string? IpAddress { get; set; }

    public int? NetworkPort { get; set; }
    /// <summary>Ancho de papel en mm (58 o 80 típicos en tiquetes).</summary>
    public short PaperWidth { get; set; } = 80;

    /// <summary>Impresora predeterminada del POS.</summary>
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
