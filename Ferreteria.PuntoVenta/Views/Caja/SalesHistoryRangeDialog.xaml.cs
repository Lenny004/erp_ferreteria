using System.Windows;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Diálogo para seleccionar un rango local inclusivo.</summary>
public partial class SalesHistoryRangeDialog : Window
{
    /// <summary>Inicializa el diálogo con las fechas indicadas.</summary>
    /// <param name="from">Fecha inicial precargada, si existe.</param>
    /// <param name="to">Fecha final precargada, si existe.</param>
    public SalesHistoryRangeDialog(DateOnly? from = null, DateOnly? to = null)
    {
        InitializeComponent();
        FromPicker.SelectedDate = (from ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        ToPicker.SelectedDate = (to ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>Fecha inicial seleccionada.</summary>
    public DateOnly From => DateOnly.FromDateTime(FromPicker.SelectedDate.GetValueOrDefault(DateTime.Today));

    /// <summary>Fecha final seleccionada.</summary>
    public DateOnly To => DateOnly.FromDateTime(ToPicker.SelectedDate.GetValueOrDefault(DateTime.Today));

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (FromPicker.SelectedDate is null || ToPicker.SelectedDate is null)
        {
            ValidationText.Text = "Seleccione Desde y Hasta.";
            ValidationText.Visibility = Visibility.Visible;
            return;
        }
        if (From > To)
        {
            ValidationText.Text = "Desde no puede ser posterior a Hasta.";
            ValidationText.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
