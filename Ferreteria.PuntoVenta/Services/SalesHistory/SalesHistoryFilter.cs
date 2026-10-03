using System.Text;
using Ferreteria.PuntoVenta.Services.Time;

namespace Ferreteria.PuntoVenta.Services.SalesHistory;

/// <summary>Atajos de fecha disponibles para el historial de ventas.</summary>
public enum SalesHistoryDateShortcut
{
    /// <summary>No aplica un atajo.</summary>
    None,
    /// <summary>Ventas del día local actual.</summary>
    Today,
    /// <summary>Ventas del día local anterior.</summary>
    Yesterday,
    /// <summary>Ventas desde el inicio de la semana local.</summary>
    Week,
    /// <summary>Ventas desde el primer día del mes local.</summary>
    Month
}

/// <summary>Filtro inmutable y normalizado para consultar el historial.</summary>
/// <param name="FromUtc">Inicio UTC del rango, inclusivo.</param>
/// <param name="ToUtc">Fin UTC del rango, exclusivo.</param>
/// <param name="Shortcut">Atajo de fecha seleccionado.</param>
/// <param name="SearchText">Texto de búsqueda libre.</param>
/// <param name="OrderStatus">Estado de la orden.</param>
/// <param name="DteType">Tipo de DTE o valor de órdenes sin DTE.</param>
/// <param name="MhStatus">Estado del DTE ante el Ministerio de Hacienda.</param>
/// <param name="EmployeeId">Empleado por el que se filtra, si aplica.</param>
/// <param name="PaymentMethod">Método de pago.</param>
/// <param name="Page">Número de página solicitado.</param>
/// <param name="PageSize">Cantidad de filas solicitada.</param>
public sealed record SalesHistoryFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    SalesHistoryDateShortcut Shortcut = SalesHistoryDateShortcut.Today,
    string? SearchText = null,
    string? OrderStatus = null,
    string? DteType = null,
    string? MhStatus = null,
    Guid? EmployeeId = null,
    string? PaymentMethod = null,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Valor del filtro que selecciona órdenes sin DTE.</summary>
    public const string NoDteFilterValue = "SIN_DTE";

    /// <summary>Tamaño máximo permitido para una página.</summary>
    public const int MaximumPageSize = 50;

    /// <summary>Normaliza texto, rango y paginación.</summary>
    /// <returns>Una copia normalizada del filtro.</returns>
    public SalesHistoryFilter Normalize()
    {
        var search = string.IsNullOrWhiteSpace(SearchText)
            ? null
            : SearchText.Trim().Normalize(NormalizationForm.FormC);
        var page = Math.Max(1, Page);
        var pageSize = Math.Clamp(PageSize, 1, MaximumPageSize);
        var from = FromUtc?.ToUniversalTime();
        var to = ToUtc?.ToUniversalTime();
        if (from is not null && to is not null && from > to)
        {
            throw new ArgumentException("El inicio del rango no puede ser posterior al final.");
        }

        return this with
        {
            SearchText = search,
            FromUtc = from,
            ToUtc = to,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>Construye los límites UTC de un atajo.</summary>
    /// <param name="shortcut">Atajo de fecha que se desea convertir.</param>
    /// <param name="calendar">Calendario que determina la fecha y los límites locales.</param>
    /// <returns>Rango UTC semiabierto correspondiente al atajo.</returns>
    public static (DateTime FromUtc, DateTime ToUtc) CreateShortcutRange(
        SalesHistoryDateShortcut shortcut,
        BusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var today = calendar.Today();
        var localDate = shortcut switch
        {
            SalesHistoryDateShortcut.Yesterday => today.AddDays(-1),
            SalesHistoryDateShortcut.Week => StartOfWeek(today),
            SalesHistoryDateShortcut.Month => new DateOnly(today.Year, today.Month, 1),
            _ => today
        };
        var endDate = shortcut switch
        {
            SalesHistoryDateShortcut.Yesterday => localDate.AddDays(1),
            SalesHistoryDateShortcut.Week => today.AddDays(1),
            SalesHistoryDateShortcut.Month => localDate.AddMonths(1),
            _ => localDate.AddDays(1)
        };
        return calendar.RangeUtc(localDate, endDate.AddDays(-1));
    }

    /// <summary>Convierte fechas locales inclusivas en un rango UTC semiabierto.</summary>
    /// <param name="from">Fecha local inicial, inclusiva.</param>
    /// <param name="to">Fecha local final, inclusiva.</param>
    /// <param name="calendar">Calendario que realiza la conversión a UTC.</param>
    /// <returns>Rango UTC con el final exclusivo del día siguiente.</returns>
    public static (DateTime FromUtc, DateTime ToUtc) CreateLocalDateRange(
        DateOnly from,
        DateOnly to,
        BusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.RangeUtc(from, to);
    }

    /// <summary>Escapa comodines de PostgreSQL para un patrón ILIKE.</summary>
    /// <param name="value">Texto que se incorporará al patrón.</param>
    /// <returns>Texto escapado para usar con el carácter de escape de ILIKE.</returns>
    public static string EscapeILikePattern(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}

/// <summary>Reglas puras para resolver el alcance autorizado del historial.</summary>
public static class SalesHistoryAccessRules
{
    /// <summary>Resuelve el alcance según el puesto y las sesiones ABIERTAS del empleado.</summary>
    /// <param name="employeeId">Identificador del empleado solicitante.</param>
    /// <param name="canCashier">Indica si el empleado tiene permiso de caja.</param>
    /// <param name="positionName">Nombre del puesto del empleado.</param>
    /// <param name="fullHistoryPositionNames">Puestos configurados con acceso completo.</param>
    /// <param name="openCashSessionIds">Sesiones ABIERTAS obtenidas por el servicio desde el servidor.</param>
    /// <returns>Alcance completo para puestos autorizados o las sesiones abiertas propias.</returns>
    /// <remarks>
    /// La lista define los puestos de acceso completo. Actualmente contiene Administrador; no existe un
    /// puesto Supervisor sembrado. Si el dueño crea ese puesto, puede agregarlo a la misma configuración.
    /// Un empleado sin permiso de caja y sin puesto de acceso completo recibe alcance vacío.
    /// </remarks>
    public static SalesHistoryScope ResolveScope(
        Guid employeeId,
        bool canCashier,
        string? positionName,
        IEnumerable<string> fullHistoryPositionNames,
        IEnumerable<Guid> openCashSessionIds)
    {
        ArgumentNullException.ThrowIfNull(fullHistoryPositionNames);
        ArgumentNullException.ThrowIfNull(openCashSessionIds);
        bool isFullHistory = fullHistoryPositionNames.Any(name =>
            string.Equals(name?.Trim(), positionName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (isFullHistory)
        {
            return new SalesHistoryScope(null, null, null, null);
        }

        if (!canCashier)
        {
            return new SalesHistoryScope(employeeId, null, null, Array.Empty<Guid>());
        }

        var sessionIds = openCashSessionIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        return new SalesHistoryScope(
            employeeId,
            null,
            null,
            sessionIds);
    }

    /// <summary>Determina si una orden está dentro de un alcance resuelto.</summary>
    /// <param name="scope">Alcance autorizado.</param>
    /// <param name="orderEmployeeId">Empleado que registró la orden.</param>
    /// <param name="cashSessionId">Sesión de caja asociada a la orden.</param>
    /// <returns><c>true</c> si la orden está dentro del alcance; de lo contrario, <c>false</c>.</returns>
    public static bool CanView(SalesHistoryScope scope, Guid orderEmployeeId, Guid? cashSessionId)
    {
        return (!scope.RestrictToEmployeeId.HasValue || scope.RestrictToEmployeeId.Value == orderEmployeeId)
            && (scope.RestrictToCashSessionIds is null
                || cashSessionId is Guid sessionId && scope.RestrictToCashSessionIds.Contains(sessionId));
    }
}

/// <summary>Formatea identificadores visibles sin confundir una orden con un DTE real.</summary>
public static class SalesHistoryNumberFormatter
{
    /// <summary>Devuelve el número de control o una etiqueta explícita para ventas sin DTE.</summary>
    /// <param name="controlNumber">Número de control real del DTE, si existe.</param>
    /// <param name="orderId">Identificador interno de la orden.</param>
    /// <returns>Número de control real o una etiqueta de comprobante interno.</returns>
    public static string FormatControlNumber(string? controlNumber, Guid orderId)
    {
        return string.IsNullOrWhiteSpace(controlNumber)
            ? $"Sin DTE - Orden {orderId.ToString()[..8].ToUpperInvariant()}"
            : controlNumber.Trim();
    }
}
