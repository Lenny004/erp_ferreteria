using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Vista pendiente: corte de caja (<see cref="Models.CashSession"/>) — apertura, cierre y arqueo.
/// Solo UI estática; la lógica de dominio se implementará en fases posteriores del POS.
/// </summary>
public partial class CorteCajaView : UserControl
{
    /// <summary>Inicializa la vista de corte de caja (UI estática por ahora).</summary>
    public CorteCajaView()
    {
        InitializeComponent();
    }
}
