using System.Windows;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Diálogo para seleccionar un rango local inclusivo.</summary>
public partial class SalesHistoryRangeDialog : Window
{
    private readonly DateOnly _defaultDate;

    /// <summary>Inicializa el diálogo con las fechas indicadas.</summary>
    /// <param name="defaultDate">Fecha de negocio que se usa si no se proporciona un valor.</param>
    /// <param name="from">Fecha inicial precargada, si existe.</param>
    /// <param name="to">Fecha final precargada, si existe.</param>
    public SalesHistoryRangeDialog(DateOnly defaultDate, DateOnly? from = null, DateOnly? to = null)
    {
        _defaultDate = defaultDate;
        InitializeComponent();
        FromPicker.SelectedDate = (from ?? defaultDate).ToDateTime(TimeOnly.MinValue);
        ToPicker.SelectedDate = (to ?? defaultDate).ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>Fecha inicial seleccionada.</summary>
    public DateOnly From => DateOnly.FromDateTime(FromPicker.SelectedDate.GetValueOrDefault(_defaultDate.ToDateTime(TimeOnly.MinValue)));

    /// <summary>Fecha final seleccionada.</summary>
    public DateOnly To => DateOnly.FromDateTime(ToPicker.SelectedDate.GetValueOrDefault(_defaultDate.ToDateTime(TimeOnly.MinValue)));

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
