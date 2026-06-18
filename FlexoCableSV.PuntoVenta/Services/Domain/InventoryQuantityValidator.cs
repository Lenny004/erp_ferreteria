namespace FlexoCableSV.PuntoVenta.Services.Domain;

/// <summary>
/// Validación de cantidades según <c>public.MeasurementTypes.Decimals</c>.
/// </summary>
public static class InventoryQuantityValidator
{
    /// <summary>
    /// Valida que la cantidad sea positiva y respete los decimales permitidos por la unidad de medida.
    /// </summary>
    /// <exception cref="InvalidInventoryQuantityException">Cuando la cantidad es inválida.</exception>
    public static void ValidatePositiveQuantity(decimal quantity, short allowedDecimalPlaces)
    {
        if (quantity <= 0)
        {
            throw new InvalidInventoryQuantityException("La cantidad debe ser mayor que cero.");
        }

        if (allowedDecimalPlaces < 0)
        {
            throw new InvalidInventoryQuantityException("La unidad de medida tiene una configuracion invalida.");
        }

        var normalizedQuantity = Math.Round(quantity, allowedDecimalPlaces, MidpointRounding.AwayFromZero);
        if (quantity != normalizedQuantity)
        {
            throw new InvalidInventoryQuantityException(
                $"La cantidad permite maximo {allowedDecimalPlaces} decimales.");
        }
    }
}
