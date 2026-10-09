using Ferreteria.PuntoVenta.Helpers;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Verifica el filtro decimal sin depender de controles WPF.
/// </summary>
public sealed class NumericInputTextRulesTests
{
    /// <summary>Permite valores completos y prefijos que pueden completarse durante la escritura.</summary>
    [Theory]
    [InlineData("", 0, 0, "1", 12, 2, false)]
    [InlineData("1", 1, 0, ",", 12, 2, false)]
    [InlineData("1,", 2, 0, "2", 12, 2, false)]
    [InlineData("1,2", 3, 0, "3", 12, 2, false)]
    [InlineData("1,23", 4, 0, "4", 12, 2, false)]
    [InlineData("1,234", 5, 0, ".", 12, 2, false)]
    [InlineData("1,234.", 6, 0, "5", 12, 2, false)]
    [InlineData("1,234.5", 7, 0, "0", 12, 2, false)]
    [InlineData("20", 2, 0, ".", 12, 2, false)]
    [InlineData("20", 2, 0, ",", 12, 2, false)]
    [InlineData("1.50", 2, 2, "25", 12, 2, false)]
    [InlineData("", 0, 0, "-", 12, 2, true)]
    public void Validate_AcceptsCompleteValuesAndTypingPrefixes(
        string current,
        int selectionStart,
        int selectionLength,
        string inserted,
        int precision,
        int scale,
        bool allowNegative)
    {
        var result = NumericInputTextRules.Validate(
            current,
            selectionStart,
            selectionLength,
            inserted,
            precision,
            scale,
            allowNegative);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>Exige que el pegado completo tenga una forma aceptada por el parser.</summary>
    [Theory]
    [InlineData("1,234.50")]
    [InlineData("1,234,567.89")]
    [InlineData("1234.50")]
    public void Validate_PasteAcceptsCanonicalParserValues(string pastedText)
    {
        var result = NumericInputTextRules.Validate(
            string.Empty,
            0,
            0,
            pastedText,
            precision: 12,
            scale: 2,
            allowNegative: false,
            allowIntermediate: false);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>Rechaza formas ambiguas o incompletas cuando se pegan como un bloque.</summary>
    [Theory]
    [InlineData("1,234")]
    [InlineData("1,2345")]
    [InlineData("1.234,5")]
    [InlineData("1,234.5,6")]
    [InlineData("a")]
    [InlineData("-")]
    [InlineData("1.234")]
    public void Validate_PasteRejectsInvalidValuesWithMessage(string pastedText)
    {
        var result = NumericInputTextRules.Validate(
            string.Empty,
            0,
            0,
            pastedText,
            precision: 12,
            scale: 2,
            allowNegative: false,
            allowIntermediate: false);

        Assert.False(result.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    /// <summary>Rechaza ediciones no permitidas y devuelve mensajes visibles en español.</summary>
    [Theory]
    [InlineData("20", 2, 0, "a", 12, 2, false)]
    [InlineData("20.", 3, 0, ".", 12, 2, false)]
    [InlineData("20", 2, 0, "-", 12, 2, false)]
    [InlineData("20", 2, 0, "-", 12, 2, true)]
    [InlineData("20.12", 5, 0, "3", 12, 2, false)]
    [InlineData("1,234", 5, 0, "5", 12, 2, false)]
    [InlineData("1,2", 3, 0, ".", 12, 2, false)]
    [InlineData("1,23.", 5, 0, "5", 12, 2, false)]
    [InlineData("1234567890", 10, 0, "1", 12, 2, false)]
    [InlineData("1,23", 3, 0, "4", 3, 2, false)]
    public void Validate_RejectsInvalidCandidateWithMessage(
        string current,
        int selectionStart,
        int selectionLength,
        string inserted,
        int precision,
        int scale,
        bool allowNegative)
    {
        var result = NumericInputTextRules.Validate(
            current,
            selectionStart,
            selectionLength,
            inserted,
            precision,
            scale,
            allowNegative);

        Assert.False(result.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}
