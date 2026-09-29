using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Services;

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
