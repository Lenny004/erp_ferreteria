using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Printing;
using Microsoft.Extensions.Logging;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Vista para consultar, validar y configurar las impresoras de tickets
/// (<c>system.Printers</c>), imprimir una página de prueba y ver una vista previa del ticket.
/// La lógica de validación vive en <see cref="PrinterConfigurationRules"/>; aquí solo se
/// leen los controles y se muestran mensajes en español.
/// </summary>
public partial class ImpresorasView : UserControl
{
    private const int DefaultNetworkPort = 9100;
    private const string DialogTitle = "Impresoras";
    private const string InvalidDataTitle = "Datos inválidos";

    private readonly IPrinterConfigService _configService;
    private readonly IReceiptPrintService _printService;
    private readonly ILogger<ImpresorasView> _logger;
    private IReadOnlyList<Printer> _printers = Array.Empty<Printer>();

    /// <summary>
    /// Inicializa la vista con los servicios de configuración e impresión (resuelta por DI).
    /// </summary>
    /// <param name="configService">Servicio de persistencia de impresoras.</param>
    /// <param name="printService">Servicio de envío a impresoras térmicas.</param>
    /// <param name="logger">Registro técnico; los detalles de error no se muestran al cajero.</param>
    public ImpresorasView(
        IPrinterConfigService configService,
        IReceiptPrintService printService,
        ILogger<ImpresorasView> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _printService = printService ?? throw new ArgumentNullException(nameof(printService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateNetworkFields();

        // La lista de Windows no depende de la BD: se carga primero para que la vista siga usable.
        WindowsPrintersList.ItemsSource = _printService.GetInstalledWindowsPrinters();

        try
        {
            await ReloadAsync();
            StatusText.Text = "Lista cargada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar la configuración de impresoras.");
            StatusText.Text = "No se pudo cargar la configuración guardada. Puede usar las impresoras de Windows.";
        }
    }

    private void OnPrinterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrintersList.SelectedItem is not Printer printer)
        {
            return;
        }

        NameTextBox.Text = printer.Name;
        ConnectionCombo.SelectedIndex = printer.ConnectionType switch
        {
            PrinterConfigurationRules.Network => 1,
            PrinterConfigurationRules.Bluetooth => 2,
            _ => 0
        };
        IpTextBox.Text = printer.IpAddress ?? string.Empty;
        PortTextBox.Text = (printer.NetworkPort ?? DefaultNetworkPort).ToString(CultureInfo.InvariantCulture);
        PaperCombo.SelectedIndex = printer.PaperWidth == PrinterConfigurationRules.PaperWidth58 ? 1 : 0;
        UpdateNetworkFields();
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Printer saved = await _configService.SaveAsync(ReadInput());
            await ReloadAsync();
            SelectPrinter(saved.Id);
            StatusText.Text = "Configuración guardada.";
        }
        catch (ArgumentException ex)
        {
            // Los mensajes de validación ya están en español y no contienen datos técnicos.
            MessageBox.Show(ex.Message, InvalidDataTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar la configuración de impresora.");
            MessageBox.Show(
                "No se pudo guardar la configuración. Intente de nuevo o avise al administrador.",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnSetDefaultClick(object sender, RoutedEventArgs e)
    {
        if (PrintersList.SelectedItem is not Printer printer)
        {
            MessageBox.Show(
                "Primero toque una impresora de la lista de impresoras configuradas.",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            $"¿Desea que '{printer.Name}' sea la impresora predeterminada para los tickets?",
            "Confirmar cambio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _configService.SetDefaultAsync(printer.Id);
            await ReloadAsync();
            StatusText.Text = "Impresora predeterminada actualizada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cambiar la impresora predeterminada.");
            MessageBox.Show(
                "No se pudo cambiar la impresora predeterminada. Intente de nuevo.",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        try
        {
            PrinterInput input = ReadInput();
            PrinterConfigurationRules.Validate(input);
            await _printService.PrintTestPageAsync(new PrinterConfig(
                input.Name.Trim(),
                PrinterConfigurationRules.NormalizeConnectionType(input.ConnectionType),
                input.IpAddress?.Trim(),
                input.NetworkPort,
                input.PaperWidth));
            StatusText.Text = "Página de prueba enviada.";
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, InvalidDataTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (PrinterException ex)
        {
            // PrinterException trae un mensaje en español; el detalle técnico ya quedó en el log.
            MessageBox.Show(ex.Message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo imprimir la página de prueba.");
            MessageBox.Show(
                "No se pudo imprimir la página de prueba. Revise que la impresora esté encendida.",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        short paperWidth = ReadPaperWidth();
        string preview = TicketReceiptRenderer.RenderPlainText(BuildSampleDocument(), paperWidth);
        var dialog = new TicketPreviewDialog(preview)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private static ReceiptDocument BuildSampleDocument()
    {
        // Datos ficticios solo para la vista previa; emisor y leyenda: A VERIFICAR con contador / normativa MH.
        return new ReceiptDocument(
            BusinessName: "EMISOR A VERIFICAR",
            BusinessTradeName: null,
            BusinessNit: "-",
            BusinessNrc: "-",
            BusinessAddress: "DIRECCIÓN A VERIFICAR",
            BusinessPhone: null,
            DteTypeName: ReceiptDocumentTypes.InternalReceiptTitle,
            DteTypeCode: ReceiptDocumentTypes.InternalReceipt,
            NumeroControl: string.Empty,
            CodigoGeneracion: string.Empty,
            SelloRecibido: null,
            Ambiente: string.Empty,
            IssuedAt: DateTime.Now,
            CashierName: "Cajero de prueba",
            CustomerName: "Cliente de prueba",
            CustomerDocument: null,
            Items: new[]
            {
                new TicketLineItem("Producto de ejemplo con nombre largo para ver la envoltura", 1, "1", 1.00m, 1.00m)
            },
            Subtotal: 1.00m,
            Tax: 0.13m,
            Total: 1.13m,
            TotalInWords: "UNO 13/100 DÓLARES (EJEMPLO)",
            PaymentMethod: "EFECTIVO",
            AmountPaid: 2.00m,
            Change: 0.87m,
            ConsultaUrl: string.Empty,
            IsContingency: false,
            FooterNote: "COMPROBANTE INTERNO - SIN VALOR FISCAL");
    }

    private PrinterInput ReadInput()
    {
        string connection = (ConnectionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? PrinterConfigurationRules.Usb;
        int? port = int.TryParse(PortTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort)
            ? parsedPort
            : null;
        bool isNetwork = connection == PrinterConfigurationRules.Network;

        return new PrinterInput(
            NameTextBox.Text,
            connection,
            isNetwork ? IpTextBox.Text : null,
            isNetwork ? port : null,
            ReadPaperWidth(),
            IsDefault: false);
    }

    private short ReadPaperWidth()
    {
        string? tag = (PaperCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return short.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out short width)
            ? width
            : PrinterConfigurationRules.PaperWidth80;
    }

    private async Task ReloadAsync()
    {
        _printers = await _configService.GetAllAsync();
        PrintersList.ItemsSource = _printers;
        RenderDefault(_printers.FirstOrDefault(printer => printer.IsDefault));
    }

    private void SelectPrinter(Guid id)
    {
        PrintersList.SelectedItem = _printers.FirstOrDefault(printer => printer.Id == id);
    }

    private void RenderDefault(Printer? printer)
    {
        DefaultNameText.Text = printer?.Name ?? "Sin configurar";
        DefaultPaperText.Text = printer is null ? "-" : $"{printer.PaperWidth} mm";
    }

    private void OnConnectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateNetworkFields();
    }

    private void UpdateNetworkFields()
    {
        // SelectionChanged se dispara durante InitializeComponent, antes de crear los TextBox.
        if (!IsInitialized)
        {
            return;
        }

        bool isNetwork = string.Equals(
            (ConnectionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            PrinterConfigurationRules.Network,
            StringComparison.Ordinal);
        IpTextBox.IsEnabled = isNetwork;
        PortTextBox.IsEnabled = isNetwork;
    }
}
