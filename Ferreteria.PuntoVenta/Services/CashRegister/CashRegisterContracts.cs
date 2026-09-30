using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Time;

namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>
/// Códigos de auditoría de las transiciones de una sesión de caja.
/// </summary>
public static class CashSessionAuditActions
{
    /// <summary>Acción registrada al abrir una sesión.</summary>
    public const string Open = "APERTURA";

    /// <summary>Acción registrada al cerrar una sesión.</summary>
    public const string Close = "CIERRE";

    /// <summary>Acción reservada para una cancelación autorizada.</summary>
    public const string Cancel = "CANCELA";

    /// <summary>Nombre lógico de la tabla auditada.</summary>
    public const string TableName = "sales.CashSessions";
}

/// <summary>Clasificación textual de la diferencia del arqueo.</summary>
public enum CashDifferenceClassification
{
    /// <summary>El efectivo contado coincide con el esperado.</summary>
    Balanced,

    /// <summary>El efectivo contado supera el esperado.</summary>
    Surplus,

    /// <summary>El efectivo contado es menor que el esperado.</summary>
    Shortage
}

/// <summary>Pago de una venta usado por el cálculo puro del corte.</summary>
/// <param name="Method">Método de pago normalizado.</param>
/// <param name="Amount">Monto del pago.</param>
/// <param name="Reference">Referencia no sensible para mostrar, si existe.</param>
public sealed record CashRegisterPaymentSnapshot(
    string Method,
    decimal Amount,
    string? Reference = null);

/// <summary>Venta y sus pagos, sin dependencia de EF Core.</summary>
/// <param name="OrderId">Identificador de la orden.</param>
/// <param name="CreatedAtUtc">Fecha de creación en UTC.</param>
/// <param name="Status">Estado persistido de la orden.</param>
/// <param name="Total">Total de la orden.</param>
/// <param name="TaxAmount">IVA persistido de la orden.</param>
/// <param name="Payments">Pagos asociados a la sesión.</param>
/// <param name="DteStatus">Estado del DTE más reciente, si existe.</param>
/// <param name="DocumentNumber">Número de documento o identificador interno.</param>
public sealed record CashRegisterSaleSnapshot(
    Guid OrderId,
    DateTime CreatedAtUtc,
    string Status,
    decimal Total,
    decimal TaxAmount,
    IReadOnlyList<CashRegisterPaymentSnapshot> Payments,
    string? DteStatus = null,
    string? DocumentNumber = null);

/// <summary>Entrada inmutable para calcular el resumen de una sesión.</summary>
/// <param name="SessionId">Identificador de la sesión.</param>
/// <param name="OpeningAmount">Fondo inicial en efectivo.</param>
/// <param name="Sales">Ventas y pagos asociados.</param>
/// <param name="CashRefunds">Devoluciones en efectivo registradas.</param>
/// <remarks>
/// Actualmente <paramref name="CashRefunds"/> debe ser cero porque el esquema no tiene un movimiento
/// de caja para devoluciones; el módulo de devoluciones queda como dependencia futura.
/// </remarks>
public sealed record CashRegisterSnapshot(
    Guid SessionId,
    decimal OpeningAmount,
    IReadOnlyList<CashRegisterSaleSnapshot> Sales,
    decimal CashRefunds = 0m);

/// <summary>Movimiento que se presenta en la tabla del corte.</summary>
/// <param name="OrderId">Orden que originó el movimiento.</param>
/// <param name="CreatedAtUtc">Fecha del movimiento en UTC.</param>
/// <param name="DocumentNumber">Número de documento o referencia interna.</param>
/// <param name="PaymentMethod">Método de pago.</param>
/// <param name="Amount">Monto del pago.</param>
/// <param name="Status">Estado de la orden.</param>
public sealed record CashRegisterMovement(
    Guid OrderId,
    DateTime CreatedAtUtc,
    string DocumentNumber,
    string PaymentMethod,
    decimal Amount,
    string Status)
{
    /// <summary>Hora local de El Salvador para la tabla de caja.</summary>
    public string LocalTimeText => TimeZoneSupport.ToLocalTime(CreatedAtUtc).ToString("HH:mm");
}

/// <summary>Resumen calculado de una sesión abierta o cerrada.</summary>
/// <param name="SessionId">Identificador de la sesión.</param>
/// <param name="OpeningAmount">Fondo inicial.</param>
/// <param name="CashPayments">Pagos en efectivo de ventas completadas.</param>
/// <param name="CardPayments">Pagos con tarjeta de ventas completadas.</param>
/// <param name="TransferPayments">Pagos por transferencia de ventas completadas.</param>
/// <param name="OtherPayments">Pagos con otros métodos.</param>
/// <param name="TotalSold">Total vendido en órdenes completadas.</param>
/// <param name="TaxAmount">IVA de órdenes completadas.</param>
/// <param name="CompletedSales">Cantidad de ventas completadas.</param>
/// <param name="PendingSales">Cantidad de órdenes pendientes.</param>
/// <param name="CancelledSales">Cantidad de órdenes canceladas.</param>
/// <param name="DteCount">Cantidad de DTE asociados a ventas completadas.</param>
/// <param name="ContingencyDteCount">Cantidad de DTE en contingencia.</param>
/// <param name="SalesWithoutDte">Cantidad de ventas completadas sin DTE.</param>
/// <param name="CashRefunds">Devoluciones en efectivo consideradas.</param>
/// <param name="ExpectedCash">Efectivo esperado: fondo + efectivo cobrado - devoluciones.</param>
/// <param name="Movements">Pagos mostrados en la tabla de movimientos.</param>
public sealed record CashRegisterSummary(
    Guid SessionId,
    decimal OpeningAmount,
    decimal CashPayments,
    decimal CardPayments,
    decimal TransferPayments,
    decimal OtherPayments,
    decimal TotalSold,
    decimal TaxAmount,
    int CompletedSales,
    int PendingSales,
    int CancelledSales,
    int DteCount,
    int ContingencyDteCount,
    int SalesWithoutDte,
    decimal CashRefunds,
    decimal ExpectedCash,
    IReadOnlyList<CashRegisterMovement> Movements);

/// <summary>Resultado de calcular una diferencia de efectivo.</summary>
/// <param name="ExpectedCash">Efectivo esperado.</param>
/// <param name="DeclaredCash">Efectivo contado y declarado.</param>
/// <param name="Difference">Declarado menos esperado.</param>
/// <param name="Classification">Clasificación de la diferencia.</param>
/// <param name="RequiresObservation">Indica si debe capturarse una observación.</param>
public sealed record CashDifferenceResult(
    decimal ExpectedCash,
    decimal DeclaredCash,
    decimal Difference,
    CashDifferenceClassification Classification,
    bool RequiresObservation)
{
    /// <summary>Texto visible para cajeros: cuadra, sobrante o faltante.</summary>
    public string DisplayText => Classification switch
    {
        CashDifferenceClassification.Surplus => $"Sobrante ${Difference:0.00}",
        CashDifferenceClassification.Shortage => $"Faltante ${Math.Abs(Difference):0.00}",
        _ => "Cuadra"
    };
}

/// <summary>Datos de un reporte de corte interno.</summary>
/// <param name="CashRegisterCode">Código de caja.</param>
/// <param name="EmployeeDisplayName">Nombre visible del cajero que abrió.</param>
/// <param name="OpenedAtUtc">Hora de apertura.</param>
/// <param name="ClosedAtUtc">Hora de cierre; null para un corte parcial.</param>
/// <param name="DeclaredCash">Efectivo contado.</param>
/// <param name="Summary">Resumen de la sesión.</param>
/// <param name="Notes">Observaciones registradas.</param>
public sealed record CashRegisterReportData(
    string CashRegisterCode,
    string EmployeeDisplayName,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    decimal DeclaredCash,
    CashRegisterSummary Summary,
    string? Notes);

/// <summary>Resultado persistido del cierre de caja.</summary>
/// <param name="SessionId">Identificador de la sesión cerrada.</param>
/// <param name="ExpectedCash">Efectivo esperado persistido.</param>
/// <param name="DeclaredCash">Efectivo declarado persistido.</param>
/// <param name="Difference">Diferencia persistida.</param>
/// <param name="Summary">Resumen tomado dentro de la transacción.</param>
/// <param name="ClosedAtUtc">Hora UTC persistida al confirmar el cierre.</param>
public sealed record CashSessionCloseResult(
    Guid SessionId,
    decimal ExpectedCash,
    decimal DeclaredCash,
    decimal Difference,
    CashRegisterSummary Summary,
    DateTime ClosedAtUtc);

/// <summary>Opciones de operación de una caja física.</summary>
public sealed class CashRegisterOptions
{
    /// <summary>Sección de configuración en <c>appsettings.json</c>.</summary>
    public const string SectionName = "Caja";

    /// <summary>Código físico de la caja activa.</summary>
    public string Codigo { get; set; } = "CAJA-01";

    /// <summary>Umbral absoluto que obliga a registrar observación al cerrar.</summary>
    public decimal UmbralDiferencia { get; set; } = 20m;

    /// <summary>Máximo razonable para fondos y conteos ingresados desde la UI.</summary>
    public decimal MontoMaximo { get; set; } = 100000m;

    /// <summary>Ancho predeterminado del reporte térmico.</summary>
    public int AnchoReporte { get; set; } = 48;
}

/// <summary>Excepción de reglas de apertura, resumen o cierre de caja.</summary>
public sealed class CashSessionException(string message) : InvalidOperationException(message);

