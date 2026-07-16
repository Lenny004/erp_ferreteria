using System.Net.Sockets;
using Microsoft.Win32;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Implementación ESC/POS de <see cref="IReceiptPrintService"/>. Envía tickets DTE
/// y reportes a impresoras térmicas por spooler de Windows (USB) o por red (TCP 9100).
/// </summary>
public sealed class ReceiptPrintService : IReceiptPrintService
{
    private const int DefaultNetworkPort = 9100;
    private const string NetworkConnection = "RED";

    /// <inheritdoc />
    public IReadOnlyList<string> GetInstalledWindowsPrinters()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Impresoras del usuario actual: valores del registro Devices.
        try
        {
            using RegistryKey? devices = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows NT\CurrentVersion\Devices");
            if (devices is not null)
            {
                foreach (string valueName in devices.GetValueNames())
                {
                    if (!string.IsNullOrWhiteSpace(valueName))
                    {
                        names.Add(valueName);
                    }
                }
            }
        }
        catch (Exception)
        {
            // El acceso al registro puede fallar por permisos; se ignora esta fuente.
        }

        // Impresoras de la máquina: subclaves de Print\Printers.
        try
        {
            using RegistryKey? printers = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Print\Printers");
            if (printers is not null)
            {
                foreach (string subKeyName in printers.GetSubKeyNames())
                {
                    if (!string.IsNullOrWhiteSpace(subKeyName))
                    {
                        names.Add(subKeyName);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Igual que arriba: se ignora si no hay acceso.
        }

        var ordered = names.ToList();
        ordered.Sort(StringComparer.OrdinalIgnoreCase);
        return ordered;
    }

    /// <inheritdoc />
    public Task PrintReceiptAsync(ReceiptDocument document, PrinterConfig printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(printer);

        byte[] payload = TicketReceiptRenderer.Render(document, printer.PaperWidthMm);
        return SendAsync(payload, printer, cancellationToken);
    }

    /// <inheritdoc />
    public Task PrintTestPageAsync(PrinterConfig printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printer);

        var builder = new EscPosDocumentBuilder(printer.PaperWidthMm);
        builder.AppendBold("PAGINA DE PRUEBA");
        builder.AppendCenter("Ferreteria - Punto de Venta");
        builder.AppendSeparator();
        builder.AppendLeft($"Impresora: {printer.Name}");
        builder.AppendLeft($"Conexion: {printer.ConnectionType}");
        builder.AppendLeft($"Ancho: {printer.PaperWidthMm} mm ({builder.Columns} columnas)");
        builder.AppendLeft($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        builder.AppendSeparator();

        // Patrón de prueba: regla de columnas y muestra de acentos y símbolo de moneda.
        builder.AppendLine(BuildColumnRuler(builder.Columns));
        builder.AppendLine("Acentos: aeiou AEIOU n con tilde");
        builder.AppendColumns("Ejemplo columnas", "$1,234.56");
        builder.AppendSeparator();
        builder.AppendQr("https://admin.factura.gob.sv/consultaPublica");
        builder.AppendCenter("QR de prueba");
        builder.Feed(2).Cut();

        return SendAsync(builder.Build(), printer, cancellationToken);
    }

    /// <inheritdoc />
    public Task PrintTextReportAsync(string title, string body, PrinterConfig printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printer);

        var builder = new EscPosDocumentBuilder(printer.PaperWidthMm);

        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendBold(title.ToUpperInvariant());
            builder.AppendSeparator();
        }

        string content = body ?? string.Empty;
        foreach (string line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            builder.AppendLeft(line);
        }

        builder.Feed(2).Cut();
        return SendAsync(builder.Build(), printer, cancellationToken);
    }

    private static Task SendAsync(byte[] payload, PrinterConfig printer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool isNetwork = string.Equals(printer.ConnectionType, NetworkConnection, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(printer.IpAddress);

        if (isNetwork)
        {
            return Task.Run(() => SendOverNetwork(payload, printer, cancellationToken), cancellationToken);
        }

        // USB / spooler de Windows (o cualquier conexión que no sea red directa).
        if (string.IsNullOrWhiteSpace(printer.Name))
        {
            throw new ArgumentException(
                "El nombre de la impresora es obligatorio para conexiones USB/spooler.",
                nameof(printer));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RawPrinterHelper.SendBytesToPrinter(printer.Name, payload);
        }, cancellationToken);
    }

    private static void SendOverNetwork(byte[] payload, PrinterConfig printer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(printer.IpAddress))
        {
            throw new ArgumentException(
                "La dirección IP es obligatoria para conexiones de red.",
                nameof(printer));
        }

        cancellationToken.ThrowIfCancellationRequested();

        int port = printer.NetworkPort ?? DefaultNetworkPort;

        try
        {
            using var client = new TcpClient();
            client.Connect(printer.IpAddress, port);

            using NetworkStream stream = client.GetStream();
            cancellationToken.ThrowIfCancellationRequested();
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PrinterException(
                $"No se pudo imprimir por red en {printer.IpAddress}:{port}.",
                ex);
        }
    }

    private static string BuildColumnRuler(int columns)
    {
        var chars = new char[columns];
        for (int index = 0; index < columns; index++)
        {
            chars[index] = (char)('0' + (index % 10));
        }

        return new string(chars);
    }
}
