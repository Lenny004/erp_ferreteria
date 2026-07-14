using System.Windows;
using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Helpers;

public static class WatermarkService
{
    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.RegisterAttached(
            "Watermark",
            typeof(string),
            typeof(WatermarkService),
            new PropertyMetadata(null));

    public static string GetWatermark(TextBox textBox) =>
        (string)textBox.GetValue(WatermarkProperty);

    public static void SetWatermark(TextBox textBox, string value) =>
        textBox.SetValue(WatermarkProperty, value);
}
