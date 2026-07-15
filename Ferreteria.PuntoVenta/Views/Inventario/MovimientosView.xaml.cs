using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;

namespace Ferreteria.PuntoVenta.Views.Inventario;

/// <summary>
/// Entradas y kardex (InventoryMovement): registra ingresos de compra/otro y ajustes de stock,
/// y muestra los movimientos recientes del inventario.
/// </summary>
public partial class MovimientosView : UserControl
{
    private readonly IInventoryService _inventory;
    private readonly IProductCatalogService _catalog;
    private readonly ICurrentSessionService _currentSession;
    private List<ProductPickItem> _products = [];

    /// <summary>Inicializa la vista de movimientos e hidrata el combo de productos al Loaded.</summary>
    public MovimientosView(
        IInventoryService inventory,
        IProductCatalogService catalog,
        ICurrentSessionService currentSession)
    {
        _inventory = inventory;
        _catalog = catalog;
        _currentSession = currentSession;
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    /// <summary>Id del Employee en sesión (quién registra el movimiento).</summary>
    private Guid CurrentUserId => _currentSession.CurrentEmployee?.Id ?? Guid.Empty;

    /// <summary>Carga productos para el combo y la lista de movimientos recientes.</summary>
    private async Task InitializeAsync()
    {
        try
        {
            var products = await _catalog.GetProductsAsync(null);
            _products = products
                .Select(p => new ProductPickItem(p.Id, $"{p.Code} - {p.Description}", p.CurrentStock))
                .ToList();
            ProductCombo.ItemsSource = _products;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo inicializar: {ex.Message}");
        }
    }

    /// <summary>Recarga los últimos InventoryMovement.</summary>
    private async Task ReloadAsync()
    {
        var items = await _inventory.GetRecentMovementsAsync(null, 150);
        ItemsList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Botón Actualizar: refresca el kardex.</summary>
    private async void OnActualizarClick(object sender, RoutedEventArgs e) => await ReloadAsync();

    /// <summary>Al elegir producto, muestra el stock actual.</summary>
    private void OnProductSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProductCombo.SelectedValue is Guid id)
        {
            var item = _products.FirstOrDefault(p => p.Id == id);
            CurrentStockText.Text = item is null ? "Stock actual: -" : $"Stock actual: {item.Stock}";
        }
    }

    /// <summary>
    /// Cambia etiquetas del formulario según tipo: entrada (cantidad + costo) o ajuste (stock final).
    /// </summary>
    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuantityLabel is null || CostPanel is null)
        {
            return;
        }

        var isAdjustment = SelectedType() == "AJUSTE";
        QuantityLabel.Text = isAdjustment ? "Nuevo stock (valor final) *" : "Cantidad a ingresar *";
        CostPanel.Visibility = isAdjustment ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Registra un InventoryMovement: ajuste de stock o entrada con costo unitario.
    /// </summary>
    private async void OnRegistrarClick(object sender, RoutedEventArgs e)
    {
        HideError();

        if (ProductCombo.SelectedValue is not Guid productId)
        {
            ShowError("Seleccione un producto.");
            return;
        }

        if (!TryParseDecimal(QuantityBox.Text, out var quantity) || quantity < 0)
        {
            ShowError("Ingrese un valor numerico valido.");
            return;
        }

        try
        {
            var type = SelectedType();
            if (type == "AJUSTE")
            {
                await _inventory.RegisterAdjustmentAsync(productId, quantity, CurrentUserId, ReasonBox.Text);
            }
            else
            {
                if (!TryParseDecimal(UnitCostBox.Text, out var unitCost) || unitCost < 0)
                {
                    ShowError("Ingrese un costo unitario valido.");
                    return;
                }

                await _inventory.RegisterEntryAsync(productId, quantity, unitCost, CurrentUserId, type, ReasonBox.Text);
            }

            QuantityBox.Text = "0";
            UnitCostBox.Text = "0";
            ReasonBox.Text = string.Empty;
            await InitializeAsync();
        }
        catch (ValidationException vex)
        {
            ShowError(vex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo registrar: {ex.Message}");
        }
    }

    /// <summary>Lee el tipo de movimiento seleccionado en el combo (Tag).</summary>
    private string SelectedType() =>
        (TypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? SalesDomainConstants.InventoryMovementTypes.PurchaseInflow;

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

    /// <summary>Ítem del combo de productos (Id, texto visible y stock).</summary>
    private sealed record ProductPickItem(Guid Id, string Display, decimal Stock);
}
