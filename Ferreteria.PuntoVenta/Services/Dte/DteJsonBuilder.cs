using System.Globalization;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Models.Dte.Json;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>Contexto de emision para construir el JSON de un DTE.</summary>
/// <param name="Order">Orden de venta origen (con detalles y producto cargados).</param>
/// <param name="Emisor">Configuracion fiscal del emisor.</param>
/// <param name="Customer">Cliente (obligatorio para CCF 03; opcional para FE 01).</param>
/// <param name="TipoDte">Tipo de DTE (01/03).</param>
/// <param name="Numbering">Numero de control y codigo de generacion.</param>
/// <param name="Ambiente">Ambiente activo (00/01).</param>
/// <param name="IssuedAtLocal">Fecha/hora local de emision.</param>
public sealed record DteBuildContext(
    Order Order,
    DteConfig Emisor,
    Customer? Customer,
    string TipoDte,
    DteNumbering Numbering,
    string Ambiente,
    DateTime IssuedAtLocal);

/// <summary>Construye el arbol JSON de un DTE a partir del dominio de ventas.</summary>
public interface IDteJsonBuilder
{
    /// <summary>Construye una Factura (01) o Comprobante de Credito Fiscal (03).</summary>
    DteDocument BuildInvoice(DteBuildContext context);

    /// <summary>
    /// Construye una Nota de Credito (05) que anula total o parcialmente un DTE previo.
    /// </summary>
    DteDocument BuildCreditNote(
        DteBuildContext context,
        DteIssued originalDte,
        IReadOnlyList<OrderDetail> returnedLines);
}

/// <summary>
/// Implementacion del constructor de JSON DTE segun la normativa del MH El Salvador.
/// Reglas de IVA:
///  - Factura (01): precios e importes incluyen IVA; el IVA total va en <c>totalIva</c>.
///  - Credito Fiscal (03): precios e importes son netos; el IVA va en <c>tributos</c>.
/// </summary>
public sealed class DteJsonBuilder : IDteJsonBuilder
{
    private const int UnidadDeMedida = 59;
    private const int MetroDeMedida = 58;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <inheritdoc />
    public DteDocument BuildInvoice(DteBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Order);
        ArgumentNullException.ThrowIfNull(context.Emisor);

        var isCreditoFiscal = context.TipoDte == DteConstants.TiposDte.CreditoFiscal;
        if (isCreditoFiscal && context.Customer is null)
        {
            throw new DteException("El Comprobante de Credito Fiscal requiere datos del cliente (NIT/NRC).");
        }

        var document = new DteDocument
        {
            Identificacion = BuildIdentificacion(context, isContingency: false),
            Emisor = BuildEmisor(context.Emisor),
            Receptor = BuildReceptor(context, isCreditoFiscal),
            CuerpoDocumento = BuildBody(context.Order.OrderDetails, isCreditoFiscal),
            Resumen = BuildResumen(context.Order, context.Order.Payments, isCreditoFiscal),
            Extension = null,
            Apendice = null
        };

        return document;
    }

    /// <inheritdoc />
    public DteDocument BuildCreditNote(
        DteBuildContext context,
        DteIssued originalDte,
        IReadOnlyList<OrderDetail> returnedLines)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(originalDte);
        ArgumentNullException.ThrowIfNull(returnedLines);

        if (context.Customer is null)
        {
            throw new DteException("La Nota de Credito requiere datos del cliente (credito fiscal).");
        }

        var document = new DteDocument
        {
            Identificacion = BuildIdentificacion(context, isContingency: false),
            DocumentoRelacionado = new List<DteDocumentoRelacionado>
            {
                new()
                {
                    TipoDocumento = "1",
                    TipoGeneracion = 2,
                    NumeroDocumento = originalDte.GenerationCode.ToString().ToUpperInvariant(),
                    FechaEmision = originalDte.IssuedAt.ToLocalTime().ToString("yyyy-MM-dd", Invariant)
                }
            },
            Emisor = BuildEmisor(context.Emisor),
            Receptor = BuildReceptor(context, isCreditoFiscal: true),
            CuerpoDocumento = BuildBody(returnedLines, isCreditoFiscal: true),
            Resumen = BuildResumenFromLines(returnedLines, context.Order.Payments, isCreditoFiscal: true),
            Extension = null,
            Apendice = null
        };

        return document;
    }

    private static DteIdentificacion BuildIdentificacion(DteBuildContext context, bool isContingency)
    {
        var version = context.TipoDte switch
        {
            DteConstants.TiposDte.Factura => DteConstants.Versiones.Factura,
            DteConstants.TiposDte.CreditoFiscal => DteConstants.Versiones.CreditoFiscal,
            DteConstants.TiposDte.NotaCredito => DteConstants.Versiones.NotaCredito,
            _ => DteConstants.Versiones.Factura
        };

        return new DteIdentificacion
        {
            Version = version,
            Ambiente = context.Ambiente,
            TipoDte = context.TipoDte,
            NumeroControl = context.Numbering.NumeroControl,
            CodigoGeneracion = context.Numbering.CodigoGeneracion,
            TipoModelo = isContingency ? 2 : 1,
            TipoOperacion = isContingency ? 2 : 1,
            TipoContingencia = isContingency ? 1 : null,
            MotivoContin = null,
            FecEmi = context.IssuedAtLocal.ToString("yyyy-MM-dd", Invariant),
            HorEmi = context.IssuedAtLocal.ToString("HH:mm:ss", Invariant),
            TipoMoneda = "USD"
        };
    }

    private static DteEmisor BuildEmisor(DteConfig emisor)
    {
        return new DteEmisor
        {
            Nit = emisor.EmisorNit,
            Nrc = emisor.EmisorNrc,
            Nombre = emisor.EmisorName,
            CodActividad = emisor.ActividadEconomica,
            DescActividad = emisor.EmisorTradeName ?? emisor.EmisorName,
            NombreComercial = emisor.EmisorTradeName,
            TipoEstablecimiento = "01",
            Direccion = new DteDireccion
            {
                Departamento = emisor.Department,
                Municipio = emisor.Municipality,
                Complemento = emisor.AddressLine
            },
            Telefono = emisor.Phone,
            Correo = emisor.Email ?? string.Empty
        };
    }

    private static DteReceptor? BuildReceptor(DteBuildContext context, bool isCreditoFiscal)
    {
        var customer = context.Customer;

        if (isCreditoFiscal)
        {
            if (customer is null)
            {
                throw new DteException("El credito fiscal requiere receptor.");
            }

            return new DteReceptor
            {
                Nit = customer.Nit,
                Nrc = customer.Nrc,
                Nombre = customer.Name,
                CodActividad = context.Emisor.ActividadEconomica,
                DescActividad = context.Emisor.EmisorTradeName ?? context.Emisor.EmisorName,
                Direccion = new DteDireccion
                {
                    Departamento = customer.Department ?? context.Emisor.Department,
                    Municipio = customer.Municipality ?? context.Emisor.Municipality,
                    Complemento = customer.Address ?? "No especificada"
                },
                Telefono = customer.Phone,
                Correo = customer.Email
            };
        }

        // Factura (01): receptor opcional. Solo se incluye si hay datos del cliente.
        if (customer is null || string.IsNullOrWhiteSpace(customer.Name))
        {
            return null;
        }

        var hasNit = !string.IsNullOrWhiteSpace(customer.Nit);
        return new DteReceptor
        {
            TipoDocumento = hasNit
                ? DteConstants.TiposDocumentoReceptor.Nit
                : DteConstants.TiposDocumentoReceptor.Dui,
            NumDocumento = hasNit ? customer.Nit : customer.Dui,
            Nombre = customer.Name,
            Direccion = null,
            Telefono = customer.Phone,
            Correo = customer.Email
        };
    }

    private static List<DteItem> BuildBody(IEnumerable<OrderDetail> details, bool isCreditoFiscal)
    {
        var items = new List<DteItem>();
        var index = 1;

        foreach (var detail in details)
        {
            var netUnitPrice = Round2(detail.UnitPrice);
            var netLineTotal = Round2(detail.UnitPrice * detail.Quantity - detail.DiscountAmount);

            decimal precioUni;
            decimal ventaGravada;
            decimal? ivaItem;
            List<string>? tributos;

            if (isCreditoFiscal)
            {
                precioUni = netUnitPrice;
                ventaGravada = netLineTotal;
                ivaItem = null;
                tributos = new List<string> { DteConstants.CodigoTributoIva };
            }
            else
            {
                precioUni = Round2(netUnitPrice * (1 + SalesDomainConstants.ElSalvadorIvaRate));
                ventaGravada = Round2(netLineTotal * (1 + SalesDomainConstants.ElSalvadorIvaRate));
                ivaItem = Round2(ventaGravada - ventaGravada / (1 + SalesDomainConstants.ElSalvadorIvaRate));
                tributos = null;
            }

            items.Add(new DteItem
            {
                NumItem = index++,
                TipoItem = 1,
                NumeroDocumento = null,
                Cantidad = detail.Quantity,
                Codigo = detail.Product?.Code,
                CodTributo = null,
                UniMedida = ResolveUnitOfMeasure(detail.Product),
                Descripcion = detail.Product?.Description ?? "Producto",
                PrecioUni = precioUni,
                MontoDescu = Round2(detail.DiscountAmount),
                VentaNoSuj = 0m,
                VentaExenta = 0m,
                VentaGravada = ventaGravada,
                Tributos = tributos,
                Psv = 0m,
                NoGravado = 0m,
                IvaItem = ivaItem
            });
        }

        return items;
    }

    private static DteResumen BuildResumen(
        Order order,
        IEnumerable<Payment> payments,
        bool isCreditoFiscal)
    {
        return BuildResumenCore(
            netSubtotal: Round2(order.Subtotal),
            ivaAmount: Round2(order.TaxAmount),
            grandTotal: Round2(order.Total),
            payments,
            isCreditoFiscal);
    }

    private static DteResumen BuildResumenFromLines(
        IReadOnlyList<OrderDetail> lines,
        IEnumerable<Payment> payments,
        bool isCreditoFiscal)
    {
        var netSubtotal = Round2(lines.Sum(line => line.UnitPrice * line.Quantity - line.DiscountAmount));
        var ivaAmount = TaxAmountCalculator.CalculateTaxAmount(netSubtotal);
        var grandTotal = netSubtotal + ivaAmount;

        return BuildResumenCore(netSubtotal, ivaAmount, grandTotal, payments, isCreditoFiscal);
    }

    private static DteResumen BuildResumenCore(
        decimal netSubtotal,
        decimal ivaAmount,
        decimal grandTotal,
        IEnumerable<Payment> payments,
        bool isCreditoFiscal)
    {
        var pagos = BuildPagos(payments, grandTotal);

        if (isCreditoFiscal)
        {
            return new DteResumen
            {
                TotalNoSuj = 0m,
                TotalExenta = 0m,
                TotalGravada = netSubtotal,
                SubTotalVentas = netSubtotal,
                DescuNoSuj = 0m,
                DescuExenta = 0m,
                DescuGravada = 0m,
                PorcentajeDescuento = 0m,
                TotalDescu = 0m,
                Tributos = new List<DteTributoResumen>
                {
                    new()
                    {
                        Codigo = DteConstants.CodigoTributoIva,
                        Descripcion = DteConstants.DescripcionTributoIva,
                        Valor = ivaAmount
                    }
                },
                SubTotal = netSubtotal,
                IvaPerci1 = 0m,
                IvaRete1 = 0m,
                ReteRenta = 0m,
                TotalIva = null,
                MontoTotalOperacion = grandTotal,
                TotalNoGravado = 0m,
                TotalPagar = grandTotal,
                TotalLetras = SpanishNumberToWords.Convert(grandTotal),
                CondicionOperacion = DteConstants.CondicionOperacion.Contado,
                Pagos = pagos,
                NumPagoElectronico = null,
                SaldoFavor = 0m
            };
        }

        // Factura (01): montos con IVA incluido.
        var totalConIva = Round2(netSubtotal + ivaAmount);
        return new DteResumen
        {
            TotalNoSuj = 0m,
            TotalExenta = 0m,
            TotalGravada = totalConIva,
            SubTotalVentas = totalConIva,
            DescuNoSuj = 0m,
            DescuExenta = 0m,
            DescuGravada = 0m,
            PorcentajeDescuento = 0m,
            TotalDescu = 0m,
            Tributos = null,
            SubTotal = totalConIva,
            IvaRete1 = 0m,
            ReteRenta = 0m,
            TotalIva = ivaAmount,
            MontoTotalOperacion = totalConIva,
            TotalNoGravado = 0m,
            TotalPagar = totalConIva,
            TotalLetras = SpanishNumberToWords.Convert(totalConIva),
            CondicionOperacion = DteConstants.CondicionOperacion.Contado,
            Pagos = pagos,
            NumPagoElectronico = null,
            SaldoFavor = 0m
        };
    }

    private static List<DtePago> BuildPagos(IEnumerable<Payment> payments, decimal grandTotal)
    {
        var pagos = payments
            .Select(payment => new DtePago
            {
                Codigo = DteConstants.MapPaymentMethodToCatalog(payment.Method),
                MontoPago = Round2(payment.Amount),
                Referencia = payment.Reference,
                Plazo = null,
                Periodo = null
            })
            .ToList();

        if (pagos.Count == 0)
        {
            pagos.Add(new DtePago
            {
                Codigo = DteConstants.FormasPago.Efectivo,
                MontoPago = grandTotal,
                Referencia = null,
                Plazo = null,
                Periodo = null
            });
        }

        return pagos;
    }

    private static int ResolveUnitOfMeasure(Product? product)
    {
        var code = product?.MeasurementType?.Code?.ToUpperInvariant();
        if (!string.IsNullOrEmpty(code) && (code.Contains("MET") || code == "MT"))
        {
            return MetroDeMedida;
        }

        return UnidadDeMedida;
    }

    private static decimal Round2(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
