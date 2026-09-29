using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Services.SalesHistory;

/// <summary>Consulta el historial directamente en PostgreSQL con alcance resuelto en servidor.</summary>
public sealed class SalesHistoryService : ISalesHistoryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly SalesHistoryOptions _options;

    /// <summary>Inicializa el servicio de historial.</summary>
    /// <param name="scopeFactory">Fábrica de ámbitos para resolver el contexto EF.</param>
    /// <param name="clock">Reloj usado para resolver el día local del alcance.</param>
    /// <param name="options">Opciones de puestos con acceso completo.</param>
    public SalesHistoryService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IOptions<SalesHistoryOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<SalesHistoryPage> SearchAsync(
        SalesHistoryFilter filter,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalized = filter.Normalize();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var accessScope = await ResolveScopeAsync(db, employeeId, cancellationToken);
        if (accessScope is null)
        {
            return new SalesHistoryPage(
                Array.Empty<SalesHistoryRow>(),
                new SalesHistorySummary(0, 0, 0, 0, 0),
                normalized.Page,
                normalized.PageSize,
                false,
                false);
        }

        var query = ApplyScope(db.Orders.AsNoTracking(), accessScope, normalized);
        query = ApplySearch(query, normalized);

        var summary = await query.Select(order => new
        {
            IsCompleted = order.Status == SalesDomainConstants.OrderStatuses.Completed,
            order.Total,
            order.TaxAmount,
            Contingency = order.DteIssued.Any(dte => dte.MhStatus == DteConstants.EstadosMh.Contingencia) ? 1 : 0,
            Reprints = db.AuditLogs.Count(audit =>
                audit.Action == SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint
                && audit.TableName == SalesDomainConstants.SalesHistoryAuditActions.OrdersTableName
                && audit.RecordId == order.Id.ToString())
        }).GroupBy(_ => 1).Select(group => new SalesHistorySummary(
            group.Count(),
            group.Where(item => item.IsCompleted).Sum(item => item.Total),
            group.Where(item => item.IsCompleted).Sum(item => item.TaxAmount),
            group.Sum(item => item.Contingency),
            group.Sum(item => item.Reprints))).FirstOrDefaultAsync(cancellationToken)
            ?? new SalesHistorySummary(0, 0, 0, 0, 0);

        var rows = await query.OrderByDescending(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize + 1)
            .Select(order => new SalesHistoryRow(
                order.Id,
                order.CreatedAt,
                order.Customer == null
                    ? SalesDomainConstants.Customers.DefaultWalkInDisplayName
                    : order.Customer.Name,
                order.Employee.FirstName + " " + order.Employee.LastName,
                order.OrderType,
                order.Payments.OrderBy(payment => payment.CreatedAt)
                    .Select(payment => payment.Method)
                    .FirstOrDefault() ?? SalesDomainConstants.OrderChannelLabels.PaymentMethodNotAvailable,
                order.Status,
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt)
                    .Select(dte => dte.DteType)
                    .FirstOrDefault(),
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt)
                    .Select(dte => dte.MhStatus)
                    .FirstOrDefault(),
                order.DteIssued.OrderByDescending(dte => dte.IssuedAt)
                    .Select(dte => dte.ControlNumber)
                    .FirstOrDefault(),
                order.Total,
                db.AuditLogs.Count(audit =>
                    audit.Action == SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint
                    && audit.TableName == SalesDomainConstants.SalesHistoryAuditActions.OrdersTableName
                    && audit.RecordId == order.Id.ToString())))
            .ToListAsync(cancellationToken);

        bool hasNext = rows.Count > normalized.PageSize;
        if (hasNext)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new SalesHistoryPage(
            rows,
            summary,
            normalized.Page,
            normalized.PageSize,
            normalized.Page > 1,
            hasNext);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ExecuteUpdateAsync</c> genera una sola sentencia <c>UPDATE ... SET reprints = reprints + 1</c>.
    /// La operación es atómica en la base de datos y evita perder incrementos concurrentes.
    /// </remarks>
    public async Task<bool> IncrementReprintsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var changed = await db.DteIssued
            .Where(dte => dte.OrderId == orderId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(dte => dte.Reprints, dte => dte.Reprints + 1),
                cancellationToken);
        return changed > 0;
    }

    /// <inheritdoc />
    public async Task<SalesHistoryDetail?> GetDetailAsync(
        Guid orderId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var accessScope = await ResolveScopeAsync(db, employeeId, cancellationToken);
        if (accessScope is null)
        {
            return null;
        }

        var detail = await ApplyScope(db.Orders.AsNoTracking(), accessScope, new SalesHistoryFilter())
            .Where(order => order.Id == orderId)
            .Select(order => new SalesHistoryDetail(
                order.Id,
                order.CreatedAt,
                order.Status,
                order.OrderType,
                order.Customer == null
                    ? SalesDomainConstants.Customers.DefaultWalkInDisplayName
                    : order.Customer.Name,
                order.Employee.FirstName + " " + order.Employee.LastName,
                order.Subtotal,
                order.TaxAmount,
                order.DiscountAmount,
                order.Total,
                order.Notes ?? string.Empty,
                order.OrderDetails.Select(line => new SalesHistoryLine(
                    line.Product.Description,
                    line.Quantity,
                    line.SaleUnit == null ? SalesDomainConstants.SalesUnitCodes.Unit : line.SaleUnit.Name,
                    line.UnitsPerPackage,
                    line.UnitPrice,
                    line.DiscountAmount,
                    line.Subtotal)).ToList(),
                order.Payments.Select(payment => new SalesHistoryPayment(
                    payment.Method,
                    payment.Amount,
                    payment.Reference)).ToList(),
                order.DteIssued.Select(dte => new SalesHistoryDte(
                    dte.DteType,
                    dte.ControlNumber,
                    dte.GenerationCode,
                    dte.MhStatus,
                    dte.MhSello,
                    dte.RelatedDteId,
                    dte.Reprints,
                    dte.CreditNotes.Select(note => new SalesHistoryCreditNote(
                        note.DteType,
                        note.ControlNumber,
                        note.MhStatus)).ToList())).ToList(),
                order.InventoryMovements.Select(movement => new SalesHistoryMovement(
                    movement.Product.Description,
                    movement.MovementType,
                    movement.Quantity,
                    movement.Reason)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
        return detail is null ? null : detail with { Notes = OrderNotesFormatter.FormatForDisplay(detail.Notes) };
    }

    private async Task<SalesHistoryScope?> ResolveScopeAsync(
        FerreteriaDbContext db,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .Include(item => item.Position)
            .SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        return SalesHistoryAccessRules.ResolveScope(
            employee.Id,
            employee.CanCashier,
            employee.Position?.Name,
            _options.FullHistoryPositionNames,
            _clock);
    }

    private static IQueryable<Order> ApplyScope(
        IQueryable<Order> query,
        SalesHistoryScope accessScope,
        SalesHistoryFilter filter)
    {
        var fromUtc = filter.FromUtc;
        var toUtc = filter.ToUtc;
        if (accessScope.MinCreatedAtUtc.HasValue
            && (!fromUtc.HasValue || fromUtc.Value < accessScope.MinCreatedAtUtc.Value))
        {
            fromUtc = accessScope.MinCreatedAtUtc;
        }
        if (accessScope.MaxCreatedAtUtc.HasValue
            && (!toUtc.HasValue || toUtc.Value > accessScope.MaxCreatedAtUtc.Value))
        {
            toUtc = accessScope.MaxCreatedAtUtc;
        }

        return query.Where(order =>
            (!accessScope.RestrictToEmployeeId.HasValue || order.EmployeeId == accessScope.RestrictToEmployeeId.Value)
            && (!fromUtc.HasValue || order.CreatedAt >= fromUtc.Value)
            && (!toUtc.HasValue || order.CreatedAt < toUtc.Value));
    }

    private static IQueryable<Order> ApplySearch(IQueryable<Order> query, SalesHistoryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var escaped = SalesHistoryFilter.EscapeILikePattern(filter.SearchText);
            var pattern = $"%{escaped}%";
            var prefixPattern = $"{escaped}%";
            if (Guid.TryParse(filter.SearchText, out var orderId))
            {
                query = query.Where(order => order.Id == orderId);
            }
            else
            {
                query = query.Where(order =>
                    EF.Functions.ILike(order.Id.ToString(), prefixPattern, "\\")
                    || (order.Customer != null && (
                        EF.Functions.ILike(order.Customer.Name, pattern, "\\")
                        || (order.Customer.Nit != null && EF.Functions.ILike(order.Customer.Nit, pattern, "\\"))
                        || (order.Customer.Dui != null && EF.Functions.ILike(order.Customer.Dui, pattern, "\\"))))
                    || (order.Notes != null && EF.Functions.ILike(order.Notes, pattern, "\\"))
                    || order.DteIssued.Any(dte => EF.Functions.ILike(dte.ControlNumber, pattern, "\\")));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.OrderStatus)
            && filter.OrderStatus != SalesDomainConstants.OrderStatuses.All)
        {
            query = query.Where(order => order.Status == filter.OrderStatus);
        }
        if (!string.IsNullOrWhiteSpace(filter.DteType))
        {
            query = filter.DteType == SalesHistoryFilter.NoDteFilterValue
                ? query.Where(order => !order.DteIssued.Any())
                : query.Where(order => order.DteIssued.Any(dte => dte.DteType == filter.DteType));
        }
        if (!string.IsNullOrWhiteSpace(filter.MhStatus))
        {
            query = query.Where(order => order.DteIssued.Any(dte => dte.MhStatus == filter.MhStatus));
        }
        if (filter.EmployeeId.HasValue)
        {
            query = query.Where(order => order.EmployeeId == filter.EmployeeId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
        {
            query = query.Where(order => order.Payments.Any(payment => payment.Method == filter.PaymentMethod));
        }

        return query;
    }
}
