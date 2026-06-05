using FlexoCableSV.PuntoVenta.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace FlexoCableSV.PuntoVenta
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        /// <summary>
        /// Inicializa la aplicación y muestra la ventana principal resolviéndola desde DI.
        /// </summary>
        /// <param name="e">Argumentos de inicio de la aplicación WPF.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        /// <summary>
        /// Registra configuración y servicios base en el contenedor de dependencias.
        /// </summary>
        /// <param name="services">Colección de servicios a configurar.</param>
        private static void ConfigureServices(IServiceCollection services)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddFlexoDatabase(configuration);
            services.AddTransient<MainWindow>();
        }
    }
}
