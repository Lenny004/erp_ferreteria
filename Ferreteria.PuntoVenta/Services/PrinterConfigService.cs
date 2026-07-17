using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Services;

/// <summary>Datos para crear o actualizar una impresora del POS.</summary>
/// <param name="Name">Nombre de la impresora (spooler Windows o etiqueta).</param>
/// <param name="ConnectionType">USB, RED o BLUETOOTH.</param>
/// <param name="IpAddress">IP para impresoras de red.</param>
/// <param name="NetworkPort">Puerto TCP (por defecto 9100 en red).</param>
/// <param name="PaperWidth">Ancho de papel en mm (58 u 80).</param>
/// <param name="IsDefault">Marca la impresora como predeterminada.</param>
public sealed record PrinterInput(
    string Name,
    string ConnectionType,
    string? IpAddress,
    int? NetworkPort,
    short PaperWidth,
    bool IsDefault);

/// <summary>Gestion de impresoras configuradas en <c>system.Printers</c>.</summary>
public interface IPrinterConfigService
{
    /// <summary>Lista las impresoras activas configuradas.</summary>
    Task<IReadOnlyList<Printer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene la impresora predeterminada, si existe.</summary>
    Task<Printer?> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>Crea o actualiza una impresora por nombre y la persiste.</summary>
    Task<Printer> SaveAsync(PrinterInput input, CancellationToken cancellationToken = default);

    /// <summary>Marca una impresora como predeterminada (desmarca las demas).</summary>
    Task SetDefaultAsync(Guid printerId, CancellationToken cancellationToken = default);
}

/// <summary>Implementacion EF Core del catalogo de impresoras del POS.</summary>
public sealed class PrinterConfigService : IPrinterConfigService
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Crea el servicio de impresoras.</summary>
    public PrinterConfigService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Printer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        return await dbContext.Printers
            .AsNoTracking()
            .Where(printer => printer.IsActive)
            .OrderByDescending(printer => printer.IsDefault)
            .ThenBy(printer => printer.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Printer?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        return await dbContext.Printers
            .AsNoTracking()
            .Where(printer => printer.IsActive && printer.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Printer> SaveAsync(PrinterInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ArgumentException("El nombre de la impresora es obligatorio.", nameof(input));
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var printer = await dbContext.Printers
            .FirstOrDefaultAsync(p => p.Name == input.Name, cancellationToken);

        if (printer is null)
        {
            printer = new Printer
            {
                Id = Guid.NewGuid(),
                Name = input.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Printers.Add(printer);
        }

        printer.ConnectionType = input.ConnectionType.Trim().ToUpperInvariant();
        printer.IpAddress = string.IsNullOrWhiteSpace(input.IpAddress) ? null : input.IpAddress.Trim();
        printer.NetworkPort = input.NetworkPort;
        printer.PaperWidth = input.PaperWidth;
        printer.IsActive = true;
        printer.UpdatedAt = DateTime.UtcNow;

        if (input.IsDefault)
        {
            await ClearDefaultsAsync(dbContext, printer.Id, cancellationToken);
            printer.IsDefault = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return printer;
    }

    /// <inheritdoc />
    public async Task SetDefaultAsync(Guid printerId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var printer = await dbContext.Printers
            .FirstOrDefaultAsync(p => p.Id == printerId, cancellationToken);

        if (printer is null)
        {
            throw new InvalidOperationException("Impresora no encontrada.");
        }

        await ClearDefaultsAsync(dbContext, printerId, cancellationToken);
        printer.IsDefault = true;
        printer.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ClearDefaultsAsync(
        FerreteriaDbContext dbContext,
        Guid exceptPrinterId,
        CancellationToken cancellationToken)
    {
        var currentDefaults = await dbContext.Printers
            .Where(printer => printer.IsDefault && printer.Id != exceptPrinterId)
            .ToListAsync(cancellationToken);

        foreach (var printer in currentDefaults)
        {
            printer.IsDefault = false;
            printer.UpdatedAt = DateTime.UtcNow;
        }
    }
}
