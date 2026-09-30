using System.Text;
using Ferreteria.PuntoVenta.Services.Printing;

var outputDirectory = args.Length == 0 ? Path.Combine(Environment.CurrentDirectory, "ticket-preview") : args[0];
Directory.CreateDirectory(outputDirectory);
var document = new ReceiptDocument(
    "FERRETERIA DEMO PRUEBA", "Punto de Venta", "00000000000000", "000000",
    "DIRECCIÓN A VERIFICAR", "0000-0000", "COMPROBANTE INTERNO", ReceiptDocumentTypes.InternalReceipt, "", "", null, "",
    DateTime.Now, "Cajero de Prueba", "Cliente de Prueba", null,
    new[] { new TicketLineItem("Cable eléctrico THHN de prueba con descripción larga", 2, "2.00", 3.50m, 7.00m) },
    7.00m, 0.91m, 7.91m, "SIETE DÓLARES CON 91/100", "EFECTIVO", 10.00m, 2.09m, string.Empty, false,
    "COMPROBANTE INTERNO - SIN VALOR FISCAL");

foreach (var width in new[] { 58, 80 })
{
    File.WriteAllText(Path.Combine(outputDirectory, $"ticket-{width}mm.txt"), TicketReceiptRenderer.RenderPlainText(document, width), Encoding.UTF8);
    File.WriteAllBytes(Path.Combine(outputDirectory, $"ticket-{width}mm.bin"), TicketReceiptRenderer.Render(document, width));
}

Console.WriteLine($"Vista previa generada en: {Path.GetFullPath(outputDirectory)}");
