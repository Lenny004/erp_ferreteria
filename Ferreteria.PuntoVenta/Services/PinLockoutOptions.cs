namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Parámetros configurables del bloqueo de PIN por terminal.
/// </summary>
public sealed class PinLockoutOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "PinLockout";

    /// <summary>Cantidad de fallos dentro de la ventana que activa un bloqueo.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Duración de la ventana deslizante de fallos, en minutos.</summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>Duración del primer bloqueo, en minutos.</summary>
    public int InitialLockoutMinutes { get; set; } = 2;

    /// <summary>Multiplicador aplicado a cada bloqueo sucesivo dentro de la ventana.</summary>
    public double ProgressiveMultiplier { get; set; } = 2d;

    /// <summary>Duración máxima de cualquier bloqueo, en minutos.</summary>
    public int MaxLockoutMinutes { get; set; } = 30;
}
