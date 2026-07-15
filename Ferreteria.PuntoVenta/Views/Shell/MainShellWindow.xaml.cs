using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Views.Caja;
using InventarioViews = Ferreteria.PuntoVenta.Views.Inventario;
using Ferreteria.PuntoVenta.Views.Inicio;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Views.Shell;

/// <summary>
/// Shell principal post-login: navegación lateral entre módulos Caja e Inventario
/// según el <see cref="OperationalModule"/> de la sesión (Employee autenticado por PIN).
/// </summary>
public partial class MainShellWindow : Window
{
    private readonly Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)> _sections;
    private readonly IConnectivityService _connectivityService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuditService _auditService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _connectivityTimer;

    /// <summary>
    /// Construye el shell, registra secciones de navegación y muestra la sección inicial de la sesión.
    /// </summary>
    public MainShellWindow(
        IConnectivityService connectivityService,
        ICurrentSessionService currentSession,
        IAuditService auditService,
        IServiceProvider serviceProvider)
    {
        _connectivityService = connectivityService;
        _currentSession = currentSession;
        _auditService = auditService;
        _serviceProvider = serviceProvider;

        InitializeComponent();

        _sections = new Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)>
        {
            [NavSections.Stock] = (BtnStock, "Consultar Stock", () => _serviceProvider.GetRequiredService<ConsultarStockView>()),
            [NavSections.Facturacion] = (BtnFacturacion, "Facturacion", () => _serviceProvider.GetRequiredService<FacturacionView>()),
            [NavSections.HistorialFacturas] = (BtnHistorialFacturas, "Historial de Facturas", () => _serviceProvider.GetRequiredService<HistorialFacturasView>()),
            [NavSections.Impresoras] = (BtnImpresoras, "Impresoras", () => new ImpresorasView()),
            [NavSections.Devoluciones] = (BtnDevoluciones, "Devoluciones", () => new DevolucionesView()),
            [NavSections.CorteCaja] = (BtnCorteCaja, "Corte de Caja", () => new CorteCajaView()),
            [NavSections.Productos] = (BtnProductos, "Catalogo de Productos", () => _serviceProvider.GetRequiredService<InventarioViews.ProductosView>()),
            [NavSections.Proveedores] = (BtnProveedores, "Proveedores", () => _serviceProvider.GetRequiredService<InventarioViews.ProveedoresView>()),
            [NavSections.Movimientos] = (BtnMovimientos, "Entradas y Kardex", () => _serviceProvider.GetRequiredService<InventarioViews.MovimientosView>()),
            [NavSections.Alertas] = (BtnAlertas, "Alertas de Stock", () => _serviceProvider.GetRequiredService<InventarioViews.AlertasView>()),
            [NavSections.Usuarios] = (BtnUsuarios, "Usuarios y RRHH", () => _serviceProvider.GetRequiredService<InventarioViews.UsuariosView>())
        };

        ApplyModuleNavigation();
        ShowSection(_currentSession.ResolveInitialSection());
        RenderCurrentSession();

        _connectivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _connectivityTimer.Tick += OnConnectivityTimerTick;
        _connectivityTimer.Start();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Muestra u oculta botones de navegación según el módulo activo (Caja u Inventario).
    /// </summary>
    private void ApplyModuleNavigation()
    {
        if (_currentSession.ActiveModule is not OperationalModule activeModule)
        {
            foreach (var section in _sections.Values)
            {
                section.Button.Visibility = Visibility.Collapsed;
            }

            TxtNavSectionCaja.Visibility = Visibility.Collapsed;
            NavSectionDivider.Visibility = Visibility.Collapsed;
            TxtNavSectionInventario.Visibility = Visibility.Collapsed;
            return;
        }

        var allowedSections = NavSections.ForModule(activeModule);
        var allowedSet = allowedSections.ToHashSet(StringComparer.Ordinal);

        var cajaVisible = false;
        var inventarioVisible = false;

        foreach (var (sectionKey, section) in _sections)
        {
            var isAllowed = allowedSet.Contains(sectionKey);
            section.Button.Visibility = isAllowed ? Visibility.Visible : Visibility.Collapsed;

            if (!isAllowed)
            {
                continue;
            }

            if (NavSections.ForModule(OperationalModule.Caja).Contains(sectionKey, StringComparer.Ordinal))
            {
                cajaVisible = true;
            }

            if (NavSections.ForModule(OperationalModule.Inventario).Contains(sectionKey, StringComparer.Ordinal))
            {
                inventarioVisible = true;
            }
        }

        TxtNavSectionCaja.Visibility = cajaVisible ? Visibility.Visible : Visibility.Collapsed;
        TxtNavSectionInventario.Visibility = inventarioVisible ? Visibility.Visible : Visibility.Collapsed;
        NavSectionDivider.Visibility = cajaVisible && inventarioVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>Al cargar, consulta el estado de conectividad con la base de datos.</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshConnectivityStatusAsync();
    }

    /// <summary>Timer periódico de conectividad (cada 15 s).</summary>
    private async void OnConnectivityTimerTick(object? sender, EventArgs e)
    {
        await RefreshConnectivityStatusAsync();
    }

    /// <summary>Detiene el timer al cerrar la ventana.</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _connectivityTimer.Stop();
    }

    /// <summary>Actualiza el indicador visual de conexión online/offline.</summary>
    private async Task RefreshConnectivityStatusAsync()
    {
        var status = await _connectivityService.GetStatusAsync();
        ConnectivityText.Text = status.Message;
        ConnectivityText.Foreground = (Brush)FindResource(status.IsOnline ? "AppSuccess" : "AppError");
    }

    /// <summary>Handler de clic en un botón del menú lateral (Tag = clave de sección).</summary>
    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionKey })
        {
            ShowSection(sectionKey);
        }
    }

    /// <summary>
    /// Cambia el contenido central a la vista de la sección indicada, si pertenece al módulo activo.
    /// </summary>
    private void ShowSection(string sectionKey)
    {
        if (_currentSession.ActiveModule is OperationalModule activeModule
            && !NavSections.BelongsToModule(sectionKey, activeModule))
        {
            return;
        }

        if (!_sections.TryGetValue(sectionKey, out var activeSection))
        {
            return;
        }

        if (activeSection.Button.Visibility != Visibility.Visible)
        {
            return;
        }

        foreach (var section in _sections.Values)
        {
            section.Button.Style = (Style)FindResource("AppNavButtonStyle");
        }

        activeSection.Button.Style = (Style)FindResource("AppNavButtonActiveStyle");
        HeaderTitle.Text = activeSection.Title;
        MainContent.Content = activeSection.CreateView();
    }

    /// <summary>
    /// Cierra sesión del Employee, registra auditoría de logout y vuelve a InicioWindow.
    /// </summary>
    private async void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession.CurrentEmployee is not null)
        {
            await _auditService.RecordLogoutAsync(_currentSession.CurrentEmployee, _currentSession.CurrentModule);
        }

        _currentSession.EndSession();
        var inicio = App.Services.GetRequiredService<InicioWindow>();
        inicio.Show();
        Close();
    }

    /// <summary>Minimiza la ventana del shell.</summary>
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

    /// <summary>Cierra la aplicación (ventana shell).</summary>
    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>Muestra en el encabezado el rol, nombre del empleado y hora de inicio de sesión.</summary>
    private void RenderCurrentSession()
    {
        if (!_currentSession.IsActive || _currentSession.CurrentEmployee is null)
        {
            SessionText.Text = "Sin sesion activa";
            return;
        }

        var employee = _currentSession.CurrentEmployee;
        var startedAt = _currentSession.StartedAtUtc?.ToLocalTime().ToString("HH:mm") ?? "--:--";
        var roleLabel = _currentSession.ActiveModule switch
        {
            OperationalModule.Caja => "Cajero",
            OperationalModule.Inventario => "Encargado",
            _ => "Usuario"
        };

        SessionText.Text = $"{roleLabel}: {employee.FirstName} {employee.LastName} | Inicio: {startedAt}";
    }
}
