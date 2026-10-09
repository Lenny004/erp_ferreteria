using Ferreteria.PuntoVenta.Helpers;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica las reglas de entrada decimal independientes de la cultura del equipo.</summary>
public sealed class DecimalInputParserTests
{
    /// <summary>Comprueba que las formas decimal y de miles admitidas produzcan el valor correcto.</summary>
    [Theory]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    [InlineData("2,25", 2.25)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1,234,567", 1234567)]
    public void Parse_AcceptsSupportedForms(string input, double expected)
    {
        var result = DecimalInputParser.Parse(input, maxDecimals: 4, allowNegative: false);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal((decimal)expected, result.Value);
    }

    /// <summary>Comprueba que las combinaciones que no permiten distinguir miles y decimales fallen.</summary>
    [Theory]
    [InlineData("1.234,5")]
    [InlineData("1,234")]
    public void Parse_RejectsAmbiguousForms(string input)
    {
        var result = DecimalInputParser.Parse(input, maxDecimals: 4, allowNegative: false);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Valor ambiguo: use punto para decimales (ej. 1.5) y no use separador de miles.",
            result.ErrorMessage);
    }

    /// <summary>Comprueba el rechazo de entradas vacías, con letras, negativas o con escala excesiva.</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("1.2345")]
    [InlineData("")]
    [InlineData("abc")]
    public void Parse_RejectsInvalidInput(string input)
    {
        var result = DecimalInputParser.Parse(input, maxDecimals: 2, allowNegative: false);

        Assert.False(result.IsValid);
    }

    /// <summary>Comprueba los mensajes específicos para signo negativo y escala excedida.</summary>
    [Fact]
    public void Parse_ReturnsSpecificMessagesForNegativeAndScale()
    {
        var negative = DecimalInputParser.Parse("-1", maxDecimals: 2, allowNegative: false);
        var excessiveScale = DecimalInputParser.Parse("1.234", maxDecimals: 2, allowNegative: false);

        Assert.Equal("El valor no puede ser negativo.", negative.ErrorMessage);
        Assert.Equal("Máximo 2 decimales.", excessiveScale.ErrorMessage);
    }
}
