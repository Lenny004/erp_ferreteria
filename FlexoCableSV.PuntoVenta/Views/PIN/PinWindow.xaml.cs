using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FlexoCableSV.PuntoVenta.Services;
using FlexoCableSV.PuntoVenta.Views.Inicio;
using FlexoCableSV.PuntoVenta.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace FlexoCableSV.PuntoVenta.Views.PIN;

public partial class PinWindow : Window
{
    private const int PinLength = 4;
    private static readonly SolidColorBrush ColorVacio = new(Color.FromRgb(0x47, 0x47, 0x46));
    private static readonly SolidColorBrush ColorRelleno = new(Color.FromRgb(0xD2, 0x25, 0x33));

    private readonly PinAuthService _pinAuth;
    private readonly IServiceProvider _services;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuditService _auditService;
    private readonly IPinAttemptService _pinAttemptService;
    private readonly string _initialSection;
    private string _pinActual = string.Empty;
    private bool _validando;

    public PinWindow(
        PinAuthService pinAuth,
        IServiceProvider services,
        ICurrentSessionService currentSession,
        IAuditService auditService,
        IPinAttemptService pinAttemptService,
        string initialSection = "Facturacion")
    {
        _pinAuth = pinAuth;
        _services = services;
        _currentSession = currentSession;
        _auditService = auditService;
        _pinAttemptService = pinAttemptService;
        _initialSection = initialSection;

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
        var inicio = _services.GetRequiredService<InicioWindow>();
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
        _ = ValidarPinAsync();
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
            _ = ValidarPinAsync();
        }
    }

    private void AgregarDigito(string digito)
    {
        if (_validando)
            return;

        if (MostrarBloqueoSiAplica())
            return;

        OcultarError();

        if (_pinActual.Length >= PinLength || string.IsNullOrEmpty(digito))
        {
            return;
        }

        _pinActual += digito;
        ActualizarDots();

        if (_pinActual.Length == PinLength)
        {
            _ = ValidarPinAsync();
        }
    }

    private void BorrarUltimoDigito()
    {
        if (_validando)
            return;

        OcultarError();

        if (_pinActual.Length == 0)
        {
            return;
        }

        _pinActual = _pinActual[..^1];
        ActualizarDots();
    }

    private async Task ValidarPinAsync()
    {
        if (_validando)
            return;

        if (MostrarBloqueoSiAplica())
            return;

        if (_pinActual.Length < PinLength)
        {
            MostrarError("Ingresa los 4 digitos completos.");
            return;
        }

        _validando = true;
        try
        {
            var employee = await _pinAuth.ValidatePinAsync(_pinActual);
            if (employee is null)
            {
                var status = _pinAttemptService.RegisterFailedAttempt();
                MostrarError(BuildFailedAttemptMessage(status));
                AnimarError();
                LimpiarPin();
                return;
            }

            _pinAttemptService.Reset();
            _currentSession.StartSession(employee, _initialSection);
            await _auditService.RecordLoginAsync(employee, _initialSection);

            var shell = ActivatorUtilities.CreateInstance<MainShellWindow>(_services, _initialSection);
            shell.Show();
            Close();
        }
        catch (Exception ex)
        {
            MostrarError($"Error al validar PIN: {ex.Message}");
            LimpiarPin();
        }
        finally
        {
            _validando = false;
        }
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

    private bool MostrarBloqueoSiAplica()
    {
        var status = _pinAttemptService.GetStatus();
        if (!status.IsLocked)
        {
            return false;
        }

        MostrarError(BuildLockoutMessage(status));
        LimpiarPin();
        return true;
    }

    private static string BuildFailedAttemptMessage(PinAttemptStatus status)
    {
        return status.IsLocked
            ? BuildLockoutMessage(status)
            : $"PIN incorrecto. Intentos restantes: {status.RemainingAttempts}.";
    }

    private static string BuildLockoutMessage(PinAttemptStatus status)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(status.RemainingLockout.TotalSeconds));
        return $"Demasiados intentos. Espere {seconds} segundos.";
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
