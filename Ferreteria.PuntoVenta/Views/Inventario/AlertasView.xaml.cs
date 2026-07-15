using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Inventario;

/// <summary>
/// Alertas de stock bajo / agotado sobre Product (módulo Inventario).
/// Solo lectura: lista las alertas activas y permite refrescar.
/// </summary>
public partial class AlertasView : UserControl
{
    private readonly IInventoryService _inventory;

    /// <summary>Inicializa la vista y carga alertas al Loaded.</summary>
    public AlertasView(IInventoryService inventory)
    {
        _inventory = inventory;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    /// <summary>Consulta alertas activas y actualiza la lista.</summary>
    private async Task ReloadAsync()
    {
        try
        {
            var items = await _inventory.GetActiveAlertsAsync();
            ItemsList.ItemsSource = items;
            EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudieron cargar las alertas: {ex.Message}", "Alertas",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Botón Actualizar: vuelve a cargar alertas.</summary>
    private async void OnActualizarClick(object sender, RoutedEventArgs e) => await ReloadAsync();
}
