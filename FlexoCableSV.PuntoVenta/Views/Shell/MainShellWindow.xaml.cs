using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Views.Caja;
using ConfeccionViews = FlexoCableSV.PuntoVenta.Views.Confeccion;
using FlexoCableSV.PuntoVenta.Views.Inicio;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Views.Shell;

public partial class MainShellWindow : Window
{
    private readonly Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)> _sections;
    private readonly IConnectivityService _connectivityService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuditService _auditService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _connectivityTimer;

    public MainShellWindow(
        IConnectivityService connectivityService,
        ICurrentSessionService currentSession,
        IAuditService auditService,
        IServiceProvider serviceProvider,
        string initialSection = "Stock")
    {
        _connectivityService = connectivityService;
        _currentSession = currentSession;
        _auditService = auditService;
        _serviceProvider = serviceProvider;

        InitializeComponent();

        _sections = new Dictionary<string, (Button Button, string Title, Func<UserControl> CreateView)>
        {
            ["Stock"] = (BtnStock, "Consultar Stock", () => _serviceProvider.GetRequiredService<ConsultarStockView>()),
            ["Facturacion"] = (BtnFacturacion, "Facturacion", () => _serviceProvider.GetRequiredService<FacturacionView>()),
            ["HistorialFacturas"] = (BtnHistorialFacturas, "Historial de Facturas", () => new HistorialFacturasView()),
            ["Impresoras"] = (BtnImpresoras, "Impresoras", () => new ImpresorasView()),
            ["Devoluciones"] = (BtnDevoluciones, "Devoluciones", () => new DevolucionesView()),
            ["CorteCaja"] = (BtnCorteCaja, "Corte de Caja", () => new CorteCajaView()),
            ["HistorialVentas"] = (BtnHistorialVentas, "Historial de Ventas", () => new ConfeccionViews.HistorialVentasView()),
            ["Ordenes"] = (BtnOrdenes, "Ordenes de Confeccion", () => _serviceProvider.GetRequiredService<ConfeccionViews.OrdenesConfeccionView>()),
            ["Codigos"] = (BtnCodigos, "Ver Codigos", () => _serviceProvider.GetRequiredService<ConfeccionViews.VerCodigosView>())
        };

        ShowSection(initialSection);
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshConnectivityStatusAsync();
    }

    private async void OnConnectivityTimerTick(object? sender, EventArgs e)
    {
        await RefreshConnectivityStatusAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _connectivityTimer.Stop();
    }

    private async Task RefreshConnectivityStatusAsync()
    {
        var status = await _connectivityService.GetStatusAsync();
        ConnectivityText.Text = status.Message;
        ConnectivityText.Foreground = (Brush)FindResource(status.IsOnline ? "FlexoSuccess" : "FlexoError");
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

    private void RenderCurrentSession()
    {
        if (!_currentSession.IsActive || _currentSession.CurrentEmployee is null)
        {
            SessionText.Text = "Sin sesion activa";
            return;
        }

        var employee = _currentSession.CurrentEmployee;
        var startedAt = _currentSession.StartedAtUtc?.ToLocalTime().ToString("HH:mm") ?? "--:--";
        SessionText.Text = $"Cajero: {employee.FirstName} {employee.LastName} | Inicio: {startedAt}";
    }
}
