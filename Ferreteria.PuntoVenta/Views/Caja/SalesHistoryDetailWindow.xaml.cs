using System.Windows;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Ferreteria.PuntoVenta.Services.Time;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Ventana de solo lectura para el detalle de una venta.</summary>
public partial class SalesHistoryDetailWindow : Window
{
    /// <summary>Inicializa la ventana con el DTO de detalle.</summary>
    /// <param name="detail">Detalle de la venta que se mostrará.</param>
    public SalesHistoryDetailWindow(SalesHistoryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        InitializeComponent();
        DataContext = new SalesHistoryDetailDisplay(detail);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class SalesHistoryDetailDisplay
    {
        /// <summary>Construye los datos de presentación del detalle.</summary>
        /// <param name="detail">Detalle persistido que se convertirá a columnas.</param>
        public SalesHistoryDetailDisplay(SalesHistoryDetail detail)
        {
            HeaderText = $"Fecha: {TimeZoneSupport.ToLocalTime(detail.CreatedAtUtc):dd/MM/yyyy HH:mm} | Estado: {detail.Status} | "
                + $"Tipo: {detail.OrderType} | Cajero: {detail.EmployeeDisplayName} | Cliente: {detail.CustomerDisplayName}";
            Lines = detail.Lines.Select(line => new SalesHistoryLineDisplay(
                line.Product,
                line.Quantity,
                line.Unit,
                line.UnitsPerPackage,
                line.UnitPrice,
                line.Discount,
                line.Subtotal)).ToList();
            Payments = detail.Payments.Select(payment => new SalesHistoryPaymentDisplay(
                payment.Method,
                payment.Amount,
                payment.Reference ?? "N/D")).ToList();
            Dtes = detail.Dtes.Select(dte => new SalesHistoryDteDisplay(
                dte.Type,
                dte.ControlNumber,
                dte.GenerationCode.ToString(),
                dte.Status,
                dte.Seal ?? "N/D",
                dte.CreditNotes.Select(note => new SalesHistoryCreditNoteDisplay(
                    note.Type,
                    note.ControlNumber,
                    note.Status)).ToList())).ToList();
            Movements = detail.Movements.Select(movement => new SalesHistoryMovementDisplay(
                movement.Product,
                movement.Type,
                movement.Quantity,
                movement.Reason ?? "N/D")).ToList();
            NoDteText = Dtes.Count == 0 ? "SIN DTE — comprobante interno" : string.Empty;
            TotalsText = $"Subtotal: {detail.Subtotal:C2} | IVA: {detail.Tax:C2} | Descuento: {detail.Discount:C2} | Total: {detail.Total:C2}";
            Notes = string.IsNullOrWhiteSpace(detail.Notes) ? "Sin notas." : detail.Notes;
        }

        public string HeaderText { get; }

        public IReadOnlyList<SalesHistoryLineDisplay> Lines { get; }

        public IReadOnlyList<SalesHistoryPaymentDisplay> Payments { get; }

        public IReadOnlyList<SalesHistoryDteDisplay> Dtes { get; }

        public IReadOnlyList<SalesHistoryMovementDisplay> Movements { get; }

        public string NoDteText { get; }

        public string TotalsText { get; }

        public string Notes { get; }
    }

    /// <summary>Columnas visibles de una línea de venta.</summary>
    /// <param name="Product">Descripción del producto.</param>
    /// <param name="Quantity">Cantidad vendida.</param>
    /// <param name="Unit">Unidad de venta.</param>
    /// <param name="UnitsPerPackage">Unidades base por presentación.</param>
    /// <param name="UnitPrice">Precio unitario.</param>
    /// <param name="Discount">Descuento de la línea.</param>
    /// <param name="Subtotal">Subtotal de la línea.</param>
    private sealed record SalesHistoryLineDisplay(
        string Product,
        decimal Quantity,
        string Unit,
        decimal UnitsPerPackage,
        decimal UnitPrice,
        decimal Discount,
        decimal Subtotal);

    /// <summary>Columnas visibles de un pago.</summary>
    /// <param name="Method">Método de pago.</param>
    /// <param name="Amount">Monto pagado.</param>
    /// <param name="Reference">Referencia del pago.</param>
    private sealed record SalesHistoryPaymentDisplay(
        string Method,
        decimal Amount,
        string Reference);

    /// <summary>Columnas visibles de un DTE y sus notas de crédito.</summary>
    /// <param name="Type">Tipo de DTE.</param>
    /// <param name="ControlNumber">Número de control.</param>
    /// <param name="GenerationCode">Código de generación.</param>
    /// <param name="Status">Estado ante el Ministerio de Hacienda.</param>
    /// <param name="Seal">Sello de recepción.</param>
    /// <param name="CreditNotes">Notas de crédito relacionadas.</param>
    private sealed record SalesHistoryDteDisplay(
        string Type,
        string ControlNumber,
        string GenerationCode,
        string Status,
        string Seal,
        IReadOnlyList<SalesHistoryCreditNoteDisplay> CreditNotes);

    /// <summary>Columnas visibles de una nota de crédito.</summary>
    /// <param name="Type">Tipo de documento.</param>
    /// <param name="ControlNumber">Número de control.</param>
    /// <param name="Status">Estado del documento.</param>
    private sealed record SalesHistoryCreditNoteDisplay(
        string Type,
        string ControlNumber,
        string Status);

    /// <summary>Columnas visibles de un movimiento de inventario.</summary>
    /// <param name="Product">Descripción del producto.</param>
    /// <param name="Type">Tipo de movimiento.</param>
    /// <param name="Quantity">Cantidad movida.</param>
    /// <param name="Reason">Motivo del movimiento.</param>
    private sealed record SalesHistoryMovementDisplay(
        string Product,
        string Type,
        decimal Quantity,
        string Reason);
}
