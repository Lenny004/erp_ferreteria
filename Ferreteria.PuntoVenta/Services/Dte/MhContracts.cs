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
    /// <summary>Estado HTTP o funcional devuelto por el servicio de autenticación.</summary>
    [JsonProperty("status")]
    public string? Status { get; set; }

    /// <summary>Cuerpo con las credenciales emitidas por el servicio.</summary>
    [JsonProperty("body")]
    public MhAuthBody? Body { get; set; }

    /// <summary>Detalle técnico de error devuelto por el servicio.</summary>
    [JsonProperty("error")]
    public string? Error { get; set; }

    /// <summary>Mensaje funcional devuelto por el servicio.</summary>
    [JsonProperty("message")]
    public string? Message { get; set; }
}

/// <summary>Cuerpo de la respuesta de autenticacion (contiene el token JWT).</summary>
public sealed class MhAuthBody
{
    /// <summary>Usuario autenticado en el servicio.</summary>
    [JsonProperty("user")]
    public string? User { get; set; }

    /// <summary>Token de autenticación emitido por el MH.</summary>
    [JsonProperty("token")]
    public string? Token { get; set; }

    /// <summary>Rol informado por el servicio de autenticación.</summary>
    [JsonProperty("rol")]
    public JToken? Rol { get; set; }

    /// <summary>Tipo del token emitido.</summary>
    [JsonProperty("tokenType")]
    public string? TokenType { get; set; }
}

/// <summary>Solicitud de recepcion de un DTE firmado (<c>/fesv/recepciondte</c>).</summary>
public sealed class MhReceptionRequest
{
    /// <summary>Ambiente del MH al que se envía el DTE.</summary>
    [JsonProperty("ambiente")]
    public string Ambiente { get; set; } = "00";

    /// <summary>Identificador secuencial del envío.</summary>
    [JsonProperty("idEnvio")]
    public int IdEnvio { get; set; } = 1;

    /// <summary>Versión del contrato de recepción.</summary>
    [JsonProperty("version")]
    public int Version { get; set; } = 1;

    /// <summary>Tipo de DTE que contiene el documento firmado.</summary>
    [JsonProperty("tipoDte")]
    public string TipoDte { get; set; } = "01";

    /// <summary>Documento DTE firmado (JWS/JWT) en formato string.</summary>
    [JsonProperty("documento")]
    public string Documento { get; set; } = string.Empty;

    /// <summary>Código de generación del DTE enviado.</summary>
    [JsonProperty("codigoGeneracion")]
    public string CodigoGeneracion { get; set; } = string.Empty;
}

/// <summary>Respuesta del servicio de recepcion del MH.</summary>
public sealed class MhReceptionResponse
{
    /// <summary>Versión del contrato usada por el MH.</summary>
    [JsonProperty("version")]
    public int? Version { get; set; }

    /// <summary>Ambiente en el que el MH procesó el documento.</summary>
    [JsonProperty("ambiente")]
    public string? Ambiente { get; set; }

    /// <summary>Versión de la aplicación del MH.</summary>
    [JsonProperty("versionApp")]
    public int? VersionApp { get; set; }

    /// <summary>Estado funcional de recepción del DTE.</summary>
    [JsonProperty("estado")]
    public string? Estado { get; set; }

    /// <summary>Código de generación reportado por el MH.</summary>
    [JsonProperty("codigoGeneracion")]
    public string? CodigoGeneracion { get; set; }

    /// <summary>Sello de recepción asignado por el MH.</summary>
    [JsonProperty("selloRecibido")]
    public string? SelloRecibido { get; set; }

    /// <summary>Fecha y hora de procesamiento informada por el MH.</summary>
    [JsonProperty("fhProcesamiento")]
    public string? FhProcesamiento { get; set; }

    /// <summary>Clasificación funcional del mensaje.</summary>
    [JsonProperty("clasificaMsg")]
    public string? ClasificaMsg { get; set; }

    /// <summary>Código funcional del mensaje.</summary>
    [JsonProperty("codigoMsg")]
    public string? CodigoMsg { get; set; }

    /// <summary>Descripción funcional del mensaje.</summary>
    [JsonProperty("descripcionMsg")]
    public string? DescripcionMsg { get; set; }

    /// <summary>Observaciones devueltas por el MH.</summary>
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
    /// <summary>NIT del emisor que firma el documento.</summary>
    [JsonProperty("nit")]
    public string Nit { get; set; } = string.Empty;

    /// <summary>Indica si el certificado del firmador está activo.</summary>
    [JsonProperty("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>Contraseña de la clave privada del certificado.</summary>
    [JsonProperty("passwordPri")]
    public string PasswordPri { get; set; } = string.Empty;

    /// <summary>Documento DTE que se solicita firmar.</summary>
    [JsonProperty("dteJson")]
    public object DteJson { get; set; } = new object();
}

/// <summary>Respuesta del Firmador local del MH.</summary>
public sealed class FirmadorResponse
{
    /// <summary>Estado funcional devuelto por el firmador.</summary>
    [JsonProperty("status")]
    public string? Status { get; set; }

    /// <summary>En exito es el JWS firmado (string); en error es un objeto con detalles.</summary>
    [JsonProperty("body")]
    public JToken? Body { get; set; }

    /// <summary>Mensaje funcional devuelto por el firmador.</summary>
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
