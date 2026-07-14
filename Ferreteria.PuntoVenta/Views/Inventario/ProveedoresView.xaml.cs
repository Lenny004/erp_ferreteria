using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Inventario;

public partial class ProveedoresView : UserControl
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentSessionService _currentSession;
    private Guid? _selectedId;

    public ProveedoresView(ISupplierService supplierService, ICurrentSessionService currentSession)
    {
        _supplierService = supplierService;
        _currentSession = currentSession;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private Guid CurrentUserId => _currentSession.CurrentEmployee?.Id ?? Guid.Empty;

    private async Task ReloadAsync()
    {
        try
        {
            var items = await _supplierService.GetSuppliersAsync(SearchTextBox.Text);
            ItemsList.ItemsSource = items;
            EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo cargar la lista: {ex.Message}");
        }
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e) => await ReloadAsync();

    private void OnNuevoClick(object sender, RoutedEventArgs e) => ClearForm();

    private void OnCancelarClick(object sender, RoutedEventArgs e) => ClearForm();

    private async void OnRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
        {
            return;
        }

        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier is null)
        {
            return;
        }

        _selectedId = supplier.Id;
        FormTitle.Text = "Editar proveedor";
        NameBox.Text = supplier.Name;
        TradeNameBox.Text = supplier.TradeName;
        NitBox.Text = supplier.Nit;
        NrcBox.Text = supplier.Nrc;
        ContactBox.Text = supplier.ContactName;
        PhoneBox.Text = supplier.Phone;
        EmailBox.Text = supplier.Email;
        AddressBox.Text = supplier.Address;
        CreditDaysBox.Text = supplier.CreditDays.ToString();
        NotesBox.Text = supplier.Notes;
        DeactivateButton.Visibility = Visibility.Visible;
        HideError();
    }

    private async void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        HideError();

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ShowError("El nombre es obligatorio.");
            return;
        }

        if (!int.TryParse(CreditDaysBox.Text, out var creditDays) || creditDays < 0)
        {
            ShowError("Los dias de credito deben ser un numero valido.");
            return;
        }

        var input = new SupplierInput(
            NameBox.Text,
            NullIfEmpty(TradeNameBox.Text),
            NullIfEmpty(NitBox.Text),
            NullIfEmpty(NrcBox.Text),
            NullIfEmpty(ContactBox.Text),
            NullIfEmpty(PhoneBox.Text),
            NullIfEmpty(EmailBox.Text),
            NullIfEmpty(AddressBox.Text),
            null,
            null,
            creditDays,
            NullIfEmpty(NotesBox.Text));

        try
        {
            if (_selectedId is { } id)
            {
                await _supplierService.UpdateAsync(id, input, CurrentUserId);
            }
            else
            {
                await _supplierService.CreateAsync(input, CurrentUserId);
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

    private async void OnDesactivarClick(object sender, RoutedEventArgs e)
    {
        if (_selectedId is not { } id)
        {
            return;
        }

        if (MessageBox.Show("Desactivar este proveedor?", "Proveedores",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _supplierService.DeactivateAsync(id, CurrentUserId);
            ClearForm();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo desactivar: {ex.Message}");
        }
    }

    private void ClearForm()
    {
        _selectedId = null;
        FormTitle.Text = "Nuevo proveedor";
        NameBox.Text = TradeNameBox.Text = NitBox.Text = NrcBox.Text = string.Empty;
        ContactBox.Text = PhoneBox.Text = EmailBox.Text = AddressBox.Text = NotesBox.Text = string.Empty;
        CreditDaysBox.Text = "0";
        DeactivateButton.Visibility = Visibility.Collapsed;
        HideError();
    }

    private void ShowError(string message)
    {
        FormErrorText.Text = message;
        FormErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => FormErrorText.Visibility = Visibility.Collapsed;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
