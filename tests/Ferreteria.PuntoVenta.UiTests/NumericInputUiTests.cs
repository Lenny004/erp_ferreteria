using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Helpers;
using Xunit;

namespace Ferreteria.PuntoVenta.UiTests;

/// <summary>
/// Verifica el comportamiento WPF de la propiedad adjunta <see cref="NumericInput.IsEnabledProperty"/>.
/// </summary>
public sealed class NumericInputUiTests
{
    /// <summary>Comprueba el enganche con precisión, escala y signo predeterminados.</summary>
    [Fact]
    public void IsEnabled_UsesDefaultRulesAndReportsBlockedText()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInput.SetIsEnabled(textBox, true);

            Assert.Equal(12, NumericInput.GetPrecision(textBox));
            Assert.Equal(2, NumericInput.GetScale(textBox));
            Assert.False(NumericInput.GetAllowNegative(textBox));

            var letter = RaisePreviewTextInput(textBox, "a");
            Assert.True(letter.Handled);
            Assert.False(string.IsNullOrWhiteSpace(NumericInput.GetErrorMessage(textBox)));

            var digit = RaisePreviewTextInput(textBox, "5");
            Assert.False(digit.Handled);
            Assert.Null(NumericInput.GetErrorMessage(textBox));

            var negativeSign = RaisePreviewTextInput(textBox, "-");
            Assert.True(negativeSign.Handled);
            Assert.False(string.IsNullOrWhiteSpace(NumericInput.GetErrorMessage(textBox)));
        });
    }

    /// <summary>Comprueba que el pegado completo usa el parser y no acepta un prefijo ambiguo.</summary>
    [Fact]
    public void IsEnabled_PasteRequiresCompleteParserValue()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInput.SetIsEnabled(textBox, true);

            var validPaste = RaisePaste(textBox, "1,234.50");
            Assert.False(validPaste.CommandCancelled);
            Assert.Null(NumericInput.GetErrorMessage(textBox));

            var invalidPaste = RaisePaste(textBox, "1,234");
            Assert.True(invalidPaste.CommandCancelled);
            Assert.False(string.IsNullOrWhiteSpace(NumericInput.GetErrorMessage(textBox)));
        });
    }

    /// <summary>Comprueba que un pegado válido con espacios inserta únicamente el valor recortado.</summary>
    [Fact]
    public void IsEnabled_PasteTrimsOuterWhitespaceBeforeInsertion()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInput.SetIsEnabled(textBox, true);

            var paste = RaisePaste(textBox, " 1,234.50 ");

            Assert.True(paste.CommandCancelled);
            Assert.Equal("1,234.50", textBox.Text);
            Assert.Null(NumericInput.GetErrorMessage(textBox));
        });
    }

    private static TextCompositionEventArgs RaisePreviewTextInput(TextBox textBox, string text)
    {
        var composition = new TextComposition(InputManager.Current, textBox, text);
        var args = new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, composition)
        {
            RoutedEvent = UIElement.PreviewTextInputEvent,
        };
        textBox.RaiseEvent(args);
        return args;
    }

    private static DataObjectPastingEventArgs RaisePaste(TextBox textBox, string text)
    {
        var dataObject = new DataObject(DataFormats.Text, text);
        var args = new DataObjectPastingEventArgs(dataObject, false, DataFormats.Text)
        {
            RoutedEvent = DataObject.PastingEvent,
        };
        textBox.RaiseEvent(args);
        return args;
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw new Xunit.Sdk.XunitException("La prueba STA falló.", exception);
        }
    }
}
