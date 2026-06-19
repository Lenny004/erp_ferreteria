using System.Windows;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Views.PIN;
using FlexoCableSV.PuntoVenta.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Views.Inicio;

public partial class InicioWindow : Window
{
    private readonly IServiceProvider _services;

    public InicioWindow(IServiceProvider services)
    {
        _services = services;
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
        var pinWindow = ActivatorUtilities.CreateInstance<PinWindow>(
            _services,
            OperationalModule.Caja,
            NavSections.Facturacion);
        pinWindow.Show();
        Close();
    }

    private void OnInventarioClick(object sender, RoutedEventArgs e)
    {
        var pinWindow = ActivatorUtilities.CreateInstance<PinWindow>(
            _services,
            OperationalModule.Confeccion,
            NavSections.Ordenes);
        pinWindow.Show();
        Close();
    }
}
