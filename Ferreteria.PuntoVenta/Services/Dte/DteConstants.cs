namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>
/// Constantes del ecosistema DTE del Ministerio de Hacienda de El Salvador
/// (tipos de documento, versiones de esquema, catalogos y estados).
/// </summary>
public static class DteConstants
{
    /// <summary>Ambientes de ejecucion (CAT-001).</summary>
    public static class Ambientes
    {
        /// <summary>Ambiente de pruebas.</summary>
        public const string Pruebas = "00";

        /// <summary>Ambiente de produccion.</summary>
        public const string Produccion = "01";
    }

    /// <summary>Tipos de DTE (CAT-002).</summary>
    public static class TiposDte
    {
        /// <summary>Factura (Consumidor Final).</summary>
        public const string Factura = "01";

        /// <summary>Comprobante de Credito Fiscal.</summary>
        public const string CreditoFiscal = "03";

        /// <summary>Nota de Credito.</summary>
        public const string NotaCredito = "05";
    }

    /// <summary>Version de esquema JSON por tipo de DTE (vigente 2026, normativa 1.2).</summary>
    public static class Versiones
    {
        /// <summary>Version de la Factura (01).</summary>
        public const int Factura = 1;

        /// <summary>Version del Comprobante de Credito Fiscal (03).</summary>
        public const int CreditoFiscal = 3;

        /// <summary>Version de la Nota de Credito (05).</summary>
        public const int NotaCredito = 3;
    }

    /// <summary>Codigo del tributo IVA (CAT-015).</summary>
    public const string CodigoTributoIva = "20";

    /// <summary>Descripcion oficial del tributo IVA.</summary>
    public const string DescripcionTributoIva = "Impuesto al Valor Agregado 13%";

    /// <summary>Estados de respuesta del MH.</summary>
    public static class EstadosMh
    {
        /// <summary>DTE procesado/recibido con sello.</summary>
        public const string Procesado = "PROCESADO";

        /// <summary>DTE rechazado por el MH.</summary>
        public const string Rechazado = "RECHAZADO";

        /// <summary>DTE pendiente de transmision.</summary>
        public const string Pendiente = "PENDIENTE";

        /// <summary>DTE emitido en contingencia (pendiente de sello).</summary>
        public const string Contingencia = "CONTINGENCIA";
    }

    /// <summary>Respuestas textuales de las APIs del MH.</summary>
    public static class RespuestasMh
    {
        /// <summary>Estado exitoso devuelto por recepcion.</summary>
        public const string Recibido = "RECIBIDO";

        /// <summary>Estado exitoso alterno.</summary>
        public const string Procesado = "PROCESADO";

        /// <summary>Estado OK del firmador.</summary>
        public const string Ok = "OK";
    }

    /// <summary>Condicion de la operacion (CAT-016).</summary>
    public static class CondicionOperacion
    {
        /// <summary>Contado.</summary>
        public const int Contado = 1;

        /// <summary>Credito.</summary>
        public const int Credito = 2;
    }

    /// <summary>Formas de pago DTE (CAT-017).</summary>
    public static class FormasPago
    {
        /// <summary>Billetes y monedas (efectivo).</summary>
        public const string Efectivo = "01";

        /// <summary>Tarjeta debito/credito.</summary>
        public const string Tarjeta = "02";

        /// <summary>Transferencia / deposito bancario.</summary>
        public const string Transferencia = "05";

        /// <summary>Otro medio.</summary>
        public const string Otro = "99";
    }

    /// <summary>Tipos de documento de identificacion del receptor (CAT-022).</summary>
    public static class TiposDocumentoReceptor
    {
        /// <summary>NIT.</summary>
        public const string Nit = "36";

        /// <summary>DUI.</summary>
        public const string Dui = "13";

        /// <summary>Otro.</summary>
        public const string Otro = "37";
    }

    /// <summary>Convierte la forma de pago interna del POS a codigo de catalogo CAT-017.</summary>
    /// <param name="internalMethod">EFECTIVO, TARJETA, TRANSFERENCIA u OTRO.</param>
    /// <returns>Codigo CAT-017 correspondiente.</returns>
    public static string MapPaymentMethodToCatalog(string? internalMethod)
    {
        return internalMethod?.Trim().ToUpperInvariant() switch
        {
            "EFECTIVO" => FormasPago.Efectivo,
            "TARJETA" => FormasPago.Tarjeta,
            "TRANSFERENCIA" => FormasPago.Transferencia,
            _ => FormasPago.Otro
        };
    }
}
