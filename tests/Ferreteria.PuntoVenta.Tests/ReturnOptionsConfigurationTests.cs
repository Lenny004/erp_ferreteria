using Ferreteria.PuntoVenta.Services.Returns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica binding, defaults y whitelist de las opciones de devoluciones.</summary>
public sealed class ReturnOptionsConfigurationTests
{
    /// <summary>Demuestra que el binder no duplica defaults y descarta métodos no contractuales.</summary>
    [Fact]
    public void ConfigurationBinding_ProducesExactlyFourReasonsAndMethods()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Devoluciones:Motivos:0:Code"] = "PRODUCTO_DEFECTUOSO",
                ["Devoluciones:Motivos:0:Label"] = "Producto defectuoso",
                ["Devoluciones:Motivos:0:RequiresNotes"] = "false",
                ["Devoluciones:Motivos:1:Code"] = "ERROR_VENTA",
                ["Devoluciones:Motivos:1:Label"] = "Error de venta",
                ["Devoluciones:Motivos:1:RequiresNotes"] = "false",
                ["Devoluciones:Motivos:2:Code"] = "CAMBIO",
                ["Devoluciones:Motivos:2:Label"] = "Cambio",
                ["Devoluciones:Motivos:2:RequiresNotes"] = "false",
                ["Devoluciones:Motivos:3:Code"] = "ANULACION_TOTAL",
                ["Devoluciones:Motivos:3:Label"] = "Anulación total",
                ["Devoluciones:Motivos:3:RequiresNotes"] = "true",
                ["Devoluciones:Motivos:4:Code"] = "CAMBIO",
                ["Devoluciones:MetodosReintegroPermitidos:0"] = "EFECTIVO",
                ["Devoluciones:MetodosReintegroPermitidos:1"] = "TARJETA",
                ["Devoluciones:MetodosReintegroPermitidos:2"] = "TRANSFERENCIA",
                ["Devoluciones:MetodosReintegroPermitidos:3"] = "NINGUNO",
                ["Devoluciones:MetodosReintegroPermitidos:4"] = "VALE"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<ReturnOptions>(configuration.GetSection(ReturnOptions.SectionName));
        services.PostConfigure<ReturnOptions>(ReturnOptions.ApplyDefaults);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ReturnOptions>>().Value;

        Assert.Equal(4, options.Motivos.Count);
        Assert.Equal(4, options.MetodosReintegroPermitidos.Count);
        Assert.DoesNotContain("VALE", options.MetodosReintegroPermitidos, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(4, options.MetodosReintegroPermitidos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
