using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>Cliente del API de transmision del Ministerio de Hacienda.</summary>
public interface IMhApiClient
{
    /// <summary>Autentica contra el API de seguridad y devuelve el token JWT (con prefijo Bearer).</summary>
    Task<string> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Transmite un DTE firmado al servicio de recepcion del MH.</summary>
    Task<MhReceptionResponse> SendDteAsync(
        MhReceptionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementacion del cliente del API del MH. Cachea el token de seguridad
/// (vigencia 24h en produccion) y lo reutiliza entre envios. Los errores de red
/// o 5xx se traducen a <see cref="DteTransmissionException"/> para activar contingencia.
/// </summary>
public sealed class MhApiClient : IMhApiClient, IDisposable
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(20);

    private readonly MhOptions _options;
    private readonly ILogger<MhApiClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private string? _cachedToken;
    private DateTime _tokenObtainedAtUtc;

    /// <summary>Crea el cliente del API del MH con las opciones configuradas.</summary>
    public MhApiClient(IOptions<MhOptions> options, ILogger<MhApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_options.HttpTimeoutSeconds)
        };
    }

    /// <inheritdoc />
    public async Task<string> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (IsTokenValid())
        {
            return _cachedToken!;
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (IsTokenValid())
            {
                return _cachedToken!;
            }

            if (string.IsNullOrWhiteSpace(_options.ApiUser) || string.IsNullOrWhiteSpace(_options.ApiPassword))
            {
                throw new DteConfigurationException(
                    "Faltan credenciales del API del MH (Mh:ApiUser / Mh:ApiPassword).");
            }

            var url = _options.ActiveBaseUrl.TrimEnd('/') + _options.AuthPath;
            using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", _options.ApiUser),
                new KeyValuePair<string, string>("pwd", _options.ApiPassword)
            });

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(url, content, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new DteTransmissionException(
                    "No se pudo autenticar contra el MH (sin conexion o timeout).", ex);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonConvert.DeserializeObject<MhAuthResponse>(body);

            if (parsed?.Body?.Token is not { Length: > 0 } token)
            {
                _logger.LogError("Autenticacion MH fallida ({Status}): {Body}", (int)response.StatusCode, body);
                throw new DteTransmissionException(
                    $"Autenticacion MH fallida: {parsed?.Message ?? parsed?.Error ?? response.StatusCode.ToString()}");
            }

            _cachedToken = token.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase)
                ? token
                : "Bearer " + token;
            _tokenObtainedAtUtc = DateTime.UtcNow;

            _logger.LogInformation("Token MH obtenido correctamente (ambiente {Ambiente}).", _options.Ambiente);
            return _cachedToken;
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MhReceptionResponse> SendDteAsync(
        MhReceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await AuthenticateAsync(cancellationToken);
        var url = _options.ActiveBaseUrl.TrimEnd('/') + _options.ReceptionPath;

        var json = JsonConvert.SerializeObject(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DteTransmissionException(
                "No se pudo transmitir el DTE al MH (sin conexion o timeout).", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // 5xx o 401/403: reintentar mediante contingencia; 400 con observaciones: rechazo.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _cachedToken = null;
            throw new DteTransmissionException(
                "El MH rechazo la autorizacion; se reintentara con un nuevo token.");
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new DteTransmissionException(
                $"El MH respondio con error de servidor {(int)response.StatusCode}.");
        }

        var parsed = JsonConvert.DeserializeObject<MhReceptionResponse>(body);
        if (parsed is null)
        {
            throw new DteTransmissionException("Respuesta ilegible del servicio de recepcion del MH.");
        }

        return parsed;
    }

    private bool IsTokenValid()
    {
        return !string.IsNullOrEmpty(_cachedToken) &&
               DateTime.UtcNow - _tokenObtainedAtUtc < TokenLifetime;
    }

    /// <summary>Libera el HttpClient y el semaforo internos.</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
        _authLock.Dispose();
    }
}
