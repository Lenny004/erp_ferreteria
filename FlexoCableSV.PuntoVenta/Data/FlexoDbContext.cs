using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlexoCableSV.PuntoVenta.Data
{
    public class FlexoDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseNpgsql("Host=localhost;Database=flexocable;Username=postgres;Password=tu_password");
        }

        // Tablas (se agregan conforme avanzas)
        public DbSet<<Models.Producto> Productos { get; set; }
        public DbSet<<Models.Tecnico> Tecnicos { get; set; }
        public DbSet<<Models.OrdenConfeccion> Ordenes { get; set; }
    }
}
