using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Views.Inicio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace FlexoCableSV.PuntoVenta;

public partial class App : Application
{
    // El host de .NET maneja DI, configuración y ciclo de vida
    private readonly IHost _host;

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
                // Base de datos — usa el DatabaseConfig que ya hiciste
                services.AddFlexoDatabase(context.Configuration);

                // Ventanas — se registran para poder inyectar dependencias en ellas
                services.AddTransient<InicioWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            await _host.StartAsync();

            // DB validation commented out for UI/UX testing
            // var dbOk = await VerifyDatabaseConnectionAsync();
            // if (!dbOk)
            // {
            //     Shutdown();
            //     return;
            // }

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

    private async Task<bool> VerifyDatabaseConnectionAsync()
    {
        try
        {
            using var scope = _host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FlexoDbContext>();
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
