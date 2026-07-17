using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>
/// Servicio en segundo plano que reintenta transmitir al MH los DTE que quedaron
/// en contingencia. Se ejecuta cada <see cref="MhOptions.ContingencyRetryMinutes"/>.
/// </summary>
public sealed class DteContingencyProcessor : BackgroundService
{
    private readonly IDteService _dteService;
    private readonly MhOptions _options;
    private readonly ILogger<DteContingencyProcessor> _logger;

    /// <summary>Crea el procesador de contingencia.</summary>
    public DteContingencyProcessor(
        IDteService dteService,
        IOptions<MhOptions> options,
        ILogger<DteContingencyProcessor> logger)
    {
        _dteService = dteService ?? throw new ArgumentNullException(nameof(dteService));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.ContingencyRetryMinutes));

        // Espera inicial para no competir con el arranque de la aplicacion.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await _dteService.ProcessPendingContingenciesAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("Contingencia DTE: {Count} documentos reintentados.", processed);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo de contingencia DTE.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
