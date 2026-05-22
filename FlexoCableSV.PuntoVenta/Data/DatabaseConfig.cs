using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Data
{
    public static class DatabaseConfig
    {
        public static IServiceCollection AddFlexoDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("FlexoDatabase")
                ?? throw new InvalidOperationException(
                    "No se encontró la cadena de conexión 'FlexoDatabase' en appsettings.json.");

            services.AddDbContext<FlexoDbContext>(options =>
                options.UseNpgsql(connectionString)
                       .UseSnakeCaseNamingConvention());

            return services;
        }
    }
}
