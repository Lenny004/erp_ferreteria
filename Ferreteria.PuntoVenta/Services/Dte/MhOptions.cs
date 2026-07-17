namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>
/// Opciones de integracion con el Ministerio de Hacienda (MH) de El Salvador.
/// Se enlaza desde la seccion "Mh" de <c>Config/appsettings.json</c>. Los valores
/// sensibles (usuario/clave del API, clave del certificado) llegan VACIOS por defecto:
/// deben completarse con los datos reales que entregue el MH al contribuyente.
/// </summary>
public sealed class MhOptions
{
    /// <summary>Nombre de la seccion de configuracion.</summary>
    public const string SectionName = "Mh";

    /// <summary>Ambiente por defecto: "00" pruebas, "01" produccion.</summary>
    public string Ambiente { get; set; } = "00";

    /// <summary>URL base del API de transmision en ambiente de pruebas.</summary>
    public string TestBaseUrl { get; set; } = "https://apitest.dtes.mh.gob.sv";

    /// <summary>URL base del API de transmision en ambiente de produccion.</summary>
    public string ProdBaseUrl { get; set; } = "https://api.dtes.mh.gob.sv";

    /// <summary>Ruta del servicio de autenticacion (JWT).</summary>
    public string AuthPath { get; set; } = "/seguridad/auth";

    /// <summary>Ruta del servicio de recepcion de DTE.</summary>
    public string ReceptionPath { get; set; } = "/fesv/recepciondte";

    /// <summary>Ruta del servicio de anulacion (evento de invalidacion).</summary>
    public string InvalidationPath { get; set; } = "/fesv/anulardte";

    /// <summary>Ruta del servicio de recepcion en lote/contingencia.</summary>
    public string ContingencyPath { get; set; } = "/fesv/recepcioncontingencia";

    /// <summary>URL del Firmador local del MH (svfe-api-firmador). Corre en la PC de caja.</summary>
    public string FirmadorUrl { get; set; } = "http://localhost:8113/firmardocumento/";

    /// <summary>
    /// Usuario del API de transmision (credencial de aplicacion MH). PLACEHOLDER: completar.
    /// Normalmente es el NIT del emisor.
    /// </summary>
    public string ApiUser { get; set; } = string.Empty;

    /// <summary>Clave del API de transmision (credencial de aplicacion MH). PLACEHOLDER: completar.</summary>
    public string ApiPassword { get; set; } = string.Empty;

    /// <summary>
    /// Clave privada del certificado .p12/.crt (passwordPri) que usa el Firmador.
    /// PLACEHOLDER: completar con la clave real del certificado emitido por el MH.
    /// </summary>
    public string CertPassword { get; set; } = string.Empty;

    /// <summary>Codigo de establecimiento (4 caracteres) asignado por el emisor. Ej. "0001".</summary>
    public string CodEstablecimiento { get; set; } = "0001";

    /// <summary>Codigo de punto de venta (4 caracteres) del POS. Ej. "0001".</summary>
    public string CodPuntoVenta { get; set; } = "0001";

    /// <summary>URL base de consulta publica del DTE (para el QR del ticket).</summary>
    public string ConsultaPublicaUrl { get; set; } = "https://admin.factura.gob.sv/consultaPublica";

    /// <summary>Tiempo maximo de espera de las llamadas HTTP (segundos).</summary>
    public int HttpTimeoutSeconds { get; set; } = 45;

    /// <summary>Intervalo en minutos entre reintentos de la cola de contingencia.</summary>
    public int ContingencyRetryMinutes { get; set; } = 15;

    /// <summary>Indica si el ambiente activo es produccion.</summary>
    public bool IsProduction => Ambiente == DteConstants.Ambientes.Produccion;

    /// <summary>Devuelve la URL base activa segun el ambiente configurado.</summary>
    public string ActiveBaseUrl => IsProduction ? ProdBaseUrl : TestBaseUrl;
}
