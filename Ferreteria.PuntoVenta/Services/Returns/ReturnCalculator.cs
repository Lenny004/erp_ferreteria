namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Calcula crédito, IVA, clasificación y costo de reingreso sin EF ni WPF.</summary>
public static class ReturnCalculator
{
    /// <summary>Calcula una devolución usando importes congelados de la venta original.</summary>
    /// <param name="request">Solicitud de cantidades y método de reintegro.</param>
    /// <param name="sale">Cabecera original de la venta.</param>
    /// <param name="lines">Líneas originales con cantidades ya devueltas.</param>
    /// <param name="previousCredits">Créditos históricos por línea.</param>
    /// <param name="options">Motivos y métodos permitidos.</param>
    /// <returns>Crédito calculado a dos decimales.</returns>
    /// <exception cref="InvalidReturnException">Si se excede una cantidad o un importe original.</exception>
    /// <remarks>
    /// El IVA parcial se prorratea sobre el neto original de la venta y el IVA original, no sobre una tasa inventada.
    /// Esta interpretación, su redondeo y el costo de reingreso quedan a verificar con contador / normativa MH.
    /// La cantidad de kardex usa la misma unidad que descontó la venta: hoy Quantity sin UnitsPerPackage.
    /// </remarks>
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
        ReturnInputRules.Validate(request, lines, options);

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

            var headerDiscount = grossLineTotal <= 0m ? 0m : sale.DiscountAmount * line.Subtotal / grossLineTotal;
            var originalDiscount = line.DiscountAmount + headerDiscount;
            var closesLine = requestLine.Quantity + previous.Quantity >= line.SoldQuantity;
            var gross = closesLine ? line.Subtotal - previous.Subtotal : line.Subtotal * requestLine.Quantity / line.SoldQuantity;
            var discount = closesLine ? originalDiscount - previous.DiscountAmount : originalDiscount * requestLine.Quantity / line.SoldQuantity;
            provisional.Add(new ProvisionalCredit(
                line,
                requestLine.Quantity,
                RoundMoney(Math.Max(0m, gross)),
                RoundMoney(Math.Max(0m, discount)),
                closesLine,
                requestLine.Restock));
        }

        var netSubtotal = provisional.Sum(item => item.Subtotal - item.DiscountAmount);
        var allLinesClosed = lines.All(line =>
        {
            var previous = previousCredits.TryGetValue(line.OrderDetailId, out var credit) ? credit.Quantity : line.AlreadyReturnedQuantity;
            var current = requested.TryGetValue(line.OrderDetailId, out var quantity) ? quantity : 0m;
            return previous + current >= line.SoldQuantity;
        });
        var previousTax = previousCredits.Values.Sum(item => item.TaxAmount);
        var previousTotal = previousCredits.Values.Sum(item => item.Total);
        var previousSubtotal = previousCredits.Values.Sum(item => item.Subtotal);
        var previousDiscount = previousCredits.Values.Sum(item => item.DiscountAmount);
        var originalNet = lines.Sum(line => line.Subtotal) - lines.Sum(line => line.DiscountAmount) - sale.DiscountAmount;
        var tax = allLinesClosed
            ? RoundMoney(sale.TaxAmount - previousTax)
            : originalNet <= 0m ? 0m : RoundMoney(sale.TaxAmount * netSubtotal / originalNet);
        var total = allLinesClosed ? RoundMoney(sale.Total - previousTotal) : RoundMoney(netSubtotal + tax);
        var taxByLine = AllocateTaxes(provisional, tax);
        var resultLines = provisional.Select((item, index) => new ReturnCreditLine(
            item.Line.OrderDetailId,
            item.Quantity,
            item.Subtotal,
            item.DiscountAmount,
            taxByLine[index],
            0m,
            item.Restock ? RoundCost(item.Line.UnitCost * item.Quantity) : 0m,
            item.Restock ? item.Quantity : 0m,
            item.Line.UnitPrice,
            item.Line.UnitsPerPackage,
            item.Line.UnitCost,
            item.Restock)).ToArray();
        resultLines = resultLines.Select((line, index) => line with
        {
            Total = RoundMoney(line.Subtotal - line.DiscountAmount + line.TaxAmount)
        }).ToArray();

        var resultSubtotal = RoundMoney(resultLines.Sum(item => item.Subtotal));
        var resultDiscount = RoundMoney(resultLines.Sum(item => item.DiscountAmount));
        var resultTax = RoundMoney(resultLines.Sum(item => item.TaxAmount));
        var resultTotal = allLinesClosed ? RoundMoney(sale.Total - previousTotal) : RoundMoney(resultLines.Sum(item => item.Total));
        if (resultTotal <= 0m)
        {
            throw new InvalidReturnException("El total de la devolución debe ser mayor que cero.");
        }

        EnsureWithinSale("subtotal", previousSubtotal + resultSubtotal, sale.Subtotal);
        var originalDiscountTotal = sale.DiscountAmount + lines.Sum(line => line.DiscountAmount);
        EnsureWithinSale("descuento", previousDiscount + resultDiscount, originalDiscountTotal);
        EnsureWithinSale("IVA", previousTax + resultTax, sale.TaxAmount);
        EnsureWithinSale("total", previousTotal + resultTotal, sale.Total);
        var isNoRefund = string.Equals(request.RefundMethod, ReturnDomainConstants.RefundMethods.None, StringComparison.OrdinalIgnoreCase);
        if (isNoRefund && request.RefundAmount != 0m)
        {
            throw new InvalidReturnException("El reintegro NINGUNO debe tener monto cero.");
        }

        if (!isNoRefund && request.RefundAmount > resultTotal)
        {
            throw new InvalidReturnException("El monto de reintegro no puede superar el total calculado.");
        }

        return new ReturnCalculationResult(
            resultLines,
            resultSubtotal,
            resultDiscount,
            resultTax,
            resultTotal,
            RoundCost(resultLines.Sum(item => item.RestockCost)),
            allLinesClosed ? ReturnDomainConstants.Types.Total : ReturnDomainConstants.Types.Partial);
    }

    private static decimal[] AllocateTaxes(IReadOnlyList<ProvisionalCredit> lines, decimal totalTax)
    {
        var allocated = new decimal[lines.Count];
        var netTotal = lines.Sum(line => line.Subtotal - line.DiscountAmount);
        for (var index = 0; index < lines.Count; index++)
        {
            allocated[index] = index == lines.Count - 1
                ? RoundMoney(totalTax - allocated.Take(index).Sum())
                : netTotal <= 0m
                    ? 0m
                    : RoundMoney(totalTax * (lines[index].Subtotal - lines[index].DiscountAmount) / netTotal);
        }

        return allocated;
    }

    private static void EnsureWithinSale(string amountName, decimal value, decimal original)
    {
        if (value > original)
        {
            throw new InvalidReturnException($"La suma de {amountName} devuelto supera el importe original de la venta.");
        }
    }

    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundCost(decimal amount) => Math.Round(amount, 4, MidpointRounding.AwayFromZero);

    private sealed record ProvisionalCredit(ReturnableLine Line, decimal Quantity, decimal Subtotal, decimal DiscountAmount, bool ClosesLine, bool Restock);
}
