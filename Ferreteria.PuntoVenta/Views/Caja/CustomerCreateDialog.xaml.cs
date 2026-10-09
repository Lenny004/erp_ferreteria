using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Helpers;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>
/// Diálogo para crear un cliente durante el flujo de facturación.
/// </summary>
/// <remarks>
/// La persistencia se delega en <see cref="ICustomerService"/>, que conserva la autorización,
/// validación y auditoría de la operación.
/// </remarks>
public partial class CustomerCreateDialog : Window
{
    private readonly ICustomerService _customerService;
    private readonly Guid _userId;

    /// <summary>Inicializa el diálogo de alta de cliente.</summary>
    /// <param name="customerService">Servicio autorizado para crear clientes.</param>
    /// <param name="userId">Empleado autenticado que ejecuta la operación.</param>
    public CustomerCreateDialog(ICustomerService customerService, Guid userId)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        _userId = userId;
        InitializeComponent();
        UpdateFiscalRequirementVisuals();
    }

    /// <summary>Identificador del cliente creado al cerrar con éxito.</summary>
    public Guid CreatedCustomerId { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus();
    }

    private void OnCustomerTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFiscalRequirementVisuals();
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var customerType = GetSelectedCustomerType();
        var name = NameTextBox.Text.Trim();
        var nit = EmptyAsNull(NitTextBox.Text);
        var nrc = EmptyAsNull(NrcTextBox.Text);

        if (string.IsNullOrWhiteSpace(name))
        {
            SetError("El nombre o razón social es obligatorio.");
            NameTextBox.Focus();
            return;
        }

        if (customerType == "CCF" && string.IsNullOrWhiteSpace(nit))
        {
            SetError("Un cliente de crédito fiscal requiere NIT.");
            NitTextBox.Focus();
            return;
        }

        if (customerType == "CCF" && string.IsNullOrWhiteSpace(nrc))
        {
            SetError("Un cliente de crédito fiscal requiere NRC.");
            NrcTextBox.Focus();
            return;
        }

        try
        {
            CreatedCustomerId = await _customerService.CreateAsync(
                new CustomerInput(
                    customerType,
                    name,
                    EmptyAsNull(DuiTextBox.Text),
                    nit,
                    nrc,
                    EmptyAsNull(PhoneTextBox.Text),
                    EmptyAsNull(EmailTextBox.Text),
                    EmptyAsNull(AddressTextBox.Text),
                    null,
                    null),
                _userId);
            DialogResult = true;
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (Exception)
        {
            SetError("No se pudo crear el cliente. Revise los campos e intente nuevamente.");
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private string GetSelectedCustomerType() =>
        (CustomerTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "CF";

    private void UpdateFiscalRequirementVisuals()
    {
        if (NitLabel is null || NrcLabel is null)
        {
            return;
        }

        var isCreditFiscal = GetSelectedCustomerType() == "CCF";
        FieldHelper.SetIsRequired(NitLabel, isCreditFiscal);
        FieldHelper.SetIsRequired(NrcLabel, isCreditFiscal);
        NitLabel.Content = "NIT";
        NrcLabel.Content = "NRC";
    }

    private void SetError(string message)
    {
        ErrorText.Text = message;
    }

    private static string? EmptyAsNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
