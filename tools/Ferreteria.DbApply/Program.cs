// Aplicador legacy de schema/migraciones SQL contra PostgreSQL del POS.
// Preferir Prisma (ferreteria_backend) como fuente de verdad; este tool solo aplica
// Squema.sql + migraciones si se fuerza --force-legacy-sql o no existe schema.prisma.
using System.Text.Json;
using Npgsql;

var root = FindRepositoryRoot();
var appsettingsPath = Path.Combine(root, "Ferreteria.PuntoVenta", "Config", "appsettings.json");
var schemaPath = Path.Combine(root, "Ferreteria.PuntoVenta", "Squema.sql");
var migrationsPath = Path.Combine(root, "..", "ferreteria_backend", "database", "migrations");
var prismaSchemaPath = Path.Combine(root, "..", "ferreteria_backend", "prisma", "schema.prisma");
var connectionString = GetArg(args, "--connection") ?? ReadConnectionString(appsettingsPath);
var forceLegacySql = HasFlag(args, "--force-legacy-sql");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No se encontro ConnectionStrings:FerreteriaDB ni se envio --connection.");
    return 2;
}

// Seguridad: enmascarar password antes de imprimir la cadena de conexión.
Console.WriteLine($"Conexion: {MaskConnectionString(connectionString)}");

try
{
    if (File.Exists(prismaSchemaPath) && !forceLegacySql)
    {
        Console.Error.WriteLine("Se detecto ferreteria_backend/prisma/schema.prisma.");
        Console.Error.WriteLine("Prisma v3.0 es la fuente de verdad del esquema. Este aplicador ya no debe ejecutar Squema.sql por defecto.");
        Console.Error.WriteLine("Use en ferreteria_backend: npm run db:push o npm run db:migrate:dev.");
        Console.Error.WriteLine("Si necesita aplicar el SQL legacy bajo su responsabilidad, ejecute: dotnet run --project tools/Ferreteria.DbApply -- --force-legacy-sql");
        return 2;
    }

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    var schemaExists = await CoreSchemaExistsAsync(connection);
    var mode = schemaExists ? "migraciones incrementales" : "schema completo + migraciones";

    Console.WriteLine($"Modo: {mode}");

    if (!schemaExists)
    {
        await ExecuteScriptAsync(connection, schemaPath, root);
    }

    if (Directory.Exists(migrationsPath))
    {
        foreach (var migration in Directory.EnumerateFiles(migrationsPath, "*.sql").Order())
        {
            await ExecuteScriptAsync(connection, migration, root);
        }
    }

    await PrintVerificationAsync(connection);

    Console.WriteLine("Base de datos aplicada correctamente.");
    return 0;
}
catch (PostgresException ex)
{
    Console.Error.WriteLine($"PostgreSQL error {ex.SqlState}: {ex.MessageText}");
    if (!string.IsNullOrWhiteSpace(ex.Detail))
    {
        Console.Error.WriteLine($"Detalle: {ex.Detail}");
    }

    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

/// <summary>Obtiene el valor del argumento nombrado (ej. --connection valor).</summary>
static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

/// <summary>Indica si el flag booleano está presente en la línea de comandos.</summary>
static bool HasFlag(string[] args, string name)
{
    return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Sube directorios hasta encontrar la raíz del repo (.git).</summary>
static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return Directory.GetCurrentDirectory();
}

/// <summary>Lee ConnectionStrings:FerreteriaDB desde appsettings.json del POS.</summary>
static string? ReadConnectionString(string appsettingsPath)
{
    using var stream = File.OpenRead(appsettingsPath);
    using var json = JsonDocument.Parse(stream);

    return json.RootElement
        .GetProperty("ConnectionStrings")
        .GetProperty("FerreteriaDB")
        .GetString();
}

/// <summary>
/// Comprueba si ya existe el núcleo sales.Orders (schema aplicado previamente).
/// </summary>
static async Task<bool> CoreSchemaExistsAsync(NpgsqlConnection connection)
{
    const string sql = """
        SELECT EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'sales'
              AND table_name = 'Orders'
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    return (bool)(await command.ExecuteScalarAsync() ?? false);
}

/// <summary>Ejecuta un archivo .sql completo contra la conexión abierta.</summary>
static async Task ExecuteScriptAsync(NpgsqlConnection connection, string scriptPath, string root)
{
    Console.WriteLine($"Script: {Path.GetRelativePath(root, scriptPath)}");

    var script = await File.ReadAllTextAsync(scriptPath);
    await using var command = new NpgsqlCommand(script, connection)
    {
        CommandTimeout = 0
    };

    await command.ExecuteNonQueryAsync();
}

/// <summary>
/// Imprime checks de tablas/columnas clave (CashSessions, Payments, seed PIN, etc.).
/// No revela valores de PIN ni secretos; solo EXISTS booleanos.
/// </summary>
static async Task PrintVerificationAsync(NpgsqlConnection connection)
{
    const string sql = """
        SELECT
            EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'sales' AND table_name = 'CashSessions') AS cash_sessions,
            EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'sales' AND table_name = 'Payments') AS payments,
            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'sales' AND table_name = 'Orders' AND column_name = 'ClientRequestId') AS client_request_id,
            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'dte' AND table_name = 'DteIssued' AND column_name = 'RelatedDteId') AS related_dte_id,
            EXISTS (SELECT 1 FROM system."Settings" WHERE "Key" = 'IvaPercentage') AS iva_setting,
            EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'hr' AND table_name = 'PayrollRuns') AS payroll_runs,
            EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'hr' AND table_name = 'EmployeeBankAccounts') AS employee_bank_accounts,
            EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'hr' AND table_name = 'Employees' AND column_name = 'SalaryType') AS employee_salary_type,
            CASE
                WHEN to_regclass('hr."Banks"') IS NULL THEN FALSE
                ELSE EXISTS (SELECT 1 FROM hr."Banks" WHERE "Code" = 'BAC')
            END AS bank_seed,
            CASE
                WHEN to_regclass('hr."IsrBrackets"') IS NULL THEN FALSE
                ELSE EXISTS (SELECT 1 FROM hr."IsrBrackets" WHERE "Year" = 2026 AND "PeriodType" = 'QUINCENAL')
            END AS isr_seed,
            EXISTS (SELECT 1 FROM hr."Employees" WHERE "Dui" = '00000001-0' AND "PinHash" IS NOT NULL) AS employee_pin_seed;
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    if (await reader.ReadAsync())
    {
        Console.WriteLine($"Verificacion CashSessions: {reader.GetBoolean(0)}");
        Console.WriteLine($"Verificacion Payments: {reader.GetBoolean(1)}");
        Console.WriteLine($"Verificacion ClientRequestId: {reader.GetBoolean(2)}");
        Console.WriteLine($"Verificacion RelatedDteId: {reader.GetBoolean(3)}");
        Console.WriteLine($"Verificacion IvaPercentage: {reader.GetBoolean(4)}");
        Console.WriteLine($"Verificacion PayrollRuns: {reader.GetBoolean(5)}");
        Console.WriteLine($"Verificacion EmployeeBankAccounts: {reader.GetBoolean(6)}");
        Console.WriteLine($"Verificacion Employee.SalaryType: {reader.GetBoolean(7)}");
        Console.WriteLine($"Verificacion seed banco BAC: {reader.GetBoolean(8)}");
        Console.WriteLine($"Verificacion seed ISR quincenal: {reader.GetBoolean(9)}");
        Console.WriteLine($"Verificacion seed empleado PIN: {reader.GetBoolean(10)}");
    }
}

/// <summary>Enmascara la contraseña de la connection string para logs seguros.</summary>
static string MaskConnectionString(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    if (!string.IsNullOrEmpty(builder.Password))
    {
        builder.Password = "***";
    }

    return builder.ConnectionString;
}
