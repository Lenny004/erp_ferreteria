namespace Ferreteria.PuntoVenta.Services.Returns;

/// <summary>Reglas de entrada reutilizables para solicitudes de devolución.</summary>
public static class ReturnInputRules
{
    /// <summary>Valida la forma de una solicitud sin consultar la base de datos.</summary>
    /// <param name="request">Solicitud no confiable recibida desde UI u otro canal.</param>
    /// <param name="options">Catálogo y métodos permitidos.</param>
    /// <exception cref="InvalidReturnException">Cuando una regla de forma no se cumple.</exception>
    public static void ValidateShape(ReturnRequest request, ReturnOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        if (request.ClientRequestId == Guid.Empty || request.OrderId == Guid.Empty)
        {
            throw new InvalidReturnException("La devolución no tiene identificadores válidos.");
        }

        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidReturnException("La devolución debe indicar quién la ejecuta.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new InvalidReturnException("Seleccione al menos una línea para devolver.");
        }

        var reason = options.Motivos.FirstOrDefault(item =>
            string.Equals(item.Code, request.ReasonCode?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (reason is null)
        {
            throw new InvalidReturnException("El motivo de devolución no está permitido.");
        }

        var notes = request.Notes?.Trim();
        if (notes?.Length > 500)
        {
            throw new InvalidReturnException("La observación no puede superar 500 caracteres.");
        }

        if (reason.RequiresNotes && string.IsNullOrWhiteSpace(notes))
        {
            throw new InvalidReturnException("Este motivo requiere una observación.");
        }

        if (!options.MetodosReintegroPermitidos.Any(item =>
                string.Equals(item, request.RefundMethod?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidReturnException("El método de reintegro no está permitido.");
        }

        var seen = new HashSet<Guid>();
        foreach (var requestedLine in request.Lines)
        {
            if (requestedLine.OrderDetailId == Guid.Empty || !seen.Add(requestedLine.OrderDetailId))
            {
                throw new InvalidReturnException("No puede repetir ni omitir el identificador de una línea.");
            }

            ValidateQuantity(requestedLine.Quantity);
        }

        if (request.RefundAmount < 0m)
        {
            throw new InvalidReturnException("El monto de reintegro no puede ser negativo.");
        }

        if (string.Equals(request.RefundMethod, ReturnDomainConstants.RefundMethods.None, StringComparison.OrdinalIgnoreCase)
            && request.RefundAmount != 0m)
        {
            throw new InvalidReturnException("El reintegro NINGUNO debe tener monto cero.");
        }
    }

    /// <summary>Valida una solicitud contra las líneas originales y las opciones activas.</summary>
    /// <param name="request">Solicitud recibida desde UI u otro canal.</param>
    /// <param name="lines">Líneas originales de la orden.</param>
    /// <param name="options">Catálogo y métodos permitidos.</param>
    /// <param name="allowUnspecifiedRefundAmount">Permite cero durante una vista previa.</param>
    /// <exception cref="InvalidReturnException">Cuando una regla no se cumple.</exception>
    public static void Validate(ReturnRequest request, IReadOnlyList<ReturnableLine> lines, ReturnOptions options, bool allowUnspecifiedRefundAmount = false)
    {
        ValidateShape(request, options);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new InvalidReturnException("La venta no tiene líneas disponibles para devolución.");
        }

        var linesById = lines.ToDictionary(item => item.OrderDetailId);
        foreach (var requestedLine in request.Lines)
        {
            if (!linesById.TryGetValue(requestedLine.OrderDetailId, out var original)
                || (original.OrderId != Guid.Empty && original.OrderId != request.OrderId))
            {
                throw new InvalidReturnException("Una de las líneas no pertenece a la venta seleccionada.");
            }

            if (requestedLine.Quantity > original.AvailableQuantity)
            {
                throw new InvalidReturnException($"La cantidad solicitada supera lo disponible para {original.ProductDescription}.");
            }
        }

        _ = allowUnspecifiedRefundAmount;
    }

    /// <summary>Valida una cantidad con hasta tres decimales y valor positivo.</summary>
    /// <param name="quantity">Cantidad a validar.</param>
    /// <exception cref="InvalidReturnException">Si la cantidad no es válida.</exception>
    public static void ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new InvalidReturnException("La cantidad a devolver debe ser mayor que cero.");
        }

        if (decimal.Round(quantity, 3, MidpointRounding.AwayFromZero) != quantity)
        {
            throw new InvalidReturnException("La cantidad puede tener como máximo 3 decimales.");
        }
    }
}
