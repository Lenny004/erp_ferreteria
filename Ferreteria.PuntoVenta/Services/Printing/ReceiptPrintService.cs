using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Implementación ESC/POS de <see cref="IReceiptPrintService"/>. Envía tickets
/// (DTE o comprobante interno) y reportes a impresoras térmicas por spooler de
/// Windows (USB) o por red (TCP 9100, con timeout configurable).
/// </summary>
public sealed class ReceiptPrintService : IReceiptPrintService
{
    private const int DefaultNetworkPort = 9100;

    private readonly PrintingOptions _options;
    private readonly ILogger<ReceiptPrintService> _logger;
    private readonly IAuditService _auditService;
    private readonly ICurrentSessionService _currentSession;
    private readonly NetworkPrinterTransport _networkTransport;

    /// <summary>
    /// Inicializa el servicio de envío con timeout de red, transporte TCP y auditoría.
    /// </summary>
    /// <param name="options">Opciones de impresión (sección <c>Printing</c>).</param>
    /// <param name="logger">Registro técnico; los detalles de error solo van aquí.</param>
    /// <param name="auditService">Bitácora donde se registra cada impresión de ticket.</param>
    /// <param name="currentSession">Sesión del cajero, para atribuir la impresión.</param>
    /// <param name="networkTransport">Transporte TCP desacoplado de WPF y del registro de Windows.</param>
    public ReceiptPrintService(
        IOptions<PrintingOptions> options,
        ILogger<ReceiptPrintService> logger,
        IAuditService auditService,
        ICurrentSessionService currentSession,
        NetworkPrinterTransport networkTransport)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        _networkTransport = networkTransport ?? throw new ArgumentNullException(nameof(networkTransport));
    }

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
        catch (Exception ex)
        {
            // El acceso al registro puede fallar por permisos; se ignora esta fuente.
            _logger.LogDebug(ex, "No se pudo leer la lista de impresoras del usuario.");
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
        catch (Exception ex)
        {
            // Igual que arriba: se ignora si no hay acceso.
            _logger.LogDebug(ex, "No se pudo leer la lista de impresoras del equipo.");
        }

        var ordered = names.ToList();
        ordered.Sort(StringComparer.OrdinalIgnoreCase);
        return ordered;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Si el documento trae <see cref="ReceiptDocument.OrderId"/>, tras un envío correcto se
    /// registra el código <c>IMPRIMIR</c> y el evento lógico <c>IMPRESION_TICKET</c> en la bitácora. Un fallo de impresión no
    /// modifica la venta: solo se propaga como <see cref="PrinterException"/>.
    /// </remarks>
    public Task PrintReceiptAsync(ReceiptDocument document, PrinterConfig printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(printer);

        byte[] payload = TicketReceiptRenderer.Render(document, printer.PaperWidthMm);
        return SendAndAuditAsync(payload, printer, document.OrderId, cancellationToken);
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

    private async Task SendAndAuditAsync(
        byte[] payload,
        PrinterConfig printer,
        Guid? orderId,
        CancellationToken cancellationToken)
    {
        await SendAsync(payload, printer, cancellationToken);

        if (orderId is Guid id)
        {
            // Solo datos no sensibles: nombre y tipo de conexión de la impresora.
            await _auditService.RecordChangeAsync(
                SalesDomainConstants.PrintingAuditActions.TicketPrint,
                SalesDomainConstants.PrintingAuditActions.OrdersTableName,
                id.ToString(),
                null,
                new
                {
                    Evento = SalesDomainConstants.PrintingAuditActions.TicketPrintEvent,
                    printer.Name,
                    printer.ConnectionType
                },
                _currentSession.CurrentEmployee?.Id,
                cancellationToken);
        }
    }

    private async Task SendAsync(byte[] payload, PrinterConfig printer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? host = printer.IpAddress;
        bool isNetworkConnection = string.Equals(
            printer.ConnectionType,
            PrinterConfigurationRules.Network,
            StringComparison.OrdinalIgnoreCase);

        if (isNetworkConnection && !string.IsNullOrWhiteSpace(host))
        {
            int port = printer.NetworkPort ?? DefaultNetworkPort;
            var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.NetworkTimeoutSeconds));

            try
            {
                await _networkTransport.SendAsync(host, port, payload, timeout, cancellationToken);
            }
            catch (PrinterException ex)
            {
                // El detalle técnico (IP, puerto, causa) solo va al log; al cajero le llega el mensaje en español.
                _logger.LogError(ex, "Fallo de impresión de red en {PrinterAddress}:{Port}", printer.IpAddress, port);
                throw;
            }

            return;
        }

        // USB / spooler de Windows (o cualquier conexión que no sea red directa).
        if (string.IsNullOrWhiteSpace(printer.Name))
        {
            throw new ArgumentException(
                "El nombre de la impresora es obligatorio para conexiones USB/spooler.",
                nameof(printer));
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RawPrinterHelper.SendBytesToPrinter(printer.Name, payload);
        }, cancellationToken);
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
