using Ferreteria.PuntoVenta.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica que la cadena de conexión no se obtenga sin credenciales.</summary>
public sealed class DatabaseConnectionResolverTests
{
    /// <summary>La ausencia de cadena produce una guía operativa clara.</summary>
    [Fact]
    public void MissingConnection_ExplainsConfigurationSource()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<DatabaseConnectionConfigurationException>(
            () => new DatabaseConnectionResolver().Resolve(configuration));

        Assert.Contains("ConnectionStrings__FerreteriaDB", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>La cadena proporcionada por configuración se devuelve intacta.</summary>
    [Fact]
    public void ConfiguredConnection_IsUsed()
    {
        const string expected = "Host=test;Database=ferreteria;Username=pos_app;Password=test-only;SSL Mode=Require";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FerreteriaDB"] = expected
            })
            .Build();

        Assert.Equal(expected, new DatabaseConnectionResolver().Resolve(configuration));
    }
}
