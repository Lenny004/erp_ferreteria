using System.Diagnostics;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Dte;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>
/// Fixture compartido: levanta UN contenedor PostgreSQL desechable (nunca la base del equipo),
/// aplica <c>Squema.sql</c> tal cual y expone el servicio de historial bajo prueba.
/// </summary>
/// <remarks>
/// <c>Squema.sql</c> ya trae datos semilla (empleados Carlos/Cajero, María/Encargado de Inventario,
/// Administrador y productos); las pruebas los reutilizan y siembran sus órdenes en días propios
/// para no depender del orden de ejecución.
/// </remarks>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    /// <summary>Id del empleado semilla con puesto "Cajero".</summary>
    public Guid CashierId { get; private set; }

    /// <summary>Id de un segundo cajero creado por el fixture para casos de concurrencia y permisos.</summary>
    public Guid SecondCashierId { get; private set; }

    /// <summary>Id del empleado semilla con puesto "Encargado de Inventario".</summary>
    public Guid NonCashierId { get; private set; }

    /// <summary>Id del empleado semilla con puesto "Administrador".</summary>
    public Guid ManagerId { get; private set; }

    /// <summary>Instante fijo de las pruebas: 27/09/2026 12:00 hora de El Salvador (18:00 UTC).</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ferreteria_test")
        .WithUsername("ferreteria_test")
        .WithPassword("ferreteria_test")
        .Build();

    private ServiceProvider? _services;

    /// <summary>Proveedor con el contexto EF y el servicio bajo prueba.</summary>
    public IServiceProvider Services => _services ?? throw new InvalidOperationException("El fixture no fue inicializado.");

    /// <summary>Cadena de conexión del contenedor efímero.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "Squema.sql"));
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(schema, connection);
            await command.ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddOptions<SalesHistoryOptions>()
            .Configure(options => options.FullHistoryPositionNames = new List<string> { "Administrador" });
        services.AddOptions<CashRegisterOptions>()
            .Configure(options =>
            {
                options.Codigo = "CAJA-INT";
                options.MontoMaximo = 100000m;
                options.UmbralDiferencia = 1m;
                options.AnchoReporte = 48;
            });
        services.AddSingleton<ISalesHistoryService, SalesHistoryService>();
        services.AddSingleton<ICashSessionService, CashSessionService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<ILogger<AuditService>>(_ => NullLogger<AuditService>.Instance);
        services.AddSingleton<ILogger<CashSessionService>>(_ => NullLogger<CashSessionService>.Instance);
        services.AddSingleton<ILogger<OrderService>>(_ => NullLogger<OrderService>.Instance);
        _services = services.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        CashierId = await ResolveEmployeeIdAsync(db, "00000002-0", "Cajero", true);
        SecondCashierId = await EnsureSecondCashierAsync(db);
        NonCashierId = await ResolveEmployeeIdAsync(db, "00000003-0", "Encargado de Inventario", false);
        ManagerId = await ResolveEmployeeIdAsync(db, "00000001-0", "Administrador", true);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>Obtiene el servicio de historial registrado en el fixture.</summary>
    /// <returns>Servicio bajo prueba.</returns>
    public ISalesHistoryService History => Services.GetRequiredService<ISalesHistoryService>();

    /// <summary>Obtiene el servicio de sesiones de caja registrado en el fixture.</summary>
    /// <returns>Servicio transaccional bajo prueba.</returns>
    public ICashSessionService CashSessions => Services.GetRequiredService<ICashSessionService>();

    /// <summary>Obtiene el servicio de órdenes transaccional enlazado al fixture.</summary>
    /// <returns>Servicio de ventas y facturación de confección.</returns>
    public IOrderService Orders => Services.GetRequiredService<IOrderService>();

    /// <summary>Fija el código esperado por el servicio de órdenes para un caso aislado.</summary>
    /// <param name="cashRegisterCode">Código único de la caja del caso.</param>
    public void SetCashRegisterCode(string cashRegisterCode)
    {
        var options = Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CashRegisterOptions>>();
        options.Value.Codigo = cashRegisterCode;
    }

    /// <summary>Ejecuta una acción de siembra con un contexto EF propio y guarda los cambios.</summary>
    /// <param name="seed">Acción que agrega entidades al contexto.</param>
    /// <returns>Tarea de la siembra.</returns>
    public async Task SeedAsync(Action<FerreteriaDbContext> seed)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Obtiene el id de un producto semilla para líneas y movimientos.</summary>
    /// <returns>Id de un producto existente.</returns>
    public async Task<Guid> GetAnyProductIdAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return await db.Products.OrderBy(product => product.Id).Select(product => product.Id).FirstAsync();
    }

    /// <summary>Convierte una hora local de El Salvador en UTC para sembrar datos.</summary>
    /// <param name="year">Año.</param>
    /// <param name="month">Mes.</param>
    /// <param name="day">Día.</param>
    /// <param name="hour">Hora local.</param>
    /// <param name="minute">Minuto local.</param>
    /// <returns>Fecha UTC equivalente.</returns>
    public static DateTime Local(int year, int month, int day, int hour, int minute = 0)
    {
        return TimeZoneSupport.ToUtc(new DateTime(year, month, day, hour, minute, 0));
    }

    /// <summary>Crea una orden con valores válidos para los CHECK del esquema.</summary>
    /// <param name="employeeId">Empleado que registró la venta.</param>
    /// <param name="createdAtUtc">Fecha UTC de la venta.</param>
    /// <param name="status">Estado de la orden.</param>
    /// <param name="total">Total con IVA.</param>
    /// <returns>Orden sin guardar.</returns>
    public static Order NewOrder(Guid employeeId, DateTime createdAtUtc, string status, decimal total = 11.30m)
    {
        var subtotal = Math.Round(total / 1.13m, 2);
        return new Order
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            OrderType = SalesDomainConstants.OrderTypes.CashRegisterSale,
            Status = status,
            Subtotal = subtotal,
            TaxAmount = total - subtotal,
            Total = total,
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        };
    }

    /// <summary>Crea un DTE con número de control único (máximo 40 caracteres).</summary>
    /// <param name="orderId">Orden asociada o null (por ejemplo, una NC enlazada solo por <c>RelatedDteId</c>).</param>
    /// <param name="type">Tipo de DTE.</param>
    /// <param name="mhStatus">Estado MH.</param>
    /// <param name="tag">Fragmento buscable del número de control.</param>
    /// <returns>DTE sin guardar.</returns>
    public static DteIssued NewDte(Guid? orderId, string type, string mhStatus, string tag)
    {
        var control = $"DTE-{type}-{tag}-{Guid.NewGuid():N}";
        return new DteIssued
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            DteType = type,
            ControlNumber = control[..Math.Min(40, control.Length)],
            MhStatus = mhStatus,
            IssuedAt = Now.UtcDateTime,
            CreatedAt = Now.UtcDateTime
        };
    }

    /// <summary>Crea un evento de auditoría de reimpresión para una orden.</summary>
    /// <param name="orderId">Orden reimpresa.</param>
    /// <returns>Evento sin guardar.</returns>
    public AuditLog NewReprintAudit(Guid orderId)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint,
            TableName = SalesDomainConstants.SalesHistoryAuditActions.OrdersTableName,
            RecordId = orderId.ToString(),
            UserId = ManagerId,
            CreatedAt = Now.UtcDateTime
        };
    }

    private static async Task<Guid> ResolveEmployeeIdAsync(
        FerreteriaDbContext db,
        string dui,
        string expectedPosition,
        bool expectedCanCashier)
    {
        var employee = await db.Employees
            .AsNoTracking()
            .Include(item => item.Position)
            .SingleOrDefaultAsync(item => item.Dui == dui);
        if (employee is null
            || !string.Equals(employee.Position?.Name, expectedPosition, StringComparison.Ordinal)
            || employee.CanCashier != expectedCanCashier)
        {
            throw new InvalidOperationException($"No se encontró el empleado semilla esperado para DUI {dui}.");
        }

        return employee.Id;
    }

    /// <summary>Crea o recupera un segundo empleado activo con permiso de cajero.</summary>
    /// <param name="db">Contexto del contenedor de pruebas.</param>
    /// <returns>Identificador del segundo cajero.</returns>
    private static async Task<Guid> EnsureSecondCashierAsync(FerreteriaDbContext db)
    {
        var existing = await db.Employees.SingleOrDefaultAsync(item => item.Dui == "00000004-0");
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.CanCashier = true;
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var source = await db.Employees.SingleAsync(item => item.Dui == "00000002-0");
        var secondCashier = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Segundo",
            LastName = "Cajero",
            Dui = "00000004-0",
            PositionId = source.PositionId,
            DepartmentId = source.DepartmentId,
            HireDate = DateTime.UtcNow.Date,
            BaseSalary = 0m,
            ContractType = "PLAZO_FIJO",
            SalaryType = "MENSUAL",
            CanCashier = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Employees.Add(secondCashier);
        await db.SaveChangesAsync();
        return secondCashier.Id;
    }
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => value;
    }
}

/// <summary>Colección que comparte un único contenedor PostgreSQL entre las pruebas de integración.</summary>
[CollectionDefinition("PostgreSql")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}

/// <summary>Pruebas de integración del historial contra PostgreSQL desechable con el esquema real.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class SalesHistoryIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>Inicializa los casos con el fixture compartido.</summary>
    /// <param name="fixture">Contenedor y servicios compartidos.</param>
    /// <param name="output">Salida de xUnit para registrar mediciones.</param>
    public SalesHistoryIntegrationTests(PostgreSqlFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>Verifica que las propiedades persistentes del modelo EF existan en el esquema real.</summary>
    /// <remarks>
    /// Este test detecta divergencias entre el modelo EF y <c>Squema.sql</c> consultando
    /// las columnas creadas en el contenedor PostgreSQL. Las navegaciones y propiedades
    /// que no se mapean a una columna se ignoran deliberadamente.
    /// </remarks>
    [Fact]
    public async Task EfModelColumns_MatchSchemaColumns()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var mappedProperties = db.Model.GetEntityTypes()
            .SelectMany(entityType =>
            {
                var tableName = entityType.GetTableName();
                if (tableName is null)
                {
                    return Enumerable.Empty<(string Schema, string Table, string Entity, string Property, string Column)>();
                }

                var schemaName = entityType.GetSchema() ?? "public";
                var storeObject = StoreObjectIdentifier.Table(tableName, schemaName);
                return entityType.GetProperties()
                    .Select(property => new
                    {
                        EntityType = entityType,
                        Property = property,
                        ColumnName = property.GetColumnName(storeObject)
                    })
                    .Where(item => item.ColumnName is not null)
                    .Select(item =>
                        (Schema: schemaName,
                         Table: tableName,
                         Entity: item.EntityType.ClrType.Name,
                         Property: item.Property.Name,
                         Column: item.ColumnName ?? string.Empty));
            })
            .ToArray();

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        const string columnsSql = """
            SELECT table_schema, table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = ANY(@schemas);
            """;
        await using var command = new NpgsqlCommand(columnsSql, connection);
        command.Parameters.Add("schemas", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = new[]
        {
            "public",
            "purchasing",
            "sales",
            "dte",
            "hr",
            "system"
        };

        var existingColumns = new HashSet<(string Schema, string Table, string Column)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingColumns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        var missing = mappedProperties
            .Where(item => !existingColumns.Contains((item.Schema, item.Table, item.Column)))
            .OrderBy(item => item.Schema)
            .ThenBy(item => item.Table)
            .ThenBy(item => item.Column)
            .ThenBy(item => item.Entity)
            .ThenBy(item => item.Property)
            .ToArray();

        var details = string.Join(", ", missing.Select(item =>
            $"{item.Schema}.{item.Table}.{item.Column} ({item.Entity}.{item.Property})"));
        Assert.True(missing.Length == 0, $"Faltan columnas del modelo EF en el esquema: {details}");
    }

    /// <summary>Cada filtro por separado y combinado devuelve exactamente las órdenes esperadas.</summary>
    [Fact]
    public async Task SearchAsync_AppliesEachFilterAndCombinations()
    {
        var tag = "FILTRO" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var customer = new Customer { Id = Guid.NewGuid(), Name = $"Cliente {tag} Uno", Nit = "0614-271111-101-1", CustomerType = "CF" };
        var o1 = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 3, 10, 10), SalesDomainConstants.OrderStatuses.Completed);
        var o2 = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Local(2026, 3, 10, 11), SalesDomainConstants.OrderStatuses.Completed);
        var o3 = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 3, 10, 12), SalesDomainConstants.OrderStatuses.Cancelled);
        var o4 = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Local(2026, 3, 10, 13), SalesDomainConstants.OrderStatuses.Pending);
        o1.CustomerId = customer.Id;
        o3.CustomerId = customer.Id;
        var dte1 = PostgreSqlFixture.NewDte(o1.Id, "01", DteConstants.EstadosMh.Procesado, tag);
        var dte2 = PostgreSqlFixture.NewDte(o2.Id, "03", DteConstants.EstadosMh.Contingencia, "X");
        await _fixture.SeedAsync(db =>
        {
            db.Customers.Add(customer);
            db.Orders.AddRange(o1, o2, o3, o4);
            db.Payments.AddRange(
                new Payment { OrderId = o1.Id, Method = SalesDomainConstants.PaymentMethods.Cash, Amount = o1.Total },
                new Payment { OrderId = o2.Id, Method = SalesDomainConstants.PaymentMethods.Card, Amount = o2.Total },
                new Payment { OrderId = o3.Id, Method = SalesDomainConstants.PaymentMethods.Transfer, Amount = o3.Total },
                new Payment { OrderId = o4.Id, Method = SalesDomainConstants.PaymentMethods.Other, Amount = o4.Total });
            db.DteIssued.AddRange(dte1, dte2);
        });

        var (fromUtc, toUtc) = SalesHistoryFilter.CreateLocalDateRange(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10));
        var baseFilter = new SalesHistoryFilter(FromUtc: fromUtc, ToUtc: toUtc, Shortcut: SalesHistoryDateShortcut.None,
            OrderStatus: SalesDomainConstants.OrderStatuses.All, PageSize: 50);

        async Task<Guid[]> Ids(SalesHistoryFilter filter)
        {
            var page = await _fixture.History.SearchAsync(filter, _fixture.ManagerId);
            return page.Rows.Select(row => row.OrderId).OrderBy(id => id).ToArray();
        }

        Guid[] Expected(params Order[] orders) => orders.Select(order => order.Id).OrderBy(id => id).ToArray();

        Assert.Equal(Expected(o1, o2, o3, o4), await Ids(baseFilter));
        Assert.Equal(Expected(o1, o2, o3, o4), await Ids(baseFilter with { OrderStatus = null }));
        Assert.Equal(Expected(o1, o2), await Ids(baseFilter with { OrderStatus = SalesDomainConstants.OrderStatuses.Completed }));
        Assert.Equal(Expected(o3), await Ids(baseFilter with { OrderStatus = SalesDomainConstants.OrderStatuses.Cancelled }));
        Assert.Equal(Expected(o1, o3), await Ids(baseFilter with { SearchText = $"{tag.ToLowerInvariant()} uno" }));
        Assert.Equal(Expected(o1, o3), await Ids(baseFilter with { SearchText = "271111" }));
        Assert.Equal(Expected(o1), await Ids(baseFilter with { SearchText = dte1.ControlNumber[3..20] }));
        Assert.Equal(Expected(o4), await Ids(baseFilter with { SearchText = o4.Id.ToString()[..8].ToUpperInvariant() }));
        Assert.Equal(Expected(o4), await Ids(baseFilter with { SearchText = o4.Id.ToString() }));
        Assert.Empty(await Ids(baseFilter with { SearchText = "%" }));
        Assert.Equal(Expected(o1), await Ids(baseFilter with { DteType = "01" }));
        Assert.Equal(Expected(o3, o4), await Ids(baseFilter with { DteType = SalesHistoryFilter.NoDteFilterValue }));
        Assert.Equal(Expected(o2), await Ids(baseFilter with { MhStatus = DteConstants.EstadosMh.Contingencia }));
        Assert.Equal(Expected(o2), await Ids(baseFilter with { PaymentMethod = SalesDomainConstants.PaymentMethods.Card }));
        Assert.Equal(Expected(o2, o4), await Ids(baseFilter with { EmployeeId = _fixture.CashierId }));
        Assert.Equal(Expected(o2), await Ids(baseFilter with
        {
            OrderStatus = SalesDomainConstants.OrderStatuses.Completed,
            EmployeeId = _fixture.CashierId,
            PaymentMethod = SalesDomainConstants.PaymentMethods.Card
        }));
        Assert.Equal(Expected(o3), await Ids(baseFilter with
        {
            SearchText = tag,
            OrderStatus = SalesDomainConstants.OrderStatuses.Cancelled
        }));

        var rows = (await _fixture.History.SearchAsync(baseFilter, _fixture.ManagerId)).Rows;
        var row1 = Assert.Single(rows, row => row.OrderId == o1.Id);
        Assert.True(row1.HasDte);
        Assert.Equal(dte1.ControlNumber, row1.ControlNumberText);
        var row4 = Assert.Single(rows, row => row.OrderId == o4.Id);
        Assert.False(row4.HasDte);
        Assert.StartsWith("Sin DTE", row4.ControlNumberText);
    }

    /// <summary>El resumen del servicio coincide con la consulta SQL de control y no duplica totales con varios DTE.</summary>
    [Fact]
    public async Task SearchAsync_SummaryMatchesControlSql()
    {
        var a = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 4, 15, 9), SalesDomainConstants.OrderStatuses.Completed, 11.30m);
        var b = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 4, 15, 10), SalesDomainConstants.OrderStatuses.Completed, 22.60m);
        var c = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Local(2026, 4, 15, 23, 59), SalesDomainConstants.OrderStatuses.Completed, 5.65m);
        var cancelled = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 4, 15, 11), SalesDomainConstants.OrderStatuses.Cancelled, 100m);
        var nextDay = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 4, 16, 0, 1), SalesDomainConstants.OrderStatuses.Completed, 50m);
        await _fixture.SeedAsync(db =>
        {
            db.Orders.AddRange(a, b, c, cancelled, nextDay);
            db.DteIssued.AddRange(
                PostgreSqlFixture.NewDte(b.Id, "01", DteConstants.EstadosMh.Contingencia, "SUM"),
                PostgreSqlFixture.NewDte(b.Id, "01", DteConstants.EstadosMh.Procesado, "SUM"));
            db.AuditLogs.AddRange(
                _fixture.NewReprintAudit(a.Id),
                _fixture.NewReprintAudit(a.Id),
                _fixture.NewReprintAudit(cancelled.Id),
                _fixture.NewReprintAudit(nextDay.Id));
        });

        var (fromUtc, toUtc) = SalesHistoryFilter.CreateLocalDateRange(new DateOnly(2026, 4, 15), new DateOnly(2026, 4, 15));
        var page = await _fixture.History.SearchAsync(
            new SalesHistoryFilter(FromUtc: fromUtc, ToUtc: toUtc, Shortcut: SalesHistoryDateShortcut.None,
                OrderStatus: SalesDomainConstants.OrderStatuses.All),
            _fixture.ManagerId);

        const string controlSql = """
            WITH filtered_orders AS (
                SELECT o."id", o."status", o."total", o."TaxAmount"
                FROM sales."Orders" o
                WHERE o."CreatedAt" >= @fromUtc AND o."CreatedAt" < @toUtc
            ), dte_by_order AS (
                SELECT d."OrderId", bool_or(d."MhStatus" = 'CONTINGENCIA') AS has_contingency
                FROM dte."DteIssued" d
                GROUP BY d."OrderId"
            ), reprints_by_order AS (
                SELECT a."RecordId", COUNT(*) AS reprints
                FROM system."AuditLog" a
                WHERE a."action" = 'REIMPRIMIR' AND a."TableName" = 'sales.Orders'
                GROUP BY a."RecordId"
            )
            SELECT COUNT(*)::int AS ventas,
                   COALESCE(SUM(f."total") FILTER (WHERE f."status" = 'COMPLETADA'), 0) AS total,
                   COALESCE(SUM(f."TaxAmount") FILTER (WHERE f."status" = 'COMPLETADA'), 0) AS iva,
                   COUNT(*) FILTER (WHERE d.has_contingency)::int AS contingencias,
                   COALESCE(SUM(r.reprints), 0)::int AS reimpresiones
            FROM filtered_orders f
            LEFT JOIN dte_by_order d ON d."OrderId" = f."id"
            LEFT JOIN reprints_by_order r ON r."RecordId" = f."id"::text;
            """;
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(controlSql, connection);
        command.Parameters.AddWithValue("fromUtc", fromUtc);
        command.Parameters.AddWithValue("toUtc", toUtc);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var control = new SalesHistorySummary(reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetInt32(3), reader.GetInt32(4));

        Assert.Equal(control, page.Summary);
        Assert.Equal(new SalesHistorySummary(4, 39.55m, a.TaxAmount + b.TaxAmount + c.TaxAmount, 1, 3), page.Summary);
        Assert.Equal(2, page.Rows.Single(row => row.OrderId == a.Id).Reprints);
    }

    /// <summary>El servicio de auditoría real permite que el historial refleje una reimpresión en la fila y el resumen.</summary>
    [Fact]
    public async Task AuditService_ReprintIsVisibleInRowAndSummary()
    {
        var order = PostgreSqlFixture.NewOrder(
            _fixture.ManagerId,
            PostgreSqlFixture.Local(2026, 8, 8, 10),
            SalesDomainConstants.OrderStatuses.Completed);
        await _fixture.SeedAsync(db => db.Orders.Add(order));

        var audit = _fixture.Services.GetRequiredService<IAuditService>();
        await audit.RecordChangeAsync(
            SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint,
            SalesDomainConstants.SalesHistoryAuditActions.OrdersTableName,
            order.Id.ToString(),
            null,
            new
            {
                Evento = SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprintEvent,
                OrderId = order.Id,
                Resultado = "IMPRESO"
            },
            _fixture.ManagerId);

        var (fromUtc, toUtc) = SalesHistoryFilter.CreateLocalDateRange(new DateOnly(2026, 8, 8), new DateOnly(2026, 8, 8));
        var page = await _fixture.History.SearchAsync(
            new SalesHistoryFilter(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Shortcut: SalesHistoryDateShortcut.None,
                OrderStatus: SalesDomainConstants.OrderStatuses.All),
            _fixture.ManagerId);

        var row = Assert.Single(page.Rows);
        Assert.Equal(order.Id, row.OrderId);
        Assert.Equal(1, row.Reprints);
        Assert.Equal(1, page.Summary.Reprints);
    }

    /// <summary>Recorre todas las páginas sin repetir ni perder órdenes, incluso con fechas empatadas.</summary>
    [Fact]
    public async Task SearchAsync_PaginationIsStableWithTies()
    {
        var orders = Enumerable.Range(0, 23)
            .Select(index => PostgreSqlFixture.NewOrder(
                _fixture.ManagerId,
                PostgreSqlFixture.Local(2026, 5, 20, 8 + (index / 4)),
                SalesDomainConstants.OrderStatuses.Completed))
            .ToList();
        await _fixture.SeedAsync(db => db.Orders.AddRange(orders));

        var (fromUtc, toUtc) = SalesHistoryFilter.CreateLocalDateRange(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 20));
        var collected = new List<Guid>();
        var pageNumber = 1;
        SalesHistoryPage page;
        do
        {
            page = await _fixture.History.SearchAsync(
                new SalesHistoryFilter(FromUtc: fromUtc, ToUtc: toUtc, Shortcut: SalesHistoryDateShortcut.None,
                    OrderStatus: SalesDomainConstants.OrderStatuses.All, Page: pageNumber, PageSize: 5),
                _fixture.ManagerId);
            Assert.Equal(pageNumber > 1, page.HasPreviousPage);
            collected.AddRange(page.Rows.Select(row => row.OrderId));
            pageNumber++;
        }
        while (page.HasNextPage);

        Assert.Equal(5, pageNumber - 1);
        Assert.Equal(23, collected.Count);
        Assert.Equal(23, collected.Distinct().Count());

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT "id" FROM sales."Orders" WHERE "CreatedAt" >= @fromUtc AND "CreatedAt" < @toUtc ORDER BY "CreatedAt" DESC, "id" """,
            connection);
        command.Parameters.AddWithValue("fromUtc", fromUtc);
        command.Parameters.AddWithValue("toUtc", toUtc);
        var expected = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            expected.Add(reader.GetGuid(0));
        }

        Assert.Equal(expected, collected);
    }

    /// <summary>El cajero solo ve sus ventas de sesiones ABIERTAS; el administrador ve todas.</summary>
    [Fact]
    public async Task SearchAndDetail_RespectCashierAndManagerScope()
    {
        var tag = "ALCANCE" + Guid.NewGuid().ToString("N")[..6];
        var customer = new Customer { Id = Guid.NewGuid(), Name = $"Cliente {tag}", CustomerType = "CF" };
        var cashierToday = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Local(2026, 9, 27, 0, 5), SalesDomainConstants.OrderStatuses.Completed);
        var cashierYesterday = PostgreSqlFixture.NewOrder(_fixture.CashierId, PostgreSqlFixture.Local(2026, 9, 26, 23, 55), SalesDomainConstants.OrderStatuses.Completed);
        var managerToday = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 9, 27, 9), SalesDomainConstants.OrderStatuses.Completed);
        var openSession = new CashSession
        {
            Id = Guid.NewGuid(),
            EmployeeId = _fixture.CashierId,
            CashRegisterCode = "CAJA-HIST-" + Guid.NewGuid().ToString("N")[..8],
            OpenedAt = PostgreSqlFixture.Local(2026, 9, 27, 0),
            Status = SalesDomainConstants.CashSessionStatuses.Open
        };
        var closedSession = new CashSession
        {
            Id = Guid.NewGuid(),
            EmployeeId = _fixture.CashierId,
            CashRegisterCode = "CAJA-HIST-CERRADA-" + Guid.NewGuid().ToString("N")[..8],
            OpenedAt = PostgreSqlFixture.Local(2026, 9, 26, 0),
            ClosedAt = PostgreSqlFixture.Local(2026, 9, 26, 23),
            Status = SalesDomainConstants.CashSessionStatuses.Closed
        };
        cashierToday.CashSessionId = openSession.Id;
        cashierYesterday.CashSessionId = closedSession.Id;
        foreach (var order in new[] { cashierToday, cashierYesterday, managerToday })
        {
            order.CustomerId = customer.Id;
        }

        await _fixture.SeedAsync(db =>
        {
            db.Customers.Add(customer);
            db.CashSessions.AddRange(openSession, closedSession);
            db.Orders.AddRange(cashierToday, cashierYesterday, managerToday);
        });

        var (fromUtc, toUtc) = SalesHistoryFilter.CreateLocalDateRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        var filter = new SalesHistoryFilter(FromUtc: fromUtc, ToUtc: toUtc, Shortcut: SalesHistoryDateShortcut.None,
            SearchText: tag, OrderStatus: SalesDomainConstants.OrderStatuses.All);

        var cashierRows = (await _fixture.History.SearchAsync(filter, _fixture.CashierId)).Rows;
        var managerRows = (await _fixture.History.SearchAsync(filter, _fixture.ManagerId)).Rows;
        var nonCashierRows = (await _fixture.History.SearchAsync(filter, _fixture.NonCashierId)).Rows;
        var unknownPage = await _fixture.History.SearchAsync(filter, Guid.NewGuid());

        Assert.Equal(new[] { cashierToday.Id }, cashierRows.Select(row => row.OrderId));
        Assert.Equal(3, managerRows.Count);
        Assert.Empty(nonCashierRows);
        Assert.Empty(unknownPage.Rows);
        Assert.Equal(0, unknownPage.Summary.Sales);

        Assert.NotNull(await _fixture.History.GetDetailAsync(cashierToday.Id, _fixture.CashierId));
        Assert.Null(await _fixture.History.GetDetailAsync(managerToday.Id, _fixture.CashierId));
        Assert.Null(await _fixture.History.GetDetailAsync(cashierYesterday.Id, _fixture.CashierId));
        Assert.NotNull(await _fixture.History.GetDetailAsync(cashierYesterday.Id, _fixture.ManagerId));
        Assert.Null(await _fixture.History.GetDetailAsync(managerToday.Id, Guid.NewGuid()));
        Assert.True(await _fixture.History.CanAccessOrderAsync(cashierToday.Id, _fixture.CashierId));
        Assert.False(await _fixture.History.CanAccessOrderAsync(cashierYesterday.Id, _fixture.CashierId));
        Assert.False(await _fixture.History.CanAccessOrderAsync(managerToday.Id, _fixture.CashierId));
        Assert.True(await _fixture.History.CanAccessOrderAsync(managerToday.Id, _fixture.ManagerId));
    }

    /// <summary>El detalle trae líneas, pagos, DTE con NC relacionada, movimientos de inventario y notas.</summary>
    [Fact]
    public async Task GetDetailAsync_ReturnsLinesPaymentsDteCreditNotesMovementsAndNotes()
    {
        var productId = await _fixture.GetAnyProductIdAsync();
        var order = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 6, 1, 10), SalesDomainConstants.OrderStatuses.Completed);
        order.Notes = "Nota de prueba del detalle";
        var dte = PostgreSqlFixture.NewDte(order.Id, "01", DteConstants.EstadosMh.Procesado, "DET");
        dte.MhSello = "SELLO-PRUEBA";
        var creditNote = PostgreSqlFixture.NewDte(null, "05", DteConstants.EstadosMh.Pendiente, "NC");
        creditNote.RelatedDteId = dte.Id;
        await _fixture.SeedAsync(db =>
        {
            db.Orders.Add(order);
            db.OrderDetails.Add(new OrderDetail
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = 2m,
                UnitsPerPackage = 1m,
                UnitPrice = 5m,
                DiscountAmount = 0m,
                Subtotal = 10m
            });
            db.Payments.Add(new Payment { OrderId = order.Id, Method = SalesDomainConstants.PaymentMethods.Card, Amount = order.Total, Reference = "AUT-123" });
            db.DteIssued.AddRange(dte, creditNote);
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = productId,
                OrderId = order.Id,
                MovementType = SalesDomainConstants.InventoryMovementTypes.SaleOutflow,
                Quantity = 2m,
                StockBefore = 10m,
                StockAfter = 8m,
                Reason = "Venta de prueba"
            });
        });

        var detail = await _fixture.History.GetDetailAsync(order.Id, _fixture.ManagerId);

        Assert.NotNull(detail);
        var line = Assert.Single(detail.Lines);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(10m, line.Subtotal);
        Assert.Equal("AUT-123", Assert.Single(detail.Payments).Reference);
        var issued = Assert.Single(detail.Dtes);
        Assert.Equal(dte.ControlNumber, issued.ControlNumber);
        Assert.Equal(dte.GenerationCode, issued.GenerationCode);
        Assert.Equal("SELLO-PRUEBA", issued.Seal);
        Assert.Equal(creditNote.ControlNumber, Assert.Single(issued.CreditNotes).ControlNumber);
        Assert.Equal(SalesDomainConstants.InventoryMovementTypes.SaleOutflow, Assert.Single(detail.Movements).Type);
        Assert.Contains("Nota de prueba del detalle", detail.Notes);
        Assert.Equal(order.Total, detail.Total);
    }

    /// <summary>El incremento es atómico con DTE (también en paralelo) y no cambia nada sin DTE.</summary>
    [Fact]
    public async Task IncrementReprintsAsync_IsAtomicAndSafeWithoutDte()
    {
        var withDte = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 7, 1, 10), SalesDomainConstants.OrderStatuses.Completed);
        var withoutDte = PostgreSqlFixture.NewOrder(_fixture.ManagerId, PostgreSqlFixture.Local(2026, 7, 1, 11), SalesDomainConstants.OrderStatuses.Completed);
        var dte = PostgreSqlFixture.NewDte(withDte.Id, "01", DteConstants.EstadosMh.Procesado, "REP");
        await _fixture.SeedAsync(db =>
        {
            db.Orders.AddRange(withDte, withoutDte);
            db.DteIssued.Add(dte);
        });

        Assert.True(await _fixture.History.IncrementReprintsAsync(withDte.Id));
        var concurrent = await Task.WhenAll(
            _fixture.History.IncrementReprintsAsync(withDte.Id),
            _fixture.History.IncrementReprintsAsync(withDte.Id));
        Assert.All(concurrent, changed => Assert.True(changed));
        Assert.False(await _fixture.History.IncrementReprintsAsync(withoutDte.Id));

        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        Assert.Equal(3, await db.DteIssued.Where(item => item.Id == dte.Id).Select(item => item.Reprints).SingleAsync());
        Assert.False(await db.DteIssued.AnyAsync(item => item.OrderId == withoutDte.Id));
    }

    /// <summary>Con 10 000 órdenes semilla, la consulta paginada (con resumen) responde en menos de 1 s.</summary>
    [Fact]
    public async Task SearchAsync_With10000Orders_RespondsUnderOneSecond()
    {
        await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await connection.OpenAsync();
            var seedSql = $"""
                INSERT INTO sales."Orders" ("id", "EmployeeId", "OrderType", "ClientRequestId", "status",
                                            "subtotal", "TaxAmount", "DiscountAmount", "total", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), @employeeId, '{SalesDomainConstants.OrderTypes.CashRegisterSale}', gen_random_uuid(),
                       CASE WHEN g % 20 = 0 THEN '{SalesDomainConstants.OrderStatuses.Cancelled}' ELSE '{SalesDomainConstants.OrderStatuses.Completed}' END,
                       10, 1.30, 0, 11.30,
                       timestamptz '2025-01-01 06:00:00+00' + g * interval '4 minutes', now()
                FROM generate_series(1, 10000) AS g;
                INSERT INTO sales."Payments" ("OrderId", "method", "amount")
                SELECT o."id", CASE WHEN random() < 0.5 THEN '{SalesDomainConstants.PaymentMethods.Cash}' ELSE '{SalesDomainConstants.PaymentMethods.Card}' END, o."total"
                FROM sales."Orders" o
                WHERE o."CreatedAt" >= timestamptz '2025-01-01 06:00:00+00'
                  AND o."CreatedAt" < timestamptz '2025-02-01 06:00:00+00';
                ANALYZE;
                """;
            await using var command = new NpgsqlCommand(seedSql, connection);
            command.Parameters.AddWithValue("employeeId", _fixture.ManagerId);
            await command.ExecuteNonQueryAsync();
        }

        var (fromUtc, toUtc) = SalesHistoryFilter.CreateLocalDateRange(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));
        var filter = new SalesHistoryFilter(FromUtc: fromUtc, ToUtc: toUtc, Shortcut: SalesHistoryDateShortcut.None,
            OrderStatus: SalesDomainConstants.OrderStatuses.All, PageSize: SalesHistoryFilter.MaximumPageSize);

        var warmUp = await _fixture.History.SearchAsync(filter, _fixture.ManagerId);
        Assert.Equal(10000, warmUp.Summary.Sales);

        foreach (var pageNumber in new[] { 1, 100 })
        {
            var stopwatch = Stopwatch.StartNew();
            var page = await _fixture.History.SearchAsync(filter with { Page = pageNumber }, _fixture.ManagerId);
            stopwatch.Stop();
            _output.WriteLine($"Historial con 10 000 órdenes, página {pageNumber} (50 filas + resumen): {stopwatch.Elapsed.TotalMilliseconds:0} ms");
            Assert.Equal(SalesHistoryFilter.MaximumPageSize, page.Rows.Count);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"La página {pageNumber} tardó {stopwatch.Elapsed.TotalMilliseconds:0} ms (criterio: < 1000 ms).");
        }
    }
}
