using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Inventario;

/// <summary>
/// ABM de Employee (usuarios / RRHH): datos laborales, permisos de caja/venta y PIN de acceso.
/// Seguridad: el PIN se captura en PasswordBox, nunca se lee desde BD al editar ni se registra en logs.
/// </summary>
public partial class UsuariosView : UserControl
{
    private readonly IEmployeeService _employees;
    private readonly ICurrentSessionService _currentSession;
    private Guid? _selectedId;

    /// <summary>Inicializa la vista e hidrata departamentos/puestos al Loaded.</summary>
    public UsuariosView(IEmployeeService employees, ICurrentSessionService currentSession)
    {
        _employees = employees;
        _currentSession = currentSession;
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    /// <summary>Id del Employee en sesión (auditoría de cambios).</summary>
    private Guid CurrentUserId => _currentSession.CurrentEmployee?.Id ?? Guid.Empty;

    /// <summary>Carga combos de departamento/puesto y la lista de empleados.</summary>
    private async Task InitializeAsync()
    {
        try
        {
            DepartmentCombo.ItemsSource = await _employees.GetDepartmentsAsync();
            PositionCombo.ItemsSource = await _employees.GetPositionsAsync(null);
            HireDatePicker.SelectedDate = DateTime.Today;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo inicializar: {ex.Message}");
        }
    }

    /// <summary>Recarga empleados según el filtro de búsqueda.</summary>
    private async Task ReloadAsync()
    {
        var items = await _employees.GetEmployeesAsync(SearchTextBox.Text);
        ItemsList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Handler de búsqueda por nombre/DUI.</summary>
    private async void OnSearchChanged(object sender, TextChangedEventArgs e) => await ReloadAsync();

    /// <summary>Prepara el formulario para un usuario nuevo (PIN obligatorio).</summary>
    private void OnNuevoClick(object sender, RoutedEventArgs e) => ClearForm();

    /// <summary>Cancela la edición y limpia el formulario.</summary>
    private void OnCancelarClick(object sender, RoutedEventArgs e) => ClearForm();

    /// <summary>Al cambiar departamento, filtra los puestos disponibles.</summary>
    private async void OnDepartmentChanged(object sender, SelectionChangedEventArgs e)
    {
        var depId = DepartmentCombo.SelectedValue as Guid?;
        PositionCombo.ItemsSource = await _employees.GetPositionsAsync(depId);
    }

    /// <summary>
    /// Selecciona un Employee y rellena el formulario.
    /// El PasswordBox de PIN queda vacío a propósito (no se recupera el hash).
    /// </summary>
    private async void OnRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
        {
            return;
        }

        var employee = await _employees.GetByIdAsync(id);
        if (employee is null)
        {
            return;
        }

        _selectedId = employee.Id;
        FormTitle.Text = "Editar usuario";
        FirstNameBox.Text = employee.FirstName;
        LastNameBox.Text = employee.LastName;
        DuiBox.Text = employee.Dui;
        DepartmentCombo.SelectedValue = employee.DepartmentId;
        PositionCombo.ItemsSource = await _employees.GetPositionsAsync(employee.DepartmentId);
        PositionCombo.SelectedValue = employee.PositionId;
        HireDatePicker.SelectedDate = employee.HireDate;
        SalaryBox.Text = employee.BaseSalary.ToString(CultureInfo.InvariantCulture);
        SelectByTag(ContractCombo, employee.ContractType);
        SelectByTag(SalaryTypeCombo, employee.SalaryType);
        PhoneBox.Text = employee.Phone;
        EmailBox.Text = employee.Email;
        CanCashierCheck.IsChecked = employee.CanCashier;
        CanSellCheck.IsChecked = employee.CanSell;
        PinBox.Password = string.Empty;
        PinLabel.Text = "Nuevo PIN (dejar vacio para no cambiar)";
        DeactivateButton.Visibility = Visibility.Visible;
        HideError();
    }

    /// <summary>
    /// Crea o actualiza Employee; el PIN solo se envía si el PasswordBox tiene valor (alta o cambio).
    /// </summary>
    private async void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        HideError();

        if (string.IsNullOrWhiteSpace(FirstNameBox.Text) || string.IsNullOrWhiteSpace(LastNameBox.Text))
        {
            ShowError("Nombre y apellido son obligatorios.");
            return;
        }

        if (!TryParseDecimal(SalaryBox.Text, out var salary) || salary < 0)
        {
            ShowError("El salario base debe ser un numero valido.");
            return;
        }

        var pin = PinBox.Password?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(pin) && (pin.Length != 4 || !pin.All(char.IsDigit)))
        {
            ShowError("El PIN debe tener 4 digitos.");
            return;
        }

        var input = new EmployeeInput(
            FirstNameBox.Text,
            LastNameBox.Text,
            NullIfEmpty(DuiBox.Text),
            PositionCombo.SelectedValue as Guid?,
            DepartmentCombo.SelectedValue as Guid?,
            HireDatePicker.SelectedDate ?? DateTime.Today,
            salary,
            TagOf(ContractCombo, "PLAZO_FIJO"),
            TagOf(SalaryTypeCombo, "MENSUAL"),
            NullIfEmpty(PhoneBox.Text),
            NullIfEmpty(EmailBox.Text),
            CanCashierCheck.IsChecked == true,
            CanSellCheck.IsChecked == true);

        try
        {
            if (_selectedId is { } id)
            {
                await _employees.UpdateAsync(id, input, CurrentUserId);
                if (!string.IsNullOrEmpty(pin))
                {
                    await _employees.SetPinAsync(id, pin, CurrentUserId);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(pin))
                {
                    ShowError("Un usuario nuevo requiere un PIN de acceso.");
                    return;
                }

                await _employees.CreateAsync(input, pin, CurrentUserId);
            }

            ClearForm();
            await ReloadAsync();
        }
        catch (ValidationException vex)
        {
            ShowError(vex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo guardar: {ex.Message}");
        }
    }

    /// <summary>Desactiva el Employee seleccionado tras confirmación.</summary>
    private async void OnDesactivarClick(object sender, RoutedEventArgs e)
    {
        if (_selectedId is not { } id)
        {
            return;
        }

        if (MessageBox.Show("Desactivar este usuario?", "Usuarios",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _employees.DeactivateAsync(id, CurrentUserId);
            ClearForm();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo desactivar: {ex.Message}");
        }
    }

    /// <summary>Limpia el formulario y vuelve al modo "nuevo usuario".</summary>
    private void ClearForm()
    {
        _selectedId = null;
        FormTitle.Text = "Nuevo usuario";
        FirstNameBox.Text = LastNameBox.Text = DuiBox.Text = PhoneBox.Text = EmailBox.Text = string.Empty;
        DepartmentCombo.SelectedIndex = -1;
        PositionCombo.ItemsSource = null;
        HireDatePicker.SelectedDate = DateTime.Today;
        SalaryBox.Text = "0";
        ContractCombo.SelectedIndex = 0;
        SalaryTypeCombo.SelectedIndex = 0;
        CanCashierCheck.IsChecked = false;
        CanSellCheck.IsChecked = false;
        PinBox.Password = string.Empty;
        PinLabel.Text = "PIN de acceso (4 digitos) *";
        DeactivateButton.Visibility = Visibility.Collapsed;
        HideError();
    }

    /// <summary>Selecciona un ComboBoxItem por su Tag.</summary>
    private static void SelectByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                combo.SelectedItem = cbi;
                return;
            }
        }
    }

    /// <summary>Lee el Tag del ítem seleccionado o un valor por defecto.</summary>
    private static string TagOf(ComboBox combo, string fallback) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    /// <summary>Muestra un error en el formulario.</summary>
    private void ShowError(string message)
    {
        FormErrorText.Text = message;
        FormErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>Oculta el mensaje de error.</summary>
    private void HideError() => FormErrorText.Visibility = Visibility.Collapsed;

    /// <summary>Parsea decimal aceptando cultura invariante o actual.</summary>
    private static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value) ||
        decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value);

    /// <summary>Convierte cadena vacía en null para campos opcionales.</summary>
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
