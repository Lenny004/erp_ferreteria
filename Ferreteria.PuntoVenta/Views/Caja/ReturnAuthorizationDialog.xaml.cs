using System.Windows;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Diálogo modal que captura el PIN de autorización sin exponerlo en un TextBox.</summary>
public partial class ReturnAuthorizationDialog : Window
{
    /// <summary>Inicializa el diálogo de autorización.</summary>
    public ReturnAuthorizationDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PinBox.Focus();
    }

    /// <summary>PIN capturado; queda disponible solo para el intento actual.</summary>
    public string AuthorizerPin => PinBox.Password;

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (PinBox.Password.Length != 4 || !PinBox.Password.All(char.IsDigit))
        {
            MessageBox.Show("Ingrese un PIN de 4 dígitos.", "Autorización", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
