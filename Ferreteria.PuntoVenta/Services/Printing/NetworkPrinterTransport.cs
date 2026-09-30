using System.IO;
using System.Net.Sockets;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>Transporta bytes ESC/POS a una impresora de red mediante TCP.</summary>
public sealed class NetworkPrinterTransport
{
    /// <summary>Envía un payload aplicando el timeout a conexión, escritura y vaciado.</summary>
    /// <param name="host">Dirección o nombre del equipo.</param>
    /// <param name="port">Puerto TCP de la impresora.</param>
    /// <param name="payload">Bytes ESC/POS que se enviarán.</param>
    /// <param name="timeout">Tiempo máximo de toda la operación de red.</param>
    /// <param name="cancellationToken">Cancelación solicitada por el llamador.</param>
    /// <returns>Tarea que termina cuando el envío concluye.</returns>
    /// <exception cref="PrinterException">Si vence el timeout o falla la red.</exception>
    /// <exception cref="OperationCanceledException">Si el llamador cancela la operación.</exception>
    /// <remarks>El timeout propio se distingue de la cancelación externa para no mostrar un motivo engañoso.</remarks>
    public async Task SendAsync(
        string host,
        int port,
        byte[] payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentNullException.ThrowIfNull(payload);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "El timeout debe ser mayor que cero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeoutSource.Token).ConfigureAwait(false);
            await using NetworkStream stream = client.GetStream();
            // Escribir por bloques permite que la presión del buffer TCP se refleje en el timeout.
            for (int offset = 0; offset < payload.Length; offset += 4096)
            {
                int count = Math.Min(4096, payload.Length - offset);
                await stream.WriteAsync(payload.AsMemory(offset, count), timeoutSource.Token).ConfigureAwait(false);
            }
            await stream.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PrinterException("La impresora de red no respondió a tiempo. Verifique la conexión.");
        }
        catch (SocketException ex)
        {
            throw new PrinterException("No se pudo conectar con la impresora de red.", ex);
        }
        catch (IOException ex)
        {
            throw new PrinterException("Falló la comunicación con la impresora de red.", ex);
        }
    }
}
