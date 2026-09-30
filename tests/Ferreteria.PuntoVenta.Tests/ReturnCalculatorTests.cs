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
        var first = ReturnCalculator.Calculate(CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None), sale, new[] { line }, null, ReturnOptions.CreateDefault());
        var history = new Dictionary<Guid, ReturnedLineCredit>
        {
            [line.OrderDetailId] = new(1m, first.Lines[0].Subtotal, first.Lines[0].DiscountAmount, first.Lines[0].TaxAmount, first.Lines[0].Total)
        };
        var second = ReturnCalculator.Calculate(
            CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None),
            sale,
            new[] { line with { AlreadyReturnedQuantity = 1m, AvailableQuantity = 1m } },
            history,
            ReturnOptions.CreateDefault());

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
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, CreateSale(), new[] { line }, null, ReturnOptions.CreateDefault()));
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
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(duplicate, CreateSale(), new[] { line }, null, ReturnOptions.CreateDefault()));

        var missingNotes = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None) with
        {
            ReasonCode = "ANULACION_TOTAL"
        };
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(missingNotes, CreateSale(), new[] { line }, null, ReturnOptions.CreateDefault()));

        var voucher = CreateRequest(line.OrderDetailId, 1m, "VALE");
        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(voucher, CreateSale(), new[] { line }, null, ReturnOptions.CreateDefault()));
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

        var result = ReturnCalculator.Calculate(request, sale, new[] { line }, null, ReturnOptions.CreateDefault());

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

        Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, sale, new[] { line }, null, ReturnOptions.CreateDefault()));
    }

    /// <summary>Una venta sin IVA no genera IVA en un tramo parcial.</summary>
    [Fact]
    public void Calculate_OriginalTaxZero_ProducesZeroTax()
    {
        var sale = CreateSale() with { TaxAmount = 0m, Total = 100m };
        var line = CreateLine(2m, 100m);
        var result = ReturnCalculator.Calculate(CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None), sale, new[] { line }, null, ReturnOptions.CreateDefault());

        Assert.Equal(0m, result.TaxAmount);
    }

    /// <summary>El cierre por remanente de tres tramos suma exactamente la venta original.</summary>
    [Fact]
    public void Calculate_ThreeSuccessiveReturns_CloseOriginalAmounts()
    {
        var sale = CreateSale();
        var line = CreateLine(3m, 100m);
        var options = ReturnOptions.CreateDefault();
        var first = ReturnCalculator.Calculate(CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None), sale, new[] { line }, null, options);
        var firstCredit = new ReturnedLineCredit(1m, first.Subtotal, first.DiscountAmount, first.TaxAmount, first.Total);
        var second = ReturnCalculator.Calculate(CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None), sale, new[] { line with { AlreadyReturnedQuantity = 1m, AvailableQuantity = 2m } }, new Dictionary<Guid, ReturnedLineCredit> { [line.OrderDetailId] = firstCredit }, options);
        var previous = new Dictionary<Guid, ReturnedLineCredit>
        {
            [line.OrderDetailId] = new ReturnedLineCredit(2m, first.Subtotal + second.Subtotal, first.DiscountAmount + second.DiscountAmount, first.TaxAmount + second.TaxAmount, first.Total + second.Total)
        };
        var third = ReturnCalculator.Calculate(CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None), sale, new[] { line with { AlreadyReturnedQuantity = 2m, AvailableQuantity = 1m } }, previous, options);

        Assert.Equal(sale.Subtotal, first.Subtotal + second.Subtotal + third.Subtotal);
        Assert.Equal(sale.DiscountAmount, first.DiscountAmount + second.DiscountAmount + third.DiscountAmount);
        Assert.Equal(sale.TaxAmount, first.TaxAmount + second.TaxAmount + third.TaxAmount);
        Assert.Equal(sale.Total, first.Total + second.Total + third.Total);
    }

    /// <summary>Rechaza total cero y un reintegro distinto del crédito.</summary>
    [Fact]
    public void Calculate_EnforcesPositiveTotalAndExactRefund()
    {
        var line = CreateLine(1m, 0m);
        var zeroSale = CreateSale() with { Subtotal = 0m, TaxAmount = 0m, Total = 0m };
        var zeroRequest = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.None);
        Assert.Contains("mayor que cero", Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(zeroRequest, zeroSale, new[] { line }, null, ReturnOptions.CreateDefault())).Message, StringComparison.OrdinalIgnoreCase);

        var paidRequest = CreateRequest(line.OrderDetailId, 1m, ReturnDomainConstants.RefundMethods.Card) with { RefundAmount = 1m };
        Assert.Contains("igual al total", Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(paidRequest, CreateSale(), new[] { line with { Subtotal = 100m, UnitPrice = 100m } }, null, ReturnOptions.CreateDefault())).Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifica la política fiscal predeterminada para CCF, factura y venta sin DTE.</summary>
    /// <summary>Rechaza que los créditos acumulados superen subtotal, descuento, IVA o total originales.</summary>
    [Fact]
    public void Calculate_EnforcesAccumulatedAmountsWithinSale()
    {
        var line = CreateLine(2m, 100m);
        var request = CreateRequest(line.OrderDetailId, 0.5m, ReturnDomainConstants.RefundMethods.None);
        var options = ReturnOptions.CreateDefault();

        Assert.Contains("subtotal", Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, CreateSale(), new[] { line }, new Dictionary<Guid, ReturnedLineCredit> { [line.OrderDetailId] = new(1m, 100m) }, options)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("descuento", Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, CreateSale(), new[] { line }, new Dictionary<Guid, ReturnedLineCredit> { [line.OrderDetailId] = new(1m, 0m, 1m) }, options)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IVA", Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, CreateSale(), new[] { line }, new Dictionary<Guid, ReturnedLineCredit> { [line.OrderDetailId] = new(1m, 0m, 0m, 13m) }, options)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("total", Assert.Throws<InvalidReturnException>(() => ReturnCalculator.Calculate(request, CreateSale(), new[] { line }, new Dictionary<Guid, ReturnedLineCredit> { [line.OrderDetailId] = new(1m, 0m, 0m, 0m, 100m) }, options)).Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifica que la política fiscal predeterminada exija la revisión esperada según el DTE original.</summary>

    /// <summary>Verifica que la política fiscal predeterminada exija la revisión esperada según el DTE original.</summary>
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
