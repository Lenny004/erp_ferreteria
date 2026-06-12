using System.Windows;

namespace FlexoCableSV.PuntoVenta.Views.Inventario;

public partial class InventarioWindow : Window
{
    public InventarioWindow()
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
}
