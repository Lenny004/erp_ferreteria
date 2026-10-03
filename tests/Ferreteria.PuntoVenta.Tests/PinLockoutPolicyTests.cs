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
        var status = PinLockoutPolicy.Evaluate(Fails(4), Start.AddSeconds(1));

        Assert.False(status.IsLocked);
        Assert.Equal(4, status.FailedAttempts);
    }

    /// <summary>El quinto fallo bloquea durante dos minutos.</summary>
    [Fact]
    public void Evaluate_QuintoFallo_BloqueaDosMinutos()
    {
        var status = PinLockoutPolicy.Evaluate(Fails(5), Start.AddSeconds(1));

        Assert.True(status.IsLocked);
        Assert.Equal(Start.AddMinutes(2).AddSeconds(4).UtcDateTime, status.LockedUntilUtc);
    }

    /// <summary>Al vencer el bloqueo, la racha vuelve a cero.</summary>
    [Fact]
    public void Evaluate_BloqueoVencido_ReiniciaRacha()
    {
        var status = PinLockoutPolicy.Evaluate(Fails(5), Start.AddMinutes(2).AddSeconds(4));

        Assert.False(status.IsLocked);
        Assert.Equal(0, status.FailedAttempts);
    }

    /// <summary>Un PIN correcto reinicia la racha antes de nuevos fallos.</summary>
    [Fact]
    public void Evaluate_PinCorrecto_ReiniciaRacha()
    {
        var events = Fails(4).Append(new PinLockoutEvent("PIN_OK", Start.AddMinutes(1)))
            .Append(new PinLockoutEvent("PIN_FAIL", Start.AddMinutes(1).AddSeconds(1)));

        var status = PinLockoutPolicy.Evaluate(events, Start.AddMinutes(1).AddSeconds(2));

        Assert.False(status.IsLocked);
        Assert.Equal(1, status.FailedAttempts);
    }

    private static IEnumerable<PinLockoutEvent> Fails(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new PinLockoutEvent("PIN_FAIL", Start.AddSeconds(index)));
}
