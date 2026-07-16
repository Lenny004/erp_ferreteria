using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Envía bytes RAW a una impresora de Windows por nombre mediante P/Invoke a
/// <c>winspool.drv</c> (OpenPrinter / StartDocPrinter / StartPagePrinter /
/// WritePrinter / EndPagePrinter / EndDocPrinter / ClosePrinter).
/// </summary>
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DocInfoA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string DocName;

        [MarshalAs(UnmanagedType.LPStr)]
        public string? OutputFile;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr defaults);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr printerHandle, int level, ref DocInfoA documentInfo);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr printerHandle, IntPtr bytes, int count, out int written);

    /// <summary>
    /// Abre la impresora indicada y envía el bloque de bytes como trabajo RAW.
    /// </summary>
    /// <param name="printerName">Nombre de la impresora en el spooler de Windows.</param>
    /// <param name="data">Bytes ESC/POS a enviar.</param>
    /// <exception cref="ArgumentException">Si el nombre está vacío.</exception>
    /// <exception cref="ArgumentNullException">Si <paramref name="data"/> es nulo.</exception>
    /// <exception cref="PrinterException">Si falla alguna llamada del spooler.</exception>
    public static void SendBytesToPrinter(string printerName, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("El nombre de la impresora es obligatorio.", nameof(printerName));
        }

        ArgumentNullException.ThrowIfNull(data);

        if (!OpenPrinter(printerName, out IntPtr printerHandle, IntPtr.Zero) || printerHandle == IntPtr.Zero)
        {
            throw BuildWin32Exception($"No se pudo abrir la impresora '{printerName}'.");
        }

        try
        {
            var documentInfo = new DocInfoA
            {
                DocName = "Ticket Ferreteria (RAW)",
                OutputFile = null,
                DataType = "RAW"
            };

            if (!StartDocPrinter(printerHandle, 1, ref documentInfo))
            {
                throw BuildWin32Exception("No se pudo iniciar el documento en la impresora.");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    throw BuildWin32Exception("No se pudo iniciar la página en la impresora.");
                }

                WriteRawBytes(printerHandle, data);

                if (!EndPagePrinter(printerHandle))
                {
                    throw BuildWin32Exception("No se pudo finalizar la página en la impresora.");
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }

    private static void WriteRawBytes(IntPtr printerHandle, byte[] data)
    {
        IntPtr unmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
        try
        {
            Marshal.Copy(data, 0, unmanagedBytes, data.Length);

            if (!WritePrinter(printerHandle, unmanagedBytes, data.Length, out int written) || written != data.Length)
            {
                throw BuildWin32Exception("No se pudieron escribir todos los bytes en la impresora.");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(unmanagedBytes);
        }
    }

    private static PrinterException BuildWin32Exception(string message)
    {
        var inner = new Win32Exception(Marshal.GetLastWin32Error());
        return new PrinterException($"{message} ({inner.Message})", inner);
    }
}
