using System.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;
using Ferreteria.PuntoVenta.Services.Printing;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica composición pura sin hardware ni base de datos.</summary>
public sealed class PrintingTests
{
    /// <summary>Verifica columnas, negrita y bytes exactos del corte.</summary>
    [Fact]
    public void Builder_UsesExpectedWidthsAndCut()
    {
        Assert.Equal(32, new EscPosDocumentBuilder(58).Columns);
        Assert.Equal(48, new EscPosDocumentBuilder(80).Columns);
        var bytes = new EscPosDocumentBuilder(80).AppendBold("TOTAL: $1.00").Cut().Build();
        Assert.Contains(new byte[] { 0x1B, 0x45, 0x01 }, bytes.AsSpan().ToArray());
        Assert.Contains(new byte[] { 0x1B, 0x45, 0x00 }, bytes.AsSpan().ToArray());
        Assert.True(bytes.AsSpan().EndsWith(new byte[] { 0x1D, 0x56, 0x42, 0x50 }));
    }

    /// <summary>Verifica envoltura y formato monetario del texto plano.</summary>
    [Fact]
    public void TextRenderer_WrapsLongNamesAndFormatsTotals()
    {
        var document = CreateDocument();
        var text = TicketReceiptRenderer.RenderPlainText(document, 58);
        Assert.Contains("Cable eléctrico THHN", text);
        Assert.Contains("$7.00", text);
        Assert.Contains("$7.91", text);
        Assert.DoesNotContain(text.Split(Environment.NewLine), line => line.Length > 32);
    }

    /// <summary>Verifica que el comprobante interno no emita contenido fiscal.</summary>
    [Fact]
    public void EscRenderer_ContainsInternalFooterAndNoQr()
    {
        var document = CreateDocument();
        var bytes = TicketReceiptRenderer.Render(document, 80);
        var text = Encoding.Latin1.GetString(bytes);
        Assert.Contains("SIN VALOR FISCAL", text);
        Assert.DoesNotContain("Consulta este DTE", text);
    }

    /// <summary>Verifica que cada línea respete el ancho y que las columnas terminen en el borde.</summary>
    [Fact]
    public void PlainRenderer_UsesExactPaperWidths()
    {
        var document = CreateDocument();
        foreach (var width in new[] { 58, 80 })
        {
            var columns = width == 58 ? 32 : 48;
            var lines = TicketReceiptRenderer.RenderPlainText(document, width)
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.All(lines, line => Assert.True(line.Length <= columns));
            Assert.Contains(new string('-', columns), lines);
        }
    }

    /// <summary>Verifica truncado seguro de la columna izquierda cuando el valor derecho ocupa el ancho.</summary>
    [Fact]
    public void Builder_TruncatesLeftColumnWithoutBreakingWidth()
    {
        var builder = new EscPosDocumentBuilder(58);
        builder.AppendColumns("Descripción extremadamente larga", "$123456789012345678901234567890");
        var line = builder.BuildPlainText().Split(Environment.NewLine)[0];
        Assert.Equal(32, line.Length);
    }

    /// <summary>Verifica normalización y validación de configuración de impresora.</summary>
    [Fact]
    public void PrinterRules_MapUiConnectionAndRejectInvalidNetworkInput()
    {
        Assert.Equal("RED", PrinterConfigurationRules.NormalizeConnectionType("Ethernet"));
        Assert.Equal("USB", PrinterConfigurationRules.NormalizeConnectionType(" usb "));
        Assert.Equal("RED", PrinterConfigurationRules.NormalizeConnectionType("Red"));
        Assert.Equal("RED", PrinterConfigurationRules.NormalizeConnectionType(" ethernet "));
        Assert.Throws<ArgumentException>(() => PrinterConfigurationRules.NormalizeConnectionType("paralelo"));
        Assert.Throws<ArgumentException>(() => PrinterConfigurationRules.Validate(
            new PrinterInput("Caja", "RED", "999.1.1.1", 9100, 80, false)));
        Assert.Throws<ArgumentException>(() => PrinterConfigurationRules.Validate(
            new PrinterInput("Caja", "USB", null, null, 72, false)));
    }

    /// <summary>Verifica que la fábrica diferencie comprobante interno, DTE y contingencia.</summary>
    [Fact]
    public void Factory_CreatesInternalAndDteDocuments()
    {
        var factory = new ReceiptDocumentFactory();
        var sale = new ReceiptSaleData(Guid.NewGuid(), "Cajero", "Cliente", null,
            new[] { new TicketLineItem("Producto", 1, "1.00", 1m, 1m) }, 1m, .13m, 1.13m, "EFECTIVO", 2m, DateTime.Now);
        var issuer = new ReceiptIssuerData("Emisor", null, "-", "-", "A verificar", null);

        var internalReceipt = factory.Create(sale, issuer, null, "LEYENDA");
        Assert.Equal(ReceiptDocumentTypes.InternalReceipt, internalReceipt.DteTypeCode);
        Assert.Equal(string.Empty, internalReceipt.Ambiente);
        Assert.DoesNotContain("*PRUEBA*", TicketReceiptRenderer.RenderPlainText(internalReceipt, 58));
        Assert.Empty(internalReceipt.ConsultaUrl);

        var dte = factory.Create(sale, issuer,
            new ReceiptDteData("01", "FACTURA", "DTE-1", "GEN-1", "SELLO", "00", "https://mh/1", true, .13m), "LEYENDA");
        var dteText = TicketReceiptRenderer.RenderPlainText(dte, 58);
        Assert.Contains("DTE-1", dteText);
        Assert.Contains("GEN-1", dteText);
        Assert.Contains("SELLO", dteText);
        Assert.Contains("[QR: https://mh/1]", dteText);
        Assert.Contains("*PRUEBA*", dteText);
        Assert.Contains("EMITIDO EN CONTINGENCIA", dteText);
    }

    /// <summary>Verifica timeout de escritura y cancelación externa del transporte TCP.</summary>
    [Fact]
    public async Task NetworkTransport_TimeoutsAndPropagatesCancellation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptedTask = listener.AcceptTcpClientAsync();
        var transport = new NetworkPrinterTransport();
        var stopwatch = Stopwatch.StartNew();
        var sendTask = transport.SendAsync("127.0.0.1", endpoint.Port,
            new byte[32 * 1024 * 1024], TimeSpan.FromSeconds(1));
        using var accepted = await acceptedTask;
        accepted.ReceiveBufferSize = 1;
        await Assert.ThrowsAsync<PrinterException>(() => sendTask);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
        listener.Stop();

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => transport.SendAsync("127.0.0.1", endpoint.Port,
            new byte[1], TimeSpan.FromSeconds(1), cancelled.Token));
    }

    /// <summary>Verifica que un puerto cerrado se exponga como error de impresora.</summary>
    [Fact]
    public async Task NetworkTransport_ClosedPortRaisesPrinterException()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await Assert.ThrowsAsync<PrinterException>(() => new NetworkPrinterTransport().SendAsync(
            "127.0.0.1", port, new byte[] { 1 }, TimeSpan.FromSeconds(1)));
    }

    private static ReceiptDocument CreateDocument() => new(
        "FERRETERIA PRUEBA", null, "-", "-", "DIRECCIÓN A VERIFICAR", null, "COMPROBANTE INTERNO", ReceiptDocumentTypes.InternalReceipt, "", "", null, "",
        DateTime.Now, "Cajero", "Cliente", null,
        new[] { new TicketLineItem("Cable eléctrico THHN de prueba con descripción larga", 2, "2.00", 3.50m, 7.00m) },
        7m, .91m, 7.91m, "SIETE DÓLARES", "EFECTIVO", 10m, 2.09m, "", false, "COMPROBANTE INTERNO - SIN VALOR FISCAL");
}
