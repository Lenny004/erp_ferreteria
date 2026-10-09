using Ferreteria.PuntoVenta.Helpers;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica los formatos compartidos que consume la presentación del POS.</summary>
public sealed class NumberFormatterTests
{
    /// <summary>El formato monetario conserva dos decimales y la cultura salvadoreña.</summary>
    [Fact]
    public void Currency_UsesEsSvWithTwoDecimals()
    {
        var formatted = NumberFormatter.Currency(1234.5m);

        Assert.Contains("1.234,50", formatted);
    }

    /// <summary>El formato de cantidad conserva como máximo tres decimales.</summary>
    [Fact]
    public void Quantity_UsesUpToThreeDecimals()
    {
        Assert.Equal("1,235", NumberFormatter.Quantity(1.2345m));
        Assert.Equal("2", NumberFormatter.Quantity(2m));
    }

    /// <summary>El formato de fecha es estable e independiente del locale del equipo.</summary>
    [Fact]
    public void Date_UsesBusinessDisplayFormat()
    {
        Assert.Equal("09/10/2026", NumberFormatter.Date(new DateTime(2026, 10, 9)));
    }
}
