using System.Windows;
using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>
/// Comportamiento adjunto para limitar la entrada decimal de un <see cref="TextBox"/>.
/// </summary>
public static class NumericInput
{
    /// <summary>Propiedad de precisión total del campo decimal.</summary>
    public static readonly DependencyProperty PrecisionProperty =
        DependencyProperty.RegisterAttached(
            "Precision",
            typeof(int),
            typeof(NumericInput),
            new PropertyMetadata(12, OnConfigurationChanged));

    /// <summary>Propiedad de escala decimal del campo.</summary>
    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.RegisterAttached(
            "Scale",
            typeof(int),
            typeof(NumericInput),
            new PropertyMetadata(2, OnConfigurationChanged));

    /// <summary>Propiedad que indica si se aceptan valores negativos.</summary>
    public static readonly DependencyProperty AllowNegativeProperty =
        DependencyProperty.RegisterAttached(
            "AllowNegative",
            typeof(bool),
            typeof(NumericInput),
            new PropertyMetadata(false, OnConfigurationChanged));

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(NumericInput),
            new PropertyMetadata(false));

    /// <summary>Obtiene la precisión configurada.</summary>
    public static int GetPrecision(DependencyObject element) => (int)element.GetValue(PrecisionProperty);

    /// <summary>Configura la precisión total del campo.</summary>
    public static void SetPrecision(DependencyObject element, int value) => element.SetValue(PrecisionProperty, value);

    /// <summary>Obtiene la escala configurada.</summary>
    public static int GetScale(DependencyObject element) => (int)element.GetValue(ScaleProperty);

    /// <summary>Configura la escala decimal del campo.</summary>
    public static void SetScale(DependencyObject element, int value) => element.SetValue(ScaleProperty, value);

    /// <summary>Obtiene si el campo acepta valores negativos.</summary>
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);

    /// <summary>Configura si el campo acepta valores negativos.</summary>
    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);

    private static void OnConfigurationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBox textBox || (bool)textBox.GetValue(IsHookedProperty))
        {
            return;
        }

        textBox.SetValue(IsHookedProperty, true);
        textBox.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(textBox, OnPasting);
    }

    private static void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = !IsAllowed(textBox, e.Text);
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string;
        if (!IsAllowed(textBox, pastedText))
        {
            e.CancelCommand();
        }
    }

    private static bool IsAllowed(TextBox textBox, string? insertedText) =>
        NumericInputTextRules.IsAllowed(
            textBox.Text,
            textBox.SelectionStart,
            textBox.SelectionLength,
            insertedText,
            GetPrecision(textBox),
            GetScale(textBox),
            GetAllowNegative(textBox));
}
