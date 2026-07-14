using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Inventario;

public partial class ProductosView : UserControl
{
    private readonly IProductCatalogService _catalog;
    private readonly ISupplierService _suppliers;
    private readonly ICurrentSessionService _currentSession;
    private Guid? _selectedId;

    public ProductosView(
        IProductCatalogService catalog,
        ISupplierService suppliers,
        ICurrentSessionService currentSession)
    {
        _catalog = catalog;
        _suppliers = suppliers;
        _currentSession = currentSession;
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    private Guid CurrentUserId => _currentSession.CurrentEmployee?.Id ?? Guid.Empty;

    private async Task InitializeAsync()
    {
        try
        {
            FamilyCombo.ItemsSource = await _catalog.GetFamiliesAsync();
            MeasurementCombo.ItemsSource = await _catalog.GetMeasurementTypesAsync();
            SupplierCombo.ItemsSource = await _suppliers.GetSuppliersAsync(null);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo cargar el catalogo: {ex.Message}");
        }
    }

    private async Task ReloadAsync()
    {
        var items = await _catalog.GetProductsAsync(SearchTextBox.Text);
        ItemsList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e) => await ReloadAsync();

    private void OnNuevoClick(object sender, RoutedEventArgs e) => ClearForm();

    private void OnCancelarClick(object sender, RoutedEventArgs e) => ClearForm();

    private async void OnFamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FamilyCombo.SelectedValue is Guid familyId)
        {
            SubfamilyCombo.ItemsSource = await _catalog.GetSubfamiliesAsync(familyId);
        }
        else
        {
            SubfamilyCombo.ItemsSource = null;
        }
    }

    private async void OnRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
        {
            return;
        }

        var product = await _catalog.GetProductByIdAsync(id);
        if (product is null)
        {
            return;
        }

        _selectedId = product.Id;
        FormTitle.Text = "Editar producto";
        CodeBox.Text = product.Code;
        BarcodeBox.Text = product.Barcode;
        DescriptionBox.Text = product.Description;
        FamilyCombo.SelectedValue = product.FamilyId;
        SubfamilyCombo.ItemsSource = await _catalog.GetSubfamiliesAsync(product.FamilyId);
        SubfamilyCombo.SelectedValue = product.SubfamilyId;
        MeasurementCombo.SelectedValue = product.MeasurementTypeId;
        SupplierCombo.SelectedValue = product.SupplierId;
        SalePriceBox.Text = product.SalePrice.ToString(CultureInfo.InvariantCulture);
        CostPriceBox.Text = product.CostPrice.ToString(CultureInfo.InvariantCulture);
        StockBox.Text = product.CurrentStock.ToString(CultureInfo.InvariantCulture);
        StockBox.IsEnabled = false;
        StockLabel.Text = "Stock actual (usar Entradas/Kardex)";
        MinStockBox.Text = product.MinStock.ToString(CultureInfo.InvariantCulture);
        NotesBox.Text = product.Notes;
        DeactivateButton.Visibility = Visibility.Visible;
        HideError();
    }

    private async void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        HideError();

        if (string.IsNullOrWhiteSpace(CodeBox.Text))
        {
            ShowError("El codigo es obligatorio.");
            return;
        }

        if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
        {
            ShowError("La descripcion es obligatoria.");
            return;
        }

        if (FamilyCombo.SelectedValue is not Guid familyId)
        {
            ShowError("Seleccione una categoria.");
            return;
        }

        if (MeasurementCombo.SelectedValue is not Guid measurementId)
        {
            ShowError("Seleccione una unidad de medida.");
            return;
        }

        if (!TryParseDecimal(SalePriceBox.Text, out var salePrice) ||
            !TryParseDecimal(CostPriceBox.Text, out var costPrice) ||
            !TryParseDecimal(StockBox.Text, out var stock) ||
            !TryParseDecimal(MinStockBox.Text, out var minStock))
        {
            ShowError("Revise los valores numericos (precio, costo, stock).");
            return;
        }

        var input = new ProductInput(
            CodeBox.Text,
            NullIfEmpty(BarcodeBox.Text),
            DescriptionBox.Text,
            familyId,
            SubfamilyCombo.SelectedValue as Guid?,
            measurementId,
            SupplierCombo.SelectedValue as Guid?,
            salePrice,
            costPrice,
            stock,
            minStock,
            null,
            null,
            NullIfEmpty(NotesBox.Text));

        try
        {
            if (_selectedId is { } id)
            {
                await _catalog.UpdateProductAsync(id, input, CurrentUserId);
            }
            else
            {
                await _catalog.CreateProductAsync(input, CurrentUserId);
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

        if (MessageBox.Show("Desactivar este producto?", "Productos",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _catalog.DeactivateProductAsync(id, CurrentUserId);
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
        FormTitle.Text = "Nuevo producto";
        CodeBox.Text = BarcodeBox.Text = DescriptionBox.Text = NotesBox.Text = string.Empty;
        FamilyCombo.SelectedIndex = -1;
        SubfamilyCombo.ItemsSource = null;
        MeasurementCombo.SelectedIndex = -1;
        SupplierCombo.SelectedIndex = -1;
        SalePriceBox.Text = CostPriceBox.Text = StockBox.Text = MinStockBox.Text = "0";
        StockBox.IsEnabled = true;
        StockLabel.Text = "Stock inicial";
        DeactivateButton.Visibility = Visibility.Collapsed;
        HideError();
    }

    private void ShowError(string message)
    {
        FormErrorText.Text = message;
        FormErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => FormErrorText.Visibility = Visibility.Collapsed;

    private static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value) ||
        decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
