namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Claves de sección del panel lateral según el módulo activo (Caja o Inventario).
/// </summary>
public static class NavSections
{
    // --- Módulo Caja ---

    /// <summary>Consulta rápida de stock desde caja.</summary>
    public const string Stock = "Stock";

    /// <summary>Pantalla de facturación / venta de mostrador.</summary>
    public const string Facturacion = "Facturacion";

    /// <summary>Historial de ventas completadas.</summary>
    public const string HistorialFacturas = "HistorialFacturas";

    /// <summary>Configuración de impresoras del POS.</summary>
    public const string Impresoras = "Impresoras";

    /// <summary>Devoluciones de venta (flujo futuro).</summary>
    public const string Devoluciones = "Devoluciones";

    /// <summary>Cierre / corte de <see cref="Models.CashSession"/>.</summary>
    public const string CorteCaja = "CorteCaja";

    // --- Módulo Inventario ---

    /// <summary>CRUD de <see cref="Models.Product"/>.</summary>
    public const string Productos = "Productos";

    /// <summary>CRUD de <see cref="Models.Supplier"/>.</summary>
    public const string Proveedores = "Proveedores";

    /// <summary>Kardex / <see cref="Models.InventoryMovement"/>.</summary>
    public const string Movimientos = "Movimientos";

    /// <summary>Alertas de stock bajo o agotado.</summary>
    public const string Alertas = "Alertas";

    /// <summary>Gestión de <see cref="Models.Employee"/> / usuarios del POS.</summary>
    public const string Usuarios = "Usuarios";

    private static readonly string[] CajaSections =
    [
        Stock,
        Facturacion,
        HistorialFacturas,
        Impresoras,
        Devoluciones,
        CorteCaja
    ];

    private static readonly string[] InventarioSections =
    [
        Productos,
        Proveedores,
        Movimientos,
        Alertas,
        Usuarios
    ];

    /// <summary>Secciones permitidas para el módulo indicado.</summary>
    public static IReadOnlyList<string> ForModule(OperationalModule module) =>
        module switch
        {
            OperationalModule.Caja => CajaSections,
            OperationalModule.Inventario => InventarioSections,
            _ => []
        };

    /// <summary>Sección por defecto al entrar al módulo.</summary>
    public static string DefaultSection(OperationalModule module) =>
        module switch
        {
            OperationalModule.Caja => Facturacion,
            OperationalModule.Inventario => Productos,
            _ => Stock
        };

    /// <summary>Indica si la clave de sección pertenece al módulo.</summary>
    public static bool BelongsToModule(string sectionKey, OperationalModule module) =>
        ForModule(module).Contains(sectionKey, StringComparer.Ordinal);
}
