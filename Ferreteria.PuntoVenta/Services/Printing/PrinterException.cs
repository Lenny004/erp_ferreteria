namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Error específico de la capa de impresión térmica. Envuelve la causa subyacente
/// (Win32, red, E/S) para exponer un tipo único a las capas superiores.
/// </summary>
public sealed class PrinterException : Exception
{
    /// <summary>
    /// Crea la excepción con un mensaje descriptivo.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    public PrinterException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Crea la excepción con un mensaje descriptivo y la causa original.
    /// </summary>
    /// <param name="message">Mensaje del error.</param>
    /// <param name="innerException">Excepción que originó el fallo.</param>
    public PrinterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
