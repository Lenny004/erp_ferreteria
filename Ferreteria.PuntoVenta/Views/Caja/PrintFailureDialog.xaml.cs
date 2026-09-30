using System.Windows;
using System.Windows.Input;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Diálogo accesible para decidir si se reintenta una impresión fallida.</summary>
public partial class PrintFailureDialog : Window
{
    /// <summary>Inicializa el diálogo con el motivo breve para el cajero.</summary>
    /// <param name="reason">Motivo no técnico de la falla.</param>
    public PrintFailureDialog(string reason)
    {
        InitializeComponent();
        ReasonText.Text = reason;
    }

    private void OnRetryClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnContinueClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}
