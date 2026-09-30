using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Diálogo táctil para capturar y confirmar el fondo inicial de caja.</summary>
public partial class CashOpeningDialog : Window
{
    /// <summary>Inicializa el teclado numérico de apertura.</summary>
    public CashOpeningDialog()
    {
        InitializeComponent();
    }

    /// <summary>Fondo inicial validado al confirmar.</summary>
    public decimal OpeningAmount { get; private set; }

    /// <summary>Observación capturada para la apertura.</summary>
    public string? Notes { get; private set; }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(OpeningKeypad.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount < 0m)
        {
            ErrorText.Text = "Ingrese un fondo inicial válido.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"¿Confirma abrir la caja con ${amount:0.00}?",
            "Confirmar apertura",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        OpeningAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>Limpia el error visible cuando cambia el monto capturado.</summary>
    private void OnAmountChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        ErrorText.Text = string.Empty;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnConfirmClick(sender, e);
        }
    }
}
