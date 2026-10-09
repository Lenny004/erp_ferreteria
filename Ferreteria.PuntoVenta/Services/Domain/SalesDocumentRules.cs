using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Dte;

namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Reglas puras para validar el tipo de documento solicitado en una venta.
/// </summary>
public static class SalesDocumentRules
{
    /// <summary>
    /// Normaliza el tipo de documento de una solicitud, usando factura como valor predeterminado.
    /// </summary>
    /// <param name="documentType">Código de DTE recibido por el contrato de venta.</param>
    /// <returns>El código CAT-002 normalizado.</returns>
    public static string NormalizeDocumentType(string? documentType) =>
        string.IsNullOrWhiteSpace(documentType)
            ? DteConstants.TiposDte.Factura
            : documentType.Trim();

    /// <summary>
    /// Comprueba que el tipo de documento sea una factura o un crédito fiscal.
    /// </summary>
    /// <param name="documentType">Código de DTE que se quiere emitir.</param>
    /// <exception cref="InvalidOrderException">Si el código no es soportado por una venta de caja.</exception>
    public static void ValidateDocumentType(string? documentType)
    {
        var normalized = NormalizeDocumentType(documentType);
        if (normalized is not (DteConstants.TiposDte.Factura or DteConstants.TiposDte.CreditoFiscal))
        {
            throw new InvalidOrderException("El tipo de documento de la venta no es válido.");
        }
    }

    /// <summary>
    /// Valida los datos fiscales del cliente requeridos por un crédito fiscal.
    /// </summary>
    /// <param name="documentType">Código de DTE solicitado.</param>
    /// <param name="customer">Cliente seleccionado, si la venta tiene uno.</param>
    /// <exception cref="InvalidOrderException">Si un crédito fiscal no tiene cliente activo con NIT y NRC.</exception>
    public static void ValidateCustomerForDocument(string? documentType, Customer? customer)
    {
        if (NormalizeDocumentType(documentType) != DteConstants.TiposDte.CreditoFiscal)
        {
            return;
        }

        if (customer is null || !customer.IsActive
            || string.IsNullOrWhiteSpace(customer.Nit)
            || string.IsNullOrWhiteSpace(customer.Nrc))
        {
            throw new InvalidOrderException(
                "El comprobante de crédito fiscal requiere un cliente activo con NIT y NRC.");
        }
    }
}
