using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexoCableSV.PuntoVenta.Data
{
    public class FlexoDbContext : DbContext
    {
        public FlexoDbContext(DbContextOptions<FlexoDbContext> options) : base(options)
        {
        }

        public DbSet<MeasurementType> MeasurementTypes => Set<MeasurementType>();
        public DbSet<Family> Families => Set<Family>();
        public DbSet<Subfamily> Subfamilies => Set<Subfamily>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
        public DbSet<StockAlert> StockAlerts => Set<StockAlert>();

        public DbSet<Application> Applications => Set<Application>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

        public DbSet<DteConfig> DteConfigs => Set<DteConfig>();
        public DbSet<DteIssued> DteIssued => Set<DteIssued>();
        public DbSet<DteContingency> DteContingencies => Set<DteContingency>();

        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Position> Positions => Set<Position>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Payroll> Payrolls => Set<Payroll>();
        public DbSet<PayrollDetail> PayrollDetails => Set<PayrollDetail>();

        public DbSet<Setting> Settings => Set<Setting>();
        public DbSet<Printer> Printers => Set<Printer>();
        public DbSet<WebUser> WebUsers => Set<WebUser>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .HasMany(o => o.Details)
                .WithOne(d => d.Order)
                .HasForeignKey(d => d.OrderId);

            modelBuilder.Entity<Payroll>()
                .HasMany(p => p.Details)
                .WithOne(d => d.Payroll)
                .HasForeignKey(d => d.PayrollId);

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Dui)
                .IsUnique();

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Nit)
                .IsUnique();
        }
    }
}
