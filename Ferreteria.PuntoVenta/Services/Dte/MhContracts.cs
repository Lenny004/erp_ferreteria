using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ferreteria.PuntoVenta.Services.Dte;

// ============================================================================
//  Contratos HTTP de las APIs del Ministerio de Hacienda (autenticacion y
//  recepcion) y del Firmador local del MH.
// ============================================================================

/// <summary>Respuesta del servicio de autenticacion del MH (<c>/seguridad/auth</c>).</summary>
public sealed class MhAuthResponse
{
    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("body")]
    public MhAuthBody? Body { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }
}

/// <summary>Cuerpo de la respuesta de autenticacion (contiene el token JWT).</summary>
public sealed class MhAuthBody
{
    [JsonProperty("user")]
    public string? User { get; set; }

    [JsonProperty("token")]
    public string? Token { get; set; }

    [JsonProperty("rol")]
    public JToken? Rol { get; set; }

    [JsonProperty("tokenType")]
    public string? TokenType { get; set; }
}

/// <summary>Solicitud de recepcion de un DTE firmado (<c>/fesv/recepciondte</c>).</summary>
public sealed class MhReceptionRequest
{
    [JsonProperty("ambiente")]
    public string Ambiente { get; set; } = "00";

    [JsonProperty("idEnvio")]
    public int IdEnvio { get; set; } = 1;

    [JsonProperty("version")]
    public int Version { get; set; } = 1;

    [JsonProperty("tipoDte")]
    public string TipoDte { get; set; } = "01";

    /// <summary>Documento DTE firmado (JWS/JWT) en formato string.</summary>
    [JsonProperty("documento")]
    public string Documento { get; set; } = string.Empty;

    [JsonProperty("codigoGeneracion")]
    public string CodigoGeneracion { get; set; } = string.Empty;
}

/// <summary>Respuesta del servicio de recepcion del MH.</summary>
public sealed class MhReceptionResponse
{
    [JsonProperty("version")]
    public int? Version { get; set; }

    [JsonProperty("ambiente")]
    public string? Ambiente { get; set; }

    [JsonProperty("versionApp")]
    public int? VersionApp { get; set; }

    [JsonProperty("estado")]
    public string? Estado { get; set; }

    [JsonProperty("codigoGeneracion")]
    public string? CodigoGeneracion { get; set; }

    [JsonProperty("selloRecibido")]
    public string? SelloRecibido { get; set; }

    [JsonProperty("fhProcesamiento")]
    public string? FhProcesamiento { get; set; }

    [JsonProperty("clasificaMsg")]
    public string? ClasificaMsg { get; set; }

    [JsonProperty("codigoMsg")]
    public string? CodigoMsg { get; set; }

    [JsonProperty("descripcionMsg")]
    public string? DescripcionMsg { get; set; }

    [JsonProperty("observaciones")]
    public List<string>? Observaciones { get; set; }

    /// <summary>Indica si el MH sello/acepto el documento.</summary>
    [JsonIgnore]
    public bool IsAccepted =>
        !string.IsNullOrWhiteSpace(SelloRecibido) &&
        (string.Equals(Estado, DteConstants.RespuestasMh.Recibido, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Estado, DteConstants.RespuestasMh.Procesado, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Solicitud al Firmador local del MH.</summary>
public sealed class FirmadorRequest
{
    [JsonProperty("nit")]
    public string Nit { get; set; } = string.Empty;

    [JsonProperty("activo")]
    public bool Activo { get; set; } = true;

    [JsonProperty("passwordPri")]
    public string PasswordPri { get; set; } = string.Empty;

    [JsonProperty("dteJson")]
    public object DteJson { get; set; } = new object();
}

/// <summary>Respuesta del Firmador local del MH.</summary>
public sealed class FirmadorResponse
{
    [JsonProperty("status")]
    public string? Status { get; set; }

    /// <summary>En exito es el JWS firmado (string); en error es un objeto con detalles.</summary>
    [JsonProperty("body")]
    public JToken? Body { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    /// <summary>Indica si la firma fue exitosa.</summary>
    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Status, DteConstants.RespuestasMh.Ok, StringComparison.OrdinalIgnoreCase) &&
        Body is { Type: JTokenType.String };

    /// <summary>Devuelve el documento firmado si la respuesta fue exitosa.</summary>
    public string GetSignedDocument()
    {
        if (!IsSuccess)
        {
            throw new DteSigningException(
                $"El firmador no devolvio un documento firmado. Estado: {Status}. Detalle: {Body}");
        }

        return Body!.Value<string>() ?? string.Empty;
    }
}
