namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Valores de negocio alineados con <c>schema.prisma</c> v3 (esquemas <c>sales</c> y <c>public</c>).
/// Fuente obligatoria: Microsoft .NET naming conventions + modelo Prisma del repositorio ferreteria_backend.
/// </summary>
public static class SalesDomainConstants
{
    /// <summary>
    /// IVA general de El Salvador (13%). Referencia fiscal: Ley de IVA y plan Ferreteria (Fase 3 / DTE).
    /// Usar vía <see cref="TaxAmountCalculator"/> al calcular <c>Order.TaxAmount</c>.
    /// </summary>
    public const decimal ElSalvadorIvaRate = 0.13m;

    /// <summary>Tipos de orden persistidos en <c>sales.Orders.OrderType</c>.</summary>
    public static class OrderTypes
    {
        /// <summary>Venta de mostrador / caja (<see cref="Models.Order"/> completada al instante).</summary>
        public const string CashRegisterSale = "VENTA_CAJA";

        /// <summary>Cotización (reservado; no usado aún en el POS WPF).</summary>
        public const string Quotation = "COTIZACION";

        /// <summary>Orden de taller/confección (compatibilidad; flujo previo al POS de ferretería).</summary>
        public const string ConfectionWorkOrder = "ORDEN_CONFECCION";
    }

    /// <summary>Estados persistidos en <c>sales.Orders.Status</c>.</summary>
    public static class OrderStatuses
    {
        /// <summary>Orden abierta, pendiente de facturar o completar.</summary>
        public const string Pending = "PENDIENTE";

        /// <summary>Orden facturada / venta cerrada con pagos e inventario aplicados.</summary>
        public const string Completed = "COMPLETADA";

        /// <summary>Orden anulada; no debe descontar stock ni figurar en reportes de venta.</summary>
        public const string Cancelled = "CANCELADA";

        /// <summary>Valor de filtro UI; no se persiste en base de datos.</summary>
        public const string All = "TODOS";
    }

    /// <summary>Tipos de movimiento en <c>public.InventoryMovements.MovementType</c>.</summary>
    public static class InventoryMovementTypes
    {
        /// <summary>Entrada por compra a proveedor.</summary>
        public const string PurchaseInflow = "ENTRADA_COMPRA";

        /// <summary>Entrada por devolución de cliente.</summary>
        public const string ReturnInflow = "ENTRADA_DEVOLUCION";

        /// <summary>Salida asociada a una venta (<see cref="Models.Order"/>).</summary>
        public const string SaleOutflow = "SALIDA_VENTA";

        /// <summary>Ajuste manual que incrementa stock.</summary>
        public const string AdjustmentIn = "AJUSTE_ENTRADA";

        /// <summary>Ajuste manual que reduce stock.</summary>
        public const string AdjustmentOut = "AJUSTE_SALIDA";
    }

    /// <summary>Métodos de pago en <c>sales.Payments.Method</c>.</summary>
    public static class PaymentMethods
    {
        /// <summary>Pago en efectivo.</summary>
        public const string Cash = "EFECTIVO";

        /// <summary>Pago con tarjeta.</summary>
        public const string Card = "TARJETA";

        /// <summary>Transferencia bancaria.</summary>
        public const string Transfer = "TRANSFERENCIA";

        /// <summary>Otro medio no tipificado.</summary>
        public const string Other = "OTRO";
    }

    /// <summary>Clasificación de rotación de stock (ABC) en <c>public.Products.RotationClass</c>.</summary>
    public static class RotationClasses
    {
        /// <summary>Alta rotación.</summary>
        public const string High = "ALTA";

        /// <summary>Rotación media.</summary>
        public const string Medium = "MEDIA";

        /// <summary>Baja rotación.</summary>
        public const string Low = "BAJA";

        /// <summary>Sin movimiento reciente.</summary>
        public const string None = "NULA";
    }

    /// <summary>Cliente por defecto cuando no se capturan datos en taller.</summary>
    public static class Customers
    {
        /// <summary>Etiqueta de consumidor final (CF) en listados.</summary>
        public const string DefaultWalkInDisplayName = "Consumidor Final";
    }

    /// <summary>Filtros de stock usados en vistas de inventario.</summary>
    public static class StockFilters
    {
        /// <summary>Sin filtro de nivel de stock.</summary>
        public const string All = "TODOS";

        /// <summary>Stock por encima del mínimo.</summary>
        public const string Sufficient = "OK";

        /// <summary>Stock entre 0 exclusivo y el mínimo inclusive.</summary>
        public const string Low = "BAJO";

        /// <summary>Stock agotado (≤ 0).</summary>
        public const string Depleted = "AGOTADO";
    }

    /// <summary>Etiquetas de presentación para historial de ventas.</summary>
    public static class OrderChannelLabels
    {
        /// <summary>Canal taller / confección.</summary>
        public const string ConfectionShop = "TALLER";

        /// <summary>Canal caja / mostrador.</summary>
        public const string CashRegister = "CAJA";

        /// <summary>Etiqueta corta de aplicación de taller.</summary>
        public const string WorkshopApplication = "Taller";

        /// <summary>Método de pago no disponible en la proyección.</summary>
        public const string PaymentMethodNotAvailable = "N/D";
    }
}
