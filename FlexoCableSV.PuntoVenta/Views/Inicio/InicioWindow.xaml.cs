using System.Windows;
using FlexoCableSV.PuntoVenta.Views.Inventario;

namespace FlexoCableSV.PuntoVenta.Views.Inicio;

public partial class InicioWindow : Window
{
    public InicioWindow()
    {
        InitializeComponent();
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

    private void OnVentasClick(object sender, RoutedEventArgs e)
    {
        // Aquí irá la ventana de Ventas (con login por PIN si aplica)
        MessageBox.Show("Módulo de Ventas", "Ventas");
    }

    private void OnInventarioClick(object sender, RoutedEventArgs e)
    {
        var inventario = new InventarioWindow();
        inventario.Show();
        Close();
    }
}
