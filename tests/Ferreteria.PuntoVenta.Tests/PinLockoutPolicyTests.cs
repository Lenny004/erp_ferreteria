using Ferreteria.PuntoVenta.Services;
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

    private static IEnumerable<PinLockoutEvent> Fails(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new PinLockoutEvent("PIN_FAIL", Start.AddSeconds(index)));
}
