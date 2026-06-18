// Importa los modelos de dominio (entidades) que serán mapeados a tablas de la base de datos.
using FlexoCableSV.PuntoVenta.Models;
// Importa Entity Framework Core, el ORM que abstrae el acceso a datos y permite trabajar con objetos .NET.
using Microsoft.EntityFrameworkCore;

namespace FlexoCableSV.PuntoVenta.Data;

/// <summary>
/// Contexto principal de Entity Framework Core para el sistema FlexoCable Punto de Venta.
/// Esta clase es el "puente" entre el código C# y la base de datos PostgreSQL.
/// Cada propiedad DbSet representa una tabla, y la configuración Fluent API en OnModelCreating
/// define la forma exacta del esquema (nombres de tablas, relaciones, índices, constraints, etc.).
/// </summary>
public class FlexoDbContext : DbContext
{
    /// <summary>
    /// Constructor que recibe las opciones de configuración inyectadas por el contenedor DI.
    /// Las opciones incluyen la cadena de conexión, el proveedor de base de datos (PostgreSQL/Npgsql),
    /// y comportamientos como logging o caché de consultas.
    /// </summary>
    /// <param name="options">Opciones de configuración del contexto, típicamente configuradas en Program.cs.</param>
    public FlexoDbContext(DbContextOptions<FlexoDbContext> options) : base(options) { }

    // ========================================================================
    //  ESQUEMA "public" — Catálogo general e inventario
    //  Este esquema contiene las tablas base del sistema: productos, familias,
    //  proveedores, movimientos de inventario, etc.
    // ========================================================================

    /// <summary>Tipos de medida asociados a productos (ej: metro, libra, unidad).</summary>
    public DbSet<MeasurementType> MeasurementTypes => Set<MeasurementType>();
    /// <summary>Categorías de alto nivel (familias) para agrupar productos.</summary>
    public DbSet<Family> Families => Set<Family>();
    /// <summary>Subcategorías hijas de una familia para una clasificación más detallada.</summary>
    public DbSet<Subfamily> Subfamilies => Set<Subfamily>();
    /// <summary>Proveedores o fabricantes de los productos.</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    /// <summary>Catálogo maestro de productos con precio, stock, impuesto, etc.</summary>
    public DbSet<Product> Products => Set<Product>();
    /// <summary>Bitácora de movimientos de inventario (entradas, salidas, ajustes).</summary>
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    /// <summary>Alertas automáticas cuando el stock de un producto baja del mínimo configurado.</summary>
    public DbSet<StockAlert> StockAlerts => Set<StockAlert>();

    // ========================================================================
    //  ESQUEMA "sales" — Ventas y pedidos
    //  Maneja las aplicaciones (puntos de venta), órdenes y su detalle.
    // ========================================================================

    /// <summary>Órdenes de venta (cabecera).</summary>
    public DbSet<Order> Orders => Set<Order>();
    /// <summary>Líneas de detalle de cada orden (productos y cantidades).</summary>
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    /// <summary>Turnos de caja por cajero y caja física.</summary>
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    /// <summary>Pagos aplicados a órdenes de venta.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    // ========================================================================
    //  ESQUEMA "dte" — Documento Tributario Electrónico (Hacienda SV)
    //  Cumplimiento fiscal para facturación electrónica en El Salvador.
    // ========================================================================

    /// <summary>Configuración general del DTE (ambiente, credenciales, etc.).</summary>
    public DbSet<DteConfig> DteConfigs => Set<DteConfig>();
    /// <summary>DTEs emitidos con su estado de envío a Hacienda.</summary>
    public DbSet<DteIssued> DteIssued => Set<DteIssued>();
    /// <summary>Registro de eventos de contingencia cuando no se puede enviar el DTE en línea.</summary>
    public DbSet<DteContingency> DteContingencies => Set<DteContingency>();

    // ========================================================================
    //  ESQUEMA "hr" — Recursos Humanos (Human Resources)
    //  Gestión de empleados, puestos, departamentos y nómina.
    // ========================================================================

    /// <summary>Departamentos de la empresa.</summary>
    public DbSet<Department> Departments => Set<Department>();
    /// <summary>Puestos o cargos dentro de cada departamento.</summary>
    public DbSet<Position> Positions => Set<Position>();
    /// <summary>Empleados activos e inactivos con datos contractuales.</summary>
    public DbSet<Employee> Employees => Set<Employee>();

    // ========================================================================
    //  ESQUEMA "system" — Configuración y seguridad del sistema
    //  Parámetros globales, impresoras y auditoría (planilla/WebUsers → admin Node).
    // ========================================================================

    /// <summary>Parámetros de configuración global del sistema (clave/valor).</summary>
    public DbSet<Setting> Settings => Set<Setting>();
    /// <summary>Impresoras configuradas para tiquets, facturas, etc.</summary>
    public DbSet<Printer> Printers => Set<Printer>();
    /// <summary>Bitácora de auditoría para rastrear cambios importantes en los datos.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ========================================================================
    //  OnModelCreating — Configuración del esquema mediante Fluent API
    //  Este método es invocado por EF Core durante la creación del modelo en memoria.
    //  Aquí se definen:
    //    - ToTable: esquema y nombre de cada tabla.
    //    - HasOne / WithMany: relaciones y claves foráneas.
    //    - HasIndex: índices únicos, compuestos y filtrados (parciales).
    //    - HasFilter: índices parciales (solo aplican a un subconjunto de filas).
    //    - HasColumnType: tipos de columna específicos de PostgreSQL.
    //
    //  NOTA: Los HasCheckConstraint se eliminaron porque se gestionan directamente
    //  en el script SQL de creación de la base de datos, que es la fuente de verdad
    //  única para constraints CHECK (STD-001: el esquema SQL es la autoridad final).
    //  Mantenerlos aquí y en SQL provocaba duplicación y riesgo de desincronización.
    // ========================================================================

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Llama al base para que ejecute cualquier convención global configurada.
        base.OnModelCreating(modelBuilder);

        // ====================================================================
        //  ESQUEMA "public"
        // ====================================================================

        // --- MeasurementType ---
        // Tabla de catálogo para tipos de medida (ej: "MT" = metro, "LB" = libra).
        // Cada tipo tiene un código único (Code) que se usa como identificador lógico.
        modelBuilder.Entity<MeasurementType>(entity =>
        {
            // Asigna explícitamente el esquema y nombre de tabla para evitar que EF
            // use el nombre por defecto (que sería "MeasurementTypes" sin esquema).
            entity.ToTable("MeasurementTypes", "public");
            entity.Property(m => m.Id).HasColumnName("id");
            entity.Property(m => m.Code).HasColumnName("code");
            entity.Property(m => m.Name).HasColumnName("name");
            entity.Property(m => m.Decimals).HasColumnName("decimals");

            // Índice único sobre Code, ya que este campo se usa como clave alternativa
            // en dropdowns y búsquedas, y debe ser irrepetible.
            entity.HasIndex(m => m.Code).IsUnique();
        });

        // --- Family ---
        // Tabla de catálogo para familias de productos (ej: "CABLE", "HERRAMIENTA").
        modelBuilder.Entity<Family>(entity =>
        {
            entity.ToTable("Families", "public");
            entity.Property(f => f.Id).HasColumnName("id");
            entity.Property(f => f.Code).HasColumnName("code");
            entity.Property(f => f.Name).HasColumnName("name");
            entity.Property(f => f.Description).HasColumnName("description");

            // Code es el identificador lógico visible en pantallas y reportes.
            entity.HasIndex(f => f.Code).IsUnique();
        });

        // --- Subfamily ---
        // Tabla hija de Family. Cada familia puede tener N subfamilias.
        // Ejemplo: Familia "CABLE" -> Subfamilias "CABLE ELÉCTRICO", "CABLE DATOS".
        modelBuilder.Entity<Subfamily>(entity =>
        {
            entity.ToTable("Subfamilies", "public");
            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.Code).HasColumnName("code");
            entity.Property(s => s.Name).HasColumnName("name");

            // Relación N:1 con Family. Una subfamilia pertenece a exactamente una familia.
            // DeleteBehavior.NoAction evita borrados en cascada no deseados desde el ORM;
            // la base de datos maneja la integridad referencial directamente.
            entity.HasOne(s => s.Family)
                .WithMany(f => f.Subfamilies)
                .HasForeignKey(s => s.FamilyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índice compuesto único: dentro de una misma familia, los códigos de subfamilia
            // no pueden repetirse. Ej: FamilyId=1 no puede tener dos subfamilias con Code="A".
            entity.HasIndex(s => new { s.FamilyId, s.Code }).IsUnique();
        });

        // --- Supplier ---
        // Tabla simple de proveedores sin configuraciones adicionales de momento.
        // Se deja la definición mínima y se heredan las columnas de la clase Supplier.
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers", "purchasing");
            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.Name).HasColumnName("name");
            entity.Property(s => s.Phone).HasColumnName("phone");
            entity.Property(s => s.Email).HasColumnName("email");
            entity.Property(s => s.Address).HasColumnName("address");
            entity.Property(s => s.Municipality).HasColumnName("municipality");
            entity.Property(s => s.Department).HasColumnName("department");
            entity.Property(s => s.Country).HasColumnName("country");
            entity.Property(s => s.Notes).HasColumnName("notes");
            entity.HasIndex(s => s.Nit).IsUnique().HasDatabaseName("IdxSuppliersNit");
        });

        // --- Product ---
        // Tabla central del catálogo. Contiene toda la información comercial,
        // de inventario e impuestos de cada artículo vendible.
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", "public");
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Code).HasColumnName("code");
            entity.Property(p => p.Barcode).HasColumnName("barcode");
            entity.Property(p => p.Description).HasColumnName("description");
            entity.Property(p => p.Notes).HasColumnName("notes");

            // Relaciones del catálogo — cada producto apunta a una familia, subfamilia,
            // tipo de medida y proveedor. Todas con DeleteBehavior.NoAction para que EF
            // no intente borrar en cascada; las FK se validan a nivel de base de datos.
            entity.HasOne(p => p.Family)
                .WithMany(f => f.Products)
                .HasForeignKey(p => p.FamilyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(p => p.Subfamily)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SubfamilyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(p => p.MeasurementType)
                .WithMany()
                .HasForeignKey(p => p.MeasurementTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            // El código de producto (Code) debe ser único en toda la tabla.
            entity.HasIndex(p => p.Code).IsUnique();

            // Índice sobre FamilyId para acelerar las consultas de productos por familia
            // (ej: "mostrar todos los productos de la familia CABLE").
            entity.HasIndex(p => p.FamilyId).HasDatabaseName("IdxProductsFamily");

            // Índice parcial (filtrado en SQL) para listar solo productos activos.
            // Acelera las consultas de catálogo donde siempre se filtra IsActive = true.
            entity.HasIndex(p => p.IsActive).HasDatabaseName("IdxProductsActive");
        });

        // --- InventoryMovement ---
        // Bitácora de todo movimiento que afecta el stock de un producto.
        // Cada movimiento tiene un tipo (entrada/salida/ajuste) y referencia opcional
        // al proveedor o empleado que lo generó.
        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements", "public");
            entity.Property(i => i.Id).HasColumnName("id");
            entity.Property(i => i.Reason).HasColumnName("reason");

            // Relación N:1 con Product. Cada movimiento afecta a un solo producto.
            entity.HasOne(i => i.Product)
                .WithMany(p => p.InventoryMovements)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relación opcional con Employee. Se usa cuando el movimiento es un ajuste
            // de inventario realizado por un empleado específico.
            entity.HasOne(i => i.Employee)
                .WithMany(e => e.InventoryMovements)
                .HasForeignKey(i => i.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(i => i.Order)
                .WithMany(o => o.InventoryMovements)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índices para las consultas más frecuentes sobre movimientos:
            // - por producto (historial de un producto específico)
            // - por tipo de movimiento (ej: solo entradas)
            // - por fecha (reportes por período)
            entity.HasIndex(i => i.ProductId).HasDatabaseName("IdxInvProduct");
            entity.HasIndex(i => i.MovementType).HasDatabaseName("IdxInvType");
            entity.HasIndex(i => i.CreatedAt).HasDatabaseName("IdxInvDate");
        });

        // --- StockAlert ---
        // Alerta generada automáticamente cuando CurrentStock <= MinimumStock.
        // Se resuelve (IsResolved = true) cuando el inventario se regulariza.
        modelBuilder.Entity<StockAlert>(entity =>
        {
            entity.ToTable("StockAlerts", "public");

            // Relación N:1 con Product. Un producto puede tener muchas alertas en el tiempo.
            entity.HasOne(s => s.Product)
                .WithMany(p => p.StockAlerts)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índice parcial: solo incluye alertas no resueltas. Como la mayoría de consultas
            // piden "alertas activas", este índice es más pequeño y rápido que uno completo.
            entity.HasIndex(s => s.ProductId)
                .HasDatabaseName("IdxStockAlertsUnresolved")
                .HasFilter("\"IsResolved\" = FALSE");
        });

        // ====================================================================
        //  ESQUEMA "sales"
        // ====================================================================

        // --- Order ---
        // Cabecera de una orden de venta. Contiene fechas, estado, empleado y punto de venta.
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", "sales");
            entity.Property(o => o.Id).HasColumnName("id");
            entity.Property(o => o.Status).HasColumnName("status");
            entity.Property(o => o.Subtotal).HasColumnName("subtotal");
            entity.Property(o => o.Total).HasColumnName("total");
            entity.Property(o => o.Notes).HasColumnName("notes");

            // Relación N:1 con Employee. Cada orden es atendida por un empleado.
            entity.HasOne(o => o.Employee)
                .WithMany(e => e.Orders)
                .HasForeignKey(o => o.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(o => o.CashSession)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CashSessionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índices para consultas comunes:
            // - por estado (órdenes pendientes, canceladas)
            // - por empleado (historial de ventas por vendedor)
            entity.HasIndex(o => o.Status).HasDatabaseName("IdxOrdersStatus");
            entity.HasIndex(o => o.EmployeeId).HasDatabaseName("IdxOrdersEmployee");
            entity.HasIndex(o => o.ClientRequestId)
                .IsUnique()
                .HasDatabaseName("IdxOrdersClientRequest");
            entity.HasIndex(o => o.CashSessionId).HasDatabaseName("IdxOrdersCashSession");
        });

        // --- OrderDetail ---
        // Líneas de detalle de cada orden. Registra qué productos y en qué cantidad
        // se vendieron, al precio unitario del momento de la venta.
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.ToTable("OrderDetails", "sales");
            entity.Property(od => od.Id).HasColumnName("id");
            entity.Property(od => od.Quantity).HasColumnName("quantity");
            entity.Property(od => od.Subtotal).HasColumnName("subtotal");
            entity.Property(od => od.Notes).HasColumnName("notes");

            // Relación N:1 con Order. El detalle se borra en cascada si se elimina la orden.
            // Esto es correcto porque el detalle no tiene sentido sin su orden padre.
            entity.HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación N:1 con Product. El producto se conserva aunque se elimine el detalle,
            // ya que el producto pertenece al catálogo maestro independientemente.
            entity.HasOne(od => od.Product)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índice para acelerar consultas que traen todo el detalle de una orden.
            entity.HasIndex(od => od.OrderId).HasDatabaseName("IdxOrderDetailsOrder");
            entity.HasIndex(od => od.ProductId).HasDatabaseName("IdxOrderDetailsProduct");
        });

        // --- CashSession ---
        modelBuilder.Entity<CashSession>(entity =>
        {
            entity.ToTable("CashSessions", "sales");
            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.Difference).HasColumnName("difference");
            entity.Property(c => c.Status).HasColumnName("status");
            entity.Property(c => c.Notes).HasColumnName("notes");

            entity.HasOne(c => c.Employee)
                .WithMany(e => e.CashSessions)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(c => new { c.EmployeeId, c.CashRegisterCode })
                .IsUnique()
                .HasDatabaseName("IdxCashSessionOpen")
                .HasFilter("\"status\" = 'ABIERTA'");
            entity.HasIndex(c => c.Status).HasDatabaseName("IdxCashSessionStatus");
            entity.HasIndex(c => c.OpenedAt).HasDatabaseName("IdxCashSessionOpened");
        });

        // --- Payment ---
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments", "sales");
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Method).HasColumnName("method");
            entity.Property(p => p.Amount).HasColumnName("amount");
            entity.Property(p => p.Reference).HasColumnName("reference");

            entity.HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.CashSession)
                .WithMany(c => c.Payments)
                .HasForeignKey(p => p.CashSessionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(p => p.OrderId).HasDatabaseName("IdxPaymentsOrder");
            entity.HasIndex(p => p.CashSessionId).HasDatabaseName("IdxPaymentsSession");
        });

        // ====================================================================
        //  ESQUEMA "dte"
        // ====================================================================

        // --- DteConfig ---
        // Configuración del módulo de facturación electrónica (DTE).
        // Guarda el ambiente (00 = pruebas, 01 = producción), credenciales MH,
        // y otros parámetros necesarios para la comunicación con Hacienda.
        modelBuilder.Entity<DteConfig>(entity =>
        {
            entity.ToTable("DteConfig", "dte");
            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.Phone).HasColumnName("phone");
            entity.Property(d => d.Email).HasColumnName("email");
            entity.Property(d => d.Ambiente).HasColumnName("ambiente");
            // NOTA: Los CHECK constraints (Ambiente en '00','01') se gestionan en SQL.
        });

        // --- DteIssued ---
        // Registro de cada DTE emitido, con toda la información fiscal requerida por
        // el Ministerio de Hacienda de El Salvador (número de control, código de generación,
        // estado de procesamiento, sello de recepción MH, etc.).
        modelBuilder.Entity<DteIssued>(entity =>
        {
            entity.ToTable("DteIssued", "dte");
            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.Ambiente).HasColumnName("ambiente");
            entity.Property(d => d.Reprints).HasColumnName("reprints");

            // Relación N:1 con Order. Un DTE puede estar asociado a una orden de venta
            // (o podría ser nota de crédito/débito sin orden directa).
            entity.HasOne(d => d.Order)
                .WithMany(o => o.DteIssued)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.RelatedDte)
                .WithMany(d => d.CreditNotes)
                .HasForeignKey(d => d.RelatedDteId)
                .OnDelete(DeleteBehavior.NoAction);

            // El número de control es el identificador único asignado por el sistema de facturación.
            // Tiene un formato definido por MH y debe ser irrepetible.
            entity.HasIndex(d => d.ControlNumber).IsUnique();

            // Código de generación: UUID único por DTE que Hacienda usa para identificar el documento.
            entity.HasIndex(d => d.GenerationCode).IsUnique();

            // Índices para consultas frecuentes:
            // - búsqueda de DTE por orden
            // - filtro por estado MH (PENDIENTE, PROCESADO, RECHAZADO)
            // - reportes por fecha de emisión
            entity.HasIndex(d => d.OrderId).HasDatabaseName("IdxDteIssuedOrder");
            entity.HasIndex(d => d.MhStatus).HasDatabaseName("IdxDteIssuedStatus");
            entity.HasIndex(d => d.DteType).HasDatabaseName("IdxDteIssuedType");
            entity.HasIndex(d => d.IssuedAt).HasDatabaseName("IdxDteIssuedAt");
        });

        // --- DteContingency ---
        // Cuando el sistema no puede enviar el DTE a Hacienda en el momento de la emisión
        // (ej: sin conexión a internet), se genera un evento de contingencia que permite
        // emitir el documento y enviarlo posteriormente cuando se restablezca la conexión.
        modelBuilder.Entity<DteContingency>(entity =>
        {
            entity.ToTable("DteContingency", "dte");
            entity.Property(d => d.Id).HasColumnName("id");

            // Relación N:1 con DteIssued. Un DTE en contingencia se asocia al DTE generado.
            // Se usa WithMany() (sin propiedad de navegación) porque la colección de
            // contingencias en DteIssued no es necesaria para el modelo de dominio.
            entity.HasOne(d => d.DteIssued)
                .WithOne(d => d.Contingency)
                .HasForeignKey<DteContingency>(d => d.DteId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(d => d.DteId).IsUnique();
            entity.HasIndex(d => d.NextRetryAt).HasDatabaseName("IdxContingencyRetry");
        });

        // ====================================================================
        //  ESQUEMA "hr" (Human Resources)
        // ====================================================================

        // --- Department ---
        // Departamentos de la organización (ej: VENTAS, BODEGA, ADMINISTRACIÓN).
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments", "hr");
            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.Name).HasColumnName("name");
            entity.Property(d => d.Description).HasColumnName("description");

            entity.HasOne(d => d.Parent)
                .WithMany(d => d.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.NoAction);

            // El nombre del departamento debe ser único.
            entity.HasIndex(d => d.Name).IsUnique();
            entity.HasIndex(d => d.ParentId).HasDatabaseName("IdxDepartmentsParent");
        });

        // --- Position ---
        // Puestos dentro de cada departamento (ej: VENDEDOR, BODEGUERO, CONTADOR).
        modelBuilder.Entity<Position>(entity =>
        {
            entity.ToTable("Positions", "hr");
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Name).HasColumnName("name");
            entity.Property(p => p.Description).HasColumnName("description");

            // Relación N:1 con Department. Cada puesto pertenece a un departamento.
            entity.HasOne(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(p => new { p.DepartmentId, p.Name }).IsUnique();
        });

        // --- Employee ---
        // Datos personales y contractuales de cada empleado.
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees", "hr");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Address).HasColumnName("address");

            // Configuración de tipo de columna para fechas administrativas.
            // Se usa "date" (no "timestamp with time zone") porque la fecha de contratación
            // y terminación son datos administrativos sin relevancia de zona horaria.
            // La hora exacta no importa, solo el día.
            entity.Property(e => e.HireDate).HasColumnType("date");
            entity.Property(e => e.TerminationDate).HasColumnType("date");

            // Relación N:1 con Position. Cada empleado tiene un puesto asignado.
            entity.HasOne(e => e.Position)
                .WithMany(p => p.Employees)
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.DirectSupervisor)
                .WithMany(e => e.Subordinates)
                .HasForeignKey(e => e.DirectSupervisorId)
                .OnDelete(DeleteBehavior.NoAction);

            // El DUI (Documento Único de Identidad) y NIT (Número de Identificación Tributaria)
            // son documentos oficiales salvadoreños que deben ser únicos por empleado.
            entity.HasIndex(e => e.Dui).IsUnique();
            entity.HasIndex(e => e.Nit).IsUnique();
            entity.HasIndex(e => e.Nup).IsUnique();
            entity.HasIndex(e => e.IsssNumber).IsUnique();
            entity.HasIndex(e => e.DepartmentId).HasDatabaseName("IdxEmployeesDepartment");
            entity.HasIndex(e => e.DirectSupervisorId).HasDatabaseName("IdxEmployeesSupervisor");
            entity.HasIndex(e => e.ContractType).HasDatabaseName("IdxEmployeesContractType");
        });

        // ====================================================================
        //  ESQUEMA "system"
        // ====================================================================

        // --- Setting ---
        // Configuración global del sistema en pares clave/valor.
        // Ej: "EMPRESA_NOMBRE", "EMPRESA_NIT", "TASA_IVA", etc.
        // No requiere configuración adicional por ahora.
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("Settings", "system");
        });

        // --- Printer ---
        // Impresoras configuradas en el sistema para tiquets, facturas, reportes.
        // Se usa "Printers" (no "Printer") como nombre de tabla estándar en plural.
        modelBuilder.Entity<Printer>(entity =>
        {
            entity.ToTable("Printers", "system");

            // Índice parcial único: solo una impresora puede ser la predeterminada.
            // El filtro "WHERE IsDefault = TRUE" garantiza que a lo sumo una fila
            // tenga IsDefault = true, sin afectar las demás filas con IsDefault = false.
            entity.HasIndex(p => p.IsDefault)
                .IsUnique()
                .HasDatabaseName("IdxPrinterDefault")
                .HasFilter("\"IsDefault\" = TRUE");
        });

        // --- AuditLog ---
        // Bitácora de auditoría que registra cambios en las tablas principales.
        // Cada entrada indica qué tabla se modificó, qué registro, qué acción (INSERT/UPDATE/DELETE),
        // el usuario que realizó el cambio, y los valores anteriores y nuevos.
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog", "system");
            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.Action).HasColumnName("action");

            // Índice para consultas de auditoría por tabla de negocio.
            entity.HasIndex(a => new { a.TableName, a.RecordId }).HasDatabaseName("IdxAuditLogRecord");
            entity.HasIndex(a => a.UserId).HasDatabaseName("IdxAuditLogUser");

            // Índice para consultas por fecha (ej: "mostrar auditoría de la última semana").
            entity.HasIndex(a => a.CreatedAt).HasDatabaseName("IdxAuditLogCreatedAt");
        });
    }
}
