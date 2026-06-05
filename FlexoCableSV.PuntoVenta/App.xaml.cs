using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Views.Inicio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace FlexoCableSV.PuntoVenta;

public partial class App : Application
{
    // El host de .NET maneja DI, configuración y ciclo de vida
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                // Lee appsettings.json automáticamente
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
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
        await _host.StartAsync();

        // Verificar conexión a BD al arrancar
        await VerifyDatabaseConnectionAsync();

        // Mostrar pantalla de inicio
        var inicioWindow = _host.Services.GetRequiredService<InicioWindow>();
        inicioWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }

    private async Task VerifyDatabaseConnectionAsync()
    {
        try
        {
            var dbContext = _host.Services.GetRequiredService<FlexoDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                MessageBox.Show(
                    "No se puede conectar a la base de datos.\n" +
                    "Verifique que PostgreSQL esté corriendo y la cadena de conexión sea correcta.",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al conectar con la base de datos:\n{ex.Message}",
                "Error de Conexión",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }
}