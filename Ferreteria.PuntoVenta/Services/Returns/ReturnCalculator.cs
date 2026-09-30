using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Calcula crédito, IVA, clasificación y costo de reingreso sin EF ni WPF.</summary>
public static class ReturnCalculator
{
    /// <summary>Calcula una devolución usando importes congelados de la venta original.</summary>
    /// <param name="request">Solicitud de cantidades y método de reintegro.</param>
    /// <param name="sale">Cabecera original de la venta.</param>
    /// <param name="lines">Líneas originales con cantidades ya devueltas.</param>
    /// <param name="previousCredits">Créditos históricos por línea.</param>
    /// <returns>Crédito calculado a dos decimales.</returns>
    /// <exception cref="InvalidReturnException">Si la solicitud no cumple las reglas.</exception>
    /// <remarks>
    /// El subtotal de OrderDetails se interpreta como UnitPrice por cantidad y DiscountAmount separado,
    /// tal como hoy lo construye OrderService. Si otro canal usa otra convención, debe verificarse con contador.
    /// El IVA del 13% y su redondeo deben verificarse con contador / normativa MH. El costo de reingreso usa
    /// OrderDetails.UnitCost y la misma unidad que descontó la venta: hoy Quantity sin multiplicar UnitsPerPackage.
    /// </remarks>
    public static ReturnCalculationResult Calculate(
        ReturnRequest request,
        ReturnableSaleSummary sale,
        IReadOnlyList<ReturnableLine> lines,
        IReadOnlyDictionary<Guid, ReturnedLineCredit>? previousCredits = null)
    {
        return Calculate(request, sale, lines, previousCredits, new ReturnOptions());
    }

    /// <summary>Calcula una devolución usando el catálogo configurable del servidor.</summary>
    /// <param name="request">Solicitud de cantidades y método de reintegro.</param>
    /// <param name="sale">Cabecera original de la venta.</param>
    /// <param name="lines">Líneas originales con cantidades ya devueltas.</param>
    /// <param name="previousCredits">Créditos históricos por línea.</param>
    /// <param name="options">Motivos y métodos permitidos.</param>
    /// <returns>Crédito calculado a dos decimales.</returns>
    /// <exception cref="InvalidReturnException">Si la solicitud no cumple las reglas.</exception>
    public static ReturnCalculationResult Calculate(
        ReturnRequest request,
        ReturnableSaleSummary sale,
        IReadOnlyList<ReturnableLine> lines,
        IReadOnlyDictionary<Guid, ReturnedLineCredit>? previousCredits,
        ReturnOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(options);
        previousCredits ??= new Dictionary<Guid, ReturnedLineCredit>();

        ReturnInputRules.Validate(request, lines, options, allowUnspecifiedRefundAmount: true);

        var linesById = lines.ToDictionary(item => item.OrderDetailId);
        var requested = request.Lines.ToDictionary(item => item.OrderDetailId, item => item.Quantity);
        var grossLineTotal = lines.Sum(line => line.Subtotal);
        var provisional = new List<ProvisionalCredit>();

        foreach (var requestLine in request.Lines)
        {
            var line = linesById[requestLine.OrderDetailId];
            var previous = previousCredits.TryGetValue(line.OrderDetailId, out var credit)
                ? credit
                : new ReturnedLineCredit(line.AlreadyReturnedQuantity);
            var available = Math.Max(0m, line.SoldQuantity - previous.Quantity);
            if (requestLine.Quantity > available)
            {
                throw new InvalidReturnException($"La cantidad solicitada supera lo disponible para {line.ProductDescription}.");
            }

            var lineHeaderDiscount = grossLineTotal <= 0m
                ? 0m
                : sale.DiscountAmount * line.Subtotal / grossLineTotal;
            var originalDiscount = line.DiscountAmount + lineHeaderDiscount;
            var closesLine = requestLine.Quantity + previous.Quantity >= line.SoldQuantity;
            var gross = closesLine
                ? line.Subtotal - previous.Subtotal
                : line.Subtotal * requestLine.Quantity / line.SoldQuantity;
            var discount = closesLine
                ? originalDiscount - previous.DiscountAmount
                : originalDiscount * requestLine.Quantity / line.SoldQuantity;
            provisional.Add(new ProvisionalCredit(
                line,
                requestLine.Quantity,
                RoundMoney(Math.Max(0m, gross)),
                RoundMoney(Math.Max(0m, discount)),
                closesLine));
        }

        var netSubtotal = provisional.Sum(item => item.Subtotal - item.DiscountAmount);
        var allLinesClosed = lines.All(line =>
        {
            var previous = previousCredits.TryGetValue(line.OrderDetailId, out var credit)
                ? credit.Quantity
                : line.AlreadyReturnedQuantity;
            var current = requested.TryGetValue(line.OrderDetailId, out var quantity) ? quantity : 0m;
            return previous + current >= line.SoldQuantity;
        });
        var previousTax = previousCredits.Values.Sum(item => item.TaxAmount);
        var previousTotal = previousCredits.Values.Sum(item => item.Total);
        var tax = allLinesClosed
            ? RoundMoney(sale.TaxAmount - previousTax)
            : TaxAmountCalculator.CalculateTaxAmount(netSubtotal);
        var total = allLinesClosed
            ? RoundMoney(sale.Total - previousTotal)
            : RoundMoney(netSubtotal + tax);
        var isNoRefund = string.Equals(
            request.RefundMethod,
            ReturnDomainConstants.RefundMethods.None,
            StringComparison.OrdinalIgnoreCase);
        if (isNoRefund && request.RefundAmount != 0m)
        {
            throw new InvalidReturnException("El reintegro NINGUNO debe tener monto cero.");
        }

        if (!isNoRefund && request.RefundAmount != 0m && RoundMoney(request.RefundAmount) != total)
        {
            throw new InvalidReturnException("El monto de reintegro no coincide con el crédito calculado.");
        }

        var taxByLine = AllocateTax(provisional, tax);
        var resultLines = provisional.Select((item, index) => new ReturnCreditLine(
            item.Line.OrderDetailId,
            item.Quantity,
            item.Subtotal,
            item.DiscountAmount,
            taxByLine[index],
            RoundMoney(item.Subtotal - item.DiscountAmount + taxByLine[index]),
            RoundCost(item.Line.UnitCost * item.Quantity),
            item.Quantity)).ToArray();

        return new ReturnCalculationResult(
            resultLines,
            RoundMoney(resultLines.Sum(item => item.Subtotal)),
            RoundMoney(resultLines.Sum(item => item.DiscountAmount)),
            RoundMoney(tax),
            total,
            RoundCost(resultLines.Sum(item => item.RestockCost)),
            allLinesClosed ? ReturnDomainConstants.Types.Total : ReturnDomainConstants.Types.Partial);
    }

    private static decimal[] AllocateTax(IReadOnlyList<ProvisionalCredit> lines, decimal totalTax)
    {
        if (lines.Count == 0)
        {
            return Array.Empty<decimal>();
        }

        var netTotal = lines.Sum(line => line.Subtotal - line.DiscountAmount);
        var allocated = new decimal[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            allocated[index] = index == lines.Count - 1
                ? RoundMoney(totalTax - allocated.Take(index).Sum())
                : RoundMoney(netTotal <= 0m ? 0m : totalTax * (lines[index].Subtotal - lines[index].DiscountAmount) / netTotal);
        }

        return allocated;
    }

    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundCost(decimal amount) => Math.Round(amount, 4, MidpointRounding.AwayFromZero);

    private sealed record ProvisionalCredit(
        ReturnableLine Line,
        decimal Quantity,
        decimal Subtotal,
        decimal DiscountAmount,
        bool ClosesLine);
}
