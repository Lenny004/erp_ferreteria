using System.Reflection;
using System.Text.RegularExpressions;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Guarda el límite de acciones de auditoría frente al modelo, SQL y código fuente.</summary>
public sealed class AuditActionLengthTests
{
    /// <summary>Comprueba que el mapeo EF y Squema.sql conservan el mismo límite.</summary>
    [Fact]
    public void AuditLogActionLimit_MatchesEfMappingAndSchema()
    {
        var options = new DbContextOptionsBuilder<FerreteriaDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conectar;Username=sin_conectar;Password=sin_conectar")
            .Options;
        using var db = new FerreteriaDbContext(options);
        var entityType = db.Model.FindEntityType(typeof(AuditLog));
        Assert.NotNull(entityType);
        var actionProperty = entityType.FindProperty(nameof(AuditLog.Action));
        Assert.NotNull(actionProperty);
        var mappedLimit = actionProperty.GetMaxLength();
        Assert.True(mappedLimit.HasValue, "AuditLog.Action debe declarar una longitud máxima en el mapeo EF.");

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "Squema.sql");
        Assert.True(File.Exists(schemaPath), $"No se encontró el esquema copiado en {schemaPath}.");
        var schema = File.ReadAllText(schemaPath);
        var schemaMatch = Regex.Match(schema, "\\\"action\\\"\\s+VARCHAR\\((?<limit>\\d+)\\)", RegexOptions.IgnoreCase);
        Assert.True(schemaMatch.Success, "Squema.sql debe declarar system.AuditLog.action como VARCHAR(n).");
        var schemaLimit = int.Parse(schemaMatch.Groups["limit"].Value);
        Assert.Equal(schemaLimit, mappedLimit.Value);
    }

    /// <summary>Comprueba todos los códigos declarados en clases de acciones.</summary>
    [Fact]
    public void AuditActionConstants_FitConventionAndLimit()
    {
        var limit = ReadEfActionLimit();
        var actionTypes = GetAuditActionTypes();
        Assert.NotEmpty(actionTypes);

        foreach (var type in actionTypes)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Where(field => !field.Name.EndsWith("Event", StringComparison.Ordinal)
                    && !field.Name.EndsWith("TableName", StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(fields);
            foreach (var field in fields)
            {
                var value = field.GetRawConstantValue() as string;
                Assert.False(string.IsNullOrWhiteSpace(value), $"{type.FullName}.{field.Name} no puede estar vacío.");
                Assert.True(Regex.IsMatch(value ?? string.Empty, "^[A-Z0-9_]+$"),
                    $"{type.FullName}.{field.Name} debe usar mayúsculas, dígitos o guion bajo.");
                Assert.True((value ?? string.Empty).Length <= limit,
                    $"{type.FullName}.{field.Name} supera el límite de {limit} caracteres.");
            }
        }
    }

    /// <summary>Comprueba que el código fuente no reintroduzca acciones largas ni clases sin enlazar.</summary>
    [Fact]
    public void AuditActionSource_ContainsOnlyBoundedActionsAndLinkedClasses()
    {
        var limit = ReadEfActionLimit();
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.GetFiles(
            Path.Combine(root, "Ferreteria.PuntoVenta"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(sourceFiles);

        var source = string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
        var declaredTypes = Regex.Matches(source, "\\bclass\\s+(?<name>\\w*AuditActions)\\b")
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var reflectedNames = GetAuditActionTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(declaredTypes);
        foreach (var declaredType in declaredTypes)
        {
            Assert.Contains(declaredType, reflectedNames);
        }

        var literalPatterns = new[]
        {
            "(?:RecordChangeAsync|RecordSessionEventAsync)\\s*\\(\\s*\\\"(?<value>[^\"]*)\\\"",
            "\\bAction\\s*=\\s*\\\"(?<value>[^\"]*)\\\""
        };
        foreach (var pattern in literalPatterns)
        {
            foreach (Match match in Regex.Matches(source, pattern))
            {
                var value = match.Groups["value"].Value;
                Assert.True(value.Length <= limit,
                    $"El literal de auditoría '{value}' supera el límite de {limit} caracteres.");
            }
        }
    }

    /// <summary>Obtiene el límite desde el modelo EF sin conectarse a PostgreSQL.</summary>
    /// <returns>Longitud máxima de AuditLog.Action.</returns>
    private static int ReadEfActionLimit()
    {
        var options = new DbContextOptionsBuilder<FerreteriaDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conectar;Username=sin_conectar;Password=sin_conectar")
            .Options;
        using var db = new FerreteriaDbContext(options);
        var entityType = db.Model.FindEntityType(typeof(AuditLog));
        Assert.NotNull(entityType);
        var property = entityType.FindProperty(nameof(AuditLog.Action));
        Assert.NotNull(property);
        var limit = property.GetMaxLength();
        Assert.True(limit.HasValue, "AuditLog.Action debe tener límite en EF.");
        return limit.Value;
    }

    /// <summary>Obtiene las clases de auditoría enlazadas al ensamblado de pruebas.</summary>
    /// <returns>Clases cuyo nombre termina en AuditActions.</returns>
    private static IReadOnlyList<Type> GetAuditActionTypes()
    {
        return typeof(AuditActionLengthTests).Assembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("AuditActions", StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>Busca la raíz del repositorio a partir del directorio de salida.</summary>
    /// <returns>Ruta absoluta de la raíz del repositorio.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "Ferreteria.PuntoVenta", "Ferreteria.PuntoVenta.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException("No se encontró la raíz del repositorio.");
    }
}
