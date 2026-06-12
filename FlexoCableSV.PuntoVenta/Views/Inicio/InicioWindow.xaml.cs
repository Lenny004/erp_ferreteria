using System.Windows;
using FlexoCableSV.PuntoVenta.Views.Shell;

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
        var shell = new MainShellWindow();
        shell.Show();
        Close();
    }

    private void OnInventarioClick(object sender, RoutedEventArgs e)
    {
        var shell = new MainShellWindow();
        shell.Show();
        Close();
    }
}
