using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>
/// Restricciones adicionales utilizadas por las pantallas migradas en la fase E3.
/// </summary>
public static partial class FieldConstraints
{
    /// <summary>Restricciones de impresoras configurables desde el POS.</summary>
    public static class Printers
    {
        /// <summary>Longitud máxima del nombre visible de la impresora.</summary>
        [FieldConstraint("system", "Printers", "Name", FieldConstraintKind.MaxLength, typeof(Printer), nameof(Printer.Name))]
        public const int NameMaxLength = 200;

        /// <summary>Longitud máxima del tipo de conexión.</summary>
        [FieldConstraint("system", "Printers", "ConnectionType", FieldConstraintKind.MaxLength, typeof(Printer), nameof(Printer.ConnectionType))]
        public const int ConnectionTypeMaxLength = 10;

        /// <summary>Longitud máxima de la dirección IP.</summary>
        [FieldConstraint("system", "Printers", "IpAddress", FieldConstraintKind.MaxLength, typeof(Printer), nameof(Printer.IpAddress))]
        public const int IpAddressMaxLength = 15;
    }

    /// <summary>Restricciones de la cabecera de devoluciones POS.</summary>
    public static class SaleReturns
    {
        /// <summary>Longitud máxima del tipo de devolución.</summary>
        [FieldConstraint("sales", "Returns", "ReturnType", FieldConstraintKind.MaxLength, typeof(SaleReturn), nameof(SaleReturn.ReturnType))]
        public const int ReturnTypeMaxLength = 10;

        /// <summary>Longitud máxima del código de motivo.</summary>
        [FieldConstraint("sales", "Returns", "ReasonCode", FieldConstraintKind.MaxLength, typeof(SaleReturn), nameof(SaleReturn.ReasonCode))]
        public const int ReasonCodeMaxLength = 30;

        /// <summary>Longitud máxima del método de reintegro.</summary>
        [FieldConstraint("sales", "Returns", "RefundMethod", FieldConstraintKind.MaxLength, typeof(SaleReturn), nameof(SaleReturn.RefundMethod))]
        public const int RefundMethodMaxLength = 20;
    }
}
