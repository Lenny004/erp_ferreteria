using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Printing;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Casos adicionales de validación de <see cref="PrinterInput"/> y de maquetación del ticket
/// (anchos exactos, montos y ausencia de datos fiscales en el comprobante interno).
/// </summary>
public sealed class PrinterRulesAndLayoutTests
{
    /// <summary>Entradas inválidas que deben rechazarse con <see cref="ArgumentException"/>.</summary>
    /// <returns>Colección de entradas inválidas para la teoría.</returns>
    public static TheoryData<PrinterInput> InvalidInputs() => new()
    {
        new PrinterInput("", "USB", null, null, 80, false),
        new PrinterInput("   ", "USB", null, null, 80, false),
        new PrinterInput(new string('x', 201), "USB", null, null, 80, false),
        new PrinterInput("Caja", "RED", "192.168.1.50", 0, 80, false),
        new PrinterInput("Caja", "RED", "192.168.1.50", 65536, 80, false),
        new PrinterInput("Caja", "RED", null, 9100, 80, false),
        new PrinterInput("Caja", "RED", "no-es-ip", 9100, 80, false),
        new PrinterInput("Caja", "USB", null, null, 72, false),
        new PrinterInput("Caja", "PARALELO", null, null, 80, false),
    };

    /// <summary>Verifica que cada entrada inválida sea rechazada.</summary>
    /// <param name="input">Entrada inválida.</param>
    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void Validate_RejectsInvalidInput(PrinterInput input)
    {
        Assert.Throws<ArgumentException>(() => PrinterConfigurationRules.Validate(input));
    }

    /// <summary>Verifica que las configuraciones USB, red y Bluetooth válidas se acepten.</summary>
    [Fact]
    public void Validate_AcceptsValidUsbNetworkAndBluetooth()
    {
        PrinterConfigurationRules.Validate(new PrinterInput("EPSON TM-T20", "USB", null, null, 80, false));
        PrinterConfigurationRules.Validate(new PrinterInput("Caja red", "Ethernet", " 192.168.1.50 ", 9100, 58, false));
        PrinterConfigurationRules.Validate(new PrinterInput("Caja red", "RED", "192.168.1.50", 1, 80, false));
        PrinterConfigurationRules.Validate(new PrinterInput("Caja red", "RED", "192.168.1.50", 65535, 80, false));
        PrinterConfigurationRules.Validate(new PrinterInput("Caja BT", "Bluetooth", null, null, 58, false));
    }

    /// <summary>Verifica que las líneas de dos columnas terminen exactamente en el borde del papel.</summary>
    /// <param name="paperWidthMm">Ancho de papel.</param>
    /// <param name="columns">Columnas esperadas.</param>
    [Theory]
    [InlineData(58, 32)]
    [InlineData(80, 48)]
    public void PlainText_ColumnLinesEndExactlyAtPaperEdge(int paperWidthMm, int columns)
    {
        string text = TicketReceiptRenderer.RenderPlainText(CreateInternalDocument(), paperWidthMm);
        string[] lines = text.Split(Environment.NewLine);

        string[] moneyLines = lines.Where(line => line.TrimEnd().EndsWith(".00", StringComparison.Ordinal)
            || line.TrimEnd().EndsWith(".91", StringComparison.Ordinal)
            || line.TrimEnd().EndsWith(".09", StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(moneyLines);
        Assert.All(moneyLines, line => Assert.Equal(columns, line.Length));
        Assert.All(lines, line => Assert.True(line.Length <= columns, $"Línea de {line.Length} columnas: '{line}'"));
    }

    /// <summary>Verifica montos con símbolo de dólar y dos decimales en todas las líneas de totales.</summary>
    [Fact]
    public void PlainText_FormatsAllAmountsWithDollarAndTwoDecimals()
    {
        string text = TicketReceiptRenderer.RenderPlainText(CreateInternalDocument(), 80);

        Assert.Matches(@"Subtotal:\s+\$7\.00", text);
        Assert.Matches(@"IVA:\s+\$0\.91", text);
        Assert.Matches(@"TOTAL:\s+\$7\.91", text);
        Assert.Matches(@"Pago:\s+\$10\.00", text);
        Assert.Matches(@"Cambio:\s+\$2\.09", text);
        Assert.Contains("2 x $3.50", text);
    }

    /// <summary>Verifica que el comprobante interno no muestre número de control, sello ni QR.</summary>
    [Fact]
    public void PlainText_InternalReceiptHasNoFiscalIdentifiers()
    {
        string text = TicketReceiptRenderer.RenderPlainText(CreateInternalDocument(), 80);

        Assert.DoesNotContain("Num. Control", text);
        Assert.DoesNotContain("Sello", text);
        Assert.DoesNotContain("[QR:", text);
        Assert.DoesNotContain("*PRUEBA*", text);
        Assert.Contains("SIN VALOR FISCAL", text);
        Assert.EndsWith("[CORTE]" + Environment.NewLine, text);
    }

    /// <summary>Verifica los bytes exactos de negrita y corte parcial usados por el ticket.</summary>
    [Fact]
    public void EscPosCommands_BoldAndCutBytesAreExact()
    {
        Assert.Equal(new byte[] { 0x1B, 0x45, 0x01 }, EscPosCommands.BoldOn);
        Assert.Equal(new byte[] { 0x1B, 0x45, 0x00 }, EscPosCommands.BoldOff);
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 0x50 }, EscPosCommands.PartialCutWithFeed(80));
    }

    private static ReceiptDocument CreateInternalDocument() => new ReceiptDocumentFactory().Create(
        new ReceiptSaleData(
            Guid.NewGuid(),
            "Cajero de prueba",
            "Cliente de prueba",
            null,
            new[] { new TicketLineItem("Cable eléctrico THHN calibre 12 color rojo por metro (rollo 100 m)", 2, "2", 3.50m, 7.00m) },
            7.00m,
            0.91m,
            7.91m,
            "EFECTIVO",
            10.00m,
            new DateTime(2026, 9, 26, 10, 30, 0)),
        new ReceiptIssuerData("EMISOR A VERIFICAR", null, "-", "-", "DIRECCIÓN A VERIFICAR", null),
        null,
        "COMPROBANTE INTERNO - SIN VALOR FISCAL");
}
