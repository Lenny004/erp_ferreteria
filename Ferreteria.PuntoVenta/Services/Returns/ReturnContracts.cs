using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Services.Dte;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Constantes del flujo de devoluciones.</summary>
public static class ReturnDomainConstants
{
    /// <summary>Tipos de devolución.</summary>
    public static class Types
    {
        /// <summary>Devolución que agota todas las líneas de la orden.</summary>
        public const string Total = "TOTAL";

        /// <summary>Devolución que deja alguna cantidad disponible.</summary>
        public const string Partial = "PARCIAL";
    }

    /// <summary>Estados persistidos de una devolución.</summary>
    public static class Statuses
    {
        /// <summary>Devolución registrada como completada.</summary>
        public const string Completed = "COMPLETADA";

        /// <summary>Estado reservado para una anulación futura.</summary>
        public const string Voided = "ANULADA";
    }

    /// <summary>Estados fiscales internos; no representan una emisión real.</summary>
    public static class FiscalStatuses
    {
        /// <summary>Documento fiscal pendiente de gestionar.</summary>
        public const string Pending = "PENDIENTE";

        /// <summary>Documento fiscal emitido por un flujo futuro.</summary>
        public const string Issued = "EMITIDO";

        /// <summary>Requiere validación de contador o normativa MH.</summary>
        public const string RequiresValidation = "REQUIERE_VALID";

        /// <summary>No aplica documento fiscal.</summary>
        public const string NotApplicable = "NO_APLICA";
    }

    /// <summary>Métodos de reintegro permitidos.</summary>
    public static class RefundMethods
    {
        /// <summary>Reintegro en efectivo; su tratamiento legal queda a verificar.</summary>
        public const string Cash = "EFECTIVO";

        /// <summary>Reverso del pago con tarjeta.</summary>
        public const string Card = "TARJETA";

        /// <summary>Reintegro mediante transferencia.</summary>
        public const string Transfer = "TRANSFERENCIA";

        /// <summary>Devolución sin reintegro monetario.</summary>
        public const string None = "NINGUNO";
    }

    /// <summary>Tipos de movimientos de efectivo del esquema persistido.</summary>
    public static class CashMovementTypes
    {
        /// <summary>Egreso de efectivo por una devolución.</summary>
        public const string CashRefund = "DEVOLUCION_EFECTIVO";

        /// <summary>Retiro manual de efectivo, sin flujo en esta fase.</summary>
        public const string Withdrawal = "RETIRO";

        /// <summary>Ingreso manual de efectivo, sin flujo en esta fase.</summary>
        public const string Deposit = "INGRESO";
    }
}

/// <summary>Códigos de auditoría del módulo de devoluciones.</summary>
public static class ReturnAuditActions
{
    /// <summary>Código persistido de la devolución.</summary>
    public const string Return = "DEVOLUCION";

    /// <summary>Código persistido del reintegro.</summary>
    public const string Refund = "REINTEGRO";

    /// <summary>Nombre lógico del evento de devolución.</summary>
    public const string ReturnEvent = "DEVOLUCION";

    /// <summary>Nombre lógico del evento de reintegro.</summary>
    public const string RefundEvent = "REINTEGRO";

    /// <summary>Nombre de la tabla persistida de devoluciones.</summary>
    public const string ReturnsTableName = "sales.Returns";

    /// <summary>Nombre de la tabla persistida de movimientos de caja.</summary>
    public const string CashMovementsTableName = "sales.CashMovements";
}

/// <summary>Filtro de búsqueda de ventas que pueden revisarse para una devolución.</summary>
/// <param name="FromUtc">Fecha UTC inicial inclusiva.</param>
/// <param name="ToUtc">Fecha UTC final exclusiva.</param>
/// <param name="SearchText">Texto, número de control, cliente, NIT o id corto.</param>
/// <param name="Page">Página solicitada.</param>
/// <param name="PageSize">Cantidad máxima de filas solicitadas.</param>
public sealed record ReturnableSalesFilter(DateTime? FromUtc = null, DateTime? ToUtc = null, string? SearchText = null, int Page = 1, int PageSize = 25)
{
    /// <summary>Tamaño máximo de página permitido por el servicio.</summary>
    public const int MaximumPageSize = 50;

    /// <summary>Normaliza texto, fechas y paginación sin consultar la base de datos.</summary>
    /// <returns>Filtro normalizado.</returns>
    public ReturnableSalesFilter Normalize()
    {
        var from = FromUtc?.ToUniversalTime();
        var to = ToUtc?.ToUniversalTime();
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("El inicio del rango no puede ser posterior al final.");
        }

        return this with { FromUtc = from, ToUtc = to, SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim().Normalize(), Page = Math.Max(1, Page), PageSize = Math.Clamp(PageSize, 1, MaximumPageSize) };
    }
}

/// <summary>Resumen de una venta encontrada para revisión.</summary>
/// <param name="OrderId">Identificador interno de la orden.</param>
/// <param name="CreatedAtUtc">Fecha de la venta en UTC.</param>
/// <param name="CustomerDisplayName">Nombre del cliente o consumidor final.</param>
/// <param name="CustomerNit">NIT del cliente, si existe.</param>
/// <param name="OrderType">Tipo original de la orden.</param>
/// <param name="Status">Estado original de la orden.</param>
/// <param name="DteType">Tipo del DTE original, si existe.</param>
/// <param name="ControlNumber">Número de control original, si existe.</param>
/// <param name="Subtotal">Subtotal original.</param>
/// <param name="DiscountAmount">Descuento de encabezado original.</param>
/// <param name="TaxAmount">IVA original.</param>
/// <param name="Total">Total original.</param>
public sealed record ReturnableSaleSummary(Guid OrderId, DateTime CreatedAtUtc, string CustomerDisplayName, string? CustomerNit, string OrderType, string Status, string? DteType, string? ControlNumber, decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal Total)
{
    /// <summary>Identificador corto para mostrar sin inventar un número fiscal.</summary>
    public string ShortOrderId => OrderId.ToString("N")[..8].ToUpperInvariant();
}

/// <summary>Línea original disponible para revisión de devolución.</summary>
/// <param name="OrderDetailId">Identificador de la línea original.</param>
/// <param name="ProductId">Producto vendido.</param>
/// <param name="ProductCode">Código original del producto.</param>
/// <param name="ProductDescription">Descripción original del producto.</param>
/// <param name="SoldQuantity">Cantidad vendida en la presentación original.</param>
/// <param name="AlreadyReturnedQuantity">Cantidad ya devuelta registrada por la fuente disponible.</param>
/// <param name="AvailableQuantity">Cantidad que todavía puede solicitarse.</param>
/// <param name="UnitPrice">Precio unitario original.</param>
/// <param name="Subtotal">Subtotal bruto original de la línea.</param>
/// <param name="DiscountAmount">Descuento original de la línea.</param>
/// <param name="UnitsPerPackage">Factor de presentación original.</param>
/// <param name="UnitCost">Costo original congelado en la venta.</param>
/// <param name="OrderId">Orden a la que pertenece la línea.</param>
public sealed record ReturnableLine(Guid OrderDetailId, Guid ProductId, string ProductCode, string ProductDescription, decimal SoldQuantity, decimal AlreadyReturnedQuantity, decimal AvailableQuantity, decimal UnitPrice, decimal Subtotal, decimal DiscountAmount, decimal UnitsPerPackage, decimal UnitCost, Guid OrderId = default);

/// <summary>Solicitud inmutable de devolución validada en el servidor.</summary>
/// <param name="ClientRequestId">Identificador de idempotencia de la solicitud.</param>
/// <param name="OrderId">Orden original.</param>
/// <param name="EmployeeId">Empleado que ejecuta la operación.</param>
/// <param name="AuthorizedByEmployeeId">Empleado que autoriza.</param>
/// <param name="ReasonCode">Código de motivo configurable.</param>
/// <param name="Notes">Observación de la devolución.</param>
/// <param name="RefundMethod">Método de reintegro permitido.</param>
/// <param name="Lines">Líneas y cantidades solicitadas.</param>
/// <param name="RefundAmount">Monto solicitado; debe coincidir con el crédito o ser cero para NINGUNO.</param>
public sealed record ReturnRequest(Guid ClientRequestId, Guid OrderId, Guid EmployeeId, Guid AuthorizedByEmployeeId, string ReasonCode, string? Notes, string RefundMethod, IReadOnlyList<ReturnLineRequest> Lines, decimal RefundAmount = 0m);

/// <summary>Cantidad solicitada para una línea original.</summary>
/// <param name="OrderDetailId">Línea original.</param>
/// <param name="Quantity">Cantidad a devolver en la misma unidad de la venta.</param>
/// <param name="Restock">Indica si la cantidad debe reingresar al inventario.</param>
public sealed record ReturnLineRequest(Guid OrderDetailId, decimal Quantity, bool Restock = true);

/// <summary>Totales de crédito ya registrados para una línea.</summary>
/// <param name="Quantity">Cantidad ya devuelta.</param>
/// <param name="Subtotal">Importe bruto ya acreditado.</param>
/// <param name="DiscountAmount">Descuento ya acreditado.</param>
/// <param name="TaxAmount">IVA ya acreditado.</param>
/// <param name="Total">Total ya acreditado.</param>
public sealed record ReturnedLineCredit(decimal Quantity, decimal Subtotal = 0m, decimal DiscountAmount = 0m, decimal TaxAmount = 0m, decimal Total = 0m);

/// <summary>Resultado de lectura de devoluciones históricas.</summary>
/// <param name="Lines">Cantidades y créditos por línea original.</param>
/// <param name="IsAuthoritative">Indica si la fuente es persistente y confiable.</param>
public sealed record ReturnedQuantityReadResult(IReadOnlyDictionary<Guid, ReturnedLineCredit> Lines, bool IsAuthoritative);

/// <summary>Crédito calculado para una línea solicitada.</summary>
/// <param name="OrderDetailId">Línea original.</param>
/// <param name="Quantity">Cantidad que se devolvería.</param>
/// <param name="Subtotal">Importe bruto de la línea.</param>
/// <param name="DiscountAmount">Descuento prorrateado.</param>
/// <param name="TaxAmount">IVA calculado.</param>
/// <param name="Total">Crédito total de la línea.</param>
/// <param name="RestockCost">Costo original de reingreso.</param>
/// <param name="RestockQuantity">Cantidad que volvería a inventario.</param>
/// <param name="UnitPrice">Precio unitario original.</param>
/// <param name="UnitsPerPackage">Factor de presentación original.</param>
/// <param name="UnitCost">Costo unitario original.</param>
/// <param name="Restocked">Indica si la línea reingresa al inventario.</param>
public sealed record ReturnCreditLine(Guid OrderDetailId, decimal Quantity, decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal Total, decimal RestockCost, decimal RestockQuantity, decimal UnitPrice = 0m, decimal UnitsPerPackage = 1m, decimal UnitCost = 0m, bool Restocked = true);

/// <summary>Resultado puro del cálculo económico de una devolución.</summary>
/// <param name="Lines">Crédito calculado por línea.</param>
/// <param name="Subtotal">Subtotal bruto devuelto.</param>
/// <param name="DiscountAmount">Descuento devuelto.</param>
/// <param name="TaxAmount">IVA devuelto.</param>
/// <param name="Total">Total del crédito.</param>
/// <param name="RestockCost">Costo total de reingreso.</param>
/// <param name="ReturnType">TOTAL o PARCIAL.</param>
public sealed record ReturnCalculationResult(IReadOnlyList<ReturnCreditLine> Lines, decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal Total, decimal RestockCost, string ReturnType);

/// <summary>Decisión fiscal prevista por una política intercambiable.</summary>
/// <param name="FiscalStatus">Estado fiscal interno.</param>
/// <param name="RequiredDocument">Documento que podría corresponder, sin emitirlo.</param>
/// <param name="UserMessage">Mensaje comprensible para el usuario.</param>
public sealed record ReturnFiscalDecision(string FiscalStatus, string? RequiredDocument, string UserMessage);

/// <summary>Resultado del servicio cuando una devolución se registra o se recupera por idempotencia.</summary>
/// <param name="ClientRequestId">Idempotencia de la operación.</param>
/// <param name="OrderId">Orden original.</param>
/// <param name="Calculation">Crédito calculado.</param>
/// <param name="FiscalDecision">Estado fiscal previsto.</param>
/// <param name="ReturnId">Identificador persistido de la devolución.</param>
/// <param name="AuthorizedByEmployeeId">Identificador persistido del empleado autorizador.</param>
/// <param name="RefundMethod">Método de reintegro persistido.</param>
/// <param name="RefundAmount">Monto de reintegro persistido.</param>
/// <param name="EmployeeId">Empleado que ejecutó la operación.</param>
public sealed record ReturnResult(
    Guid ClientRequestId,
    Guid OrderId,
    ReturnCalculationResult Calculation,
    ReturnFiscalDecision FiscalDecision,
    Guid ReturnId = default,
    Guid AuthorizedByEmployeeId = default,
    string? RefundMethod = null,
    decimal RefundAmount = 0m,
    Guid EmployeeId = default);

/// <summary>Capacidades habilitadas del servicio de devoluciones.</summary>
/// <param name="CanConfirmReturns">Indica si existe persistencia autoritativa para confirmar.</param>
/// <param name="Message">Explicación visible cuando la confirmación está bloqueada.</param>
public sealed record ReturnCapabilities(bool CanConfirmReturns, string Message);

/// <summary>Opciones de motivos y comportamiento visible del comprobante.</summary>
public sealed class ReturnOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "Devoluciones";

    /// <summary>Plazo comercial/legal, pendiente de verificación.</summary>
    public int? PlazoDias { get; set; }

    /// <summary>Ventana operativa por defecto para buscar ventas.</summary>
    public int DiasBusquedaMaximos { get; set; } = 90;

    /// <summary>Catálogo configurable de motivos.</summary>
    public IList<ReturnReasonOption> Motivos { get; set; } = new List<ReturnReasonOption>();

    /// <summary>Métodos de reintegro que se pueden elegir.</summary>
    public IList<string> MetodosReintegroPermitidos { get; set; } = new List<string>();

    /// <summary>Leyenda provisional del comprobante interno; a verificar con contador / normativa MH.</summary>
    public string LeyendaComprobante { get; set; } = "Comprobante interno de devolución. No es documento fiscal.";

    /// <summary>Ancho térmico permitido, equivalente a <c>Caja:AnchoReporte</c>.</summary>
    public int AnchoComprobante { get; set; } = 48;

    /// <summary>Crea opciones completas para pruebas y uso fuera del host.</summary>
    /// <returns>Opciones con el catálogo predeterminado y los cuatro reintegros válidos.</returns>
    public static ReturnOptions CreateDefault()
    {
        var options = new ReturnOptions { Motivos = CreateDefaultReasons(), MetodosReintegroPermitidos = new List<string>(ValidRefundMethods) };
        ApplyDefaults(options);
        return options;
    }

    /// <summary>Completa y normaliza opciones enlazadas desde configuración.</summary>
    /// <param name="options">Instancia que modificará el binder de opciones.</param>
    /// <remarks>La whitelist de reintegros excluye valores no contractuales; lo fiscal queda a verificar con contador / normativa MH.</remarks>
    public static void ApplyDefaults(ReturnOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Motivos ??= new List<ReturnReasonOption>();
        options.MetodosReintegroPermitidos ??= new List<string>();
        if (options.Motivos.Count == 0)
        {
            options.Motivos = CreateDefaultReasons();
        }

        var uniqueReasons = new List<ReturnReasonOption>();
        var reasonCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reason in options.Motivos)
        {
            var code = reason.Code?.Trim() ?? string.Empty;
            if (code.Length is < 1 or > 30)
            {
                throw new InvalidOperationException("Cada código de motivo debe tener entre 1 y 30 caracteres.");
            }

            if (reasonCodes.Add(code))
            {
                uniqueReasons.Add(reason with { Code = code });
            }
        }

        options.Motivos = uniqueReasons;
        var validMethods = new HashSet<string>(ValidRefundMethods, StringComparer.OrdinalIgnoreCase);
        var uniqueMethods = options.MetodosReintegroPermitidos
            .Select(method => method?.Trim() ?? string.Empty)
            .Where(validMethods.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(method => ValidRefundMethods.First(valid => string.Equals(valid, method, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        options.MetodosReintegroPermitidos = uniqueMethods.Count == 0 ? new List<string>(ValidRefundMethods) : uniqueMethods;
    }

    /// <summary>Métodos de reintegro admitidos por el contrato de devoluciones.</summary>
    public static IReadOnlyList<string> ValidRefundMethods { get; } = new[]
    {
        ReturnDomainConstants.RefundMethods.Cash,
        ReturnDomainConstants.RefundMethods.Card,
        ReturnDomainConstants.RefundMethods.Transfer,
        ReturnDomainConstants.RefundMethods.None
    };

    private static IList<ReturnReasonOption> CreateDefaultReasons() => new List<ReturnReasonOption>
    {
        new("PRODUCTO_DEFECTUOSO", "Producto defectuoso", false),
        new("ERROR_VENTA", "Error de venta", false),
        new("CAMBIO", "Cambio", false),
        new("ANULACION_TOTAL", "Anulación total", true)
    };
}

/// <summary>Motivo configurable que puede exigir observación.</summary>
/// <param name="Code">Código persistido.</param>
/// <param name="Label">Texto visible.</param>
/// <param name="RequiresNotes">Indica si la observación es obligatoria.</param>
public sealed record ReturnReasonOption(string Code, string Label, bool RequiresNotes);

/// <summary>Política fiscal que decide el estado previsto sin emitir DTE.</summary>
public interface IReturnFiscalPolicy
{
    /// <summary>Decide el estado fiscal para una devolución.</summary>
    /// <param name="originalDteType">Tipo del DTE original o <c>null</c>.</param>
    /// <param name="returnType">TOTAL o PARCIAL.</param>
    /// <returns>Decisión que la UI puede mostrar.</returns>
    ReturnFiscalDecision Decide(string? originalDteType, string returnType);
}

/// <summary>Política predeterminada de revisión fiscal para la fase sin emisión.</summary>
public sealed class DefaultReturnFiscalPolicy : IReturnFiscalPolicy
{
    /// <inheritdoc />
    public ReturnFiscalDecision Decide(string? originalDteType, string returnType)
    {
        var normalized = originalDteType?.Trim();
        if (normalized == DteConstants.TiposDte.CreditoFiscal)
        {
            return new ReturnFiscalDecision(ReturnDomainConstants.FiscalStatuses.Pending, "Nota de crédito 05 (a verificar con contador / normativa MH)", $"Devolución {returnType.ToLowerInvariant()}: documento tributario pendiente de validación.");
        }

        if (normalized == DteConstants.TiposDte.Factura)
        {
            return new ReturnFiscalDecision(ReturnDomainConstants.FiscalStatuses.RequiresValidation, "Invalidación u otro documento (a verificar con contador / normativa MH)", "La devolución requiere validación fiscal antes de emitir cualquier documento.");
        }

        return new ReturnFiscalDecision(ReturnDomainConstants.FiscalStatuses.RequiresValidation, "Validación fiscal (a verificar con contador / normativa MH)", "La venta no tiene DTE original; la devolución requiere validación fiscal.");
    }
}

/// <summary>Abstracción para leer devoluciones persistidas desde la base de datos.</summary>
public interface IReturnedQuantityReader
{
    /// <summary>Indica si la fuente refleja todas las devoluciones confirmadas.</summary>
    bool IsAuthoritative { get; }

    /// <summary>Lee cantidades y créditos ya registrados para una orden.</summary>
    /// <param name="dbContext">Contexto EF que puede estar dentro de una transacción.</param>
    /// <param name="orderId">Orden original.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lectura por línea e indicador de autoridad.</returns>
    Task<ReturnedQuantityReadResult> GetAsync(FerreteriaDbContext dbContext, Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>Lee devoluciones confirmadas desde sales.ReturnDetails.</summary>
public sealed class ReturnDetailsReturnedQuantityReader : IReturnedQuantityReader
{
    /// <inheritdoc />
    public bool IsAuthoritative => true;

    /// <inheritdoc />
    public async Task<ReturnedQuantityReadResult> GetAsync(FerreteriaDbContext dbContext, Guid orderId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var rows = await dbContext.ReturnDetails
            .Where(detail => detail.Return != null && detail.Return.OrderId == orderId && detail.Return.Status == ReturnDomainConstants.Statuses.Completed)
            .GroupBy(detail => detail.OrderDetailId)
            .Select(group => new
            {
                OrderDetailId = group.Key,
                Quantity = group.Sum(detail => detail.Quantity),
                Subtotal = group.Sum(detail => detail.Subtotal),
                DiscountAmount = group.Sum(detail => detail.DiscountAmount),
                TaxAmount = group.Sum(detail => detail.TaxAmount)
            })
            .ToListAsync(cancellationToken);
        var result = rows.ToDictionary(
            row => row.OrderDetailId,
            row => new ReturnedLineCredit(row.Quantity, row.Subtotal, row.DiscountAmount, row.TaxAmount, row.Subtotal - row.DiscountAmount + row.TaxAmount));
        return new ReturnedQuantityReadResult(result, true);
    }
}

/// <summary>Excepción de entrada inválida para el dominio de devoluciones.</summary>
public sealed class InvalidReturnException : Exception
{
    /// <summary>Inicializa la excepción con un mensaje de validación.</summary>
    /// <param name="message">Mensaje claro para el usuario.</param>
    public InvalidReturnException(string message) : base(message) { }
}

/// <summary>Excepción que indica que la confirmación está bloqueada por falta de persistencia.</summary>
public sealed class ReturnsUnavailableException : Exception
{
    /// <summary>Inicializa la excepción con el motivo operativo.</summary>
    /// <param name="message">Mensaje controlado.</param>
    public ReturnsUnavailableException(string message) : base(message) { }
}

/// <summary>Contrato de consulta y confirmación de devoluciones.</summary>
public interface IReturnService
{
    /// <summary>Capacidades actuales del servicio.</summary>
    ReturnCapabilities Capabilities { get; }

    /// <summary>Busca ventas completadas que el empleado puede revisar.</summary>
    /// <param name="filter">Filtro de búsqueda.</param>
    /// <param name="requestedByEmployeeId">Empleado solicitante.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resumen paginado de ventas.</returns>
    Task<IReadOnlyList<ReturnableSaleSummary>> SearchReturnableSalesAsync(ReturnableSalesFilter filter, Guid requestedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Obtiene las líneas originales y su cantidad disponible.</summary>
    /// <param name="orderId">Orden original.</param>
    /// <param name="requestedByEmployeeId">Empleado solicitante.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Líneas disponibles o una lista vacía si no hay autorización.</returns>
    Task<IReadOnlyList<ReturnableLine>> GetReturnableLinesAsync(Guid orderId, Guid requestedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Valida y registra una devolución dentro de una transacción protegida.</summary>
    /// <param name="request">Solicitud de devolución.</param>
    /// <param name="authorizerPin">PIN del empleado que autoriza, solo en memoria durante la validación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado registrado o recuperado por idempotencia.</returns>
    /// <exception cref="InvalidReturnException">Si la solicitud no cumple las reglas.</exception>
    /// <exception cref="ReturnsUnavailableException">Si la persistencia autoritativa no está disponible.</exception>
    /// <remarks>Usa Serializable, bloquea la orden con FOR UPDATE y lee las devoluciones después del bloqueo; la orden conserva COMPLETADA.</remarks>
    Task<ReturnResult> CreateReturnAsync(ReturnRequest request, string authorizerPin, CancellationToken cancellationToken = default);
}
