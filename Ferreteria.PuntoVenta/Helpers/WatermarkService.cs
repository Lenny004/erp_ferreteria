using System.Windows;
using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>
/// Propiedad adjunta para mostrar texto guía (placeholder) en un <see cref="TextBox"/>.
/// Uso típico: campos de búsqueda de Product, Customer o Supplier en vistas WPF.
/// </summary>
public static class WatermarkService
{
    /// <summary>Texto de marca de agua asociado al TextBox.</summary>
    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.RegisterAttached(
            "Watermark",
            typeof(string),
            typeof(WatermarkService),
            new PropertyMetadata(null));

    /// <summary>Obtiene el watermark del TextBox.</summary>
    public static string GetWatermark(TextBox textBox) =>
        (string)textBox.GetValue(WatermarkProperty);

    /// <summary>Asigna el watermark del TextBox.</summary>
    public static void SetWatermark(TextBox textBox, string value) =>
        textBox.SetValue(WatermarkProperty, value);
}
