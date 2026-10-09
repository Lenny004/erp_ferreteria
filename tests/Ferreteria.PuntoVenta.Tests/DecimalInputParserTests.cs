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
        var result = DecimalInputParser.Parse(input, precision: 12, scale: 4, allowNegative: false);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal((decimal)expected, result.Value);
    }

    /// <summary>Comprueba que un separador decimal final se interprete como un número entero.</summary>
    [Theory]
    [InlineData("20.", 20)]
    [InlineData("20,", 20)]
    [InlineData("-20.", -20)]
    [InlineData("-20,", -20)]
    public void Parse_AcceptsTrailingDecimalSeparator(string input, double expected)
    {
        var result = DecimalInputParser.Parse(input, precision: 12, scale: 2, allowNegative: true);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal((decimal)expected, result.Value);
    }

    /// <summary>Rechaza un separador decimal sin dígitos enteros.</summary>
    [Theory]
    [InlineData(".")]
    [InlineData(",")]
    public void Parse_RejectsSeparatorWithoutInteger(string input)
    {
        var result = DecimalInputParser.Parse(input, precision: 12, scale: 2, allowNegative: false);

        Assert.False(result.IsValid);
    }

    /// <summary>Comprueba que las combinaciones que no permiten distinguir miles y decimales fallen.</summary>
    [Theory]
    [InlineData("1.234,5")]
    [InlineData("1,234")]
    public void Parse_RejectsAmbiguousForms(string input)
    {
        var result = DecimalInputParser.Parse(input, precision: 12, scale: 4, allowNegative: false);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Valor ambiguo: escriba los decimales con punto o coma (ej. 1.5 o 1,5) y los miles sin separador o como 1,234.5.",
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
        var result = DecimalInputParser.Parse(input, precision: 12, scale: 2, allowNegative: false);

        Assert.False(result.IsValid);
    }

    /// <summary>Comprueba los mensajes específicos para signo negativo y escala excedida.</summary>
    [Fact]
    public void Parse_ReturnsSpecificMessagesForNegativeAndScale()
    {
        var negative = DecimalInputParser.Parse("-1", precision: 12, scale: 2, allowNegative: false);
        var excessiveScale = DecimalInputParser.Parse("1.234", precision: 12, scale: 2, allowNegative: false);

        Assert.Equal("El valor no puede ser negativo.", negative.ErrorMessage);
        Assert.Equal("Máximo 2 decimales.", excessiveScale.ErrorMessage);
    }

    /// <summary>Comprueba la precisión total, la escala y el conteo de enteros significativos.</summary>
    [Fact]
    public void Parse_EnforcesPrecisionAndScale()
    {
        var valid = DecimalInputParser.Parse("999.99", precision: 5, scale: 2, allowNegative: false);
        var leadingZeros = DecimalInputParser.Parse("000999.99", precision: 5, scale: 2, allowNegative: false);
        var tooManyIntegerDigits = DecimalInputParser.Parse("1000", precision: 5, scale: 2, allowNegative: false);
        var tooManyIntegerDigitsWithScale = DecimalInputParser.Parse("1000.5", precision: 5, scale: 2, allowNegative: false);
        var tooManyIntegerDigitsWithThousands = DecimalInputParser.Parse("1,234.5", precision: 5, scale: 2, allowNegative: false);
        var tooManyDecimals = DecimalInputParser.Parse("1.234", precision: 5, scale: 2, allowNegative: false);

        Assert.True(valid.IsValid, valid.ErrorMessage);
        Assert.True(leadingZeros.IsValid, leadingZeros.ErrorMessage);
        Assert.Equal("Máximo 3 dígitos enteros.", tooManyIntegerDigits.ErrorMessage);
        Assert.Equal("Máximo 3 dígitos enteros.", tooManyIntegerDigitsWithScale.ErrorMessage);
        Assert.Equal("Máximo 3 dígitos enteros.", tooManyIntegerDigitsWithThousands.ErrorMessage);
        Assert.Equal("Máximo 2 decimales.", tooManyDecimals.ErrorMessage);
    }

    /// <summary>Rechaza precisiones y escalas incompatibles con Decimal(p,s).</summary>
    [Fact]
    public void Parse_RejectsInvalidPrecisionAndScaleArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecimalInputParser.Parse("1", precision: 0, scale: 0, allowNegative: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecimalInputParser.Parse("1", precision: 5, scale: -1, allowNegative: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecimalInputParser.Parse("1", precision: 5, scale: 6, allowNegative: false));
    }
}
