using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>
/// Descuento por volumen aplicable a un producto o a toda una categoría.
/// Tabla <c>public.VolumeDiscounts</c>.
/// </summary>
public class VolumeDiscount
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Descuento para un producto específico (excluyente con FamilyId).</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Descuento para toda una categoría (excluyente con ProductId).</summary>
    public Guid? FamilyId { get; set; }

    /// <summary>Cantidad mínima (en unidades base) para aplicar el descuento.</summary>
    [Column(TypeName = "numeric(12,3)")]
    public decimal MinQuantity { get; set; }

    /// <summary>Porcentaje de descuento (ej: 5.00 = 5%).</summary>
    [Column(TypeName = "numeric(5,2)")]
    public decimal? DiscountPercent { get; set; }

    /// <summary>Precio unitario fijo al alcanzar la cantidad mínima (alternativa al %).</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal? FixedUnitPrice { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "timestamptz")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProductId))]
    public Product? Product { get; set; }

    [ForeignKey(nameof(FamilyId))]
    public Family? Family { get; set; }
}
