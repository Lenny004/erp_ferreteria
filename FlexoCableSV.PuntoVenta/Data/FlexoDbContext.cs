using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexoCableSV.PuntoVenta.Data
{
    public class FlexoDbContext : DbContext
    {
        public FlexoDbContext(DbContextOptions<FlexoDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<DteIssued> DteIssued => Set<DteIssued>();
        public DbSet<DteContingency> DteContingencies => Set<DteContingency>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.OrderDate).HasColumnType("date");
                entity.Property(o => o.OrderTime).HasColumnType("time");
            });
        }
    }
}
