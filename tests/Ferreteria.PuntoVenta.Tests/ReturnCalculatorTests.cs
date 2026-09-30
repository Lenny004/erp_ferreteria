using Ferreteria.PuntoVenta.Services.Returns;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas puras de reglas, crédito, cierre por remanente y comprobante.</summary>
public sealed class ReturnCalculatorTests
{
    /// <summary>Comprueba que dos devoluciones sucesivas cierran exactamente el crédito original.</summary>
    [Fact]
    public void Calculate_SuccessiveReturns_CloseByRemainingCredit()
    {
        var sale = CreateSale();
        var line = CreateLine(2m, 100m);
        var first = ReturnCalculator.Calculate(CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None), sale, new[] { line });
        var history = new Dictionary<Guid, ReturnedLineCredit>
        {
            [line.OrderDetailId] = new(1m, first.Lines[0].Subtotal, first.Lines[0].DiscountAmount, first.Lines[0].TaxAmount, first.Lines[0].Total)
        };
        var second = ReturnCalculator.Calculate(
            CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None),
            sale,
            new[] { line with { AlreadyReturnedQuantity = 1m, AvailableQuantity = 1m } },
            history);

        Assert.Equal(ReturnDomainConstants.Types.Total, second.ReturnType);
        Assert.Equal(sale.Total, first.Total + second.Total);
        Assert.Equal(30m, second.Lines[0].RestockCost);
    }

    /// <summary>Rechaza cantidades cero, negativas, con cuatro decimales y superiores al disponible.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.2345)]
    [InlineData(3)]
    public void Calculate_InvalidQuantity_Throws(decimal quantity)
    {
        var line = CreateLine(2m, 100m);
        var request = CreateRequest(line.OrderDetailId, quantity, ReturnDomainConstants.RefundMethods.None);
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, CreateSale(), new[] { line }));
    }

    /// <summary>Rechaza líneas repetidas, motivo que exige notas sin observación y vales.</summary>
    [Fact]
    public void InputRules_RejectsDuplicateReasonNotesAndVoucher()
    {
        var line = CreateLine(2m, 100m);
        var duplicate = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None) with
        {
            Lines = new[] { new ReturnLineRequest(line.OrderDetailId, 1m), new ReturnLineRequest(line.OrderDetailId, 1m) }
        };
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(duplicate, CreateSale(), new[] { line }));

        var missingNotes = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None) with
        {
            ReasonCode = "ANULACION_TOTAL"
        };
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(missingNotes, CreateSale(), new[] { line }));

        var voucher = CreateRequest(line.OrderDetailId, 1m, "VALE");
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(voucher, CreateSale(), new[] { line }));
    }

    /// <summary>Usa descuentos de línea y encabezado, IVA original y monto de reintegro exacto.</summary>
    [Fact]
    public void Calculate_UsesOriginalLineAndHeaderDiscounts()
    {
        var sale = CreateSale() with { DiscountAmount = 10m, TaxAmount = 11.05m, Total = 96.05m };
        var line = CreateLine(1m, 100m) with { DiscountAmount = 5m };
        var request = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.Card) with
        {
            OrderId = sale.OrderId,
            RefundAmount = 96.05m
        };

        var result = ReturnCalculator.Calculate(request, sale, new[] { line });

        Assert.Equal(100m, result.Subtotal);
        Assert.Equal(15m, result.DiscountAmount);
        Assert.Equal(11.05m, result.TaxAmount);
        Assert.Equal(96.05m, result.Total);
    }

    /// <summary>Rechaza una línea cuyo identificador de orden no coincide con la solicitud.</summary>
    [Fact]
    public void Calculate_RejectsLineFromAnotherOrder()
    {
        var sale = CreateSale();
        var line = CreateLine(1m, 100m) with { OrderId = Guid.NewGuid() };
        var request = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None) with { OrderId = sale.OrderId };

        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, sale, new[] { line }));
    }

    /// <summary>Verifica la política fiscal predeterminada para CCF, factura y venta sin DTE.</summary>
    [Fact]
    public void FiscalPolicy_RequiresExpectedReview()
    {
        var policy = new DefaultReturnFiscalPolicy();
        Assert.Equal(ReturnDomainConstants.FiscalStatuses.Pending, policy.Decide("03", "PARCIAL").FiscalStatus);
        Assert.Equal(ReturnDomainConstants.FiscalStatuses.RequiresValidation, policy.Decide("01", "TOTAL").FiscalStatus);
        Assert.Equal(ReturnDomainConstants.FiscalStatuses.RequiresValidation, policy.Decide(null, "TOTAL").FiscalStatus);
    }

    /// <summary>Garantiza ancho exacto, leyenda, montos y ausencia de identificadores inventados.</summary>
    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void ReceiptComposer_UsesExactWidthAndConfiguredData(int width)
    {
        var report = ReturnReceiptComposer.Compose(
            new ReturnReceiptData(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                null,
                DateTime.UtcNow,
                new[] { new ReturnReceiptLine("Cable", 1m, 10m) },
                10m,
                0m,
                1.30m,
                11.30m,
                "NINGUNO",
                "CAMBIO",
                "Cajero",
                "Administrador",
                ReturnDomainConstants.FiscalStatuses.RequiresValidation,
                "Leyenda provisional"),
            width);

        Assert.All(report.Split(Environment.NewLine), item => Assert.Equal(width, item.Length));
        Assert.Contains("Leyenda provisional", report);
        Assert.Contains("$11.30", report);
        Assert.Contains("No disponible", report);
        Assert.DoesNotContain("DTE-05-", report);
    }

    private static ReturnableSaleSummary CreateSale() => new(
        Guid.NewGuid(), DateTime.UtcNow, "Cliente", null, "VENTA_CAJA", "COMPLETADA", null, null, 100m, 0m, 13m, 113m);

    private static ReturnableLine CreateLine(decimal soldQuantity, decimal subtotal) => new(
        Guid.NewGuid(), Guid.NewGuid(), "P-01", "Producto", soldQuantity, 0m, soldQuantity,
        subtotal / soldQuantity, subtotal, 0m, 1m, 30m);

    private static ReturnRequest CreateRequest(Guid lineId, decimal quantity, string refundMethod) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CAMBIO", null, refundMethod,
        new[] { new ReturnLineRequest(lineId, quantity) });
}
