using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica que los límites EF de campos corregidos coincidan con la BD aprobada.</summary>
public sealed class EfStringLengthMetadataTests
{
    /// <summary>Comprueba los máximos EF de DTE y familia frente a los límites aprobados.</summary>
    [Fact]
    public void CorrectedStringLengths_MatchEfMetadata()
    {
        var options = new DbContextOptionsBuilder<FerreteriaDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conectar")
            .Options;
        using var db = new FerreteriaDbContext(options);

        Assert.Equal(40, GetMaxLength(db, typeof(DteIssued), nameof(DteIssued.ControlNumber)));
        Assert.Equal(5, GetMaxLength(db, typeof(DteIssued), nameof(DteIssued.DteType)));
        Assert.Equal(10, GetMaxLength(db, typeof(Family), nameof(Family.Code)));
        Assert.Equal(300, GetMaxLength(db, typeof(Family), nameof(Family.Description)));
        Assert.Equal(300, GetMaxLength(db, typeof(Setting), nameof(Setting.Description)));
    }

    private static int? GetMaxLength(FerreteriaDbContext db, Type entityType, string propertyName)
    {
        var property = db.Model.FindEntityType(entityType)?.FindProperty(propertyName);
        Assert.NotNull(property);
        return property!.GetMaxLength();
    }
}
