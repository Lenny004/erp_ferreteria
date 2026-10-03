using Npgsql;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Comprueba en el PostgreSQL desechable que el rol del POS no recibe privilegios peligrosos.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class DatabaseRoleIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Aplica el script al contenedor efímero y comprueba lectura POS, WebUsers y DDL.</summary>
    [Fact]
    public async Task PosAppRole_AllowsPosReadAndRejectsWebUsersAndDdl()
    {
        // El script de docs/pos se enlaza en el csproj y se copia a Data/, igual que Squema.sql.
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Data", "pos_app_rol_minimo.sql");
        var script = await File.ReadAllTextAsync(scriptPath);
        var adminBuilder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        script = script.Replace(
            "GRANT CONNECT ON DATABASE ferreteria TO pos_app;",
            $"GRANT CONNECT ON DATABASE \"{adminBuilder.Database}\" TO pos_app;",
            StringComparison.Ordinal);

        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand(script, admin);
            await command.ExecuteNonQueryAsync();
            await using var password = new NpgsqlCommand("ALTER ROLE pos_app PASSWORD 'qa-only-password'", admin);
            await password.ExecuteNonQueryAsync();
            await using var webUsers = new NpgsqlCommand(
                "CREATE TABLE IF NOT EXISTS system.\"WebUsers\" (\"id\" uuid PRIMARY KEY)",
                admin);
            await webUsers.ExecuteNonQueryAsync();
        }

        adminBuilder.Username = "pos_app";
        adminBuilder.Password = "qa-only-password";
        await using var restricted = new NpgsqlConnection(adminBuilder.ConnectionString);
        await restricted.OpenAsync();

        await using (var currentUser = new NpgsqlCommand("SELECT current_user", restricted))
        {
            Assert.Equal("pos_app", (string?)await currentUser.ExecuteScalarAsync());
        }

        await using (var readable = new NpgsqlCommand("SELECT COUNT(*) FROM sales.\"Orders\"", restricted))
        {
            Assert.True(Convert.ToInt64(await readable.ExecuteScalarAsync()) >= 0);
        }

        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("SELECT COUNT(*) FROM system.\"WebUsers\"", restricted).ExecuteScalarAsync());
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("CREATE TABLE public.pos_app_ddl_forbidden (id integer)", restricted).ExecuteNonQueryAsync());
    }
}
