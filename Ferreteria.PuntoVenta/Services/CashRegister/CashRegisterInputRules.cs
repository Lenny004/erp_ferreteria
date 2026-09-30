namespace Ferreteria.PuntoVenta.Services.CashRegister;

/// <summary>Valida entradas monetarias y códigos usados por el módulo de caja.</summary>
public static class CashRegisterInputRules
{
    /// <summary>Redondea y valida un monto no negativo dentro del límite configurado.</summary>
    /// <param name="amount">Monto capturado.</param>
    /// <param name="fieldName">Nombre lógico del campo para el mensaje de validación.</param>
    /// <param name="maximumAmount">Límite máximo razonable.</param>
    /// <returns>Monto normalizado a dos decimales.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el monto es negativo, no finito o supera el límite.</exception>
    public static decimal ValidateAmount(decimal amount, string fieldName, decimal maximumAmount)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("El nombre del campo es obligatorio.", nameof(fieldName));
        }

        if (maximumAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAmount), "El límite máximo debe ser mayor que cero.");
        }

        var normalized = CashRegisterCalculator.RoundAmount(amount);
        if (normalized < 0m || normalized > maximumAmount)
        {
            throw new ArgumentOutOfRangeException(
                fieldName,
                $"{fieldName} debe estar entre $0.00 y ${maximumAmount:0.00}.");
        }

        return normalized;
    }

    /// <summary>Valida y normaliza el código de una caja física.</summary>
    /// <param name="cashRegisterCode">Código capturado o configurado.</param>
    /// <returns>Código sin espacios laterales.</returns>
    /// <exception cref="ArgumentException">Si falta el código o supera 50 caracteres.</exception>
    public static string ValidateCashRegisterCode(string? cashRegisterCode)
    {
        var normalized = cashRegisterCode?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 50)
        {
            throw new ArgumentException("El código de caja es obligatorio y debe tener hasta 50 caracteres.", nameof(cashRegisterCode));
        }

        return normalized;
    }
}

