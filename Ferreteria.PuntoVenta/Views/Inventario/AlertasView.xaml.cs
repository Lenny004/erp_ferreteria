using System.Windows;
using System.Windows.Controls;
using Ferreteria.PuntoVenta.Services;

namespace Ferreteria.PuntoVenta.Views.Inventario;

public partial class AlertasView : UserControl
{
    private readonly IInventoryService _inventory;

    public AlertasView(IInventoryService inventory)
    {
        _inventory = inventory;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

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

    private async void OnActualizarClick(object sender, RoutedEventArgs e) => await ReloadAsync();
}
