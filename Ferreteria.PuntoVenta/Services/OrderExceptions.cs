namespace Ferreteria.PuntoVenta.Services;

/// <summary>Regla de negocio incumplida al crear o completar una orden.</summary>
public sealed class InvalidOrderException(string message) : InvalidOperationException(message);
