using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ferreteria.PuntoVenta.Services.Security;

namespace Ferreteria.PuntoVenta.Services;


/// <summary>Implementacion EF Core del catalogo de impresoras del POS.</summary>
public sealed class PrinterConfigService : IPrinterConfigService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditService _auditService;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuthorizationGuard _authorizationGuard;

    /// <summary>Crea el servicio de impresoras.</summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de datos.</param>
    /// <param name="auditService">Servicio de auditoría.</param>
    /// <param name="currentSession">Sesión vigente del POS.</param>
    /// <param name="authorizationGuard">Guard obligatorio de autorización.</param>
    public PrinterConfigService(
        IServiceScopeFactory scopeFactory,
        IAuditService auditService,
        ICurrentSessionService currentSession,
        IAuthorizationGuard authorizationGuard)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        _authorizationGuard = authorizationGuard ?? throw new ArgumentNullException(nameof(authorizationGuard));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Printer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await RequireConfigurationAdministrationAsync(cancellationToken);
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
        await RequireConfigurationAdministrationAsync(cancellationToken);
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
        var actorId = await RequireConfigurationAdministrationAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(input);
        PrinterConfigurationRules.Validate(input);
        var connectionType = PrinterConfigurationRules.NormalizeConnectionType(input.ConnectionType);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var printer = await dbContext.Printers
            .FirstOrDefaultAsync(p => p.Name == input.Name, cancellationToken);

        var oldData = printer is null ? null : new
        {
            printer.Name,
            printer.ConnectionType,
            printer.IpAddress,
            printer.NetworkPort,
            printer.PaperWidth,
            printer.IsDefault
        };

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

        printer.ConnectionType = connectionType;
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
        await _auditService.RecordChangeAsync(
            SalesDomainConstants.PrintingAuditActions.PrinterConfiguration,
            SalesDomainConstants.PrintingAuditActions.PrintersTableName,
            printer.Id.ToString(),
            oldData,
            new
            {
                Evento = SalesDomainConstants.PrintingAuditActions.PrinterConfigurationEvent,
                printer.Name,
                printer.ConnectionType,
                printer.IpAddress,
                printer.NetworkPort,
                printer.PaperWidth,
                printer.IsDefault
            },
            actorId,
            cancellationToken);
        return printer;
    }

    /// <inheritdoc />
    public async Task SetDefaultAsync(Guid printerId, CancellationToken cancellationToken = default)
    {
        var actorId = await RequireConfigurationAdministrationAsync(cancellationToken);
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var printer = await dbContext.Printers
            .FirstOrDefaultAsync(p => p.Id == printerId, cancellationToken);

        if (printer is null)
        {
            throw new InvalidOperationException("Impresora no encontrada.");
        }

        var previousDefault = await dbContext.Printers
            .AsNoTracking()
            .Where(p => p.IsActive && p.IsDefault && p.Id != printerId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        await ClearDefaultsAsync(dbContext, printerId, cancellationToken);
        printer.IsDefault = true;
        printer.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await _auditService.RecordChangeAsync(
            SalesDomainConstants.PrintingAuditActions.DefaultPrinter,
            SalesDomainConstants.PrintingAuditActions.PrintersTableName,
            printer.Id.ToString(),
            previousDefault,
            new
            {
                Evento = SalesDomainConstants.PrintingAuditActions.DefaultPrinterEvent,
                printer.IsDefault
            },
            actorId,
            cancellationToken);
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

    private async Task<Guid?> RequireConfigurationAdministrationAsync(CancellationToken cancellationToken)
    {
        var actor = await _authorizationGuard.RequireAsync(
            PosPermission.AdministrarConfiguracion,
            cancellationToken: cancellationToken);
        return actor.Id;
    }
}
