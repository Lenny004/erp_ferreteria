using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Unidad de medida del producto (metro, unidad, etc.). Tabla <c>public.MeasurementTypes</c>.</summary>
public class MeasurementType
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string UnitLabel { get; set; } = string.Empty;

    public short Decimals { get; set; } = 0;
}
