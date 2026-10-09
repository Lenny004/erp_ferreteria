using Ferreteria.PuntoVenta.Helpers;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica el filtro decimal sin depender de controles WPF.</summary>
public sealed class NumericInputTextRulesTests
{
    /// <summary>Permite separadores únicos y estados intermedios válidos.</summary>
    [Theory]
    [InlineData("20", 2, 0, ".", 12, 2, false, true)]
    [InlineData("20", 2, 0, ",", 12, 2, false, true)]
    [InlineData("20.", 3, 0, "5", 12, 2, false, true)]
    [InlineData("", 0, 0, "-", 12, 2, true, true)]
    [InlineData("1.50", 2, 2, "25", 12, 2, false, true)]
    public void IsAllowed_AcceptsValidCandidate(
        string current,
        int selectionStart,
        int selectionLength,
        string inserted,
        int precision,
        int scale,
        bool allowNegative,
        bool expected)
    {
        Assert.Equal(
            expected,
            NumericInputTextRules.IsAllowed(
                current,
                selectionStart,
                selectionLength,
                inserted,
                precision,
                scale,
                allowNegative));
    }

    /// <summary>Rechaza letras, signos fuera de posición y escalas excedidas.</summary>
    [Theory]
    [InlineData("20", 2, 0, "a", 12, 2, false)]
    [InlineData("20.", 3, 0, ".", 12, 2, false)]
    [InlineData("20", 2, 0, "-", 12, 2, false)]
    [InlineData("20", 2, 0, "-", 12, 2, true)]
    [InlineData("20.12", 5, 0, "3", 12, 2, false)]
    public void IsAllowed_RejectsInvalidCandidate(
        string current,
        int selectionStart,
        int selectionLength,
        string inserted,
        int precision,
        int scale,
        bool allowNegative)
    {
        Assert.False(
            NumericInputTextRules.IsAllowed(
                current,
                selectionStart,
                selectionLength,
                inserted,
                precision,
                scale,
                allowNegative));
    }
}
