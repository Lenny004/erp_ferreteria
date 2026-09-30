using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Calcula totales y diferencias de caja sin depender de WPF ni EF Core.</summary>
public static class CashRegisterCalculator
{
    /// <summary>Redondea un monto monetario a dos decimales.</summary>
    /// <param name="amount">Monto que se redondeará.</param>
    /// <returns>Monto redondeado con <see cref="MidpointRounding.AwayFromZero"/>.</returns>
    public static decimal RoundAmount(decimal amount)
    {
        // El criterio contable definitivo debe verificarse con contador / normativa MH.
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Calcula el resumen de ventas completadas y pagos de una sesión.</summary>
    /// <param name="snapshot">Ventas, pagos y fondo inicial de la sesión.</param>
    /// <returns>Totales agrupados y movimientos listos para presentar.</returns>
    public static CashRegisterSummary Calculate(CashRegisterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var completedSales = snapshot.Sales
            .Where(sale => sale.Status == SalesDomainConstants.OrderStatuses.Completed)
            .ToArray();
        var completedPayments = completedSales.SelectMany(sale => sale.Payments).ToArray();

        var cashPayments = SumByMethod(completedPayments, SalesDomainConstants.PaymentMethods.Cash);
        var cardPayments = SumByMethod(completedPayments, SalesDomainConstants.PaymentMethods.Card);
        var transferPayments = SumByMethod(completedPayments, SalesDomainConstants.PaymentMethods.Transfer);
        var otherPayments = completedPayments
            .Where(payment => payment.Method != SalesDomainConstants.PaymentMethods.Cash
                && payment.Method != SalesDomainConstants.PaymentMethods.Card
                && payment.Method != SalesDomainConstants.PaymentMethods.Transfer)
            .Sum(payment => payment.Amount);

        var openingAmount = RoundAmount(snapshot.OpeningAmount);
        var cashRefunds = RoundAmount(snapshot.CashRefunds);
        var expectedCash = RoundAmount(openingAmount + cashPayments - cashRefunds);
        var movements = completedSales
            .SelectMany(sale => sale.Payments.Select(payment => new CashRegisterMovement(
                sale.OrderId,
                sale.CreatedAtUtc,
                sale.DocumentNumber ?? $"ORD-{sale.OrderId.ToString()[..8].ToUpperInvariant()}",
                payment.Method,
                RoundAmount(payment.Amount),
                sale.Status)))
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ThenBy(movement => movement.OrderId)
            .ToArray();

        return new CashRegisterSummary(
            snapshot.SessionId,
            openingAmount,
            RoundAmount(cashPayments),
            RoundAmount(cardPayments),
            RoundAmount(transferPayments),
            RoundAmount(otherPayments),
            RoundAmount(completedSales.Sum(sale => sale.Total)),
            RoundAmount(completedSales.Sum(sale => sale.TaxAmount)),
            completedSales.Length,
            snapshot.Sales.Count(sale => sale.Status == SalesDomainConstants.OrderStatuses.Pending),
            snapshot.Sales.Count(sale => sale.Status == SalesDomainConstants.OrderStatuses.Cancelled),
            completedSales.Count(sale => !string.IsNullOrWhiteSpace(sale.DteStatus)),
            completedSales.Count(sale => sale.DteStatus == DteStatus.Contingency),
            completedSales.Count(sale => string.IsNullOrWhiteSpace(sale.DteStatus)),
            cashRefunds,
            expectedCash,
            movements);
    }

    /// <summary>Calcula la diferencia declarada menos esperada y su clasificación.</summary>
    /// <param name="expectedCash">Efectivo esperado.</param>
    /// <param name="declaredCash">Efectivo contado.</param>
    /// <param name="observationThreshold">Umbral absoluto que exige observación.</param>
    /// <returns>Resultado de conciliación redondeado a dos decimales.</returns>
    public static CashDifferenceResult CalculateDifference(
        decimal expectedCash,
        decimal declaredCash,
        decimal observationThreshold)
    {
        var normalizedExpected = RoundAmount(expectedCash);
        var normalizedDeclared = RoundAmount(declaredCash);
        var difference = RoundAmount(normalizedDeclared - normalizedExpected);
        var classification = difference switch
        {
            > 0m => CashDifferenceClassification.Surplus,
            < 0m => CashDifferenceClassification.Shortage,
            _ => CashDifferenceClassification.Balanced
        };

        return new CashDifferenceResult(
            normalizedExpected,
            normalizedDeclared,
            difference,
            classification,
            Math.Abs(difference) >= RoundAmount(observationThreshold));
    }

    private static decimal SumByMethod(
        IEnumerable<CashRegisterPaymentSnapshot> payments,
        string method)
    {
        return payments
            .Where(payment => payment.Method == method)
            .Sum(payment => payment.Amount);
    }

    private static class DteStatus
    {
        public const string Contingency = "CONTINGENCIA";
    }
}

