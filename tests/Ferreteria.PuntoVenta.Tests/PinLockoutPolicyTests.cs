using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la función pura del lockout persistente de PIN.</summary>
public sealed class PinLockoutPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Cuatro fallos no bloquean el terminal.</summary>
    [Fact]
    public void Evaluate_CuatroFallos_NoBloquea()
    {
        var status = PinLockoutPolicy.Evaluate(Fails(4), Start.AddSeconds(4));

        Assert.False(status.IsLocked);
        Assert.Equal(4, status.FailedAttempts);
    }

    /// <summary>El quinto fallo bloquea durante dos minutos.</summary>
    [Fact]
    public void Evaluate_QuintoFallo_BloqueaDosMinutos()
    {
        var status = PinLockoutPolicy.Evaluate(Fails(5), Start.AddSeconds(5));

        Assert.True(status.IsLocked);
        Assert.Equal(Start.AddMinutes(2).AddSeconds(4).UtcDateTime, status.LockedUntilUtc);
    }

    /// <summary>Al vencer el bloqueo, los fallos permanecen mientras sigan dentro de la ventana.</summary>
    [Fact]
    public void Evaluate_BloqueoVencido_ReiniciaRacha()
    {
        var status = PinLockoutPolicy.Evaluate(Fails(5), Start.AddMinutes(2).AddSeconds(4));

        Assert.False(status.IsLocked);
        Assert.Equal(5, status.FailedAttempts);
    }

    /// <summary>Un PIN correcto no borra fallos de otros empleados del mismo terminal.</summary>
    [Fact]
    public void Evaluate_PinCorrecto_NoReiniciaRachaDelTerminal()
    {
        var events = Fails(4).Append(new PinLockoutEvent("PIN_OK", Start.AddMinutes(1)))
            .Append(new PinLockoutEvent("PIN_FAIL", Start.AddMinutes(1).AddSeconds(1)));

        var status = PinLockoutPolicy.Evaluate(events, Start.AddMinutes(1).AddSeconds(2));

        Assert.True(status.IsLocked);
        Assert.Equal(5, status.FailedAttempts);
    }

    /// <summary>El siguiente bloqueo dentro de la ventana duplica la duración inicial.</summary>
    [Fact]
    public void Evaluate_BloqueosSucesivos_ProgresanHastaElTope()
    {
        var options = new PinLockoutOptions
        {
            MaxAttempts = 2,
            WindowMinutes = 30,
            InitialLockoutMinutes = 2,
            ProgressiveMultiplier = 2,
            MaxLockoutMinutes = 5
        };
        var events = Fails(2)
            .Append(new PinLockoutEvent("PIN_FAIL", Start.AddMinutes(3)))
            .Append(new PinLockoutEvent("PIN_FAIL", Start.AddMinutes(3).AddSeconds(1)))
            .Append(new PinLockoutEvent("PIN_FAIL", Start.AddMinutes(8)));

        var first = PinLockoutPolicy.Evaluate(events.Take(2), Start.AddSeconds(1), options);
        var second = PinLockoutPolicy.Evaluate(events.Take(4), Start.AddMinutes(3).AddSeconds(2), options);
        var capped = PinLockoutPolicy.Evaluate(events, Start.AddMinutes(8).AddSeconds(1), options);

        Assert.Equal(Start.AddMinutes(2).AddSeconds(1).UtcDateTime, first.LockedUntilUtc);
        Assert.Equal(Start.AddMinutes(7).UtcDateTime, second.LockedUntilUtc);
        Assert.Equal(Start.AddMinutes(13).UtcDateTime, capped.LockedUntilUtc);
    }

    /// <summary>Los fallos fuera de la ventana deslizante dejan de participar en el cálculo.</summary>
    [Fact]
    public void Evaluate_FallosFueraDeVentana_NoBloquean()
    {
        var options = new PinLockoutOptions { WindowMinutes = 5 };
        var status = PinLockoutPolicy.Evaluate(Fails(4), Start.AddMinutes(10), options);

        Assert.False(status.IsLocked);
        Assert.Equal(0, status.FailedAttempts);
    }

    /// <summary>Los fallos durante horas nunca producen un bloqueo mayor que el tope.</summary>
    [Fact]
    public void Evaluate_FallosContinuosDuranteHoras_NoSuperaElTope()
    {
        var options = new PinLockoutOptions
        {
            MaxAttempts = 2,
            WindowMinutes = 15,
            InitialLockoutMinutes = 4,
            ProgressiveMultiplier = 3,
            MaxLockoutMinutes = 7
        };
        var events = new List<PinLockoutEvent>();

        for (var minute = 0; minute < 240; minute++)
        {
            var now = Start.AddMinutes(minute);
            events.Add(new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, now));
            var status = PinLockoutPolicy.Evaluate(events, now, options);

            if (status.IsLocked)
            {
                Assert.True(status.LockedUntilUtc <= now.AddMinutes(options.MaxLockoutMinutes).UtcDateTime);
            }
        }
    }

    /// <summary>Los fallos recibidos durante un bloqueo no extienden su vencimiento ni se cuentan.</summary>
    [Fact]
    public void Evaluate_FallosDuranteBloqueoActivo_NoExtiendenNiCuentan()
    {
        var options = new PinLockoutOptions
        {
            MaxAttempts = 2,
            WindowMinutes = 30,
            InitialLockoutMinutes = 2,
            ProgressiveMultiplier = 2,
            MaxLockoutMinutes = 5
        };
        var events = new[]
        {
            new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, Start),
            new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, Start.AddSeconds(1)),
            new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, Start.AddSeconds(30)),
            new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, Start.AddMinutes(1))
        };

        var status = PinLockoutPolicy.Evaluate(events, Start.AddMinutes(1), options);

        Assert.True(status.IsLocked);
        Assert.Equal(Start.AddMinutes(2).AddSeconds(1).UtcDateTime, status.LockedUntilUtc);
        Assert.Equal(options.MaxAttempts, status.FailedAttempts);
    }

    /// <summary>El último desbloqueo administrativo descarta fallos anteriores.</summary>
    [Fact]
    public void Evaluate_DesbloqueoAdministrativo_ReiniciaContadorYProgresion()
    {
        var events = Fails(5)
            .Append(new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinUnlock, Start.AddMinutes(1)))
            .Append(new PinLockoutEvent(SalesDomainConstants.PinAuditActions.PinFail, Start.AddMinutes(1).AddSeconds(1)));

        var status = PinLockoutPolicy.Evaluate(events, Start.AddMinutes(1).AddSeconds(1));

        Assert.False(status.IsLocked);
        Assert.Equal(1, status.FailedAttempts);
    }

    /// <summary>Con el reloj detenido, cada evento nuevo de la terminal queda estrictamente después del anterior.</summary>
    [Fact]
    public void NextEventTimestamp_FrozenClock_IsStrictlyIncreasing()
    {
        var first = PinLockoutPolicy.NextEventTimestamp(null, Start);
        var second = PinLockoutPolicy.NextEventTimestamp(first, Start);

        Assert.Equal(Start, first);
        Assert.Equal(Start + PinLockoutPolicy.EventResolution, second);
    }

    /// <summary>Si el reloj retrocede, el evento nuevo igual queda después del último registrado.</summary>
    [Fact]
    public void NextEventTimestamp_ClockGoesBack_StaysAfterLastEvent()
    {
        var last = Start.AddMinutes(10);

        Assert.Equal(last + PinLockoutPolicy.EventResolution, PinLockoutPolicy.NextEventTimestamp(last, Start));
    }

    /// <summary>Un desbloqueo grabado con reloj adelantado se aplica y los fallos posteriores cuentan desde cero.</summary>
    [Fact]
    public void ResolveEvaluationTime_UnlockStampedAhead_IsHonored()
    {
        var unlockAt = Start.AddMinutes(3);
        var events = Fails(5).Append(new PinLockoutEvent("PIN_UNLOCK", unlockAt)).ToList();
        events.Add(new PinLockoutEvent("PIN_FAIL", PinLockoutPolicy.NextEventTimestamp(unlockAt, Start.AddMinutes(1))));

        var status = PinLockoutPolicy.Evaluate(
            events,
            PinLockoutPolicy.ResolveEvaluationTime(events, Start.AddMinutes(1)),
            new PinLockoutOptions());

        Assert.False(status.IsLocked);
        Assert.Equal(1, status.FailedAttempts);
    }

    private static IEnumerable<PinLockoutEvent> Fails(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new PinLockoutEvent("PIN_FAIL", Start.AddSeconds(index)));
}
