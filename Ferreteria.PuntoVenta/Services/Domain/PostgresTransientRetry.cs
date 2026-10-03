using Npgsql;

namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Ejecuta operaciones PostgreSQL que pueden abortar por serialización o deadlock.
/// </summary>
public static class PostgresTransientRetry
{
    /// <summary>Cantidad máxima de reintentos posteriores al intento inicial.</summary>
    public const int MaximumRetries = 2;

    /// <summary>
    /// Ejecuta una operación y reintenta errores transitorios con una operación nueva.
    /// </summary>
    /// <typeparam name="TResult">Tipo del resultado.</typeparam>
    /// <param name="operation">Operación que debe crear su propio contexto y transacción.</param>
    /// <param name="onRetry">Callback opcional para telemetría sin datos sensibles.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado de la primera ejecución exitosa.</returns>
    /// <exception cref="PostgresTransientOperationException">Si se agotan los reintentos.</exception>
    public static async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Action<int, PostgresException>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var retry = 0; ; retry++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception exception) when (TryGetTransientException(exception, out var postgresException))
            {
                if (retry >= MaximumRetries)
                {
                    throw new PostgresTransientOperationException(postgresException!.SqlState, postgresException);
                }

                onRetry?.Invoke(retry + 1, postgresException!);
            }
        }
    }

    /// <summary>Indica si una excepción contiene un error transitorio soportado.</summary>
    /// <param name="exception">Excepción original o envolvente.</param>
    /// <param name="postgresException">Excepción PostgreSQL encontrada.</param>
    /// <returns><c>true</c> para serialización o deadlock.</returns>
    public static bool TryGetTransientException(Exception exception, out PostgresException? postgresException)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && (postgres.SqlState == PostgresErrorCodes.SerializationFailure
                    || postgres.SqlState == PostgresErrorCodes.DeadlockDetected))
            {
                postgresException = postgres;
                return true;
            }
        }

        postgresException = null;
        return false;
    }
}

/// <summary>Indica que una operación PostgreSQL agotó sus reintentos transitorios.</summary>
public sealed class PostgresTransientOperationException : Exception
{
    /// <summary>Inicializa la excepción con el código SQLSTATE y la causa original.</summary>
    /// <param name="sqlState">Código SQLSTATE del último error.</param>
    /// <param name="innerException">Excepción PostgreSQL original.</param>
    public PostgresTransientOperationException(string sqlState, Exception innerException)
        : base("La operación PostgreSQL no pudo completarse después de los reintentos.", innerException)
    {
        SqlState = sqlState;
    }

    /// <summary>Código SQLSTATE que agotó los reintentos.</summary>
    public string SqlState { get; }
}
