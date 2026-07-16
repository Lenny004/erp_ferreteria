using System.Text;

namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Constructor fluido de documentos ESC/POS. Acumula bytes respetando el ancho
/// de papel (80mm = 48 columnas, 58mm = 32 columnas en Fuente A) y expone
/// helpers para alineación, negrita, columnas, separadores y QR nativo.
/// </summary>
internal sealed class EscPosDocumentBuilder
{
    private readonly List<byte> _buffer = new();
    private readonly Encoding _encoding;
    private readonly int _columns;

    /// <summary>
    /// Crea el constructor para el ancho de papel indicado e inicializa la impresora.
    /// </summary>
    /// <param name="paperWidthMm">Ancho de papel en milímetros (80 o 58).</param>
    public EscPosDocumentBuilder(int paperWidthMm)
    {
        _columns = paperWidthMm <= 58 ? 32 : 48;
        _encoding = ResolveEncoding();

        // Inicializa y fija la tabla de códigos WPC1252 (índice 16) para acentos y símbolo $.
        _buffer.AddRange(EscPosCommands.Initialize);
        _buffer.AddRange(EscPosCommands.SelectCharacterCodeTable(16));
    }

    /// <summary>Cantidad de columnas de texto disponibles para el ancho actual.</summary>
    public int Columns => _columns;

    /// <summary>
    /// Escribe una línea alineada a la izquierda.
    /// </summary>
    /// <param name="text">Texto de la línea.</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendLeft(string text)
    {
        _buffer.AddRange(EscPosCommands.AlignLeft);
        WriteTextLine(text);
        return this;
    }

    /// <summary>
    /// Escribe una línea centrada y restablece la alineación a la izquierda.
    /// </summary>
    /// <param name="text">Texto de la línea.</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendCenter(string text)
    {
        _buffer.AddRange(EscPosCommands.AlignCenter);
        WriteTextLine(text);
        _buffer.AddRange(EscPosCommands.AlignLeft);
        return this;
    }

    /// <summary>
    /// Escribe una línea en negrita (alineada a la izquierda).
    /// </summary>
    /// <param name="text">Texto de la línea.</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendBold(string text)
    {
        _buffer.AddRange(EscPosCommands.AlignLeft);
        _buffer.AddRange(EscPosCommands.BoldOn);
        WriteTextLine(text);
        _buffer.AddRange(EscPosCommands.BoldOff);
        return this;
    }

    /// <summary>
    /// Escribe una línea de texto tal cual en la alineación actual.
    /// </summary>
    /// <param name="text">Texto de la línea (vacío por defecto para dejar un renglón).</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendLine(string text = "")
    {
        WriteTextLine(text);
        return this;
    }

    /// <summary>
    /// Escribe un texto largo dividido (envuelto) al ancho del papel.
    /// </summary>
    /// <param name="text">Texto a envolver.</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendWrapped(string text)
    {
        _buffer.AddRange(EscPosCommands.AlignLeft);
        foreach (string line in WrapText(text ?? string.Empty, _columns))
        {
            WriteTextLine(line);
        }

        return this;
    }

    /// <summary>
    /// Escribe una línea separadora de guiones del ancho completo.
    /// </summary>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendSeparator()
    {
        _buffer.AddRange(EscPosCommands.AlignLeft);
        WriteTextLine(new string('-', _columns));
        return this;
    }

    /// <summary>
    /// Escribe una línea con dos columnas: texto a la izquierda y texto a la derecha,
    /// rellenando el espacio intermedio para alinear el valor derecho al borde.
    /// </summary>
    /// <param name="left">Texto de la columna izquierda.</param>
    /// <param name="right">Texto de la columna derecha.</param>
    /// <param name="emphasized">Si es verdadero, imprime en negrita y doble alto.</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendColumns(string left, string right, bool emphasized = false)
    {
        _buffer.AddRange(EscPosCommands.AlignLeft);

        left ??= string.Empty;
        right ??= string.Empty;

        // La columna derecha manda; si no cabe, se recorta la izquierda.
        int availableForLeft = _columns - right.Length - 1;
        if (availableForLeft < 1)
        {
            availableForLeft = 1;
        }

        if (left.Length > availableForLeft)
        {
            left = left.Substring(0, availableForLeft);
        }

        int padding = _columns - left.Length - right.Length;
        if (padding < 1)
        {
            padding = 1;
        }

        string line = string.Concat(left, new string(' ', padding), right);
        if (line.Length > _columns)
        {
            line = line.Substring(0, _columns);
        }

        if (emphasized)
        {
            _buffer.AddRange(EscPosCommands.BoldOn);
            _buffer.AddRange(EscPosCommands.SizeDoubleHeight);
        }

        WriteTextLine(line);

        if (emphasized)
        {
            _buffer.AddRange(EscPosCommands.SizeNormal);
            _buffer.AddRange(EscPosCommands.BoldOff);
        }

        return this;
    }

    /// <summary>
    /// Imprime un código QR nativo ESC/POS centrado con el contenido indicado.
    /// </summary>
    /// <param name="data">Texto o URL a codificar.</param>
    /// <param name="moduleSize">Tamaño de módulo (1-16).</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder AppendQr(string data, byte moduleSize = 6)
    {
        _buffer.AddRange(EscPosCommands.AlignCenter);
        _buffer.AddRange(EscPosCommands.BuildQr(data, moduleSize));
        _buffer.Add(EscPosCommands.LineFeed);
        _buffer.AddRange(EscPosCommands.AlignLeft);
        return this;
    }

    /// <summary>
    /// Avanza el papel la cantidad de líneas indicada.
    /// </summary>
    /// <param name="lines">Líneas a avanzar.</param>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder Feed(int lines = 1)
    {
        byte safeLines = lines < 0 ? (byte)0 : (lines > 255 ? (byte)255 : (byte)lines);
        _buffer.AddRange(EscPosCommands.FeedLines(safeLines));
        return this;
    }

    /// <summary>
    /// Realiza un corte parcial del papel (con un pequeño avance previo).
    /// </summary>
    /// <returns>El propio constructor para encadenar llamadas.</returns>
    public EscPosDocumentBuilder Cut()
    {
        _buffer.AddRange(EscPosCommands.PartialCutWithFeed(80));
        return this;
    }

    /// <summary>
    /// Devuelve el documento acumulado como arreglo de bytes ESC/POS.
    /// </summary>
    /// <returns>Bytes crudos listos para enviar a la impresora.</returns>
    public byte[] Build()
    {
        return _buffer.ToArray();
    }

    private void WriteTextLine(string text)
    {
        _buffer.AddRange(_encoding.GetBytes(text ?? string.Empty));
        _buffer.Add(EscPosCommands.LineFeed);
    }

    private static IEnumerable<string> WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();

        foreach (string word in words)
        {
            string piece = word;

            // Una palabra más larga que el ancho se parte en trozos duros.
            while (piece.Length > width)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                yield return piece.Substring(0, width);
                piece = piece.Substring(width);
            }

            if (current.Length == 0)
            {
                current.Append(piece);
            }
            else if (current.Length + 1 + piece.Length <= width)
            {
                current.Append(' ').Append(piece);
            }
            else
            {
                yield return current.ToString();
                current.Clear();
                current.Append(piece);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static Encoding ResolveEncoding()
    {
        // Intenta CP858 (con símbolo de Euro) o CP1252; si el proveedor de páginas de
        // código no está registrado (no se permite agregar paquetes), cae a Latin1,
        // que siempre existe en la BCL y cubre acentos y el símbolo $.
        try
        {
            return Encoding.GetEncoding(858);
        }
        catch (Exception)
        {
            // Ignora y prueba la siguiente opción.
        }

        try
        {
            return Encoding.GetEncoding(1252);
        }
        catch (Exception)
        {
            // Ignora y usa el respaldo garantizado.
        }

        return Encoding.Latin1;
    }
}
