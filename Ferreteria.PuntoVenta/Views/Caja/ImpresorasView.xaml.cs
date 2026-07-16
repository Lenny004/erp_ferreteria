using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Vista pendiente: configuración de impresoras de tickets/facturas (<see cref="Models.Printer"/>).
/// Solo UI estática; se cableará a <see cref="Services.ImpresionService"/> en fases posteriores.
/// </summary>
public partial class ImpresorasView : UserControl
{
    /// <summary>Inicializa la vista de impresoras (UI estática por ahora).</summary>
    public ImpresorasView()
    {
        InitializeComponent();
    }
}
