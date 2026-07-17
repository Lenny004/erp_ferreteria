namespace Ferreteria.PuntoVenta.Services.Dte;

/// <summary>Error base del modulo de facturacion electronica DTE.</summary>
public class DteException : Exception
{
    /// <summary>Crea la excepcion con un mensaje.</summary>
    public DteException(string message) : base(message)
    {
    }

    /// <summary>Crea la excepcion con un mensaje y la causa interna.</summary>
    public DteException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>El emisor no esta configurado (falta fila en <c>dte.DteConfig</c> o credenciales MH).</summary>
public sealed class DteConfigurationException : DteException
{
    /// <summary>Crea la excepcion de configuracion faltante.</summary>
    public DteConfigurationException(string message) : base(message)
    {
    }
}

/// <summary>Fallo al firmar el documento en el Firmador local del MH.</summary>
public sealed class DteSigningException : DteException
{
    /// <summary>Crea la excepcion de firma.</summary>
    public DteSigningException(string message) : base(message)
    {
    }

    /// <summary>Crea la excepcion de firma con causa interna.</summary>
    public DteSigningException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Fallo de transmision al MH que amerita contingencia (red caida, timeout, 5xx).
/// El documento se conserva en cola para reintento.
/// </summary>
public sealed class DteTransmissionException : DteException
{
    /// <summary>Crea la excepcion de transmision (contingencia).</summary>
    public DteTransmissionException(string message) : base(message)
    {
    }

    /// <summary>Crea la excepcion de transmision con causa interna.</summary>
    public DteTransmissionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>El MH proceso la solicitud pero rechazo el documento (observaciones).</summary>
public sealed class DteRejectedException : DteException
{
    /// <summary>Observaciones devueltas por el MH.</summary>
    public IReadOnlyList<string> Observaciones { get; }

    /// <summary>Crea la excepcion de rechazo con las observaciones del MH.</summary>
    public DteRejectedException(string message, IReadOnlyList<string> observaciones) : base(message)
    {
        Observaciones = observaciones;
    }
}
