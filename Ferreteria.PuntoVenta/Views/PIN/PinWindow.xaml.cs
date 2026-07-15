using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Views.Inicio;
using Ferreteria.PuntoVenta.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Ferreteria.PuntoVenta.Views.PIN;

/// <summary>
/// Pantalla de autenticación por PIN de Employee (4 dígitos).
/// Valida acceso al módulo Caja o Inventario y, si es correcto, abre <see cref="MainShellWindow"/>.
/// Seguridad: el PIN nunca se escribe en logs ni en MessageBox; solo se muestran dots y mensajes de error genéricos.
/// </summary>
public partial class PinWindow : Window
{
    private const int PinLength = 4;
    private static readonly SolidColorBrush ColorVacio = new(Color.FromRgb(0x5A, 0x30, 0x38));
    private static readonly SolidColorBrush ColorRelleno = new(Color.FromRgb(0xE6, 0x0A, 0x2E));

    private readonly PinAuthService _pinAuth;
    private readonly IServiceProvider _services;
    private readonly ICurrentSessionService _currentSession;
    private readonly IAuditService _auditService;
    private readonly IPinAttemptService _pinAttemptService;
    private readonly OperationalModule _module;
    private readonly string _initialSection;
    private string _pinIngresado = string.Empty;
    private bool _validando;

    /// <summary>
    /// Crea la ventana PIN para el módulo indicado (Caja → facturación, Inventario → productos, etc.).
    /// </summary>
    /// <param name="module">Módulo operativo al que se intenta entrar.</param>
    /// <param name="initialSection">Sección inicial del shell tras login (opcional).</param>
    public PinWindow(
        PinAuthService pinAuth,
        IServiceProvider services,
        ICurrentSessionService currentSession,
        IAuditService auditService,
        IPinAttemptService pinAttemptService,
        OperationalModule module,
        string initialSection = "")
    {
        _pinAuth = pinAuth;
        _services = services;
        _currentSession = currentSession;
        _auditService = auditService;
        _pinAttemptService = pinAttemptService;
        _module = module;
        _initialSection = string.IsNullOrWhiteSpace(initialSection)
            ? NavSections.DefaultSection(module)
            : initialSection;

        InitializeComponent();

        PinTitleText.Text = _module switch
        {
            OperationalModule.Caja => "Acceso a Caja",
            OperationalModule.Inventario => "Acceso a Inventario",
            _ => "Ingrese su PIN"
        };

        PinSubtitleText.Text = _module switch
        {
            OperationalModule.Caja => "PIN de cajero autorizado",
            OperationalModule.Inventario => "PIN de encargado de inventario",
            _ => "4 digitos"
        };

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnTeclaFisica;
    }

    /// <summary>Permite arrastrar la ventana sin borde.</summary>
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>Minimiza la ventana de PIN.</summary>
    private void OnMinimizarClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>Cancela el login y regresa a InicioWindow.</summary>
    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        var inicio = _services.GetRequiredService<InicioWindow>();
        inicio.Show();
        Close();
    }

    /// <summary>Handler del teclado numérico en pantalla (Tag = dígito).</summary>
    private void OnDigitoClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            AgregarDigito(button.Tag?.ToString() ?? string.Empty);
        }
    }

    /// <summary>Borra el último dígito del PIN ingresado.</summary>
    private void OnBorrarClick(object sender, RoutedEventArgs e)
    {
        BorrarUltimoDigito();
    }

    /// <summary>Confirma el PIN cuando ya hay 4 dígitos (o muestra error si faltan).</summary>
    private void OnConfirmarClick(object sender, RoutedEventArgs e)
    {
        _ = ValidarPinAsync();
    }

    /// <summary>Acepta dígitos, Backspace/Delete y Enter desde el teclado físico.</summary>
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

    /// <summary>Agrega un dígito y dispara validación automática al completar 4 caracteres.</summary>
    private void AgregarDigito(string digito)
    {
        if (_validando)
            return;

        if (MostrarBloqueoSiAplica())
            return;

        OcultarError();

        if (_pinIngresado.Length >= PinLength || string.IsNullOrEmpty(digito))
        {
            return;
        }

        _pinIngresado += digito;
        ActualizarDots();

        if (_pinIngresado.Length == PinLength)
        {
            _ = ValidarPinAsync();
        }
    }

    /// <summary>Elimina el último dígito y actualiza los dots.</summary>
    private void BorrarUltimoDigito()
    {
        if (_validando)
            return;

        OcultarError();

        if (_pinIngresado.Length == 0)
        {
            return;
        }

        _pinIngresado = _pinIngresado[..^1];
        ActualizarDots();
    }

    /// <summary>
    /// Valida el PIN contra PinAuthService, aplica lockout por intentos fallidos
    /// e inicia sesión + auditoría de login si es correcto.
    /// </summary>
    private async Task ValidarPinAsync()
    {
        if (_validando)
            return;

        if (MostrarBloqueoSiAplica())
            return;

        if (_pinIngresado.Length < PinLength)
        {
            MostrarError("Ingresa los 4 digitos completos.");
            return;
        }

        _validando = true;
        try
        {
            var employee = await _pinAuth.ValidatePinAsync(_pinIngresado, _module);
            if (employee is null)
            {
                var status = _pinAttemptService.RegisterFailedAttempt();
                MostrarError(BuildFailedAttemptMessage(status, _module));
                AnimarError();
                LimpiarPin();
                return;
            }

            _pinAttemptService.Reset();
            _currentSession.StartSession(employee, _module, _initialSection);
            await _auditService.RecordLoginAsync(employee, _currentSession.CurrentModule!);

            var shell = _services.GetRequiredService<MainShellWindow>();
            shell.Show();
            Close();
        }
        catch (Exception ex)
        {
            // No incluir el PIN en el mensaje: solo el error técnico.
            MostrarError($"Error al validar PIN: {ex.Message}");
            LimpiarPin();
        }
        finally
        {
            _validando = false;
        }
    }

    /// <summary>Vacía el PIN en memoria y reinicia los dots.</summary>
    private void LimpiarPin()
    {
        _pinIngresado = string.Empty;
        ActualizarDots();
    }

    /// <summary>Pinta los 4 dots según cuántos dígitos hay (sin mostrar el valor).</summary>
    private void ActualizarDots()
    {
        Dot1.Fill = _pinIngresado.Length >= 1 ? ColorRelleno : ColorVacio;
        Dot2.Fill = _pinIngresado.Length >= 2 ? ColorRelleno : ColorVacio;
        Dot3.Fill = _pinIngresado.Length >= 3 ? ColorRelleno : ColorVacio;
        Dot4.Fill = _pinIngresado.Length >= 4 ? ColorRelleno : ColorVacio;
    }

    /// <summary>Muestra un mensaje de error en el panel de la UI.</summary>
    private void MostrarError(string mensaje)
    {
        ErrorMsg.Text = mensaje;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Si hay lockout activo por intentos fallidos, muestra el mensaje y limpia el PIN.</summary>
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

    /// <summary>Arma el mensaje de PIN incorrecto (sin revelar el valor ingresado).</summary>
    private static string BuildFailedAttemptMessage(PinAttemptStatus status, OperationalModule module)
    {
        if (status.IsLocked)
        {
            return BuildLockoutMessage(status);
        }

        var roleHint = module switch
        {
            OperationalModule.Caja => "cajero",
            OperationalModule.Inventario => "encargado de inventario",
            _ => "usuario"
        };

        return $"PIN incorrecto o sin permiso de {roleHint}. Intentos restantes: {status.RemainingAttempts}.";
    }

    /// <summary>Mensaje de bloqueo temporal tras demasiados intentos.</summary>
    private static string BuildLockoutMessage(PinAttemptStatus status)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(status.RemainingLockout.TotalSeconds));
        return $"Demasiados intentos. Espere {seconds} segundos.";
    }

    /// <summary>Oculta el panel de error.</summary>
    private void OcultarError()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>Animación de sacudida del panel de error.</summary>
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
