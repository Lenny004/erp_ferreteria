using System.Net.Http;
using System.Text;
using Ferreteria.PuntoVenta.Models.Dte.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>Firma un DTE usando el Firmador local del Ministerio de Hacienda.</summary>
public interface IDteSigningService
{
    /// <summary>
    /// Envia el documento al Firmador local y devuelve el JWS firmado.
    /// </summary>
    /// <param name="document">Documento DTE a firmar.</param>
    /// <param name="emisorNit">NIT del emisor (14 digitos).</param>
    /// <param name="certPassword">Clave privada del certificado (passwordPri).</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>Documento firmado en formato JWS.</returns>
    Task<string> SignAsync(
        DteDocument document,
        string emisorNit,
        string certPassword,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cliente HTTP del Firmador local del MH (svfe-api-firmador), tipicamente en
/// <c>http://localhost:8113/firmardocumento/</c>. El firmador usa el certificado
/// .p12 instalado localmente; nunca sale de la infraestructura del contribuyente.
/// </summary>
public sealed class FirmadorHttpClient : IDteSigningService, IDisposable
{
    private readonly MhOptions _options;
    private readonly ILogger<FirmadorHttpClient> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>Crea el cliente del firmador con las opciones del MH.</summary>
    public FirmadorHttpClient(IOptions<MhOptions> options, ILogger<FirmadorHttpClient> logger)
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
    public async Task<string> SignAsync(
        DteDocument document,
        string emisorNit,
        string certPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(emisorNit))
        {
            throw new DteConfigurationException("Falta el NIT del emisor para firmar el DTE.");
        }

        if (string.IsNullOrWhiteSpace(certPassword))
        {
            throw new DteConfigurationException(
                "Falta la clave del certificado (Mh:CertPassword) para firmar el DTE.");
        }

        var request = new FirmadorRequest
        {
            Nit = emisorNit,
            Activo = true,
            PasswordPri = certPassword,
            DteJson = document
        };

        var json = JsonConvert.SerializeObject(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(_options.FirmadorUrl, content, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "No se pudo contactar al Firmador local en {Url}", _options.FirmadorUrl);
            throw new DteSigningException(
                "No se pudo contactar al Firmador local del MH. Verifique que este en ejecucion (puerto 8113).",
                ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Firmador respondio {Status}: {Body}", (int)response.StatusCode, body);
            throw new DteSigningException(
                $"El firmador respondio con error HTTP {(int)response.StatusCode}.");
        }

        var parsed = JsonConvert.DeserializeObject<FirmadorResponse>(body)
            ?? throw new DteSigningException("Respuesta vacia del firmador.");

        return parsed.GetSignedDocument();
    }

    /// <summary>Libera el HttpClient interno.</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
