using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;

[Table("measurement_types", Schema = "public")]
public class MeasurementType
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string UnitLabel { get; set; } = string.Empty;

    public short Decimals { get; set; } = 0;
}
