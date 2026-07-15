using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>
/// Presentación de venta de un producto con su factor de conversión y precio propio.
/// Ej: un producto contado por UNIDAD puede venderse por DOCENA (12) o CAJA (100).
/// Tabla <c>public.ProductSaleUnits</c>.
/// </summary>
public class ProductSaleUnit
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }
    public Guid SaleUnitId { get; set; }

    /// <summary>Cuántas unidades base descuenta del stock esta presentación (UNIDAD=1, DOCENA=12).</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal UnitsPerPackage { get; set; } = 1;

    [Column(TypeName = "numeric(12,2)")]
    public decimal SalePrice { get; set; } = 0;

    /// <summary>Código de barras propio de esta presentación (si difiere del producto).</summary>
    [MaxLength(50)]
    public string? Barcode { get; set; }

    /// <summary>Presentación por defecto al vender el producto.</summary>
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(SaleUnitId))]
    public SaleUnit SaleUnit { get; set; } = null!;
}
