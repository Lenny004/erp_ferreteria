using System.Windows;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Coordina la recuperación o apertura interactiva de la sesión de caja del empleado autenticado.
/// </summary>
/// <remarks>
/// Centraliza el flujo usado por el shell y facturación: consulta el servidor, rechaza una caja abierta
/// por otro cajero, solicita el fondo táctil, confirma la apertura y actualiza <see cref="ICurrentSessionService"/>.
/// </remarks>
public sealed class CashSessionOpeningFlow
{
    private readonly ICashSessionService _cashSessionService;
    private readonly ICurrentSessionService _currentSession;
    private readonly CashRegisterOptions _options;
    private readonly ILogger<CashSessionOpeningFlow> _logger;

    /// <summary>Inicializa el flujo con los servicios de caja y sesión local.</summary>
    /// <param name="cashSessionService">Servicio transaccional de sesiones.</param>
    /// <param name="currentSession">Estado del empleado autenticado.</param>
    /// <param name="options">Configuración de la caja física.</param>
    /// <param name="logger">Registrador de fallos técnicos.</param>
    public CashSessionOpeningFlow(
        ICashSessionService cashSessionService,
        ICurrentSessionService currentSession,
        IOptions<CashRegisterOptions> options,
        ILogger<CashSessionOpeningFlow> logger)
    {
        _cashSessionService = cashSessionService ?? throw new ArgumentNullException(nameof(cashSessionService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Recupera la sesión propia o guía al empleado para abrir una nueva caja.
    /// </summary>
    /// <param name="owner">Ventana propietaria de los diálogos, si existe.</param>
    /// <param name="askBeforeOpening">Indica si debe solicitarse una confirmación previa adicional.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> cuando queda una sesión propia activa; de lo contrario, <c>false</c>.</returns>
    /// <remarks>
    /// La consulta de sesión y la apertura se delegan al servicio de caja; este flujo solo coordina UI y
    /// estado local. Los detalles de excepciones técnicas se escriben en el log y no se muestran al usuario.
    /// </remarks>
    public async Task<bool> EnsureOpenAsync(
        Window? owner,
        bool askBeforeOpening = false,
        CancellationToken cancellationToken = default)
    {
        if (_currentSession.CurrentEmployee is not { } employee)
        {
            ShowMessage(owner, "No hay un empleado autenticado para operar caja.", "Acceso a caja", MessageBoxImage.Warning);
            return false;
        }

        try
        {
            var cashRegisterCode = CashRegisterInputRules.ValidateCashRegisterCode(_options.Codigo);
            var openSession = await _cashSessionService.GetOpenSessionAsync(cashRegisterCode, cancellationToken);
            if (openSession is not null)
            {
                if (openSession.EmployeeId == employee.Id)
                {
                    _currentSession.SetActiveCashSession(openSession.Id);
                    return true;
                }

                _currentSession.ClearActiveCashSession();
                ShowMessage(
                    owner,
                    "La caja está abierta por otro cajero. Debe cerrarse el turno antes de abrir uno nuevo.",
                    "Caja no disponible",
                    MessageBoxImage.Warning);
                return false;
            }

            if (!employee.CanCashier)
            {
                _currentSession.ClearActiveCashSession();
                ShowMessage(owner, "El empleado autenticado no tiene permiso para operar caja.", "Acceso a caja", MessageBoxImage.Warning);
                return false;
            }

            if (askBeforeOpening
                && ShowMessage(
                    owner,
                    "No hay una caja abierta. ¿Desea abrirla ahora?",
                    "Abrir caja",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return false;
            }

            var openingDialog = new CashOpeningDialog
            {
                Owner = owner
            };
            if (openingDialog.ShowDialog() != true)
            {
                return false;
            }

            var session = await _cashSessionService.OpenAsync(
                employee.Id,
                cashRegisterCode,
                openingDialog.OpeningAmount,
                openingDialog.Notes,
                cancellationToken);
            _currentSession.SetActiveCashSession(session.Id);
            return true;
        }
        catch (CashSessionException exception)
        {
            _logger.LogWarning(exception, "No se pudo preparar la sesión de caja para {EmployeeId}", employee.Id);
            _currentSession.ClearActiveCashSession();
            ShowMessage(owner, "No se pudo abrir o recuperar la caja. Verifique si ya existe un turno abierto.", "Caja no disponible", MessageBoxImage.Warning);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error técnico al preparar la sesión de caja");
            _currentSession.ClearActiveCashSession();
            ShowMessage(owner, "No se pudo preparar la caja. El cobro permanecerá bloqueado hasta resolver el problema.", "Caja no disponible", MessageBoxImage.Error);
            return false;
        }
    }

    private static MessageBoxResult ShowMessage(
        Window? owner,
        string message,
        string title,
        MessageBoxImage image,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        return owner is null
            ? MessageBox.Show(message, title, buttons, image)
            : MessageBox.Show(owner, message, title, buttons, image);
    }
}
