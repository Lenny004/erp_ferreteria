namespace FlexoCableSV.PuntoVenta.Services;

public sealed class InvalidOrderException(string message) : InvalidOperationException(message);
