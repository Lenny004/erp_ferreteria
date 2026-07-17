using Ferreteria.PuntoVenta.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>Numeracion generada para un DTE (numero de control + codigo de generacion).</summary>
/// <param name="NumeroControl">Formato <c>DTE-TT-XXXXXXXX-000000000000000</c>.</param>
/// <param name="CodigoGeneracion">UUID v4 en mayusculas.</param>
/// <param name="Correlativo">Correlativo entero del tipo de DTE en el ambiente.</param>
public sealed record DteNumbering(string NumeroControl, string CodigoGeneracion, long Correlativo);

/// <summary>Genera el numero de control y el codigo de generacion de un DTE.</summary>
public interface IDteNumberingService
{
    /// <summary>
    /// Calcula el siguiente numero de control y un codigo de generacion unico
    /// para el tipo de DTE indicado, dentro de la transaccion del contexto dado.
    /// </summary>
    Task<DteNumbering> GenerateNextAsync(
        FerreteriaDbContext dbContext,
        string tipoDte,
        string ambiente,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementacion de numeracion DTE segun el estandar del MH El Salvador.
/// El correlativo se deriva del maximo existente en <c>dte.DteIssued</c> por
/// tipo de documento y ambiente, garantizando unicidad dentro de la transaccion.
/// </summary>
public sealed class DteNumberingService : IDteNumberingService
{
    private const int EstablecimientoLength = 4;
    private const int PuntoVentaLength = 4;
    private const int CorrelativoLength = 15;

    private readonly MhOptions _options;

    /// <summary>Crea el servicio de numeracion con las opciones del MH.</summary>
    public DteNumberingService(IOptions<MhOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<DteNumbering> GenerateNextAsync(
        FerreteriaDbContext dbContext,
        string tipoDte,
        string ambiente,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (string.IsNullOrWhiteSpace(tipoDte))
        {
            throw new ArgumentException("El tipo de DTE es obligatorio.", nameof(tipoDte));
        }

        var establecimiento = NormalizeCode(_options.CodEstablecimiento, EstablecimientoLength, "0001");
        var puntoVenta = NormalizeCode(_options.CodPuntoVenta, PuntoVentaLength, "0001");
        var prefix = $"DTE-{tipoDte}-{establecimiento}{puntoVenta}-";

        var lastControlNumber = await dbContext.DteIssued
            .AsNoTracking()
            .Where(dte => dte.DteType == tipoDte && dte.Ambiente == ambiente)
            .OrderByDescending(dte => dte.IssuedAt)
            .Select(dte => dte.ControlNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var correlativo = ExtractCorrelativo(lastControlNumber) + 1;
        var numeroControl = prefix + correlativo.ToString().PadLeft(CorrelativoLength, '0');
        var codigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant();

        return new DteNumbering(numeroControl, codigoGeneracion, correlativo);
    }

    private static long ExtractCorrelativo(string? controlNumber)
    {
        if (string.IsNullOrWhiteSpace(controlNumber))
        {
            return 0;
        }

        var lastSegment = controlNumber.Split('-').LastOrDefault();
        if (long.TryParse(lastSegment, out var value))
        {
            return value;
        }

        return 0;
    }

    private static string NormalizeCode(string? value, int length, string fallback)
    {
        var normalized = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            normalized = fallback;
        }

        if (normalized.Length > length)
        {
            return normalized[..length];
        }

        return normalized.PadLeft(length, '0');
    }
}
