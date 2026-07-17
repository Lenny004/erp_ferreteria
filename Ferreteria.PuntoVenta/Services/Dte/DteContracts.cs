namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>Solicitud para emitir un DTE a partir de una orden completada.</summary>
/// <param name="OrderId">Orden de venta ya completada.</param>
/// <param name="TipoDte">Tipo de DTE: 01 (factura) o 03 (credito fiscal).</param>
/// <param name="CustomerId">Cliente (obligatorio para credito fiscal).</param>
public sealed record EmitDteRequest(Guid OrderId, string TipoDte, Guid? CustomerId);

/// <summary>Resultado de la emision de un DTE.</summary>
/// <param name="DteIssuedId">Id del registro <c>dte.DteIssued</c>.</param>
/// <param name="OrderId">Orden asociada.</param>
/// <param name="DteType">Tipo de DTE emitido.</param>
/// <param name="NumeroControl">Numero de control asignado.</param>
/// <param name="CodigoGeneracion">Codigo de generacion (UUID).</param>
/// <param name="MhStatus">Estado ante el MH (PROCESADO, CONTINGENCIA, RECHAZADO).</param>
/// <param name="SelloRecibido">Sello de recepcion del MH (si fue aceptado).</param>
/// <param name="Ambiente">Ambiente usado (00/01).</param>
/// <param name="ConsultaUrl">Enlace de consulta publica para el QR del ticket.</param>
/// <param name="TotalPagar">Total a pagar del documento.</param>
/// <param name="TotalEnLetras">Total en letras.</param>
/// <param name="Message">Mensaje o motivo (rechazo/contingencia).</param>
public sealed record EmitDteResult(
    Guid DteIssuedId,
    Guid OrderId,
    string DteType,
    string NumeroControl,
    string CodigoGeneracion,
    string MhStatus,
    string? SelloRecibido,
    string Ambiente,
    string ConsultaUrl,
    decimal TotalPagar,
    string TotalEnLetras,
    string? Message)
{
    /// <summary>Indica si el DTE quedo en contingencia (pendiente de sello).</summary>
    public bool IsContingency => MhStatus == DteConstants.EstadosMh.Contingencia;

    /// <summary>Indica si el DTE fue sellado por el MH.</summary>
    public bool IsAccepted => MhStatus == DteConstants.EstadosMh.Procesado;

    /// <summary>Indica si el DTE fue rechazado por el MH.</summary>
    public bool IsRejected => MhStatus == DteConstants.EstadosMh.Rechazado;
}
