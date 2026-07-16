using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Vista pendiente: devoluciones y notas de crédito asociadas a <see cref="Models.Order"/>.
/// Solo UI estática; la lógica de dominio se implementará en fases posteriores del POS.
/// </summary>
public partial class DevolucionesView : UserControl
{
    /// <summary>Inicializa la vista de devoluciones (UI estática por ahora).</summary>
    public DevolucionesView()
    {
        InitializeComponent();
    }
}
