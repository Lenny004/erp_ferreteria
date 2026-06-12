using System.Windows;
using System.Windows.Controls;
using FlexoCableSV.PuntoVenta.Views.Caja;
using ConfeccionViews = FlexoCableSV.PuntoVenta.Views.Confeccion;
using FlexoCableSV.PuntoVenta.Views.Inicio;

namespace FlexoCableSV.PuntoVenta.Views.Shell;

public partial class MainShellWindow : Window
{
    private readonly Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)> _sections;

    public MainShellWindow()
    {
        InitializeComponent();

        _sections = new Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)>
        {
            ["Stock"] = (BtnStock, "Consultar Stock", () => new ConsultarStockView()),
            ["Facturacion"] = (BtnFacturacion, "Facturacion", () => new FacturacionView()),
            ["HistorialFacturas"] = (BtnHistorialFacturas, "Historial de Facturas", () => new HistorialFacturasView()),
            ["Impresoras"] = (BtnImpresoras, "Impresoras", () => new ImpresorasView()),
            ["Devoluciones"] = (BtnDevoluciones, "Devoluciones", () => new DevolucionesView()),
            ["CorteCaja"] = (BtnCorteCaja, "Corte de Caja", () => new CorteCajaView()),
            ["HistorialVentas"] = (BtnHistorialVentas, "Historial de Ventas", () => new ConfeccionViews.HistorialVentasView()),
            ["Ordenes"] = (BtnOrdenes, "Ordenes de Confeccion", () => new ConfeccionViews.OrdenesConfeccionView()),
            ["Codigos"] = (BtnCodigos, "Ver Codigos", () => new ConfeccionViews.VerCodigosView())
        };

        ShowSection("Stock");
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionKey })
        {
            ShowSection(sectionKey);
        }
    }

    private void ShowSection(string sectionKey)
    {
        if (!_sections.TryGetValue(sectionKey, out var activeSection))
        {
            return;
        }

        foreach (var section in _sections.Values)
        {
            section.Button.Style = (Style)FindResource("FlexoNavButtonStyle");
        }

        activeSection.Button.Style = (Style)FindResource("FlexoNavButtonActiveStyle");
        HeaderTitle.Text = activeSection.Title;
        MainContent.Content = activeSection.CreateView();
    }

    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        new InicioWindow().Show();
        Close();
    }

    private void OnMinimizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
