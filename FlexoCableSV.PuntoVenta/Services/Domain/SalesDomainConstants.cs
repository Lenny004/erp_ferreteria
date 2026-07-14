namespace FlexoCableSV.PuntoVenta.Services.Domain;

/// <summary>
/// Valores de negocio alineados con <c>schema.prisma</c> v3 (esquemas <c>sales</c> y <c>public</c>).
/// Fuente obligatoria: Microsoft .NET naming conventions + modelo Prisma del repositorio FlexoCable-backend.
/// </summary>
public static class SalesDomainConstants
{
    /// <summary>
    /// IVA general de El Salvador (13%). Referencia fiscal: Ley de IVA y plan FlexoCable (Fase 3 / DTE).
    /// </summary>
    public const decimal ElSalvadorIvaRate = 0.13m;

    /// <summary>Tipos de orden persistidos en <c>sales.Orders.OrderType</c>.</summary>
    public static class OrderTypes
    {
        public const string CashRegisterSale = "VENTA_CAJA";
        public const string Quotation = "COTIZACION";
        // Compatibilidad con flujos previos (no se usa en ferretería).
        public const string ConfectionWorkOrder = "ORDEN_CONFECCION";
    }

    /// <summary>Estados persistidos en <c>sales.Orders.Status</c>.</summary>
    public static class OrderStatuses
    {
        public const string Pending = "PENDIENTE";
        public const string Completed = "COMPLETADA";
        public const string Cancelled = "CANCELADA";

        /// <summary>Valor de filtro UI; no se persiste en base de datos.</summary>
        public const string All = "TODOS";
    }

    /// <summary>Tipos de movimiento en <c>public.InventoryMovements.MovementType</c>.</summary>
    public static class InventoryMovementTypes
    {
        public const string PurchaseInflow = "ENTRADA_COMPRA";
        public const string ReturnInflow = "ENTRADA_DEVOLUCION";
        public const string SaleOutflow = "SALIDA_VENTA";
        public const string AdjustmentIn = "AJUSTE_ENTRADA";
        public const string AdjustmentOut = "AJUSTE_SALIDA";
    }

    /// <summary>Métodos de pago en <c>sales.Payments.Method</c>.</summary>
    public static class PaymentMethods
    {
        public const string Cash = "EFECTIVO";
        public const string Card = "TARJETA";
        public const string Transfer = "TRANSFERENCIA";
        public const string Other = "OTRO";
    }

    /// <summary>Clasificación de rotación de stock (ABC).</summary>
    public static class RotationClasses
    {
        public const string High = "ALTA";
        public const string Medium = "MEDIA";
        public const string Low = "BAJA";
        public const string None = "NULA";
    }

    /// <summary>Cliente por defecto cuando no se capturan datos en taller.</summary>
    public static class Customers
    {
        public const string DefaultWalkInDisplayName = "Consumidor Final";
    }

    /// <summary>Filtros de stock usados en vistas de inventario.</summary>
    public static class StockFilters
    {
        public const string All = "TODOS";
        public const string Sufficient = "OK";
        public const string Low = "BAJO";
        public const string Depleted = "AGOTADO";
    }

    /// <summary>Etiquetas de presentación para historial de ventas.</summary>
    public static class OrderChannelLabels
    {
        public const string ConfectionShop = "TALLER";
        public const string CashRegister = "CAJA";
        public const string WorkshopApplication = "Taller";
        public const string PaymentMethodNotAvailable = "N/D";
    }
}
