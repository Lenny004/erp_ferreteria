using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexoCableSV.PuntoVenta.Data;

public class FlexoDbContext : DbContext
{
    public FlexoDbContext(DbContextOptions<FlexoDbContext> options) : base(options) { }

    // public schema — catálogo e inventario
    public DbSet<MeasurementType> MeasurementTypes => Set<MeasurementType>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<Subfamily> Subfamilies => Set<Subfamily>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<StockAlert> StockAlerts => Set<StockAlert>();

    // sales schema
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    // dte schema
    public DbSet<DteConfig> DteConfigs => Set<DteConfig>();
    public DbSet<DteIssued> DteIssued => Set<DteIssued>();
    public DbSet<DteContingency> DteContingencies => Set<DteContingency>();

    // hr schema
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<PayrollDetail> PayrollDetails => Set<PayrollDetail>();

    // system schema
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<WebUser> WebUsers => Set<WebUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // public schema
        modelBuilder.Entity<MeasurementType>().ToTable("MeasurementTypes", "public");
        modelBuilder.Entity<Family>().ToTable("Families", "public");
        modelBuilder.Entity<Subfamily>().ToTable("Subfamilies", "public");
        modelBuilder.Entity<Supplier>().ToTable("Suppliers", "public");
        modelBuilder.Entity<Product>().ToTable("Products", "public");
        modelBuilder.Entity<InventoryMovement>().ToTable("InventoryMovements", "public");
        modelBuilder.Entity<StockAlert>().ToTable("StockAlerts", "public");

        // sales schema
        modelBuilder.Entity<Application>().ToTable("Applications", "sales");
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", "sales");
            entity.Property(o => o.OrderDate).HasColumnType("date");
            entity.Property(o => o.OrderTime).HasColumnType("time");
        });
        modelBuilder.Entity<OrderDetail>().ToTable("OrderDetails", "sales");

        // dte schema
        modelBuilder.Entity<DteConfig>().ToTable("DteConfig", "dte");
        modelBuilder.Entity<DteIssued>().ToTable("DteIssued", "dte");
        modelBuilder.Entity<DteContingency>().ToTable("DteContingency", "dte");

        // hr schema
        modelBuilder.Entity<Department>().ToTable("Departments", "hr");
        modelBuilder.Entity<Position>().ToTable("Positions", "hr");
        modelBuilder.Entity<Employee>().ToTable("Employees", "hr");
        modelBuilder.Entity<Payroll>().ToTable("Payroll", "hr");
        modelBuilder.Entity<PayrollDetail>().ToTable("PayrollDetails", "hr");

        // system schema
        modelBuilder.Entity<Setting>().ToTable("Settings", "system");
        modelBuilder.Entity<Printer>().ToTable("Printers", "system");
        modelBuilder.Entity<WebUser>().ToTable("WebUsers", "system");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLog", "system");

        // relaciones (Regla 8 — Fluent API)
        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(od => od.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Position)
            .WithMany(p => p.Employees)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
