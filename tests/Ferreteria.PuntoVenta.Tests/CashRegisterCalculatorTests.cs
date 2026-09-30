using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas de cálculo y reporte del corte sin WPF ni base de datos.</summary>
public sealed class CashRegisterCalculatorTests
{
    /// <summary>Inicializa la zona de negocio usada al formatear las fechas del reporte.</summary>
    public CashRegisterCalculatorTests()
    {
        TestBusinessTime.EnsureInitialized();
    }

    /// <summary>Incluye pagos mixtos y excluye órdenes pendientes o canceladas.</summary>
    [Fact]
    public void Calculate_MixedPayments_ExcludesNonCompletedSales()
    {
        var completed = new CashRegisterSaleSnapshot(
            Guid.NewGuid(),
            DateTime.UtcNow,
            SalesDomainConstants.OrderStatuses.Completed,
            113m,
            13m,
            new[]
            {
                new CashRegisterPaymentSnapshot(SalesDomainConstants.PaymentMethods.Cash, 50m),
                new CashRegisterPaymentSnapshot(SalesDomainConstants.PaymentMethods.Card, 63m)
            },
            DocumentNumber: "DTE-01-1");
        var pending = completed with { Status = SalesDomainConstants.OrderStatuses.Pending, Total = 20m };
        var cancelled = completed with { Status = SalesDomainConstants.OrderStatuses.Cancelled, Total = 30m };

        var summary = CashRegisterCalculator.Calculate(new CashRegisterSnapshot(
            Guid.NewGuid(),
            10m,
            new[] { completed, pending, cancelled }));

        Assert.Equal(1, summary.CompletedSales);
        Assert.Equal(113m, summary.TotalSold);
        Assert.Equal(50m, summary.CashPayments);
        Assert.Equal(63m, summary.CardPayments);
        Assert.Equal(60m, summary.ExpectedCash);
        Assert.Equal(2, summary.PendingSales + summary.CancelledSales);
    }

    /// <summary>Aplica redondeo monetario AwayFromZero y clasifica las tres diferencias.</summary>
    [Fact]
    public void Difference_UsesAwayFromZeroAndText()
    {
        Assert.Equal(1.01m, CashRegisterCalculator.RoundAmount(1.005m));

        var surplus = CashRegisterCalculator.CalculateDifference(10m, 12.50m, 2m);
        var shortage = CashRegisterCalculator.CalculateDifference(10m, 7.25m, 2m);
        var balanced = CashRegisterCalculator.CalculateDifference(10m, 10m, 2m);

        Assert.Equal(CashDifferenceClassification.Surplus, surplus.Classification);
        Assert.Equal("Sobrante $2.50", surplus.DisplayText);
        Assert.True(surplus.RequiresObservation);
        Assert.Equal(CashDifferenceClassification.Shortage, shortage.Classification);
        Assert.Equal("Faltante $2.75", shortage.DisplayText);
        Assert.True(shortage.RequiresObservation);
        Assert.Equal(CashDifferenceClassification.Balanced, balanced.Classification);
        Assert.Equal("Cuadra", balanced.DisplayText);
        Assert.False(balanced.RequiresObservation);
    }

    /// <summary>Activa el umbral de observación en igualdad y por encima del límite absoluto.</summary>
    [Fact]
    public void Difference_ObservationThreshold_IsInclusive()
    {
        Assert.False(CashRegisterCalculator.CalculateDifference(100m, 119.99m, 20m).RequiresObservation);
        Assert.True(CashRegisterCalculator.CalculateDifference(100m, 120m, 20m).RequiresObservation);
    }

    /// <summary>Rechaza montos negativos y superiores al límite configurable.</summary>
    [Fact]
    public void InputRules_RejectNegativeAndOverLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CashRegisterInputRules.ValidateAmount(-0.01m, "Monto", 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CashRegisterInputRules.ValidateAmount(100.01m, "Monto", 100m));
        Assert.Equal(10.01m, CashRegisterInputRules.ValidateAmount(10.005m, "Monto", 100m));
    }

    /// <summary>Genera reportes de 32 y 48 columnas sin sobrepasar el ancho.</summary>
    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void ReportComposer_RespectsWidthAndInternalDisclaimer(int width)
    {
        var sale = new CashRegisterSaleSnapshot(
            Guid.NewGuid(),
            DateTime.UtcNow,
            SalesDomainConstants.OrderStatuses.Completed,
            113m,
            13m,
            new[] { new CashRegisterPaymentSnapshot(SalesDomainConstants.PaymentMethods.Cash, 113m) },
            DocumentNumber: "ORDEN-LARGA-1234567890");
        var summary = CashRegisterCalculator.Calculate(new CashRegisterSnapshot(Guid.NewGuid(), 10m, new[] { sale }));
        var report = CashRegisterReportComposer.Compose(
            new CashRegisterReportData("CAJA-01", "Cajero", DateTime.UtcNow, DateTime.UtcNow, 123m, summary, null),
            width);

        Assert.All(report.Split(Environment.NewLine), line => Assert.True(line.Length <= width));
        Assert.Contains("No es documento fiscal", report);
        var partial = CashRegisterReportComposer.Compose(
            new CashRegisterReportData("CAJA-01", "Cajero", DateTime.UtcNow, null, 123m, summary, null),
            width,
            partial: true);
        Assert.Contains("CORTE PARCIAL", partial);
        Assert.Contains("NO CIERRA TURNO", partial);
    }
}
