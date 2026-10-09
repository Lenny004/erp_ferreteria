using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas unitarias de las reglas fiscales puras de una venta.</summary>
public sealed class SalesDocumentRulesTests
{
    /// <summary>Factura es el tipo predeterminado del contrato de venta.</summary>
    [Fact]
    public void NormalizeDocumentType_DefaultsToInvoice()
    {
        Assert.Equal(DteConstants.TiposDte.Factura, SalesDocumentRules.NormalizeDocumentType(null));
        Assert.Equal(DteConstants.TiposDte.Factura, SalesDocumentRules.NormalizeDocumentType(" "));
    }

    /// <summary>Un crédito fiscal exige cliente activo con NIT y NRC.</summary>
    [Theory]
    [InlineData(false, "NIT-1", "NRC-1")]
    [InlineData(true, null, "NRC-1")]
    [InlineData(true, "NIT-1", null)]
    public void CreditFiscal_RejectsMissingOrInactiveCustomer(bool isActive, string? nit, string? nrc)
    {
        var customer = new Customer
        {
            CustomerType = "CCF",
            Name = "Cliente de prueba",
            IsActive = isActive,
            Nit = nit,
            Nrc = nrc
        };

        var exception = Assert.Throws<InvalidOrderException>(() =>
            SalesDocumentRules.ValidateCustomerForDocument(DteConstants.TiposDte.CreditoFiscal, customer));

        Assert.Contains("NIT y NRC", exception.Message);
    }

    /// <summary>Un cliente CCF completo permite validar el documento 03.</summary>
    [Fact]
    public void CreditFiscal_AllowsActiveCustomerWithTaxIdentifiers()
    {
        var customer = new Customer
        {
            CustomerType = "CCF",
            Name = "Cliente de prueba",
            IsActive = true,
            Nit = "NIT-1",
            Nrc = "NRC-1"
        };

        SalesDocumentRules.ValidateCustomerForDocument(DteConstants.TiposDte.CreditoFiscal, customer);
    }
}
