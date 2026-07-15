using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Pantalla placeholder de configuración de impresoras de tickets/facturas.
/// Independiente de entidades de dominio; se cableará a impresión en fases posteriores.
/// </summary>
public partial class ImpresorasView : UserControl
{
    /// <summary>Inicializa la vista de impresoras (UI estática por ahora).</summary>
    public ImpresorasView()
    {
        InitializeComponent();
    }
}
