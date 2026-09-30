using System.Windows;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Muestra el comprobante interno generado antes de una impresión física futura.</summary>
public partial class ReturnReceiptPreviewWindow : Window
{
    /// <summary>Inicializa la vista previa con el texto compuesto.</summary>
    /// <param name="receiptText">Texto monoespaciado del comprobante.</param>
    public ReturnReceiptPreviewWindow(string receiptText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptText);
        InitializeComponent();
        ReceiptTextBox.Text = receiptText;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
