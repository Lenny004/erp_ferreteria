using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Construye datos propios y aislados para pruebas de integración del POS.</summary>
internal static class TestDataFactory
{
    /// <summary>Crea productos con códigos únicos y stock independiente de la semilla.</summary>
    /// <param name="db">Contexto de prueba que persistirá los productos.</param>
    /// <param name="count">Cantidad de productos a crear.</param>
    /// <param name="stock">Stock inicial de cada producto.</param>
    /// <param name="salePrices">Precios opcionales por posición.</param>
    /// <returns>Productos recién persistidos.</returns>
    internal static async Task<List<Product>> CreateProductsAsync(
        FerreteriaDbContext db,
        int count,
        decimal stock,
        IReadOnlyList<decimal>? salePrices = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (salePrices is not null && salePrices.Count < count)
        {
            throw new ArgumentException("Debe existir un precio por cada producto.", nameof(salePrices));
        }

        var familyId = await db.Families.Select(item => item.Id).FirstAsync();
        var measurementTypeId = await db.MeasurementTypes.Select(item => item.Id).FirstAsync();
        var products = Enumerable.Range(0, count)
            .Select(index =>
            {
                var code = $"QA-P-{Guid.NewGuid():N}"[..30];
                return new Product
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Description = code,
                    FamilyId = familyId,
                    MeasurementTypeId = measurementTypeId,
                    SalePrice = salePrices?[index] ?? 10m,
                    CostPrice = 5m,
                    CurrentStock = stock,
                    MinStock = 0m,
                    IsActive = true
                };
            })
            .ToList();

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return products;
    }

    /// <summary>Crea un cliente consumidor final exclusivo para un caso.</summary>
    /// <param name="db">Contexto de prueba que persistirá el cliente.</param>
    /// <param name="tag">Etiqueta única del caso.</param>
    /// <returns>Cliente recién persistido.</returns>
    internal static async Task<Customer> CreateCustomerAsync(FerreteriaDbContext db, string tag)
    {
        ArgumentNullException.ThrowIfNull(db);
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CustomerType = "CF",
            Name = $"Cliente QA {tag}",
            IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }
}
