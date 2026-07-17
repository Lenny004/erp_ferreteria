using Newtonsoft.Json;

namespace Ferreteria.PuntoVenta.Models.Dte.Json;

// ============================================================================
//  DTOs del Documento Tributario Electronico (DTE) - Ministerio de Hacienda
//  de El Salvador. Estructura JSON comun de 5 secciones: identificacion,
//  emisor, receptor, cuerpoDocumento y resumen; mas documentoRelacionado,
//  extension y apendice. Cubren: 01 Factura, 03 Comprobante Credito Fiscal
//  y 05 Nota de Credito.
//
//  Los nombres de propiedad JSON son sensibles a mayusculas y deben coincidir
//  EXACTAMENTE con el esquema oficial del MH. Los campos que el esquema exige
//  presentes aunque sean nulos se serializan con NullValueHandling.Include.
// ============================================================================

/// <summary>Raiz del Documento Tributario Electronico transmitido al MH.</summary>
public sealed class DteDocument
{
    [JsonProperty("identificacion")]
    public DteIdentificacion Identificacion { get; set; } = new();

    [JsonProperty("documentoRelacionado")]
    public List<DteDocumentoRelacionado>? DocumentoRelacionado { get; set; }

    [JsonProperty("emisor")]
    public DteEmisor Emisor { get; set; } = new();

    [JsonProperty("receptor")]
    public DteReceptor? Receptor { get; set; }

    [JsonProperty("otrosDocumentos")]
    public object? OtrosDocumentos { get; set; }

    [JsonProperty("ventaTercero")]
    public object? VentaTercero { get; set; }

    [JsonProperty("cuerpoDocumento")]
    public List<DteItem> CuerpoDocumento { get; set; } = new();

    [JsonProperty("resumen")]
    public DteResumen Resumen { get; set; } = new();

    [JsonProperty("extension")]
    public DteExtension? Extension { get; set; }

    [JsonProperty("apendice")]
    public List<DteApendiceCampo>? Apendice { get; set; }
}

/// <summary>Seccion identificacion del DTE.</summary>
public sealed class DteIdentificacion
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("ambiente")]
    public string Ambiente { get; set; } = "00";

    [JsonProperty("tipoDte")]
    public string TipoDte { get; set; } = "01";

    [JsonProperty("numeroControl")]
    public string NumeroControl { get; set; } = string.Empty;

    [JsonProperty("codigoGeneracion")]
    public string CodigoGeneracion { get; set; } = string.Empty;

    /// <summary>1 = modelo previo (normal), 2 = contingencia.</summary>
    [JsonProperty("tipoModelo")]
    public int TipoModelo { get; set; } = 1;

    /// <summary>1 = transmision normal, 2 = contingencia.</summary>
    [JsonProperty("tipoOperacion")]
    public int TipoOperacion { get; set; } = 1;

    [JsonProperty("tipoContingencia")]
    public int? TipoContingencia { get; set; }

    [JsonProperty("motivoContin")]
    public string? MotivoContin { get; set; }

    [JsonProperty("fecEmi")]
    public string FecEmi { get; set; } = string.Empty;

    [JsonProperty("horEmi")]
    public string HorEmi { get; set; } = string.Empty;

    [JsonProperty("tipoMoneda")]
    public string TipoMoneda { get; set; } = "USD";
}

/// <summary>Documento relacionado (usado por notas de credito 05).</summary>
public sealed class DteDocumentoRelacionado
{
    /// <summary>1 = DTE, 2 = documento fisico.</summary>
    [JsonProperty("tipoDocumento")]
    public string TipoDocumento { get; set; } = "1";

    /// <summary>1 = normal.</summary>
    [JsonProperty("tipoGeneracion")]
    public int TipoGeneracion { get; set; } = 2;

    [JsonProperty("numeroDocumento")]
    public string NumeroDocumento { get; set; } = string.Empty;

    [JsonProperty("fechaEmision")]
    public string FechaEmision { get; set; } = string.Empty;
}

/// <summary>Datos del emisor del DTE.</summary>
public sealed class DteEmisor
{
    [JsonProperty("nit")]
    public string Nit { get; set; } = string.Empty;

    [JsonProperty("nrc")]
    public string Nrc { get; set; } = string.Empty;

    [JsonProperty("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonProperty("codActividad")]
    public string CodActividad { get; set; } = string.Empty;

    [JsonProperty("descActividad")]
    public string DescActividad { get; set; } = string.Empty;

    [JsonProperty("nombreComercial")]
    public string? NombreComercial { get; set; }

    /// <summary>01 = casa matriz, 02 = sucursal, etc. (CAT-009).</summary>
    [JsonProperty("tipoEstablecimiento")]
    public string TipoEstablecimiento { get; set; } = "01";

    [JsonProperty("direccion")]
    public DteDireccion Direccion { get; set; } = new();

    [JsonProperty("telefono")]
    public string? Telefono { get; set; }

    [JsonProperty("correo")]
    public string Correo { get; set; } = string.Empty;

    [JsonProperty("codEstableMH")]
    public string? CodEstableMH { get; set; }

    [JsonProperty("codEstable")]
    public string? CodEstable { get; set; }

    [JsonProperty("codPuntoVentaMH")]
    public string? CodPuntoVentaMH { get; set; }

    [JsonProperty("codPuntoVenta")]
    public string? CodPuntoVenta { get; set; }
}

/// <summary>Datos del receptor/cliente del DTE.</summary>
public sealed class DteReceptor
{
    /// <summary>Tipo de documento del receptor (CAT-022): 36 NIT, 13 DUI, etc. Solo FE.</summary>
    [JsonProperty("tipoDocumento", NullValueHandling = NullValueHandling.Ignore)]
    public string? TipoDocumento { get; set; }

    [JsonProperty("numDocumento", NullValueHandling = NullValueHandling.Ignore)]
    public string? NumDocumento { get; set; }

    /// <summary>NIT del receptor (usado en CCF 03).</summary>
    [JsonProperty("nit", NullValueHandling = NullValueHandling.Ignore)]
    public string? Nit { get; set; }

    [JsonProperty("nrc")]
    public string? Nrc { get; set; }

    [JsonProperty("nombre")]
    public string? Nombre { get; set; }

    [JsonProperty("codActividad")]
    public string? CodActividad { get; set; }

    [JsonProperty("descActividad")]
    public string? DescActividad { get; set; }

    [JsonProperty("nombreComercial", NullValueHandling = NullValueHandling.Ignore)]
    public string? NombreComercial { get; set; }

    [JsonProperty("direccion")]
    public DteDireccion? Direccion { get; set; }

    [JsonProperty("telefono")]
    public string? Telefono { get; set; }

    [JsonProperty("correo")]
    public string? Correo { get; set; }
}

/// <summary>Direccion territorial segun catalogo CAT-012/013 del MH.</summary>
public sealed class DteDireccion
{
    [JsonProperty("departamento")]
    public string Departamento { get; set; } = string.Empty;

    [JsonProperty("municipio")]
    public string Municipio { get; set; } = string.Empty;

    [JsonProperty("complemento")]
    public string Complemento { get; set; } = string.Empty;
}

/// <summary>Linea de detalle (cuerpoDocumento) del DTE.</summary>
public sealed class DteItem
{
    [JsonProperty("numItem")]
    public int NumItem { get; set; }

    /// <summary>1 = bien, 2 = servicio, 3 = ambos, 4 = otros.</summary>
    [JsonProperty("tipoItem")]
    public int TipoItem { get; set; } = 1;

    [JsonProperty("numeroDocumento")]
    public string? NumeroDocumento { get; set; }

    [JsonProperty("cantidad")]
    public decimal Cantidad { get; set; }

    [JsonProperty("codigo")]
    public string? Codigo { get; set; }

    [JsonProperty("codTributo")]
    public string? CodTributo { get; set; }

    /// <summary>Unidad de medida (CAT-014). 59 = unidad, 58 = metro, etc.</summary>
    [JsonProperty("uniMedida")]
    public int UniMedida { get; set; } = 59;

    [JsonProperty("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonProperty("precioUni")]
    public decimal PrecioUni { get; set; }

    [JsonProperty("montoDescu")]
    public decimal MontoDescu { get; set; }

    [JsonProperty("ventaNoSuj")]
    public decimal VentaNoSuj { get; set; }

    [JsonProperty("ventaExenta")]
    public decimal VentaExenta { get; set; }

    [JsonProperty("ventaGravada")]
    public decimal VentaGravada { get; set; }

    /// <summary>Codigos de tributo aplicados al item (ej. ["20"] IVA en CCF). Null en FE.</summary>
    [JsonProperty("tributos")]
    public List<string>? Tributos { get; set; }

    [JsonProperty("psv")]
    public decimal Psv { get; set; }

    [JsonProperty("noGravado")]
    public decimal NoGravado { get; set; }

    /// <summary>IVA del item; solo aplica en Factura (01), no en CCF.</summary>
    [JsonProperty("ivaItem", NullValueHandling = NullValueHandling.Ignore)]
    public decimal? IvaItem { get; set; }
}

/// <summary>Tributo del resumen (ej. IVA 13%).</summary>
public sealed class DteTributoResumen
{
    [JsonProperty("codigo")]
    public string Codigo { get; set; } = "20";

    [JsonProperty("descripcion")]
    public string Descripcion { get; set; } = "Impuesto al Valor Agregado 13%";

    [JsonProperty("valor")]
    public decimal Valor { get; set; }
}

/// <summary>Forma de pago del resumen.</summary>
public sealed class DtePago
{
    /// <summary>Forma de pago (CAT-017): 01 efectivo, 02 tarjeta, 05 transferencia...</summary>
    [JsonProperty("codigo")]
    public string Codigo { get; set; } = "01";

    [JsonProperty("montoPago")]
    public decimal MontoPago { get; set; }

    [JsonProperty("referencia")]
    public string? Referencia { get; set; }

    /// <summary>Plazo (CAT-018) para operaciones a credito; null al contado.</summary>
    [JsonProperty("plazo")]
    public string? Plazo { get; set; }

    [JsonProperty("periodo")]
    public int? Periodo { get; set; }
}

/// <summary>Resumen de montos del DTE.</summary>
public sealed class DteResumen
{
    [JsonProperty("totalNoSuj")]
    public decimal TotalNoSuj { get; set; }

    [JsonProperty("totalExenta")]
    public decimal TotalExenta { get; set; }

    [JsonProperty("totalGravada")]
    public decimal TotalGravada { get; set; }

    [JsonProperty("subTotalVentas")]
    public decimal SubTotalVentas { get; set; }

    [JsonProperty("descuNoSuj")]
    public decimal DescuNoSuj { get; set; }

    [JsonProperty("descuExenta")]
    public decimal DescuExenta { get; set; }

    [JsonProperty("descuGravada")]
    public decimal DescuGravada { get; set; }

    [JsonProperty("porcentajeDescuento")]
    public decimal PorcentajeDescuento { get; set; }

    [JsonProperty("totalDescu")]
    public decimal TotalDescu { get; set; }

    [JsonProperty("tributos")]
    public List<DteTributoResumen>? Tributos { get; set; }

    [JsonProperty("subTotal")]
    public decimal SubTotal { get; set; }

    /// <summary>IVA percibido; solo CCF (03).</summary>
    [JsonProperty("ivaPerci1", NullValueHandling = NullValueHandling.Ignore)]
    public decimal? IvaPerci1 { get; set; }

    [JsonProperty("ivaRete1")]
    public decimal IvaRete1 { get; set; }

    [JsonProperty("reteRenta")]
    public decimal ReteRenta { get; set; }

    /// <summary>IVA total; solo Factura (01) lo lleva en el resumen.</summary>
    [JsonProperty("totalIva", NullValueHandling = NullValueHandling.Ignore)]
    public decimal? TotalIva { get; set; }

    [JsonProperty("montoTotalOperacion")]
    public decimal MontoTotalOperacion { get; set; }

    [JsonProperty("totalNoGravado")]
    public decimal TotalNoGravado { get; set; }

    [JsonProperty("totalPagar")]
    public decimal TotalPagar { get; set; }

    [JsonProperty("totalLetras")]
    public string TotalLetras { get; set; } = string.Empty;

    /// <summary>1 = contado, 2 = credito, 3 = otro.</summary>
    [JsonProperty("condicionOperacion")]
    public int CondicionOperacion { get; set; } = 1;

    [JsonProperty("pagos")]
    public List<DtePago>? Pagos { get; set; }

    [JsonProperty("numPagoElectronico")]
    public string? NumPagoElectronico { get; set; }

    [JsonProperty("saldoFavor")]
    public decimal SaldoFavor { get; set; }
}

/// <summary>Extension del DTE (datos de entrega/recepcion).</summary>
public sealed class DteExtension
{
    [JsonProperty("nombEntrega")]
    public string? NombEntrega { get; set; }

    [JsonProperty("docuEntrega")]
    public string? DocuEntrega { get; set; }

    [JsonProperty("nombRecibe")]
    public string? NombRecibe { get; set; }

    [JsonProperty("docuRecibe")]
    public string? DocuRecibe { get; set; }

    [JsonProperty("observaciones")]
    public string? Observaciones { get; set; }

    [JsonProperty("placaVehiculo")]
    public string? PlacaVehiculo { get; set; }
}

/// <summary>Campo del apendice (informacion adicional).</summary>
public sealed class DteApendiceCampo
{
    [JsonProperty("campo")]
    public string Campo { get; set; } = string.Empty;

    [JsonProperty("etiqueta")]
    public string Etiqueta { get; set; } = string.Empty;

    [JsonProperty("valor")]
    public string Valor { get; set; } = string.Empty;
}
