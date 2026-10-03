using Ferreteria.PuntoVenta.Services.Domain;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la duración del identificador lógico de intento de venta.</summary>
public sealed class SaleAttemptTrackerTests
{
    /// <summary>Un fallo conserva el identificador para el mismo contenido.</summary>
    [Fact]
    public void SameInput_KeepsRequestIdAfterFailure()
    {
        var tracker = new SaleAttemptTracker();
        var input = Input(1m);

        var first = tracker.GetOrCreate(input);
        var retry = tracker.GetOrCreate(input);

        Assert.Equal(first, retry);
    }

    /// <summary>El éxito genera un identificador nuevo para la venta siguiente.</summary>
    [Fact]
    public void Success_RegeneratesRequestId()
    {
        var tracker = new SaleAttemptTracker();
        var input = Input(1m);
        var first = tracker.GetOrCreate(input);

        tracker.MarkSucceeded(first);

        Assert.NotEqual(first, tracker.GetOrCreate(input));
    }

    /// <summary>Cambiar cantidad o pago genera un nuevo intento.</summary>
    [Fact]
    public void CartOrPaymentChange_RegeneratesRequestId()
    {
        var tracker = new SaleAttemptTracker();
        var first = tracker.GetOrCreate(Input(1m));

        var quantityChanged = tracker.GetOrCreate(Input(2m));
        var paymentChanged = tracker.GetOrCreate(Input(2m, "TARJETA"));

        Assert.NotEqual(first, quantityChanged);
        Assert.NotEqual(quantityChanged, paymentChanged);
    }

    private static SaleAttemptInput Input(decimal quantity, string method = "EFECTIVO") =>
        new([new SaleAttemptLine(Guid.Parse("00000000-0000-0000-0000-000000000001"), quantity)], method, quantity);
}
