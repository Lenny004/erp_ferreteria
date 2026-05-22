using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Printer
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string ConnectionType { get; set; } = "USB";

    [MaxLength(15)]
    public string? IpAddress { get; set; }
    public int? NetworkPort { get; set; }
    public short PaperWidth { get; set; } = 80;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}