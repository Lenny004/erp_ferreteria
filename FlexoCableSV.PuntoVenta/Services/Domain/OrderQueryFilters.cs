using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexoCableSV.PuntoVenta.Services.Domain;

/// <summary>
/// Filtros reutilizables para consultas de <see cref="Order"/> en EF Core.
/// </summary>
internal static class OrderQueryFilters
{
    /// <summary>
    /// Filtra por UUID de orden o por coincidencia en notas / nombre del empleado.
    /// </summary>
    public static IQueryable<Order> ApplySearchTextFilter(this IQueryable<Order> orders, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return orders;
        }

        var normalizedSearch = searchText.Trim().TrimStart('#');
        var likePattern = $"%{normalizedSearch}%";

        if (Guid.TryParse(normalizedSearch, out var orderId))
        {
            return orders.Where(order => order.Id == orderId);
        }

        return orders.Where(order =>
            (order.Notes != null && EF.Functions.ILike(order.Notes, likePattern)) ||
            EF.Functions.ILike(order.Employee.FirstName, likePattern) ||
            EF.Functions.ILike(order.Employee.LastName, likePattern));
    }
}
