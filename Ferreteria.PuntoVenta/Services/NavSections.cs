namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Secciones del panel lateral según el módulo activo (Caja o Inventario) de la ferretería.
/// </summary>
public static class NavSections
{
  // Módulo Caja
  public const string Stock = "Stock";
  public const string Facturacion = "Facturacion";
  public const string HistorialFacturas = "HistorialFacturas";
  public const string Impresoras = "Impresoras";
  public const string Devoluciones = "Devoluciones";
  public const string CorteCaja = "CorteCaja";

  // Módulo Inventario
  public const string Productos = "Productos";
  public const string Proveedores = "Proveedores";
  public const string Movimientos = "Movimientos";
  public const string Alertas = "Alertas";
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

  public static IReadOnlyList<string> ForModule(OperationalModule module) =>
    module switch
    {
      OperationalModule.Caja => CajaSections,
      OperationalModule.Inventario => InventarioSections,
      _ => []
    };

  public static string DefaultSection(OperationalModule module) =>
    module switch
    {
      OperationalModule.Caja => Facturacion,
      OperationalModule.Inventario => Productos,
      _ => Stock
    };

  public static bool BelongsToModule(string sectionKey, OperationalModule module) =>
    ForModule(module).Contains(sectionKey, StringComparer.Ordinal);
}
