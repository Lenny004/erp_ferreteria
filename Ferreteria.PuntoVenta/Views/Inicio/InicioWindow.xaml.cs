using System.Windows;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Views.PIN;
using Ferreteria.PuntoVenta.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Views.Inicio;

/// <summary>
/// Pantalla de bienvenida (entrada de la app): el usuario elige módulo Caja o Inventario
/// y se abre <see cref="PinWindow"/> para autenticar al Employee.
/// </summary>
public partial class InicioWindow : Window
{
    private readonly IServiceProvider _services;

    /// <summary>Inicializa la pantalla de inicio con el contenedor DI.</summary>
    public InicioWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    /// <summary>Minimiza la ventana de inicio.</summary>
    private void OnMinimizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>Alterna maximizar / restaurar.</summary>
    private void OnMaximizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>Cierra la aplicación.</summary>
    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Entrada a módulo Caja: abre PIN con sección inicial Facturación (Order / venta de mostrador).
    /// </summary>
    private void OnVentasClick(object sender, RoutedEventArgs e)
    {
        var pinWindow = ActivatorUtilities.CreateInstance<PinWindow>(
            _services,
            OperationalModule.Caja,
            NavSections.Facturacion);
        pinWindow.Show();
        Close();
    }

    /// <summary>
    /// Entrada a módulo Inventario: abre PIN con sección inicial Productos (catálogo Product).
    /// </summary>
    private void OnInventarioClick(object sender, RoutedEventArgs e)
    {
        var pinWindow = ActivatorUtilities.CreateInstance<PinWindow>(
            _services,
            OperationalModule.Inventario,
            NavSections.Productos);
        pinWindow.Show();
        Close();
    }
}
