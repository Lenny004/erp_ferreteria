using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Dte;
using Ferreteria.PuntoVenta.Services.Printing;
using Ferreteria.PuntoVenta.Views.Caja;
using Ferreteria.PuntoVenta.Views.Inventario;
using Ferreteria.PuntoVenta.Views.Inicio;
using Ferreteria.PuntoVenta.Views.PIN;
using Ferreteria.PuntoVenta.Views.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace Ferreteria.PuntoVenta;

/// <summary>
/// Punto de entrada WPF del punto de venta. Configura el host genérico, DI, EF Core (PostgreSQL)
/// y abre la ventana de inicio tras verificar conectividad.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    /// <summary>Proveedor de servicios del host (acceso estático para ventanas resueltas fuera del ctor).</summary>
    public static IServiceProvider Services =>
        ((App)Current)._host.Services;

    /// <summary>
    /// Construye el host: carga <c>Config/appsettings.json</c>, registra DbContext y servicios de negocio/UI.
    /// </summary>
    public App()
    {
        if (ResourceAssembly is null)
        {
            ResourceAssembly = typeof(App).Assembly;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        Path.Combine("Config", "appsettings.json"),
                        optional: false,
                        reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddFerreteriaDatabase(context.Configuration);

                services.AddSingleton<ICurrentSessionService, CurrentSessionService>();
                services.AddSingleton<IConnectivityService, ConnectivityService>();
                services.AddSingleton<IAuditService, AuditService>();
                services.AddSingleton<IPinAttemptService, PinAttemptService>();
                services.AddSingleton<IInventoryService, InventoryService>();
                services.AddSingleton<IOrderService, OrderService>();
                services.AddSingleton<IProductCatalogService, ProductCatalogService>();
                services.AddSingleton<ISupplierService, SupplierService>();
                services.AddSingleton<IEmployeeService, EmployeeService>();
                services.AddSingleton<ICustomerService, CustomerService>();
                services.AddSingleton<IReportService, ReportService>();
                services.AddSingleton<PinAuthService>();

                services.AddTransient<PinWindow>();
                services.AddTransient<InicioWindow>();
                services.AddTransient<MainShellWindow>();
                services.AddTransient<FacturacionView>();
                services.AddTransient<HistorialFacturasView>();
                services.AddTransient<ConsultarStockView>();
                services.AddTransient<ProductosView>();
                services.AddTransient<ProveedoresView>();
                services.AddTransient<MovimientosView>();
                services.AddTransient<AlertasView>();
                services.AddTransient<UsuariosView>();
            })
            .Build();
    }

    /// <summary>Arranca el host, verifica PostgreSQL y muestra <see cref="InicioWindow"/>.</summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            await _host.StartAsync();

            var dbOk = await VerifyDatabaseConnectionAsync();
            if (!dbOk)
            {
                Shutdown();
                return;
            }

            var inicioWindow = _host.Services.GetRequiredService<InicioWindow>();
            inicioWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error critico al iniciar la aplicacion:\n{ex.Message}",
                "Error de Arranque",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>Detiene y libera el host genérico al cerrar la aplicación.</summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await _host.StopAsync();
        }
        finally
        {
            _host.Dispose();
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Comprueba conectividad a la base de datos antes de abrir la UI.
    /// </summary>
    /// <returns>True si PostgreSQL responde; false si se mostró error y debe cerrarse la app.</returns>
    private async Task<bool> VerifyDatabaseConnectionAsync()
    {
        try
        {
            using var scope = _host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                MessageBox.Show(
                    "No se puede conectar a la base de datos.\n" +
                    "Verifique que PostgreSQL esté corriendo y la cadena de conexión sea correcta.",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al conectar con la base de datos:\n{ex.Message}",
                "Error de Conexión",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }
}
