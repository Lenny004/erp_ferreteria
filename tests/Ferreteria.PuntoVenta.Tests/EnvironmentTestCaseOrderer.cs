using Xunit.Abstractions;
using Xunit.Sdk;
using Xunit;

[assembly: TestCaseOrderer(
    "Ferreteria.PuntoVenta.Tests.EnvironmentTestCaseOrderer",
    "Ferreteria.PuntoVenta.Tests")]

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Reordena casos de cada clase únicamente cuando QA lo solicita por entorno.</summary>
public sealed class EnvironmentTestCaseOrderer : ITestCaseOrderer
{
    /// <summary>Invierte o baraja los casos de una clase según <c>FERRETERIA_TEST_ORDER</c>.</summary>
    /// <typeparam name="TTestCase">Tipo concreto de caso de xUnit.</typeparam>
    /// <param name="testCases">Casos recibidos en el orden predeterminado de xUnit.</param>
    /// <returns>Casos reordenados o la secuencia original si no se pidió una variante.</returns>
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        var mode = Environment.GetEnvironmentVariable("FERRETERIA_TEST_ORDER");
        if (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase))
        {
            return testCases.Reverse();
        }

        const string randomPrefix = "random:";
        if (mode is null || !mode.StartsWith(randomPrefix, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(mode[randomPrefix.Length..], out var seed))
        {
            return testCases;
        }

        var ordered = testCases.ToList();
        var random = new Random(seed);
        for (var index = ordered.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (ordered[index], ordered[swapIndex]) = (ordered[swapIndex], ordered[index]);
        }

        return ordered;
    }
}
