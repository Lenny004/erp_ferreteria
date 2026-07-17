using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Compone la representacion imprimible (<see cref="ReceiptDocument"/>) de un DTE
/// a partir de la orden de venta, el DTE emitido y la configuracion del emisor.
/// </summary>
public interface IReceiptCompositionService
{
    /// <summary>Arma el ticket del ultimo DTE emitido para una orden.</summary>
    /// <returns>El documento imprimible o null si la orden no tiene DTE.</returns>
    Task<ReceiptDocument?> ComposeForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>Implementacion de la composicion de tickets DTE.</summary>
public sealed class ReceiptCompositionService : IReceiptCompositionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDteService _dteService;

    /// <summary>Crea el servicio de composicion de tickets.</summary>
    public ReceiptCompositionService(IServiceScopeFactory scopeFactory, IDteService dteService)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _dteService = dteService ?? throw new ArgumentNullException(nameof(dteService));
    }

    /// <inheritdoc />
    public async Task<ReceiptDocument?> ComposeForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Employee)
            .Include(o => o.Customer)
            .Include(o => o.Payments)
            .Include(o => o.OrderDetails)
                .ThenInclude(detail => detail.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var dte = await dbContext.DteIssued
            .AsNoTracking()
            .Where(d => d.OrderId == orderId)
            .OrderByDescending(d => d.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (dte is null)
        {
            return null;
        }

        var emisor = await dbContext.DteConfigs
            .AsNoTracking()
            .Where(config => config.IsActive)
            .OrderByDescending(config => config.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var items = order.OrderDetails
            .Select(detail => new TicketLineItem(
                detail.Product?.Description ?? "Producto",
                detail.Quantity,
                detail.Quantity.ToString("0.###"),
                detail.UnitPrice,
                detail.Subtotal))
            .ToList();

        var paymentMethod = order.Payments
            .OrderBy(payment => payment.CreatedAt)
            .Select(payment => payment.Method)
            .FirstOrDefault() ?? SalesDomainConstants.PaymentMethods.Cash;

        var amountPaid = order.Payments.Sum(payment => payment.Amount);
        if (amountPaid <= 0)
        {
            amountPaid = order.Total;
        }

        var codigoGeneracion = dte.GenerationCode.ToString().ToUpperInvariant();
        var issuedLocal = dte.IssuedAt.ToLocalTime();
        var consultaUrl = _dteService.BuildConsultaUrl(dte.Ambiente, codigoGeneracion, issuedLocal);
        var isContingency = dte.MhStatus == DteConstants.EstadosMh.Contingencia;

        var customerName = order.Customer?.Name ?? SalesDomainConstants.Customers.DefaultWalkInDisplayName;
        var customerDocument = order.Customer?.Nit ?? order.Customer?.Dui;

        return new ReceiptDocument(
            BusinessName: emisor?.EmisorName ?? "FERRETERIA",
            BusinessTradeName: emisor?.EmisorTradeName,
            BusinessNit: emisor?.EmisorNit ?? "-",
            BusinessNrc: emisor?.EmisorNrc ?? "-",
            BusinessAddress: emisor is null
                ? "San Salvador, El Salvador"
                : $"{emisor.AddressLine}, {emisor.Municipality}, {emisor.Department}",
            BusinessPhone: emisor?.Phone,
            DteTypeName: MapDteTypeName(dte.DteType),
            DteTypeCode: dte.DteType,
            NumeroControl: dte.ControlNumber,
            CodigoGeneracion: codigoGeneracion,
            SelloRecibido: dte.MhSello,
            Ambiente: dte.Ambiente,
            IssuedAt: issuedLocal,
            CashierName: BuildEmployeeName(order.Employee),
            CustomerName: customerName,
            CustomerDocument: customerDocument,
            Items: items,
            Subtotal: order.Subtotal,
            Tax: dte.TotalIva,
            Total: order.Total,
            TotalInWords: SpanishNumberToWords.Convert(order.Total),
            PaymentMethod: paymentMethod,
            AmountPaid: amountPaid,
            Change: Math.Max(0m, amountPaid - order.Total),
            ConsultaUrl: consultaUrl,
            IsContingency: isContingency,
            FooterNote: isContingency
                ? "Documento pendiente de sello del Ministerio de Hacienda."
                : null);
    }

    private static string MapDteTypeName(string dteType)
    {
        return dteType switch
        {
            DteConstants.TiposDte.Factura => "FACTURA CONSUMIDOR FINAL",
            DteConstants.TiposDte.CreditoFiscal => "COMPROBANTE DE CREDITO FISCAL",
            DteConstants.TiposDte.NotaCredito => "NOTA DE CREDITO",
            _ => "DOCUMENTO TRIBUTARIO ELECTRONICO"
        };
    }

    private static string BuildEmployeeName(Employee? employee)
    {
        if (employee is null)
        {
            return "Cajero";
        }

        return $"{employee.FirstName} {employee.LastName}".Trim();
    }
}
