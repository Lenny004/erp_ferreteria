using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Comprueba que el lockout de PIN sobrevive a una nueva instancia del servicio.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class PinLockoutPersistenceIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Los cinco fallos quedan bloqueados tras reconstruir el servicio y vencen con el reloj inyectado.</summary>
    [Fact]
    public async Task LockoutPersistente_SobreviveNuevaInstancia_YVenceAlAvanzarReloj()
    {
        var clock = new ManualTimeProvider(PostgreSqlFixture.Now);
        var code = $"QA-{Guid.NewGuid():N}";

        await using (var firstProvider = BuildProvider(code, clock))
        {
            var attempts = firstProvider.GetRequiredService<IPinAttemptService>();
            for (var index = 0; index < PinLockoutPolicy.MaxAttempts; index++)
            {
                await attempts.RegisterFailedAttemptAsync();
            }
        }

        await using (var secondProvider = BuildProvider(code, clock))
        {
            var attempts = secondProvider.GetRequiredService<IPinAttemptService>();
            var blocked = await attempts.GetStatusAsync();
            Assert.True(blocked.IsLocked);

            clock.Advance(PinLockoutPolicy.LockoutDuration + TimeSpan.FromSeconds(1));
            var unlocked = await attempts.GetStatusAsync();
            Assert.False(unlocked.IsLocked);
            Assert.Equal(0, unlocked.FailedAttempts);
        }
    }

    private ServiceProvider BuildProvider(string code, TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = code);
        services.AddSingleton(clock);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<PinAttemptService>>(
            _ => NullLogger<PinAttemptService>.Instance);
        services.AddSingleton<IPinAttemptService, PinAttemptService>();
        return services.BuildServiceProvider();
    }

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
