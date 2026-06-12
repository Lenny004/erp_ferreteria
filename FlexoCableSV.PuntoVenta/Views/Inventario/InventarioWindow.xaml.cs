using System.Windows;
using System.Windows.Controls;

namespace FlexoCableSV.PuntoVenta.Views.Inventario;

public partial class InventarioWindow : Window
{
    private readonly Dictionary<string, (Grid Panel, Button Button, string Title)> _sections;

    public InventarioWindow()
    {
        InitializeComponent();

        _sections = new Dictionary<string, (Grid Panel, Button Button, string Title)>
        {
            ["Stock"] = (PanelStock, BtnStock, "Consultar Stock"),
            ["Facturacion"] = (PanelFacturacion, BtnFacturacion, "Facturacion"),
            ["HistorialFacturas"] = (PanelHistorialFacturas, BtnHistorialFacturas, "Historial de Facturas"),
            ["Impresoras"] = (PanelImpresoras, BtnImpresoras, "Impresoras"),
            ["Devoluciones"] = (PanelDevoluciones, BtnDevoluciones, "Devoluciones"),
            ["CorteCaja"] = (PanelCorteCaja, BtnCorteCaja, "Corte de Caja"),
            ["HistorialVentas"] = (PanelHistorialVentas, BtnHistorialVentas, "Historial de Ventas"),
            ["Ordenes"] = (PanelOrdenes, BtnOrdenes, "Ordenes de Confeccion"),
            ["Codigos"] = (PanelCodigos, BtnCodigos, "Ver Codigos")
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
            section.Panel.Visibility = Visibility.Collapsed;
            section.Button.Style = (Style)FindResource("FlexoNavButtonStyle");
        }

        activeSection.Panel.Visibility = Visibility.Visible;
        activeSection.Button.Style = (Style)FindResource("FlexoNavButtonActiveStyle");
        HeaderTitle.Text = activeSection.Title;
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
