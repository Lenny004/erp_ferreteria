namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Cálculo de montos gravados según la tasa IVA configurada para Ferreteria.
/// </summary>
public static class TaxAmountCalculator
{
    /// <summary>Calcula el IVA sobre un subtotal gravado.</summary>
    public static decimal CalculateTaxAmount(decimal taxableSubtotal)
    {
        return Math.Round(
            taxableSubtotal * SalesDomainConstants.ElSalvadorIvaRate,
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>Calcula subtotal + IVA.</summary>
    public static decimal CalculateGrandTotal(decimal taxableSubtotal)
    {
        return taxableSubtotal + CalculateTaxAmount(taxableSubtotal);
    }
}
