using System.Data;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Models.Dte.Json;
using Ferreteria.PuntoVenta.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>
/// Orquesta la emision de Documentos Tributarios Electronicos: construccion del
/// JSON, firma en el Firmador local, transmision al MH y persistencia del estado,
/// incluyendo la cola de contingencia y sus reintentos.
/// </summary>
public interface IDteService
{
    /// <summary>Emite un DTE (01/03) para una orden completada. No lanza en contingencia.</summary>
    Task<EmitDteResult> EmitForOrderAsync(EmitDteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emite una Nota de Credito (05) que anula un Comprobante de Credito Fiscal previo,
    /// restaura el inventario y marca la orden como cancelada.
    /// </summary>
    Task<EmitDteResult> EmitCreditNoteAsync(
        string originalControlNumber,
        string reason,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene el ultimo DTE emitido para una orden, si existe.</summary>
    Task<DteIssued?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene la configuracion fiscal activa del emisor.</summary>
    Task<DteConfig?> GetActiveEmisorAsync(CancellationToken cancellationToken = default);

    /// <summary>Reintenta las contingencias pendientes cuya hora de reintento ya vencio.</summary>
    Task<int> ProcessPendingContingenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>Construye el enlace de consulta publica del DTE (contenido del QR).</summary>
    string BuildConsultaUrl(string ambiente, string codigoGeneracion, DateTime issuedAtLocal);
}

/// <summary>Implementacion del servicio de facturacion electronica DTE.</summary>
public sealed class DteService : IDteService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDteNumberingService _numberingService;
    private readonly IDteJsonBuilder _jsonBuilder;
    private readonly IDteSigningService _signingService;
    private readonly IMhApiClient _mhApiClient;
    private readonly MhOptions _options;
    private readonly ILogger<DteService> _logger;

    /// <summary>Crea el servicio DTE con sus dependencias.</summary>
    public DteService(
        IServiceScopeFactory scopeFactory,
        IDteNumberingService numberingService,
        IDteJsonBuilder jsonBuilder,
        IDteSigningService signingService,
        IMhApiClient mhApiClient,
        IOptions<MhOptions> options,
        ILogger<DteService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _numberingService = numberingService ?? throw new ArgumentNullException(nameof(numberingService));
        _jsonBuilder = jsonBuilder ?? throw new ArgumentNullException(nameof(jsonBuilder));
        _signingService = signingService ?? throw new ArgumentNullException(nameof(signingService));
        _mhApiClient = mhApiClient ?? throw new ArgumentNullException(nameof(mhApiClient));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EmitDteResult> EmitForOrderAsync(
        EmitDteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrderId == Guid.Empty)
        {
            throw new DteException("La emision de DTE requiere una orden valida.");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var emisor = await LoadActiveEmisorAsync(dbContext, cancellationToken);

        var order = await dbContext.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(detail => detail.Product)
                    .ThenInclude(product => product!.MeasurementType)
            .Include(o => o.Payments)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            throw new DteException("Orden no encontrada para emitir DTE.");
        }

        if (order.Status != SalesDomainConstants.OrderStatuses.Completed)
        {
            throw new DteException("Solo se puede facturar una orden completada.");
        }

        var existing = await dbContext.DteIssued
            .Where(d => d.OrderId == order.Id && d.MhStatus != DteConstants.EstadosMh.Rechazado)
            .OrderByDescending(d => d.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            if (existing.MhStatus != DteConstants.EstadosMh.Procesado)
            {
                await ProcessTransmissionAsync(existing.Id, cancellationToken);
                existing = await ReloadDteAsync(dbContext, existing.Id, cancellationToken);
            }

            return MapToResult(existing!);
        }

        var customer = await ResolveCustomerAsync(dbContext, request, order, cancellationToken);
        var issuedAtLocal = DateTime.Now;
        var ambiente = _options.Ambiente;

        Guid newDteId;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken))
        {
            var numbering = await _numberingService.GenerateNextAsync(
                dbContext, request.TipoDte, ambiente, cancellationToken);

            var context = new DteBuildContext(
                order, emisor, customer, request.TipoDte, numbering, ambiente, issuedAtLocal);

            var document = _jsonBuilder.BuildInvoice(context);
            var payloadJson = JsonConvert.SerializeObject(document);
            var ivaValue = ResolveIvaValue(document.Resumen);

            var dteIssued = new DteIssued
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                DteType = request.TipoDte,
                ControlNumber = numbering.NumeroControl,
                GenerationCode = Guid.Parse(numbering.CodigoGeneracion),
                MhStatus = DteConstants.EstadosMh.Pendiente,
                Ambiente = ambiente,
                JsonPayload = payloadJson,
                TotalExenta = document.Resumen.TotalExenta,
                TotalGravada = document.Resumen.TotalGravada,
                TotalIva = ivaValue,
                TotalPagar = document.Resumen.TotalPagar,
                IssuedAt = issuedAtLocal.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow
            };

            dbContext.DteIssued.Add(dteIssued);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            newDteId = dteIssued.Id;
        }

        await ProcessTransmissionAsync(newDteId, cancellationToken);

        var finalDte = await ReloadDteAsync(dbContext, newDteId, cancellationToken);
        return MapToResult(finalDte!);
    }

    /// <inheritdoc />
    public async Task<EmitDteResult> EmitCreditNoteAsync(
        string originalControlNumber,
        string reason,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalControlNumber))
        {
            throw new DteException("Indique el numero de control del DTE original.");
        }

        if (employeeId == Guid.Empty)
        {
            throw new DteException("La nota de credito requiere un cajero autenticado.");
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var emisor = await LoadActiveEmisorAsync(dbContext, cancellationToken);
        var controlNumber = originalControlNumber.Trim();

        var original = await dbContext.DteIssued
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ControlNumber == controlNumber, cancellationToken);

        if (original is null)
        {
            throw new DteException($"No se encontro el DTE con numero de control {controlNumber}.");
        }

        if (original.DteType != DteConstants.TiposDte.CreditoFiscal)
        {
            throw new DteException(
                "Las notas de credito solo aplican a Comprobantes de Credito Fiscal (03).");
        }

        if (original.MhStatus == DteConstants.EstadosMh.Rechazado)
        {
            throw new DteException("No se puede anular un DTE rechazado por el MH.");
        }

        var existingNote = await dbContext.DteIssued
            .Where(d => d.RelatedDteId == original.Id
                && d.DteType == DteConstants.TiposDte.NotaCredito
                && d.MhStatus != DteConstants.EstadosMh.Rechazado)
            .OrderByDescending(d => d.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingNote is not null)
        {
            return MapToResult(existingNote);
        }

        if (original.OrderId is null)
        {
            throw new DteException("El DTE original no esta asociado a una orden.");
        }

        var order = await dbContext.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(detail => detail.Product)
                    .ThenInclude(product => product!.MeasurementType)
            .Include(o => o.Payments)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == original.OrderId, cancellationToken);

        if (order is null)
        {
            throw new DteException("Orden original no encontrada.");
        }

        if (order.Customer is null)
        {
            throw new DteException("La nota de credito requiere el cliente del credito fiscal.");
        }

        var issuedAtLocal = DateTime.Now;
        var ambiente = _options.Ambiente;
        Guid noteId;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken))
        {
            var numbering = await _numberingService.GenerateNextAsync(
                dbContext, DteConstants.TiposDte.NotaCredito, ambiente, cancellationToken);

            var context = new DteBuildContext(
                order, emisor, order.Customer, DteConstants.TiposDte.NotaCredito,
                numbering, ambiente, issuedAtLocal);

            var document = _jsonBuilder.BuildCreditNote(context, original, order.OrderDetails.ToList());
            var payloadJson = JsonConvert.SerializeObject(document);
            var ivaValue = ResolveIvaValue(document.Resumen);

            var note = new DteIssued
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                RelatedDteId = original.Id,
                DteType = DteConstants.TiposDte.NotaCredito,
                ControlNumber = numbering.NumeroControl,
                GenerationCode = Guid.Parse(numbering.CodigoGeneracion),
                MhStatus = DteConstants.EstadosMh.Pendiente,
                Ambiente = ambiente,
                JsonPayload = payloadJson,
                TotalExenta = document.Resumen.TotalExenta,
                TotalGravada = document.Resumen.TotalGravada,
                TotalIva = ivaValue,
                TotalPagar = document.Resumen.TotalPagar,
                IssuedAt = issuedAtLocal.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow
            };

            dbContext.DteIssued.Add(note);

            RestoreInventory(dbContext, order, employeeId, reason);
            order.Status = SalesDomainConstants.OrderStatuses.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            noteId = note.Id;
        }

        await ProcessTransmissionAsync(noteId, cancellationToken);

        var finalNote = await ReloadDteAsync(dbContext, noteId, cancellationToken);
        return MapToResult(finalNote!);
    }

    /// <inheritdoc />
    public async Task<DteIssued?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        return await dbContext.DteIssued
            .AsNoTracking()
            .Where(d => d.OrderId == orderId)
            .OrderByDescending(d => d.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DteConfig?> GetActiveEmisorAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        return await dbContext.DteConfigs
            .AsNoTracking()
            .Where(config => config.IsActive)
            .OrderByDescending(config => config.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ProcessPendingContingenciesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var now = DateTime.UtcNow;
        var pending = await dbContext.DteContingencies
            .AsNoTracking()
            .Where(c => c.ResolvedAt == null && (c.NextRetryAt == null || c.NextRetryAt <= now))
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.DteId)
            .Take(50)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var dteId in pending)
        {
            try
            {
                await ProcessTransmissionAsync(dteId, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al reintentar contingencia del DTE {DteId}", dteId);
            }
        }

        return processed;
    }

    /// <inheritdoc />
    public string BuildConsultaUrl(string ambiente, string codigoGeneracion, DateTime issuedAtLocal)
    {
        var fecha = issuedAtLocal.ToString("yyyy-MM-dd");
        return $"{_options.ConsultaPublicaUrl}?ambiente={ambiente}&codGen={codigoGeneracion}&fechaEmi={fecha}";
    }

    private async Task ProcessTransmissionAsync(Guid dteId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

        var dte = await dbContext.DteIssued.FirstOrDefaultAsync(d => d.Id == dteId, cancellationToken);
        if (dte is null || dte.MhStatus == DteConstants.EstadosMh.Procesado)
        {
            return;
        }

        var emisor = await LoadActiveEmisorAsync(dbContext, cancellationToken);

        DteDocument? document;
        try
        {
            document = JsonConvert.DeserializeObject<DteDocument>(dte.JsonPayload ?? string.Empty);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON de DTE {DteId} ilegible", dteId);
            document = null;
        }

        if (document is null)
        {
            throw new DteException($"No se pudo reconstruir el DTE {dteId} para su transmision.");
        }

        try
        {
            var signed = await _signingService.SignAsync(
                document, emisor.EmisorNit, _options.CertPassword, cancellationToken);

            var request = new MhReceptionRequest
            {
                Ambiente = dte.Ambiente,
                IdEnvio = Random.Shared.Next(1, int.MaxValue),
                Version = document.Identificacion.Version,
                TipoDte = dte.DteType,
                Documento = signed,
                CodigoGeneracion = dte.GenerationCode.ToString().ToUpperInvariant()
            };

            var response = await _mhApiClient.SendDteAsync(request, cancellationToken);
            dte.MhResponse = JsonConvert.SerializeObject(response);

            if (response.IsAccepted)
            {
                dte.MhStatus = DteConstants.EstadosMh.Procesado;
                dte.MhSello = response.SelloRecibido;
                dte.ProcessedAt = DateTime.UtcNow;
                await ResolveContingencyAsync(dbContext, dte.Id, cancellationToken);
                _logger.LogInformation("DTE {Control} sellado por el MH.", dte.ControlNumber);
            }
            else
            {
                dte.MhStatus = DteConstants.EstadosMh.Rechazado;
                _logger.LogWarning(
                    "DTE {Control} rechazado por el MH: {Msg}",
                    dte.ControlNumber,
                    response.DescripcionMsg);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DteException ex) when (ex is DteSigningException or DteTransmissionException)
        {
            dte.MhStatus = DteConstants.EstadosMh.Contingencia;
            await UpsertContingencyAsync(dbContext, dte.Id, ex.Message, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "DTE {Control} en contingencia: {Reason}", dte.ControlNumber, ex.Message);
        }
    }

    private async Task UpsertContingencyAsync(
        FerreteriaDbContext dbContext,
        Guid dteId,
        string lastError,
        CancellationToken cancellationToken)
    {
        var contingency = await dbContext.DteContingencies
            .FirstOrDefaultAsync(c => c.DteId == dteId, cancellationToken);

        var nextRetry = DateTime.UtcNow.AddMinutes(_options.ContingencyRetryMinutes);

        if (contingency is null)
        {
            dbContext.DteContingencies.Add(new DteContingency
            {
                Id = Guid.NewGuid(),
                DteId = dteId,
                AttemptCount = 1,
                LastError = Truncate(lastError, 1000),
                NextRetryAt = nextRetry,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            contingency.AttemptCount += 1;
            contingency.LastError = Truncate(lastError, 1000);
            contingency.NextRetryAt = nextRetry;
            contingency.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task ResolveContingencyAsync(
        FerreteriaDbContext dbContext,
        Guid dteId,
        CancellationToken cancellationToken)
    {
        var contingency = await dbContext.DteContingencies
            .FirstOrDefaultAsync(c => c.DteId == dteId && c.ResolvedAt == null, cancellationToken);

        if (contingency is not null)
        {
            contingency.ResolvedAt = DateTime.UtcNow;
            contingency.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<DteConfig> LoadActiveEmisorAsync(
        FerreteriaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var emisor = await dbContext.DteConfigs
            .Where(config => config.IsActive)
            .OrderByDescending(config => config.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (emisor is null)
        {
            throw new DteConfigurationException(
                "No hay configuracion de emisor DTE activa. Configure la tabla dte.DteConfig con los datos del Ministerio de Hacienda.");
        }

        return emisor;
    }

    private static async Task<Customer?> ResolveCustomerAsync(
        FerreteriaDbContext dbContext,
        EmitDteRequest request,
        Order order,
        CancellationToken cancellationToken)
    {
        var customerId = request.CustomerId ?? order.CustomerId;
        if (customerId is null)
        {
            if (request.TipoDte == DteConstants.TiposDte.CreditoFiscal)
            {
                throw new DteException("El Comprobante de Credito Fiscal requiere un cliente con NIT/NRC.");
            }

            return order.Customer;
        }

        return await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
    }

    private static async Task<DteIssued?> ReloadDteAsync(
        FerreteriaDbContext dbContext,
        Guid dteId,
        CancellationToken cancellationToken)
    {
        return await dbContext.DteIssued
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dteId, cancellationToken);
    }

    private EmitDteResult MapToResult(DteIssued dte)
    {
        var codigoGeneracion = dte.GenerationCode.ToString().ToUpperInvariant();
        var issuedLocal = dte.IssuedAt.ToLocalTime();
        var consultaUrl = BuildConsultaUrl(dte.Ambiente, codigoGeneracion, issuedLocal);
        var message = BuildStatusMessage(dte);

        return new EmitDteResult(
            dte.Id,
            dte.OrderId ?? Guid.Empty,
            dte.DteType,
            dte.ControlNumber,
            codigoGeneracion,
            dte.MhStatus,
            dte.MhSello,
            dte.Ambiente,
            consultaUrl,
            dte.TotalPagar,
            SpanishNumberToWords.Convert(dte.TotalPagar),
            message);
    }

    private static string? BuildStatusMessage(DteIssued dte)
    {
        return dte.MhStatus switch
        {
            DteConstants.EstadosMh.Contingencia =>
                "DTE emitido en contingencia. Se reintentara el envio al MH automaticamente.",
            DteConstants.EstadosMh.Rechazado =>
                "DTE rechazado por el MH. Revise las observaciones antes de reintentar.",
            DteConstants.EstadosMh.Procesado => "DTE sellado por el Ministerio de Hacienda.",
            _ => null
        };
    }

    private static void RestoreInventory(
        FerreteriaDbContext dbContext,
        Order order,
        Guid employeeId,
        string reason)
    {
        var motive = string.IsNullOrWhiteSpace(reason)
            ? "Devolucion por Nota de Credito"
            : $"Devolucion por Nota de Credito: {reason.Trim()}";

        foreach (var detail in order.OrderDetails)
        {
            var product = detail.Product;
            if (product is null || detail.Quantity <= 0)
            {
                continue;
            }

            var stockBefore = product.CurrentStock;
            var stockAfter = stockBefore + detail.Quantity;
            product.CurrentStock = stockAfter;
            product.UpdatedAt = DateTime.UtcNow;

            dbContext.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                MovementType = SalesDomainConstants.InventoryMovementTypes.ReturnInflow,
                Quantity = detail.Quantity,
                UnitCost = product.CostPrice,
                TotalCost = product.CostPrice * detail.Quantity,
                StockBefore = stockBefore,
                StockAfter = stockAfter,
                OrderId = order.Id,
                EmployeeId = employeeId,
                Reason = Truncate(motive, 300),
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static decimal ResolveIvaValue(DteResumen resumen)
    {
        if (resumen.TotalIva is { } totalIva)
        {
            return totalIva;
        }

        return resumen.Tributos?.Sum(tributo => tributo.Valor) ?? 0m;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
