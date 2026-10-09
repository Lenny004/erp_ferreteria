using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Helpers;

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

    /// <summary>Propiedad de dependencia de la precisión total del valor capturado.</summary>
    public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register(
        nameof(Precision),
        typeof(int),
        typeof(NumericKeypad),
        new PropertyMetadata(12));

    /// <summary>Propiedad de dependencia de la escala decimal del valor capturado.</summary>
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale),
        typeof(int),
        typeof(NumericKeypad),
        new PropertyMetadata(2));

    /// <summary>Propiedad de dependencia que permite valores negativos.</summary>
    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.Register(
        nameof(AllowNegative),
        typeof(bool),
        typeof(NumericKeypad),
        new PropertyMetadata(false));

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

    /// <summary>Precisión total máxima del valor capturado; por defecto, 12 dígitos.</summary>
    public int Precision
    {
        get => (int)GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, value);
    }

    /// <summary>Escala decimal máxima del valor capturado; por defecto, 2 decimales.</summary>
    public int Scale
    {
        get => (int)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Indica si se aceptan valores negativos; por defecto, <see langword="false"/>.</summary>
    public bool AllowNegative
    {
        get => (bool)GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
    }

    /// <summary>Se produce cuando cambia el texto monetario capturado.</summary>
    public event DependencyPropertyChangedEventHandler? TextChanged;

    /// <summary>Inicializa el control numérico.</summary>
    public NumericKeypad()
    {
        InitializeComponent();
        InputTextBox.TextChanged += OnInputTextChanged;
        InputTextBox.KeyDown += OnInputKeyDown;
    }

    /// <summary>Coloca el foco de teclado en el campo decimal interno.</summary>
    public void FocusInput()
    {
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private static void OnTextPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not NumericKeypad keypad || keypad.InputTextBox is null)
        {
            return;
        }

        keypad.SetInputText(keypad.NormalizeText(e.NewValue as string));
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
                if (Scale > 0 && !Text.Contains('.', StringComparison.Ordinal))
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
        if (value == "." && (Text.Contains('.', StringComparison.Ordinal)
            || Text.Contains(',', StringComparison.Ordinal)))
        {
            return;
        }

        var replacingInitialZero = Text == "0" && value != ".";
        var selectionStart = replacingInitialZero ? 0 : Text.Length;
        var selectionLength = replacingInitialZero ? Text.Length : 0;
        var result = NumericInputTextRules.Validate(
                Text,
                selectionStart,
                selectionLength,
                value,
                Precision,
                Scale,
                AllowNegative);
        NumericInput.SetErrorMessage(InputTextBox, result.IsAllowed ? null : result.ErrorMessage);
        if (!result.IsAllowed)
        {
            return;
        }

        if (replacingInitialZero)
        {
            SetInputText(value);
        }
        else
        {
            SetInputText(Text + value);
        }
    }

    private void SetInputText(string value)
    {
        var normalized = NormalizeText(value);
        NumericInput.SetErrorMessage(InputTextBox, null);
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

    private string NormalizeText(string? value)
    {
        var normalized = value ?? string.Empty;
        return normalized.Length == 0 ? "0" : normalized;
    }
}
