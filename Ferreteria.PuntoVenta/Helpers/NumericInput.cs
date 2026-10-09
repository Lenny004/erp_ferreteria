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
            new PropertyMetadata(12));

    /// <summary>Propiedad de escala decimal del campo.</summary>
    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.RegisterAttached(
            "Scale",
            typeof(int),
            typeof(NumericInput),
            new PropertyMetadata(2));

    /// <summary>Propiedad que indica si se aceptan valores negativos.</summary>
    public static readonly DependencyProperty AllowNegativeProperty =
        DependencyProperty.RegisterAttached(
            "AllowNegative",
            typeof(bool),
            typeof(NumericInput),
            new PropertyMetadata(false));

    /// <summary>
    /// Propiedad que activa o desactiva los manejadores de escritura y pegado del campo.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(NumericInput),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(NumericInput),
            new PropertyMetadata(false));

    private static readonly DependencyPropertyKey ErrorMessagePropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "ErrorMessage",
            typeof(string),
            typeof(NumericInput),
            new PropertyMetadata(null));

    /// <summary>Propiedad adjunta de solo lectura con el último error de edición.</summary>
    public static readonly DependencyProperty ErrorMessageProperty = ErrorMessagePropertyKey.DependencyProperty;

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

    /// <summary>Obtiene si el filtrado numérico está habilitado.</summary>
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    /// <summary>Habilita o deshabilita el filtrado numérico del campo.</summary>
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>Obtiene el mensaje del último intento de edición rechazado.</summary>
    public static string? GetErrorMessage(DependencyObject element) => (string?)element.GetValue(ErrorMessageProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        var isEnabled = (bool)args.NewValue;
        var isHooked = (bool)textBox.GetValue(IsHookedProperty);
        if (isEnabled && !isHooked)
        {
            textBox.SetValue(IsHookedProperty, true);
            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
        else if (!isEnabled && isHooked)
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(textBox, OnPasting);
            textBox.SetValue(IsHookedProperty, false);
            SetErrorMessage(textBox, null);
        }
    }

    private static void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var result = NumericInputTextRules.Validate(
            textBox.Text,
            textBox.SelectionStart,
            textBox.SelectionLength,
            e.Text,
            GetPrecision(textBox),
            GetScale(textBox),
            GetAllowNegative(textBox));
        SetErrorMessage(textBox, result.IsAllowed ? null : result.ErrorMessage);
        e.Handled = !result.IsAllowed;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            e.CancelCommand();
            return;
        }

        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            SetErrorMessage(textBox, "Solo se puede pegar texto numérico.");
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string;
        var result = NumericInputTextRules.Validate(
            textBox.Text,
            textBox.SelectionStart,
            textBox.SelectionLength,
            pastedText,
            GetPrecision(textBox),
            GetScale(textBox),
            GetAllowNegative(textBox),
            allowIntermediate: false);
        SetErrorMessage(textBox, result.IsAllowed ? null : result.ErrorMessage);
        if (!result.IsAllowed)
        {
            e.CancelCommand();
        }
    }

    internal static void SetErrorMessage(DependencyObject element, string? message) =>
        element.SetValue(ErrorMessagePropertyKey, message);
}
