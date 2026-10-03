using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Printing;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Time;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas unitarias de reglas puras del historial e impresión.</summary>
public sealed class SalesHistoryFilterTests
{
    private static BusinessCalendar CreateCalendar(DateTimeOffset now)
    {
        var timeZone = BusinessTimeZone.Create(new BusinessTimeOptions { ZonaHoraria = "America/El_Salvador" });
        return new BusinessCalendar(timeZone, new FixedTimeProvider(now));
    }

    /// <summary>Verifica los límites locales de hoy cerca de medianoche.</summary>
    [Fact]
    public void TodayRange_UsesElSalvadorMidnight()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 27, 6, 30, 0, TimeSpan.Zero));
        var range = SalesHistoryFilter.CreateShortcutRange(SalesHistoryDateShortcut.Today, CreateCalendar(clock.GetUtcNow()));
        Assert.Equal(new DateTime(2026, 9, 27, 6, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 28, 6, 0, 0, DateTimeKind.Utc), range.ToUtc);
    }

    /// <summary>Verifica los límites exactos de ayer, semana y mes.</summary>
    [Fact]
    public void Shortcuts_CreateExpectedBoundaries()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 18, 0, 0, TimeSpan.Zero));
        var calendar = CreateCalendar(clock.GetUtcNow());
        var yesterday = SalesHistoryFilter.CreateShortcutRange(SalesHistoryDateShortcut.Yesterday, calendar);
        var week = SalesHistoryFilter.CreateShortcutRange(SalesHistoryDateShortcut.Week, calendar);
        var month = SalesHistoryFilter.CreateShortcutRange(SalesHistoryDateShortcut.Month, calendar);
        Assert.Equal(new DateTime(2026, 9, 15, 6, 0, 0, DateTimeKind.Utc), yesterday.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 16, 6, 0, 0, DateTimeKind.Utc), yesterday.ToUtc);
        Assert.Equal(new DateTime(2026, 9, 14, 6, 0, 0, DateTimeKind.Utc), week.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 17, 6, 0, 0, DateTimeKind.Utc), week.ToUtc);
        Assert.Equal(new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc), month.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc), month.ToUtc);
    }

    /// <summary>Verifica el rango local inclusivo.</summary>
    [Fact]
    public void CreateLocalDateRange_IsInclusiveAtBothEnds()
    {
        var range = SalesHistoryFilter.CreateLocalDateRange(
            new DateOnly(2026, 9, 16),
            new DateOnly(2026, 9, 16),
            CreateCalendar(new DateTimeOffset(2026, 9, 16, 18, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTime(2026, 9, 16, 6, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 17, 6, 0, 0, DateTimeKind.Utc), range.ToUtc);
    }

    /// <summary>Verifica validación y límites de paginación.</summary>
    [Fact]
    public void Normalize_ClampsPageAndPageSizeAndRejectsInvertedRange()
    {
        var normalized = new SalesHistoryFilter(SearchText: "  cliente  ", Page: 0, PageSize: 500).Normalize();
        Assert.Equal("cliente", normalized.SearchText);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(50, normalized.PageSize);
        Assert.Throws<ArgumentException>(() => new SalesHistoryFilter(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)).Normalize());
    }

    /// <summary>Verifica el escape de comodines de ILIKE.</summary>
    [Fact]
    public void EscapeILike_EscapesWildcardsAndEscapeCharacter()
    {
        Assert.Equal("a\\%b\\_c\\\\d", SalesHistoryFilter.EscapeILikePattern("a%b_c\\d"));
    }

    /// <summary>Verifica el alcance de cajero, encargado y configuración vacía.</summary>
    [Fact]
    public void ResolveScope_UsesPositionNamesCaseInsensitive()
    {
        var employee = Guid.NewGuid();
        var openSession = Guid.NewGuid();
        var cashier = SalesHistoryAccessRules.ResolveScope(employee, true, "Cajero", Array.Empty<string>(), new[] { openSession });
        var restrictedWithoutCashier = SalesHistoryAccessRules.ResolveScope(employee, false, "Sin puesto de encargado", Array.Empty<string>(), new[] { openSession });
        var manager = SalesHistoryAccessRules.ResolveScope(employee, true, "Administrador", new[] { "administrador" }, Array.Empty<Guid>());
        var empty = SalesHistoryAccessRules.ResolveScope(employee, true, "Cajero", Array.Empty<string>(), Array.Empty<Guid>());
        Assert.Equal(employee, cashier.RestrictToEmployeeId);
        Assert.Equal(new[] { openSession }, cashier.RestrictToCashSessionIds);
        Assert.Equal(employee, restrictedWithoutCashier.RestrictToEmployeeId);
        Assert.Empty(restrictedWithoutCashier.RestrictToCashSessionIds ?? Array.Empty<Guid>());
        Assert.Null(manager.RestrictToEmployeeId);
        Assert.Null(manager.RestrictToCashSessionIds);
        Assert.Empty(empty.RestrictToCashSessionIds ?? Array.Empty<Guid>());
    }

    /// <summary>Exige que la orden pertenezca a una sesión ABIERTA autorizada.</summary>
    [Fact]
    public void CanView_RequiresAuthorizedOpenCashSession()
    {
        var employeeId = Guid.NewGuid();
        var openSessionId = Guid.NewGuid();
        var scope = SalesHistoryAccessRules.ResolveScope(
            employeeId,
            true,
            "Cajero",
            Array.Empty<string>(),
            new[] { openSessionId });

        Assert.True(SalesHistoryAccessRules.CanView(scope, employeeId, openSessionId));
        Assert.False(SalesHistoryAccessRules.CanView(scope, employeeId, Guid.NewGuid()));
        Assert.False(SalesHistoryAccessRules.CanView(scope, employeeId, null));
        Assert.False(SalesHistoryAccessRules.CanView(scope, Guid.NewGuid(), openSessionId));
    }

    /// <summary>Verifica la etiqueta de DTE y canal de una fila.</summary>
    [Fact]
    public void Row_ExposesControlNumberAndChannel()
    {
        var orderId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var row = new SalesHistoryRow(orderId, DateTime.UtcNow, "Cliente", "Cajero", "ORDEN_CONFECCION", "EFECTIVO", "COMPLETADA", null, null, null, 1m, 0);
        Assert.Equal("Sin DTE - Orden 11111111", row.ControlNumberText);
        Assert.Equal("TALLER", row.ChannelLabel);
        Assert.False(row.HasDte);
    }

    /// <summary>Verifica que la leyenda solo aparezca al reimprimir.</summary>
    [Fact]
    public void Renderer_UsesReprintLegendOnlyWhenRequested()
    {
        var sale = new ReceiptSaleData(Guid.NewGuid(), "Cajero", "Cliente", null, new[] { new TicketLineItem("Producto", 1, "1", 1m, 1m) }, 1m, .13m, 1.13m, "EFECTIVO", 2m, DateTime.UtcNow);
        var factory = new ReceiptDocumentFactory();
        var issuer = new ReceiptIssuerData("Emisor", null, "-", "-", "A verificar", null);
        var printed = TicketReceiptRenderer.RenderPlainText(factory.Create(sale, issuer, null, "Interno", true), 58);
        var original = TicketReceiptRenderer.RenderPlainText(factory.Create(sale, issuer, null, "Interno", false), 58);
        Assert.Contains(ReceiptDocumentTypes.ReprintLegend, printed);
        Assert.DoesNotContain(ReceiptDocumentTypes.ReprintLegend, original);
    }

    /// <summary>Verifica que el código persistido de reimpresión cabe en la columna de auditoría.</summary>
    [Fact]
    public void ReceiptReprint_FitsAuditLogActionMaxLength()
    {
        var property = typeof(AuditLog).GetProperty(nameof(AuditLog.Action), BindingFlags.Public | BindingFlags.Instance);
        var maxLength = property?.GetCustomAttribute<MaxLengthAttribute>();
        var limit = maxLength?.Length ?? throw new Xunit.Sdk.XunitException("AuditLog.Action debe tener MaxLength.");

        Assert.True(
            SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint.Length <= limit,
            $"La acción de reimpresión supera el límite {limit} de AuditLog.Action.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => value;
    }
}
