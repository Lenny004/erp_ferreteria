using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Comprueba que los contratos UI, EF y <c>Squema.sql</c> no deriven.</summary>
public sealed class FieldConstraintsDriftTests
{
    /// <summary>Verifica todas las constantes públicas de restricciones declaradas.</summary>
    [Fact]
    public void EveryConstraint_MatchesSchemaAndEfMetadata()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "Squema.sql");
        var schema = ParseSchema(File.ReadAllText(schemaPath));
        var options = new DbContextOptionsBuilder<FerreteriaDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conectar")
            .Options;
        using var db = new FerreteriaDbContext(options);

        var fields = typeof(FieldConstraints)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));

        var fieldCount = 0;
        foreach (var field in fields)
        {
            fieldCount++;
            var constraint = field.GetCustomAttribute<FieldConstraintAttribute>();
            Assert.NotNull(constraint);
            var key = new SchemaKey(constraint!.Schema, constraint.Table, constraint.Column);
            Assert.True(schema.TryGetValue(key, out var sqlType), $"No se encontró {key} para {field.DeclaringType!.Name}.{field.Name}.");

            var expected = (int)field.GetRawConstantValue()!;
            if (constraint.Kind == FieldConstraintKind.MaxLength)
            {
                Assert.Equal("VARCHAR", sqlType!.TypeName);
                Assert.Equal(expected, sqlType.Precision);

                var efProperty = db.Model.FindEntityType(constraint.EntityType)?.FindProperty(constraint.PropertyName);
                Assert.NotNull(efProperty);
                Assert.Equal(expected, efProperty!.PropertyInfo?.GetCustomAttribute<MaxLengthAttribute>()?.Length);
                Assert.Equal(expected, efProperty.GetMaxLength());
            }
            else
            {
                Assert.Contains(sqlType!.TypeName, new[] { "NUMERIC", "DECIMAL" });
                Assert.Equal(
                    constraint.Kind == FieldConstraintKind.Precision ? sqlType.Precision : sqlType.Scale,
                    expected);

                var efProperty = db.Model.FindEntityType(constraint.EntityType)?.FindProperty(constraint.PropertyName);
                Assert.NotNull(efProperty);
                var efTypeMatch = Regex.Match(
                    efProperty!.GetColumnType() ?? string.Empty,
                    @"(?:NUMERIC|DECIMAL)\s*\((?<precision>\d+)\s*,\s*(?<scale>\d+)\)",
                    RegexOptions.IgnoreCase);
                Assert.True(efTypeMatch.Success, $"{constraint.EntityType.Name}.{constraint.PropertyName} debe conservar numeric(p,s) en EF.");
                Assert.Equal(sqlType.Precision, int.Parse(efTypeMatch.Groups["precision"].Value));
                Assert.Equal(sqlType.Scale, int.Parse(efTypeMatch.Groups["scale"].Value));
            }
        }

        Assert.True(fieldCount > 0, "FieldConstraints debe declarar al menos una constante pública.");
    }

    private static Dictionary<SchemaKey, SchemaType> ParseSchema(string sql)
    {
        var result = new Dictionary<SchemaKey, SchemaType>();
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline;
        const string tablePattern =
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<schema>[A-Za-z_][A-Za-z0-9_]*)\.""(?<table>[^""]+)""\s*\((?<body>.*?)\);";
        const string columnPattern =
            @"^\s*""(?<column>[^""]+)""\s+(?<type>VARCHAR|NUMERIC|DECIMAL)\s*(?:\((?<precision>\d+)\s*(?:,\s*(?<scale>\d+))?\))?";

        foreach (Match tableMatch in Regex.Matches(sql, tablePattern, options))
        {
            var schema = tableMatch.Groups["schema"].Value;
            var table = tableMatch.Groups["table"].Value;
            foreach (Match columnMatch in Regex.Matches(tableMatch.Groups["body"].Value, columnPattern, options))
            {
                var precision = columnMatch.Groups["precision"].Success
                    ? int.Parse(columnMatch.Groups["precision"].Value)
                    : (int?)null;
                var scale = columnMatch.Groups["scale"].Success
                    ? int.Parse(columnMatch.Groups["scale"].Value)
                    : (int?)null;
                result[new SchemaKey(schema, table, columnMatch.Groups["column"].Value)] =
                    new SchemaType(columnMatch.Groups["type"].Value.ToUpperInvariant(), precision, scale);
            }
        }

        return result;
    }

    private sealed record SchemaType(string TypeName, int? Precision, int? Scale);

    private readonly record struct SchemaKey(string Schema, string Table, string Column)
    {
        public override string ToString() => $"{Schema}.{Table}.{Column}";
    }
}
