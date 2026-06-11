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

    /// <summary>Puntos de venta o aplicaciones desde donde se generan órdenes.</summary>
    public DbSet<Application> Applications => Set<Application>();
    /// <summary>Órdenes de venta (cabecera).</summary>
    public DbSet<Order> Orders => Set<Order>();
    /// <summary>Líneas de detalle de cada orden (productos y cantidades).</summary>
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

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
    /// <summary>Nóminas periódicas (quincena/mes).</summary>
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    /// <summary>Detalle de cada empleado en una nómina específica.</summary>
    public DbSet<PayrollDetail> PayrollDetails => Set<PayrollDetail>();

    // ========================================================================
    //  ESQUEMA "system" — Configuración y seguridad del sistema
    //  Parámetros globales, impresoras, usuarios web y auditoría.
    // ========================================================================

    /// <summary>Parámetros de configuración global del sistema (clave/valor).</summary>
    public DbSet<Setting> Settings => Set<Setting>();
    /// <summary>Impresoras configuradas para tiquets, facturas, etc.</summary>
    public DbSet<Printer> Printers => Set<Printer>();
    /// <summary>Usuarios del sistema web con roles y acceso.</summary>
    public DbSet<WebUser> WebUsers => Set<WebUser>();
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

            // Índice único sobre Code, ya que este campo se usa como clave alternativa
            // en dropdowns y búsquedas, y debe ser irrepetible.
            entity.HasIndex(m => m.Code).IsUnique();
        });

        // --- Family ---
        // Tabla de catálogo para familias de productos (ej: "CABLE", "HERRAMIENTA").
        modelBuilder.Entity<Family>(entity =>
        {
            entity.ToTable("Families", "public");

            // Code es el identificador lógico visible en pantallas y reportes.
            entity.HasIndex(f => f.Code).IsUnique();
        });

        // --- Subfamily ---
        // Tabla hija de Family. Cada familia puede tener N subfamilias.
        // Ejemplo: Familia "CABLE" -> Subfamilias "CABLE ELÉCTRICO", "CABLE DATOS".
        modelBuilder.Entity<Subfamily>(entity =>
        {
            entity.ToTable("Subfamilies", "public");

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
        modelBuilder.Entity<Supplier>().ToTable("Suppliers", "public");

        // --- Product ---
        // Tabla central del catálogo. Contiene toda la información comercial,
        // de inventario e impuestos de cada artículo vendible.
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", "public");

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

            entity.HasOne(p => p.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierId)
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

            // Relación N:1 con Product. Cada movimiento afecta a un solo producto.
            entity.HasOne(i => i.Product)
                .WithMany(p => p.InventoryMovements)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relación opcional con Supplier. Se usa cuando el movimiento es una
            // entrada por compra o devolución a proveedor.
            entity.HasOne(i => i.Supplier)
                .WithMany(s => s.InventoryMovements)
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relación opcional con Employee. Se usa cuando el movimiento es un ajuste
            // de inventario realizado por un empleado específico.
            entity.HasOne(i => i.Employee)
                .WithMany(e => e.InventoryMovements)
                .HasForeignKey(i => i.EmployeeId)
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

        // --- Application ---
        // Punto de venta o aplicación que genera órdenes.
        // Ej: "CAJA_01", "CAJA_02", "TIENDA_ONLINE".
        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("Applications", "sales");

            // Cada aplicación se identifica por un código único.
            entity.HasIndex(a => a.Code).IsUnique();
        });

        // --- Order ---
        // Cabecera de una orden de venta. Contiene fechas, estado, empleado y punto de venta.
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", "sales");

            // Configuración explícita de tipos DATE y TIME en lugar de DateTime completa.
            // La orden se emite en una fecha civil local (OrderDate) y una hora (OrderTime),
            // sin componente de zona horaria. Esto evita conversiones UTC/locales innecesarias
            // ya que la hora del punto de venta es siempre la hora local de El Salvador.
            entity.Property(o => o.OrderDate).HasColumnType("date");
            entity.Property(o => o.OrderTime).HasColumnType("time");

            // Relación N:1 con Employee. Cada orden es atendida por un empleado.
            entity.HasOne(o => o.Employee)
                .WithMany(e => e.Orders)
                .HasForeignKey(o => o.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relación N:1 con Application. Cada orden pertenece a un punto de venta.
            entity.HasOne(o => o.Application)
                .WithMany(a => a.Orders)
                .HasForeignKey(o => o.ApplicationId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índices para consultas comunes:
            // - por fecha (reportes diarios/mensuales)
            // - por estado (órdenes pendientes, canceladas)
            // - por empleado (historial de ventas por vendedor)
            entity.HasIndex(o => o.OrderDate).HasDatabaseName("IdxOrdersDate");
            entity.HasIndex(o => o.Status).HasDatabaseName("IdxOrdersStatus");
            entity.HasIndex(o => o.EmployeeId).HasDatabaseName("IdxOrdersEmployee");
        });

        // --- OrderDetail ---
        // Líneas de detalle de cada orden. Registra qué productos y en qué cantidad
        // se vendieron, al precio unitario del momento de la venta.
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.ToTable("OrderDetails", "sales");

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
            entity.HasIndex(od => od.OrderId).HasDatabaseName("IdxDetailOrder");
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
            // NOTA: Los CHECK constraints (Environment en '00','01') se gestionan en SQL.
        });

        // --- DteIssued ---
        // Registro de cada DTE emitido, con toda la información fiscal requerida por
        // el Ministerio de Hacienda de El Salvador (número de control, código de generación,
        // estado de procesamiento, sello de recepción MH, etc.).
        modelBuilder.Entity<DteIssued>(entity =>
        {
            entity.ToTable("DteIssued", "dte");

            // Relación N:1 con Order. Un DTE puede estar asociado a una orden de venta
            // (o podría ser nota de crédito/débito sin orden directa).
            entity.HasOne(d => d.Order)
                .WithMany(o => o.DteIssued)
                .HasForeignKey(d => d.OrderId)
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
            entity.HasIndex(d => d.OrderId).HasDatabaseName("IdxDteOrder");
            entity.HasIndex(d => d.MhStatus).HasDatabaseName("IdxDteStatus");
            entity.HasIndex(d => d.CreatedAt).HasDatabaseName("IdxDteDate");
        });

        // --- DteContingency ---
        // Cuando el sistema no puede enviar el DTE a Hacienda en el momento de la emisión
        // (ej: sin conexión a internet), se genera un evento de contingencia que permite
        // emitir el documento y enviarlo posteriormente cuando se restablezca la conexión.
        modelBuilder.Entity<DteContingency>(entity =>
        {
            entity.ToTable("DteContingency", "dte");

            // Relación N:1 con DteIssued. Un DTE en contingencia se asocia al DTE generado.
            // Se usa WithMany() (sin propiedad de navegación) porque la colección de
            // contingencias en DteIssued no es necesaria para el modelo de dominio.
            entity.HasOne(d => d.DteIssued)
                .WithMany()
                .HasForeignKey(d => d.DteId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ====================================================================
        //  ESQUEMA "hr" (Human Resources)
        // ====================================================================

        // --- Department ---
        // Departamentos de la organización (ej: VENTAS, BODEGA, ADMINISTRACIÓN).
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments", "hr");

            // El nombre del departamento debe ser único.
            entity.HasIndex(d => d.Name).IsUnique();
        });

        // --- Position ---
        // Puestos dentro de cada departamento (ej: VENDEDOR, BODEGUERO, CONTADOR).
        modelBuilder.Entity<Position>(entity =>
        {
            entity.ToTable("Positions", "hr");

            // Relación N:1 con Department. Cada puesto pertenece a un departamento.
            entity.HasOne(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // --- Employee ---
        // Datos personales y contractuales de cada empleado.
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees", "hr");

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

            // El DUI (Documento Único de Identidad) y NIT (Número de Identificación Tributaria)
            // son documentos oficiales salvadoreños que deben ser únicos por empleado.
            entity.HasIndex(e => e.Dui).IsUnique();
            entity.HasIndex(e => e.Nit).IsUnique();
        });

        // --- Payroll ---
        // Nómina: registro de un período de pago (quincena/mes) que agrupa los salarios
        // y deducciones de todos los empleados para ese período.
        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.ToTable("Payroll", "hr");

            // Índice único compuesto: no puede haber dos nóminas para el mismo mes y año.
            entity.HasIndex(p => new { p.PeriodMonth, p.PeriodYear }).IsUnique();
        });

        // --- PayrollDetail ---
        // Detalle individual de cada empleado en una nómina específica.
        // Contiene salario base, horas extras, deducciones, ISSS, AFP, ISR, etc.
        modelBuilder.Entity<PayrollDetail>(entity =>
        {
            entity.ToTable("PayrollDetails", "hr");

            // Relación N:1 con Payroll. Cada detalle pertenece a una nómina.
            // Se usa NoAction para evitar que EF borre detalles accidentalmente
            // al eliminar una nómina; la BD maneja esta lógica.
            entity.HasOne(pd => pd.Payroll)
                .WithMany(p => p.PayrollDetails)
                .HasForeignKey(pd => pd.PayrollId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relación N:1 con Employee. Cada detalle corresponde a un empleado.
            entity.HasOne(pd => pd.Employee)
                .WithMany(e => e.PayrollDetails)
                .HasForeignKey(pd => pd.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Índice único compuesto: un mismo empleado no puede aparecer dos veces
            // en la misma nómina.
            entity.HasIndex(pd => new { pd.PayrollId, pd.EmployeeId }).IsUnique();
        });

        // ====================================================================
        //  ESQUEMA "system"
        // ====================================================================

        // --- Setting ---
        // Configuración global del sistema en pares clave/valor.
        // Ej: "EMPRESA_NOMBRE", "EMPRESA_NIT", "TASA_IVA", etc.
        // No requiere configuración adicional por ahora.
        modelBuilder.Entity<Setting>().ToTable("Settings", "system");

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

        // --- WebUser ---
        // Usuarios del sistema con acceso a la interfaz web y roles de seguridad.
        modelBuilder.Entity<WebUser>(entity =>
        {
            entity.ToTable("WebUsers", "system");

            // Relación N:1 con Employee. Cada usuario web está vinculado a un empleado real.
            // Esto permite que al desactivar un empleado, su usuario también pierda acceso.
            entity.HasOne(w => w.Employee)
                .WithMany(e => e.WebUsers)
                .HasForeignKey(w => w.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // El nombre de usuario (Username) y el correo electrónico (Email) deben ser
            // únicos en el sistema para evitar duplicados y garantizar identificadores
            // de inicio de sesión sin conflictos.
            entity.HasIndex(w => w.Username).IsUnique();
            entity.HasIndex(w => w.Email).IsUnique();
        });

        // --- AuditLog ---
        // Bitácora de auditoría que registra cambios en las tablas principales.
        // Cada entrada indica qué tabla se modificó, qué registro, qué acción (INSERT/UPDATE/DELETE),
        // el usuario que realizó el cambio, y los valores anteriores y nuevos.
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog", "system");

            // Índice para consultas de auditoría por tabla de negocio.
            entity.HasIndex(a => a.TableName).HasDatabaseName("IdxAuditTable");

            // Índice para consultas por fecha (ej: "mostrar auditoría de la última semana").
            entity.HasIndex(a => a.CreatedAt).HasDatabaseName("IdxAuditDate");
        });
    }
}
