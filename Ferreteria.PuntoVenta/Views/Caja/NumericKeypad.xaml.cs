using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Control táctil reutilizable para capturar montos decimales con teclado físico o botones grandes.
/// </summary>
/// <remarks>
/// El valor se mantiene como texto para conservar el punto decimal durante la captura; cada consumidor
/// valida el monto definitivo con sus propias reglas de negocio antes de persistirlo.
/// </remarks>
public partial class NumericKeypad : UserControl
{
    private bool _updatingText;

    /// <summary>Propiedad de dependencia con el texto monetario capturado.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(NumericKeypad),
        new FrameworkPropertyMetadata(
            "0",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextPropertyChanged));

    /// <summary>Texto capturado, usando punto como separador decimal.</summary>
    public string Text
    {
        get => GetValue(TextProperty) as string ?? "0";
        set => SetValue(TextProperty, value);
    }

    /// <summary>Se produce cuando cambia el texto monetario capturado.</summary>
    public event DependencyPropertyChangedEventHandler? TextChanged;

    /// <summary>Inicializa el control numérico.</summary>
    public NumericKeypad()
    {
        InitializeComponent();
        InputTextBox.PreviewTextInput += OnPreviewTextInput;
        InputTextBox.TextChanged += OnInputTextChanged;
        InputTextBox.KeyDown += OnInputKeyDown;
    }

    private static void OnTextPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not NumericKeypad keypad || keypad.InputTextBox is null)
        {
            return;
        }

        keypad.SetInputText(NormalizeText(e.NewValue as string));
        keypad.TextChanged?.Invoke(keypad, e);
    }

    private void OnDigitClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string digit })
        {
            AppendText(digit);
        }
    }

    private void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string command })
        {
            return;
        }

        switch (command)
        {
            case "DECIMAL":
                if (!Text.Contains('.', StringComparison.Ordinal))
                {
                    AppendText(".");
                }

                break;
            case "BACKSPACE":
                SetInputText(Text.Length <= 1 ? "0" : Text[..^1]);
                break;
            case "CLEAR":
                SetInputText("0");
                break;
        }
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(character => !char.IsDigit(character) && character != '.');
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingText)
        {
            return;
        }

        SetInputText(NormalizeText(InputTextBox.Text));
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            SetInputText("0");
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            SetInputText(Text.Length <= 1 ? "0" : Text[..^1]);
            e.Handled = true;
        }
    }

    private void AppendText(string value)
    {
        if (value == "." && Text.Contains('.', StringComparison.Ordinal))
        {
            return;
        }

        if (Text == "0" && value != ".")
        {
            SetInputText(value);
        }
        else if (Text.Length < 12)
        {
            SetInputText(Text + value);
        }
    }

    private void SetInputText(string value)
    {
        var normalized = NormalizeText(value);
        _updatingText = true;
        try
        {
            if (InputTextBox.Text != normalized)
            {
                InputTextBox.Text = normalized;
            }

            SetCurrentValue(TextProperty, normalized);
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
        }
        finally
        {
            _updatingText = false;
        }
    }

    private static string NormalizeText(string? value)
    {
        var normalized = new string((value ?? string.Empty)
            .Where(character => char.IsDigit(character) || character == '.')
            .ToArray());
        var decimalIndex = normalized.IndexOf('.', StringComparison.Ordinal);
        if (decimalIndex >= 0)
        {
            normalized = normalized[..(decimalIndex + 1)]
                + normalized[(decimalIndex + 1)..].Replace(".", string.Empty, StringComparison.Ordinal);
        }

        if (normalized.Length > 12)
        {
            normalized = normalized[..12];
        }

        return normalized.Length == 0 || normalized == "." ? "0" : normalized;
    }
}
