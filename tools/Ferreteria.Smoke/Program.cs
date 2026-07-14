using System.Text.Json;
using Ferreteria.PuntoVenta.Data;
using Microsoft.EntityFrameworkCore;

var root = FindRepositoryRoot();
var appsettingsPath = Path.Combine(root, "Ferreteria.PuntoVenta", "Config", "appsettings.json");
var connectionString = ReadConnectionString(appsettingsPath);

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No se encontro ConnectionStrings:FerreteriaDB.");
    return 2;
}

var options = new DbContextOptionsBuilder<FerreteriaDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new FerreteriaDbContext(options);

Console.WriteLine($"Conexion EF: {await db.Database.CanConnectAsync()}");

var measurementTypes = await db.MeasurementTypes.CountAsync();
var families = await db.Families.CountAsync();
var settings = await db.Settings.CountAsync();
var employees = await db.Employees.CountAsync();
var cashiers = await db.Employees.CountAsync(e => e.IsActive && e.CanCashier && e.PinHash != null);

Console.WriteLine($"MeasurementTypes: {measurementTypes}");
Console.WriteLine($"Families: {families}");
Console.WriteLine($"Settings: {settings}");
Console.WriteLine($"Employees: {employees}");
Console.WriteLine($"Cashiers with PIN: {cashiers}");

var cashier = await db.Employees
    .AsNoTracking()
    .Where(e => e.IsActive && e.CanCashier && e.PinHash != null)
    .OrderBy(e => e.Dui)
    .FirstOrDefaultAsync();

if (cashier is null)
{
    Console.Error.WriteLine("No hay cajero activo con PIN.");
    return 1;
}

var validDemoPin = BCrypt.Net.BCrypt.Verify(cashier.Dui == "00000003-0" ? "0000" : "1234", cashier.PinHash!);
Console.WriteLine($"PIN demo valida: {validDemoPin}");

if (!validDemoPin)
{
    return 1;
}

Console.WriteLine("Smoke OK");
return 0;

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

static string? ReadConnectionString(string appsettingsPath)
{
    using var stream = File.OpenRead(appsettingsPath);
    using var json = JsonDocument.Parse(stream);

    return json.RootElement
        .GetProperty("ConnectionStrings")
        .GetProperty("FerreteriaDB")
        .GetString();
}
