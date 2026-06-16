using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FlexoCableSV.PuntoVenta.Views.Inicio;
using FlexoCableSV.PuntoVenta.Views.Shell;

namespace FlexoCableSV.PuntoVenta.Views.PIN;

public partial class PinWindow : Window
{
    private static readonly Dictionary<string, string> PinesValidos = new()
    {
        ["1234"] = "Admin",
        ["5678"] = "Tecnico 1",
        ["0000"] = "Caja Demo"
    };

    private const int PinLength = 4;
    private static readonly SolidColorBrush ColorVacio = new(Color.FromRgb(0x47, 0x47, 0x46));
    private static readonly SolidColorBrush ColorRelleno = new(Color.FromRgb(0xD2, 0x25, 0x33));

    private string _pinActual = string.Empty;

    public PinWindow()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnTeclaFisica;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        var inicio = new InicioWindow();
        inicio.Show();
        Close();
    }

    private void OnDigitoClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            AgregarDigito(button.Tag?.ToString() ?? string.Empty);
        }
    }

    private void OnBorrarClick(object sender, RoutedEventArgs e)
    {
        BorrarUltimoDigito();
    }

    private void OnConfirmarClick(object sender, RoutedEventArgs e)
    {
        ValidarPin();
    }

    private void OnTeclaFisica(object sender, KeyEventArgs e)
    {
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            AgregarDigito(((int)(e.Key - Key.D0)).ToString());
        }
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            AgregarDigito(((int)(e.Key - Key.NumPad0)).ToString());
        }
        else if (e.Key is Key.Back or Key.Delete)
        {
            BorrarUltimoDigito();
        }
        else if (e.Key == Key.Enter)
        {
            ValidarPin();
        }
    }

    private void AgregarDigito(string digito)
    {
        OcultarError();

        if (_pinActual.Length >= PinLength || string.IsNullOrEmpty(digito))
        {
            return;
        }

        _pinActual += digito;
        ActualizarDots();

        if (_pinActual.Length == PinLength)
        {
            ValidarPin();
        }
    }

    private void BorrarUltimoDigito()
    {
        OcultarError();

        if (_pinActual.Length == 0)
        {
            return;
        }

        _pinActual = _pinActual[..^1];
        ActualizarDots();
    }

    private void ValidarPin()
    {
        if (_pinActual.Length < PinLength)
        {
            MostrarError("Ingresa los 4 digitos completos.");
            return;
        }

        if (PinesValidos.ContainsKey(_pinActual))
        {
            var shell = new MainShellWindow("Facturacion");
            shell.Show();
            Close();
            return;
        }

        MostrarError("PIN incorrecto. Intente de nuevo.");
        AnimarError();
        LimpiarPin();
    }

    private void LimpiarPin()
    {
        _pinActual = string.Empty;
        ActualizarDots();
    }

    private void ActualizarDots()
    {
        Dot1.Fill = _pinActual.Length >= 1 ? ColorRelleno : ColorVacio;
        Dot2.Fill = _pinActual.Length >= 2 ? ColorRelleno : ColorVacio;
        Dot3.Fill = _pinActual.Length >= 3 ? ColorRelleno : ColorVacio;
        Dot4.Fill = _pinActual.Length >= 4 ? ColorRelleno : ColorVacio;
    }

    private void MostrarError(string mensaje)
    {
        ErrorMsg.Text = mensaje;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void OcultarError()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void AnimarError()
    {
        var transform = new TranslateTransform();
        ErrorPanel.RenderTransform = transform;

        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(-6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
