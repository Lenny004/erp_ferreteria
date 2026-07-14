using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>
/// Unidad de venta / presentación (unidad, docena, caja, bulto, saco...).
/// Tabla <c>public.SaleUnits</c>.
/// </summary>
public class SaleUnit
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>UNIDAD, DOCENA, CAJA, BULTO, SACO, PAR, CIENTO, MILLAR...</summary>
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Abbreviation { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductSaleUnit> ProductSaleUnits { get; set; } = new List<ProductSaleUnit>();
}
