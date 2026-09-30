using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>Implementacion de la composicion de tickets DTE.</summary>
public sealed class ReceiptCompositionService : IReceiptCompositionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDteService _dteService;
    private readonly PrintingOptions _printingOptions;
    private readonly ReceiptDocumentFactory _documentFactory;

    /// <summary>Crea el servicio de composicion de tickets.</summary>
    public ReceiptCompositionService(
        IServiceScopeFactory scopeFactory,
        IDteService dteService,
        IOptions<PrintingOptions> printingOptions,
        ReceiptDocumentFactory documentFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _dteService = dteService ?? throw new ArgumentNullException(nameof(dteService));
        ArgumentNullException.ThrowIfNull(printingOptions);
        _printingOptions = printingOptions.Value;
        _documentFactory = documentFactory ?? throw new ArgumentNullException(nameof(documentFactory));
    }

    /// <inheritdoc />
    public async Task<ReceiptDocument?> ComposeForOrderAsync(
        Guid orderId,
        bool isReprint = false,
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

        var customerName = order.Customer?.Name ?? SalesDomainConstants.Customers.DefaultWalkInDisplayName;
        var customerDocument = order.Customer?.Nit ?? order.Customer?.Dui;

        if (dte is null)
        {
            // A VERIFICAR con contador / normativa MH: validez y leyenda del comprobante interno.
            return _documentFactory.Create(
                new ReceiptSaleData(order.Id, BuildEmployeeName(order.Employee), customerName, customerDocument,
                    items, order.Subtotal, order.TaxAmount, order.Total, paymentMethod, amountPaid, order.CreatedAt.ToLocalTime()),
                MapIssuer(emisor), null, _printingOptions.InternalReceiptFooter, isReprint);
        }

        var codigoGeneracion = dte.GenerationCode.ToString().ToUpperInvariant();
        var issuedLocal = dte.IssuedAt.ToLocalTime();
        var consultaUrl = _dteService.BuildConsultaUrl(dte.Ambiente, codigoGeneracion, issuedLocal);
        var isContingency = dte.MhStatus == DteConstants.EstadosMh.Contingencia;

        return _documentFactory.Create(
            new ReceiptSaleData(order.Id, BuildEmployeeName(order.Employee), customerName, customerDocument,
                items, order.Subtotal, order.TaxAmount, order.Total, paymentMethod, amountPaid, issuedLocal),
            MapIssuer(emisor),
            new ReceiptDteData(dte.DteType, MapDteTypeName(dte.DteType), dte.ControlNumber,
                codigoGeneracion, dte.MhSello, dte.Ambiente, consultaUrl, isContingency, dte.TotalIva),
            _printingOptions.InternalReceiptFooter, isReprint);
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

    private static ReceiptIssuerData MapIssuer(DteConfig? emisor)
    {
        // A VERIFICAR con contador / normativa MH: valores mostrados cuando falta configuración activa.
        return emisor is null
            ? new ReceiptIssuerData("EMISOR A VERIFICAR", null, "-", "-", "DIRECCIÓN A VERIFICAR", null)
            : new ReceiptIssuerData(emisor.EmisorName, emisor.EmisorTradeName, emisor.EmisorNit,
                emisor.EmisorNrc, $"{emisor.AddressLine}, {emisor.Municipality}, {emisor.Department}", emisor.Phone);
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
