using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>Ayudas de accesibilidad para campos de formularios WPF.</summary>
public static class FieldHelper
{
    /// <summary>
    /// Indica que un campo es obligatorio y sincroniza el contrato accesible del control.
    /// En un <see cref="Label"/> se aplica al control indicado por <see cref="Label.Target"/>.
    /// </summary>
    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.RegisterAttached(
            "IsRequired",
            typeof(bool),
            typeof(FieldHelper),
            new PropertyMetadata(false, OnIsRequiredChanged));

    private static readonly DependencyProperty IsLoadedHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsLoadedHooked",
            typeof(bool),
            typeof(FieldHelper),
            new PropertyMetadata(false));

    /// <summary>Obtiene si el elemento representa un campo obligatorio.</summary>
    public static bool GetIsRequired(DependencyObject element) => (bool)element.GetValue(IsRequiredProperty);

    /// <summary>Marca el elemento como campo obligatorio.</summary>
    public static void SetIsRequired(DependencyObject element, bool value) => element.SetValue(IsRequiredProperty, value);

    private static void OnIsRequiredChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true)
        {
            return;
        }

        if (dependencyObject is Label label)
        {
            ApplyToLabelTarget(label);
            if (!(bool)label.GetValue(IsLoadedHookedProperty))
            {
                label.SetValue(IsLoadedHookedProperty, true);
                label.Loaded += OnLabelLoaded;
            }

            return;
        }

        ApplyToControl(dependencyObject, AutomationProperties.GetName(dependencyObject));
    }

    private static void OnLabelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Label label)
        {
            ApplyToLabelTarget(label);
        }
    }

    private static void ApplyToLabelTarget(Label label)
    {
        if (label.Target is DependencyObject target)
        {
            var labelText = label.Content?.ToString();
            ApplyToControl(target, labelText);
        }
    }

    private static void ApplyToControl(DependencyObject control, string? suggestedName)
    {
        AutomationProperties.SetIsRequiredForForm(control, true);
        var currentName = AutomationProperties.GetName(control);
        var accessibleName = string.IsNullOrWhiteSpace(currentName) ? suggestedName : currentName;
        if (string.IsNullOrWhiteSpace(accessibleName))
        {
            accessibleName = "Campo";
        }

        if (!accessibleName.Contains("obligatorio", StringComparison.OrdinalIgnoreCase))
        {
            accessibleName = $"{accessibleName} (obligatorio)";
        }

        AutomationProperties.SetName(control, accessibleName);
    }
}
