using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas unitarias del día de negocio y su zona horaria configurable.</summary>
public sealed class BusinessCalendarTests
{
    private static BusinessTimeZone ElSalvadorZone()
    {
        return BusinessTimeZone.Create(new BusinessTimeOptions { ZonaHoraria = "America/El_Salvador" });
    }

    private static BusinessCalendar Calendar(DateTimeOffset now)
    {
        return new BusinessCalendar(ElSalvadorZone(), new FixedTimeProvider(now));
    }

    /// <summary>Separa correctamente una venta de las 23:30 de otra de las 00:05 local.</summary>
    [Fact]
    public void ToLocalDate_SeparatesLateNightAndAfterMidnight()
    {
        var calendar = Calendar(new DateTimeOffset(2026, 9, 30, 5, 30, 0, TimeSpan.Zero));
        var lateNightUtc = new DateTime(2026, 9, 30, 5, 30, 0, DateTimeKind.Utc);
        var afterMidnightUtc = new DateTime(2026, 9, 30, 6, 5, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 9, 29), calendar.ToLocalDate(lateNightUtc));
        Assert.Equal(new DateOnly(2026, 9, 30), calendar.ToLocalDate(afterMidnightUtc));
        Assert.Equal(new DateOnly(2026, 9, 30), DateOnly.FromDateTime(lateNightUtc));
        Assert.Equal(DateOnly.FromDateTime(lateNightUtc), DateOnly.FromDateTime(afterMidnightUtc));
    }

    /// <summary>Verifica que el día local 29 de septiembre tenga límites UTC semiabiertos.</summary>
    [Fact]
    public void DayRangeUtc_UsesExclusiveEndAndContainsOnlyTheLocalDay()
    {
        var range = Calendar(new DateTimeOffset(2026, 9, 30, 5, 30, 0, TimeSpan.Zero))
            .DayRangeUtc(new DateOnly(2026, 9, 29));
        var lateNightUtc = new DateTime(2026, 9, 30, 5, 30, 0, DateTimeKind.Utc);
        var afterMidnightUtc = new DateTime(2026, 9, 30, 6, 5, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 9, 29, 6, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc), range.EndUtc);
        Assert.True(lateNightUtc >= range.StartUtc && lateNightUtc < range.EndUtc);
        Assert.False(afterMidnightUtc >= range.StartUtc && afterMidnightUtc < range.EndUtc);
    }

    /// <summary>Calcula hoy usando el reloj UTC inyectado y no el reloj del equipo.</summary>
    [Fact]
    public void Today_UsesBusinessTimeZone()
    {
        var calendar = Calendar(new DateTimeOffset(2026, 9, 30, 5, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 9, 29), calendar.Today());
    }

    /// <summary>Recibo e historial muestran la hora de El Salvador aunque la PC esté en otra zona.</summary>
    [Fact]
    public void ReceiptAndSaleText_UseBusinessTimeZone()
    {
        var utc = new DateTime(2026, 9, 30, 5, 30, 0, DateTimeKind.Utc);
        TimeZoneSupport.Initialize(ElSalvadorZone());

        var summary = new SalesOrderSummary(
            Guid.NewGuid(),
            utc,
            "Cliente",
            "VENTA_CAJA",
            "EFECTIVO",
            "COMPLETADA",
            1m);

        Assert.Equal("29/09 23:30", summary.DateText);
    }

    /// <summary>Verifica el rango de varios días y el rechazo de fechas invertidas.</summary>
    [Fact]
    public void RangeUtc_IsSemiOpenAndRejectsInvertedDates()
    {
        var calendar = Calendar(new DateTimeOffset(2026, 9, 30, 5, 30, 0, TimeSpan.Zero));
        var range = calendar.RangeUtc(new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 30));

        Assert.Equal(new DateTime(2026, 9, 29, 6, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc), range.EndUtc);
        Assert.Throws<ArgumentException>(() => calendar.RangeUtc(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 29)));
    }

    /// <summary>Verifica que el cambio de año conserve los días locales correctos.</summary>
    [Fact]
    public void ToLocalDate_HandlesMonthAndYearBoundary()
    {
        var calendar = Calendar(new DateTimeOffset(2027, 1, 1, 5, 30, 0, TimeSpan.Zero));
        var newYearUtc = new DateTime(2027, 1, 1, 5, 30, 0, DateTimeKind.Utc);
        var afterMidnightUtc = new DateTime(2027, 1, 1, 6, 5, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 12, 31), calendar.ToLocalDate(newYearUtc));
        Assert.Equal(new DateOnly(2027, 1, 1), calendar.ToLocalDate(afterMidnightUtc));
        Assert.Equal(new DateTime(2026, 12, 31, 6, 0, 0, DateTimeKind.Utc), calendar.DayRangeUtc(new DateOnly(2026, 12, 31)).StartUtc);
        Assert.Equal(new DateTime(2027, 1, 1, 6, 0, 0, DateTimeKind.Utc), calendar.DayRangeUtc(new DateOnly(2027, 1, 1)).StartUtc);
    }

    /// <summary>Acepta el identificador Windows equivalente y expone un identificador IANA.</summary>
    [Fact]
    public void BusinessTimeZone_AcceptsWindowsIdAndResolvesIanaId()
    {
        var zone = BusinessTimeZone.Create(new BusinessTimeOptions { ZonaHoraria = "Central America Standard Time" });

        Assert.False(string.IsNullOrWhiteSpace(zone.IanaId));
        Assert.True(TimeZoneInfo.TryConvertWindowsIdToIanaId("Central America Standard Time", out var expectedIana));
        Assert.Equal(expectedIana, zone.IanaId);
    }

    /// <summary>Rechaza zonas inválidas y mensajes que no identifican la clave de configuración.</summary>
    [Fact]
    public void BusinessTimeZone_RejectsInvalidAndEmptyConfiguration()
    {
        var invalid = Assert.Throws<InvalidOperationException>(() =>
            BusinessTimeZone.Create(new BusinessTimeOptions { ZonaHoraria = "Zona/NoExiste" }));
        var empty = Assert.Throws<InvalidOperationException>(() =>
            BusinessTimeZone.Create(new BusinessTimeOptions { ZonaHoraria = " " }));

        Assert.Contains("Negocio:ZonaHoraria", invalid.Message, StringComparison.Ordinal);
        Assert.Contains("Negocio:ZonaHoraria", empty.Message, StringComparison.Ordinal);
    }

    /// <summary>Comprueba que la validación de opciones falle al resolver su valor.</summary>
    [Fact]
    public void BusinessTimeOptions_ValidateOnStartRejectsInvalidZone()
    {
        using var provider = new ServiceCollection()
            .AddOptions<BusinessTimeOptions>()
            .Configure(options => options.ZonaHoraria = "Zona/NoExiste")
            .Validate(BusinessTimeZone.IsValid, "La clave de configuración 'Negocio:ZonaHoraria' debe contener una zona válida.")
            .ValidateOnStart()
            .Services
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<BusinessTimeOptions>>().Value);
    }

    /// <summary>Verifica hoy y ayer a las 00:10 local incluyendo y excluyendo los instantes correctos.</summary>
    [Fact]
    public void SalesHistoryShortcuts_UseLocalMidnight()
    {
        var calendar = Calendar(new DateTimeOffset(2026, 9, 30, 6, 10, 0, TimeSpan.Zero));
        var lateNightUtc = new DateTime(2026, 9, 30, 5, 30, 0, DateTimeKind.Utc);
        var afterMidnightUtc = new DateTime(2026, 9, 30, 6, 5, 0, DateTimeKind.Utc);
        var today = SalesHistoryFilter.CreateShortcutRange(SalesHistoryDateShortcut.Today, calendar);
        var yesterday = SalesHistoryFilter.CreateShortcutRange(SalesHistoryDateShortcut.Yesterday, calendar);

        Assert.True(afterMidnightUtc >= today.FromUtc && afterMidnightUtc < today.ToUtc);
        Assert.False(lateNightUtc >= today.FromUtc && lateNightUtc < today.ToUtc);
        Assert.True(lateNightUtc >= yesterday.FromUtc && lateNightUtc < yesterday.ToUtc);
        Assert.False(afterMidnightUtc >= yesterday.FromUtc && afterMidnightUtc < yesterday.ToUtc);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => value;
    }
}
