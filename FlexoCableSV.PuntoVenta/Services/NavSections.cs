namespace FlexoCableSV.PuntoVenta.Services;

/// <summary>
/// Secciones del panel lateral según el módulo activo (ver README — App de Escritorio).
/// </summary>
public static class NavSections
{
  public const string Stock = "Stock";
  public const string Facturacion = "Facturacion";
  public const string HistorialFacturas = "HistorialFacturas";
  public const string Impresoras = "Impresoras";
  public const string Devoluciones = "Devoluciones";
  public const string CorteCaja = "CorteCaja";
  public const string HistorialVentas = "HistorialVentas";
  public const string Ordenes = "Ordenes";
  public const string Codigos = "Codigos";

  private static readonly string[] CajaSections =
  [
    Stock,
    Facturacion,
    HistorialFacturas,
    Impresoras,
    Devoluciones,
    CorteCaja
  ];

  private static readonly string[] ConfeccionSections =
  [
    HistorialVentas,
    Ordenes,
    Codigos
  ];

  public static IReadOnlyList<string> ForModule(OperationalModule module) =>
    module switch
    {
      OperationalModule.Caja => CajaSections,
      OperationalModule.Confeccion => ConfeccionSections,
      _ => []
    };

  public static string DefaultSection(OperationalModule module) =>
    module switch
    {
      OperationalModule.Caja => Facturacion,
      OperationalModule.Confeccion => Ordenes,
      _ => Stock
    };

  public static bool BelongsToModule(string sectionKey, OperationalModule module) =>
    ForModule(module).Contains(sectionKey, StringComparer.Ordinal);
}
