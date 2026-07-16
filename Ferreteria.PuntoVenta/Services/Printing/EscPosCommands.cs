namespace Ferreteria.PuntoVenta.Services.Printing;

/// <summary>
/// Constantes y helpers de bajo nivel para el juego de comandos ESC/POS.
/// Todos los valores son secuencias de bytes crudos listas para enviar a la impresora.
/// </summary>
internal static class EscPosCommands
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;

    /// <summary>Salto de línea (LF).</summary>
    public const byte LineFeed = 0x0A;

    /// <summary>Inicializa la impresora: ESC @.</summary>
    public static readonly byte[] Initialize = { Esc, 0x40 };

    /// <summary>Alineación a la izquierda: ESC a 0.</summary>
    public static readonly byte[] AlignLeft = { Esc, 0x61, 0x00 };

    /// <summary>Alineación al centro: ESC a 1.</summary>
    public static readonly byte[] AlignCenter = { Esc, 0x61, 0x01 };

    /// <summary>Alineación a la derecha: ESC a 2.</summary>
    public static readonly byte[] AlignRight = { Esc, 0x61, 0x02 };

    /// <summary>Activa negrita: ESC E 1.</summary>
    public static readonly byte[] BoldOn = { Esc, 0x45, 0x01 };

    /// <summary>Desactiva negrita: ESC E 0.</summary>
    public static readonly byte[] BoldOff = { Esc, 0x45, 0x00 };

    /// <summary>Tamaño normal: GS ! 0.</summary>
    public static readonly byte[] SizeNormal = { Gs, 0x21, 0x00 };

    /// <summary>Doble alto: GS ! 0x01.</summary>
    public static readonly byte[] SizeDoubleHeight = { Gs, 0x21, 0x01 };

    /// <summary>Doble ancho: GS ! 0x10.</summary>
    public static readonly byte[] SizeDoubleWidth = { Gs, 0x21, 0x10 };

    /// <summary>Doble alto y ancho: GS ! 0x11.</summary>
    public static readonly byte[] SizeDoubleBoth = { Gs, 0x21, 0x11 };

    /// <summary>Corte parcial simple: GS V 1.</summary>
    public static readonly byte[] PartialCut = { Gs, 0x56, 0x01 };

    /// <summary>
    /// Corte parcial con avance de papel (función B): GS V 66 n.
    /// Avanza <paramref name="feedUnits"/> unidades de movimiento y luego corta.
    /// </summary>
    /// <param name="feedUnits">Unidades de avance antes del corte (0-255).</param>
    /// <returns>Secuencia de bytes del comando de corte con avance.</returns>
    public static byte[] PartialCutWithFeed(byte feedUnits)
    {
        return new byte[] { Gs, 0x56, 0x42, feedUnits };
    }

    /// <summary>
    /// Avanza n líneas de papel: ESC d n.
    /// </summary>
    /// <param name="lines">Cantidad de líneas a avanzar (0-255).</param>
    /// <returns>Secuencia de bytes del comando de avance.</returns>
    public static byte[] FeedLines(byte lines)
    {
        return new byte[] { Esc, 0x64, lines };
    }

    /// <summary>
    /// Selecciona la tabla de códigos de caracteres: ESC t n.
    /// Se usa para que los acentos y el símbolo de moneda impriman correctamente.
    /// </summary>
    /// <param name="codeTable">Índice de tabla de códigos del fabricante.</param>
    /// <returns>Secuencia de bytes del comando de selección de tabla.</returns>
    public static byte[] SelectCharacterCodeTable(byte codeTable)
    {
        return new byte[] { Esc, 0x74, codeTable };
    }

    /// <summary>
    /// Construye la secuencia ESC/POS nativa para imprimir un código QR (Modelo 2)
    /// con el contenido indicado, usando comandos GS ( k.
    /// </summary>
    /// <param name="data">Texto o URL a codificar en el QR.</param>
    /// <param name="moduleSize">Tamaño del módulo en puntos (1-16). Por defecto 6.</param>
    /// <param name="errorCorrection">
    /// Nivel de corrección de error: 'L', 'M', 'Q' o 'H'. Por defecto 'M'.
    /// </param>
    /// <returns>Bytes que seleccionan modelo, tamaño, corrección, almacenan e imprimen el QR.</returns>
    /// <exception cref="ArgumentException">Si <paramref name="data"/> es nulo o vacío.</exception>
    public static byte[] BuildQr(string data, byte moduleSize = 6, char errorCorrection = 'M')
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException("El contenido del QR no puede ser vacío.", nameof(data));
        }

        // Los datos del QR se codifican en bytes; ASCII/Latin1 es suficiente para una URL de consulta.
        byte[] payload = System.Text.Encoding.Latin1.GetBytes(data);

        byte correctionByte = errorCorrection switch
        {
            'L' => 0x30,
            'M' => 0x31,
            'Q' => 0x32,
            'H' => 0x33,
            _ => 0x31
        };

        byte clampedModule = moduleSize < 1 ? (byte)1 : (moduleSize > 16 ? (byte)16 : moduleSize);

        // 1) Seleccionar modelo 2: GS ( k pL pH cn fn n1 n2
        //    pL pH = 4 0 ; cn = 49 (0x31) ; fn = 65 (0x41) ; n1 = 50 (modelo 2) ; n2 = 0
        byte[] selectModel = { Gs, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 };

        // 2) Tamaño del módulo: GS ( k 03 00 31 43 n
        byte[] setModuleSize = { Gs, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, clampedModule };

        // 3) Nivel de corrección de error: GS ( k 03 00 31 45 n
        byte[] setErrorCorrection = { Gs, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, correctionByte };

        // 4) Almacenar datos en el búfer del símbolo: GS ( k pL pH 31 50 30 d1...dk
        //    (pL + pH*256) = longitud de datos + 3 (por cn=0x31, fn=0x50, m=0x30).
        int storeLength = payload.Length + 3;
        byte storePl = (byte)(storeLength & 0xFF);
        byte storePh = (byte)((storeLength >> 8) & 0xFF);

        // 5) Imprimir el símbolo almacenado: GS ( k 03 00 31 51 30
        byte[] printSymbol = { Gs, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 };

        var sequence = new List<byte>(
            selectModel.Length
            + setModuleSize.Length
            + setErrorCorrection.Length
            + 8
            + payload.Length
            + printSymbol.Length);

        sequence.AddRange(selectModel);
        sequence.AddRange(setModuleSize);
        sequence.AddRange(setErrorCorrection);
        sequence.AddRange(new byte[] { Gs, 0x28, 0x6B, storePl, storePh, 0x31, 0x50, 0x30 });
        sequence.AddRange(payload);
        sequence.AddRange(printSymbol);

        return sequence.ToArray();
    }
}
