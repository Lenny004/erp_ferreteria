using Ferreteria.PuntoVenta.Services.Domain;
using Npgsql;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la política común de reintentos PostgreSQL.</summary>
public sealed class PostgresTransientRetryTests
{
    /// <summary>Reintenta serialización y deadlock hasta alcanzar el éxito.</summary>
    [Fact]
    public async Task SerializationAndDeadlock_AreRetried()
    {
        var attempts = 0;
        var result = await PostgresTransientRetry.ExecuteAsync<int>(_ =>
        {
            attempts++;
            return attempts < 3
                ? Task.FromException<int>(new PostgresException("transient", "ERROR", "ERROR", attempts == 1 ? "40001" : "40P01"))
                : Task.FromResult(7);
        });

        Assert.Equal(7, result);
        Assert.Equal(3, attempts);
    }

    /// <summary>No reintenta errores que no son transitorios.</summary>
    [Fact]
    public async Task NonTransient_IsNotRetried()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => PostgresTransientRetry.ExecuteAsync<int>(_ =>
        {
            attempts++;
            return Task.FromException<int>(new InvalidOperationException("invalid"));
        }));

        Assert.Equal(1, attempts);
    }

    /// <summary>Simula un 40001 y comprueba que el reintento conserva la misma clave de venta.</summary>
    [Fact]
    public async Task SerializationRetry_ReusesSameClientRequestId()
    {
        var clientRequestId = Guid.NewGuid();
        var observed = new List<Guid>();
        var attempts = 0;

        var result = await PostgresTransientRetry.ExecuteAsync(_ =>
        {
            observed.Add(clientRequestId);
            attempts++;
            return attempts == 1
                ? Task.FromException<Guid>(new PostgresException("transient", "ERROR", "ERROR", "40001"))
                : Task.FromResult(clientRequestId);
        });

        Assert.Equal(clientRequestId, result);
        Assert.Equal(new[] { clientRequestId, clientRequestId }, observed);
    }
}
