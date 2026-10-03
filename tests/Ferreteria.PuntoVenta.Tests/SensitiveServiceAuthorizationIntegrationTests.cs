using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.CashRegister;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Verifica la matriz RBAC, la suplantación y la recarga de empleados desde PostgreSQL.</summary>
[Collection("PostgreSql")]
[Trait("Category", "Integration")]
public sealed class SensitiveServiceAuthorizationIntegrationTests(PostgreSqlFixture fixture)
{
    /// <summary>Operaciones de escritura protegidas por el guard.</summary>
    public static IEnumerable<object[]> SensitiveOperations => new[]
    {
        new object[] { "EmployeeCreate" }, new object[] { "EmployeeUpdate" }, new object[] { "EmployeeSetPin" },
        new object[] { "EmployeeDeactivate" }, new object[] { "ProductCreate" }, new object[] { "ProductUpdate" },
        new object[] { "ProductDeactivate" }, new object[] { "ProductReactivate" }, new object[] { "PrinterSave" },
        new object[] { "PrinterSetDefault" }, new object[] { "InventoryDecrease" }, new object[] { "InventoryEntry" },
        new object[] { "InventoryAdjustment" }, new object[] { "SupplierCreate" }, new object[] { "SupplierUpdate" },
        new object[] { "SupplierDeactivate" }, new object[] { "CustomerCreate" }, new object[] { "CustomerUpdate" },
        new object[] { "CustomerDeactivate" }, new object[] { "OrderCashSale" }, new object[] { "OrderConfection" },
        new object[] { "OrderCompleteConfection" }
    };

    /// <summary>Escenarios aplicables a operaciones que declaran empleado actuante.</summary>
    public static IEnumerable<object[]> ActingEmployeeScenarios =>
        SensitiveOperations.Where(operation => operation[0] is not "PrinterSave" and not "PrinterSetDefault").SelectMany(operation => new[]
        {
            new object[] { operation[0], "InsufficientPermission" }, new object[] { operation[0], "Authorized" },
            new object[] { operation[0], "SpoofedActingEmployee" }, new object[] { operation[0], "DeactivatedSessionEmployee" }
        });

    /// <summary>Operaciones de impresora, cuyo contrato no recibe un identificador actuante.</summary>
    public static IEnumerable<object[]> SessionOnlyScenarios => new[]
    {
        new object[] { "PrinterSave", "InsufficientPermission" }, new object[] { "PrinterSave", "Authorized" },
        new object[] { "PrinterSave", "DeactivatedSessionEmployee" }, new object[] { "PrinterSetDefault", "InsufficientPermission" },
        new object[] { "PrinterSetDefault", "Authorized" }, new object[] { "PrinterSetDefault", "DeactivatedSessionEmployee" }
    };

    /// <summary>Verifica autorización insuficiente, autorización, suplantación y empleado desactivado.</summary>
    /// <param name="operation">Operación protegida.</param>
    /// <param name="scenario">Escenario de autorización.</param>
    [Theory]
    [MemberData(nameof(ActingEmployeeScenarios))]
    public Task SensitiveOperation_ActingEmployeeMatrix(string operation, string scenario) => RunAuthorizationCaseAsync(operation, scenario);

    /// <summary>Verifica la autorización de impresoras con la identidad recargada desde la BD.</summary>
    /// <param name="operation">Operación de impresora.</param>
    /// <param name="scenario">Escenario de autorización.</param>
    [Theory]
    [MemberData(nameof(SessionOnlyScenarios))]
    public Task PrinterOperation_SessionMatrix(string operation, string scenario) => RunAuthorizationCaseAsync(operation, scenario);

    /// <summary>Impide desactivar o quitar el puesto del último administrador activo.</summary>
    [Fact]
    public async Task EmployeeService_LastActiveAdministrator_CannotBeRemoved()
    {
        await using var provider = BuildProvider(fixture.ManagerId, typeof(EmployeeService));
        var service = provider.GetRequiredService<EmployeeService>();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var manager = await db.Employees.AsNoTracking().SingleAsync(item => item.Id == fixture.ManagerId);
        var cashierPosition = await db.Positions.AsNoTracking().SingleAsync(item => item.Name == "Cajero");
        var input = new EmployeeInput(manager.FirstName, manager.LastName, manager.Dui, cashierPosition.Id, manager.DepartmentId,
            manager.HireDate, manager.BaseSalary, manager.ContractType, manager.SalaryType, manager.Phone, manager.Email,
            manager.CanCashier, manager.CanSell);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(manager.Id, input, fixture.ManagerId));
        await Assert.ThrowsAsync<ValidationException>(() => service.DeactivateAsync(manager.Id, fixture.ManagerId));

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var state = await verifyDb.Employees.AsNoTracking().Where(item => item.Id == fixture.ManagerId)
            .Select(item => new { item.IsActive, item.PositionId }).SingleAsync();
        Assert.True(state.IsActive);
        Assert.Equal(await verifyDb.Positions.Where(item => item.Name == "Administrador").Select(item => (Guid?)item.Id).SingleAsync(), state.PositionId);
    }

    /// <summary>Dejar sin puesto al único administrador activo se rechaza y la BD queda igual.</summary>
    [Fact]
    public async Task EmployeeService_LastActiveAdministrator_NullPosition_IsRejected()
    {
        await using var provider = BuildProvider(fixture.ManagerId, typeof(EmployeeService));
        var service = provider.GetRequiredService<EmployeeService>();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var manager = await db.Employees.AsNoTracking().SingleAsync(item => item.Id == fixture.ManagerId);
        var input = new EmployeeInput(manager.FirstName, manager.LastName, manager.Dui, null, manager.DepartmentId,
            manager.HireDate, manager.BaseSalary, manager.ContractType, manager.SalaryType, manager.Phone, manager.Email,
            manager.CanCashier, manager.CanSell);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(manager.Id, input, fixture.ManagerId));
        Assert.Contains("puesto", error.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var state = await verifyDb.Employees.AsNoTracking().Where(item => item.Id == fixture.ManagerId)
            .Select(item => new { item.IsActive, item.PositionId }).SingleAsync();
        Assert.True(state.IsActive);
        Assert.Equal(manager.PositionId, state.PositionId);
    }

    private async Task RunAuthorizationCaseAsync(string operation, string scenario)
    {
        var sessionEmployeeId = scenario == "InsufficientPermission" ? ResolveInsufficientEmployee(operation) : fixture.ManagerId;
        var actingEmployeeId = scenario == "SpoofedActingEmployee" ? fixture.CashierId : sessionEmployeeId;
        var probe = await PrepareOperationAsync(operation, actingEmployeeId);
        var before = await probe.Snapshot();
        try
        {
            if (scenario == "DeactivatedSessionEmployee") await SetEmployeeActiveAsync(fixture.ManagerId, false);
            await using var provider = BuildProvider(sessionEmployeeId, probe.ServiceType);
            if (scenario == "Authorized")
            {
                await probe.Execute(provider);
                Assert.NotEqual(before, await probe.Snapshot());
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedOperationException>(() => probe.Execute(provider));
                Assert.Equal(before, await probe.Snapshot());
            }
        }
        finally
        {
            if (scenario == "DeactivatedSessionEmployee") await SetEmployeeActiveAsync(fixture.ManagerId, true);
            if (probe.Cleanup is not null) await probe.Cleanup();
        }
    }

    private Guid ResolveInsufficientEmployee(string operation) => operation switch
    {
        "OrderCashSale" or "OrderCompleteConfection" or "CustomerCreate" or "CustomerUpdate" or "CustomerDeactivate" => fixture.NonCashierId,
        _ => fixture.CashierId
    };

    private async Task<AuthorizationProbe> PrepareOperationAsync(string operation, Guid actingEmployeeId) => operation switch
    {
        "EmployeeCreate" => await PrepareEmployeeCreateAsync(actingEmployeeId), "EmployeeUpdate" => await PrepareEmployeeUpdateAsync(actingEmployeeId),
        "EmployeeSetPin" => await PrepareEmployeeSetPinAsync(actingEmployeeId), "EmployeeDeactivate" => await PrepareEmployeeDeactivateAsync(actingEmployeeId),
        "ProductCreate" => await PrepareProductCreateAsync(actingEmployeeId), "ProductUpdate" => await PrepareProductUpdateAsync(actingEmployeeId),
        "ProductDeactivate" => await PrepareProductDeactivateAsync(actingEmployeeId), "ProductReactivate" => await PrepareProductReactivateAsync(actingEmployeeId),
        "PrinterSave" => await PreparePrinterSaveAsync(), "PrinterSetDefault" => await PreparePrinterDefaultAsync(),
        "InventoryDecrease" => await PrepareInventoryDecreaseAsync(actingEmployeeId), "InventoryEntry" => await PrepareInventoryEntryAsync(actingEmployeeId),
        "InventoryAdjustment" => await PrepareInventoryAdjustmentAsync(actingEmployeeId), "SupplierCreate" => await PrepareSupplierCreateAsync(actingEmployeeId),
        "SupplierUpdate" => await PrepareSupplierUpdateAsync(actingEmployeeId), "SupplierDeactivate" => await PrepareSupplierDeactivateAsync(actingEmployeeId),
        "CustomerCreate" => await PrepareCustomerCreateAsync(actingEmployeeId), "CustomerUpdate" => await PrepareCustomerUpdateAsync(actingEmployeeId),
        "CustomerDeactivate" => await PrepareCustomerDeactivateAsync(actingEmployeeId), "OrderCashSale" => await PrepareCashSaleAsync(actingEmployeeId),
        "OrderConfection" => await PrepareConfectionOrderAsync(actingEmployeeId), "OrderCompleteConfection" => await PrepareCompleteConfectionOrderAsync(actingEmployeeId),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private async Task<AuthorizationProbe> PrepareEmployeeCreateAsync(Guid employeeId)
    {
        var marker = $"QA-EMP-{Guid.NewGuid():N}";
        var input = new EmployeeInput("QA", marker, null, await LoadPositionIdAsync("Cajero"), null, DateTime.UtcNow.Date, 0m, "PLAZO_FIJO", "MENSUAL", null, null, false, false);
        return new(typeof(EmployeeService), p => p.GetRequiredService<EmployeeService>().CreateAsync(input, null, employeeId), () => CaptureAsync("employee-create", marker), () => DeleteEmployeesAsync(marker));
    }

    private async Task<AuthorizationProbe> PrepareEmployeeUpdateAsync(Guid employeeId)
    {
        var id = await CreateEmployeeAsync(); var employee = await LoadEmployeeAsync(id); var marker = $"QA-UPD-{Guid.NewGuid():N}";
        var input = new EmployeeInput(marker, employee.LastName, employee.Dui, employee.PositionId, employee.DepartmentId, employee.HireDate, employee.BaseSalary, employee.ContractType, employee.SalaryType, employee.Phone, employee.Email, employee.CanCashier, employee.CanSell);
        return new(typeof(EmployeeService), p => p.GetRequiredService<EmployeeService>().UpdateAsync(id, input, employeeId), () => CaptureAsync("employee", id), () => DeleteEmployeesAsync(id));
    }

    private async Task<AuthorizationProbe> PrepareEmployeeSetPinAsync(Guid employeeId)
    {
        var id = await CreateEmployeeAsync(); var pin = "2468";
        return new(typeof(EmployeeService), p => p.GetRequiredService<EmployeeService>().SetPinAsync(id, pin, employeeId), () => CaptureAsync("employee", id), () => DeleteEmployeesAsync(id));
    }

    private async Task<AuthorizationProbe> PrepareEmployeeDeactivateAsync(Guid employeeId)
    {
        var id = await CreateEmployeeAsync();
        return new(typeof(EmployeeService), p => p.GetRequiredService<EmployeeService>().DeactivateAsync(id, employeeId), () => CaptureAsync("employee", id), () => DeleteEmployeesAsync(id));
    }

    private async Task<AuthorizationProbe> PrepareProductCreateAsync(Guid employeeId)
    {
        var marker = $"QA-PROD-{Guid.NewGuid():N}"[..28]; var refs = await LoadCatalogReferencesAsync();
        var input = new ProductInput(marker, null, marker, refs.FamilyId, null, refs.MeasurementTypeId, null, 1m, .5m, 2m, 0m, null, null, null);
        return new(typeof(ProductCatalogService), p => p.GetRequiredService<ProductCatalogService>().CreateProductAsync(input, employeeId), () => CaptureAsync("product-create", marker), () => DeleteProductsAsync(marker));
    }

    private async Task<AuthorizationProbe> PrepareProductUpdateAsync(Guid employeeId)
    {
        var id = await CreateProductAsync(); var product = await LoadProductAsync(id);
        var input = new ProductInput(product.Code, product.Barcode, $"QA-{Guid.NewGuid():N}", product.FamilyId, product.SubfamilyId, product.MeasurementTypeId, product.SupplierId, product.SalePrice + 1m, product.CostPrice, product.CurrentStock, product.MinStock, product.MaxStock, product.ReorderPoint, product.Notes);
        return new(typeof(ProductCatalogService), p => p.GetRequiredService<ProductCatalogService>().UpdateProductAsync(id, input, employeeId), () => CaptureAsync("product", id), () => DeleteProductsAsync(product.Code));
    }

    private async Task<AuthorizationProbe> PrepareProductDeactivateAsync(Guid employeeId)
    {
        var id = await CreateProductAsync(); var product = await LoadProductAsync(id);
        return new(typeof(ProductCatalogService), p => p.GetRequiredService<ProductCatalogService>().DeactivateProductAsync(id, employeeId), () => CaptureAsync("product", id), () => DeleteProductsAsync(product.Code));
    }

    private async Task<AuthorizationProbe> PrepareProductReactivateAsync(Guid employeeId)
    {
        var id = await CreateProductAsync(); var product = await LoadProductAsync(id); await SetProductActiveAsync(id, false);
        return new(typeof(ProductCatalogService), p => p.GetRequiredService<ProductCatalogService>().ReactivateProductAsync(id, employeeId), () => CaptureAsync("product", id), () => DeleteProductsAsync(product.Code));
    }

    private async Task<AuthorizationProbe> PreparePrinterSaveAsync()
    {
        var name = $"QA-PRN-{Guid.NewGuid():N}"[..32]; var input = new PrinterInput(name, PrinterConfigurationRules.Usb, null, null, PrinterConfigurationRules.PaperWidth80, false);
        return new(typeof(PrinterConfigService), p => p.GetRequiredService<PrinterConfigService>().SaveAsync(input), () => CaptureAsync("printer", name), () => DeletePrintersAsync(name));
    }

    private async Task<AuthorizationProbe> PreparePrinterDefaultAsync()
    {
        var previousDefaultId = await LoadDefaultPrinterIdAsync();
        var name = $"QA-PRN-{Guid.NewGuid():N}"[..32]; var id = await CreatePrinterAsync(name);
        return new(typeof(PrinterConfigService), p => p.GetRequiredService<PrinterConfigService>().SetDefaultAsync(id), () => CaptureAsync("printer", name), () => DeletePrinterAndRestoreDefaultAsync(name, previousDefaultId));
    }

    private async Task<AuthorizationProbe> PrepareInventoryDecreaseAsync(Guid employeeId)
    {
        var id = await CreateProductAsync(10m); var orderId = await CreateInventoryOrderAsync(); var product = await LoadProductAsync(id);
        return new(typeof(InventoryService), p => p.GetRequiredService<InventoryService>().DecreaseStockAsync(id, 1m, orderId, employeeId, "QA"), () => CaptureAsync("inventory", id), () => DeleteProductAndOrderAsync(product.Code, orderId));
    }

    private async Task<AuthorizationProbe> PrepareInventoryEntryAsync(Guid employeeId)
    {
        var id = await CreateProductAsync(1m); var product = await LoadProductAsync(id);
        return new(typeof(InventoryService), p => p.GetRequiredService<InventoryService>().RegisterEntryAsync(id, 1m, 1m, employeeId, SalesDomainConstants.InventoryMovementTypes.PurchaseInflow, "QA"), () => CaptureAsync("inventory", id), () => DeleteProductsAsync(product.Code));
    }

    private async Task<AuthorizationProbe> PrepareInventoryAdjustmentAsync(Guid employeeId)
    {
        var id = await CreateProductAsync(1m); var product = await LoadProductAsync(id);
        return new(typeof(InventoryService), p => p.GetRequiredService<InventoryService>().RegisterAdjustmentAsync(id, 2m, employeeId, "QA ajuste"), () => CaptureAsync("inventory", id), () => DeleteProductsAsync(product.Code));
    }

    private async Task<AuthorizationProbe> PrepareSupplierCreateAsync(Guid employeeId)
    {
        var marker = $"QA-SUP-{Guid.NewGuid():N}"; var input = new SupplierInput(marker, null, null, null, null, null, null, null, null, null, 0, null);
        return new(typeof(SupplierService), p => p.GetRequiredService<SupplierService>().CreateAsync(input, employeeId), () => CaptureAsync("supplier-create", marker), () => DeleteSuppliersAsync(marker));
    }

    private async Task<AuthorizationProbe> PrepareSupplierUpdateAsync(Guid employeeId)
    {
        var marker = $"QA-SUP-{Guid.NewGuid():N}"; var id = await CreateSupplierAsync(marker); var input = new SupplierInput($"{marker}-UPD", null, null, null, null, null, null, null, null, null, 1, null);
        return new(typeof(SupplierService), p => p.GetRequiredService<SupplierService>().UpdateAsync(id, input, employeeId), () => CaptureAsync("supplier", id), () => DeleteSuppliersAsync(marker, $"{marker}-UPD"));
    }

    private async Task<AuthorizationProbe> PrepareSupplierDeactivateAsync(Guid employeeId)
    {
        var marker = $"QA-SUP-{Guid.NewGuid():N}"; var id = await CreateSupplierAsync(marker);
        return new(typeof(SupplierService), p => p.GetRequiredService<SupplierService>().DeactivateAsync(id, employeeId), () => CaptureAsync("supplier", id), () => DeleteSuppliersAsync(marker));
    }

    private async Task<AuthorizationProbe> PrepareCustomerCreateAsync(Guid employeeId)
    {
        var marker = $"QA-CUS-{Guid.NewGuid():N}"; var input = new CustomerInput("CF", marker, null, null, null, null, null, null, null, null);
        return new(typeof(CustomerService), p => p.GetRequiredService<CustomerService>().CreateAsync(input, employeeId), () => CaptureAsync("customer-create", marker), () => DeleteCustomersAsync(marker));
    }

    private async Task<AuthorizationProbe> PrepareCustomerUpdateAsync(Guid employeeId)
    {
        var marker = $"QA-CUS-{Guid.NewGuid():N}"; var id = await CreateCustomerAsync(marker); var input = new CustomerInput("CF", $"{marker}-UPD", null, null, null, null, null, null, null, null);
        return new(typeof(CustomerService), p => p.GetRequiredService<CustomerService>().UpdateAsync(id, input, employeeId), () => CaptureAsync("customer", id), () => DeleteCustomersAsync(marker, $"{marker}-UPD"));
    }

    private async Task<AuthorizationProbe> PrepareCustomerDeactivateAsync(Guid employeeId)
    {
        var marker = $"QA-CUS-{Guid.NewGuid():N}"; var id = await CreateCustomerAsync(marker);
        return new(typeof(CustomerService), p => p.GetRequiredService<CustomerService>().DeactivateAsync(id, employeeId), () => CaptureAsync("customer", id), () => DeleteCustomersAsync(marker));
    }

    private async Task<AuthorizationProbe> PrepareCashSaleAsync(Guid employeeId)
    {
        var productId = await CreateProductAsync(10m); var product = await LoadProductAsync(productId); var code = UniqueCashRegisterCode(); fixture.SetCashRegisterCode(code); var session = await fixture.CashSessions.OpenAsync(ResolveSessionOpener(employeeId), code, 0m, "QA");
        var request = new CreateCashSaleRequest(employeeId, session.Id, null, Guid.NewGuid(), new[] { new CashSaleLineRequest(productId, 1m) }, new[] { new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, TaxAmountCalculator.CalculateGrandTotal(product.SalePrice)) }, "QA");
        return new(typeof(OrderService), p => p.GetRequiredService<OrderService>().CreateCashSaleAsync(request), () => CaptureAsync("order", request.ClientRequestId!.Value, productId), async () => { await CloseSessionDirectlyAsync(session.Id); await DeleteOrderByClientRequestAsync(request.ClientRequestId!.Value); await DeleteProductsAsync(product.Code); });
    }

    private async Task<AuthorizationProbe> PrepareConfectionOrderAsync(Guid employeeId)
    {
        var productId = await CreateProductAsync(10m); var product = await LoadProductAsync(productId); var request = new CreateConfectionOrderRequest(employeeId, null, Guid.NewGuid(), "QA", null, new[] { new CashSaleLineRequest(productId, 1m) }, "QA");
        return new(typeof(OrderService), p => p.GetRequiredService<OrderService>().CreateConfectionOrderAsync(request), () => CaptureAsync("order", request.ClientRequestId!.Value, productId), async () => { await DeleteOrderByClientRequestAsync(request.ClientRequestId!.Value); await DeleteProductsAsync(product.Code); });
    }

    private async Task<AuthorizationProbe> PrepareCompleteConfectionOrderAsync(Guid employeeId)
    {
        var productId = await CreateProductAsync(10m); var product = await LoadProductAsync(productId); var workOrder = await fixture.Orders.CreateConfectionOrderAsync(new CreateConfectionOrderRequest(fixture.ManagerId, null, Guid.NewGuid(), "QA", null, new[] { new CashSaleLineRequest(productId, 1m) }, "QA"));
        var code = UniqueCashRegisterCode(); fixture.SetCashRegisterCode(code); var session = await fixture.CashSessions.OpenAsync(ResolveSessionOpener(employeeId), code, 0m, "QA");
        var request = new CompleteConfectionOrderRequest(workOrder.OrderId, employeeId, session.Id, new[] { new CashSalePaymentRequest(SalesDomainConstants.PaymentMethods.Cash, workOrder.Total) });
        return new(typeof(OrderService), p => p.GetRequiredService<OrderService>().CompleteConfectionOrderAsync(request), () => CaptureAsync("order-existing", workOrder.OrderId, productId), async () => { await CloseSessionDirectlyAsync(session.Id); await DeleteOrdersAsync(workOrder.OrderId); await DeleteProductsAsync(product.Code); });
    }

    /// <summary>La caja se abre con un empleado que puede operarla: el permiso bajo prueba es el del actuante en la venta, no el de la apertura.</summary>
    private Guid ResolveSessionOpener(Guid actingEmployeeId) => actingEmployeeId == fixture.NonCashierId ? fixture.CashierId : actingEmployeeId;

    private ServiceProvider BuildProvider(Guid sessionEmployeeId, Type serviceType)
    {
        var services = new ServiceCollection(); services.AddDbContext<FerreteriaDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<ICurrentSessionService>(new SessionDouble(sessionEmployeeId)); services.AddOptions<AuthorizationOptions>().Configure(options => options.PuestosAdministracion = new List<string> { "Administrador" });
        services.AddOptions<CashRegisterOptions>().Configure(options => options.Codigo = CurrentCashRegisterCode()); services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ILogger<AuthorizationGuard>>(_ => NullLogger<AuthorizationGuard>.Instance); services.AddSingleton<IAuthorizationGuard, AuthorizationGuard>(); services.AddSingleton<IAuditService, NoOpAuditService>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)); services.AddSingleton(serviceType); return services.BuildServiceProvider();
    }

    private async Task<DbState> CaptureAsync(string resource, object key, Guid? productId = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        return resource switch
        {
            "employee-create" => new(await db.Employees.CountAsync(), string.Join("|", await db.Employees.Where(item => item.LastName == (string)key).Select(item => item.Id + ":" + item.IsActive).ToListAsync())),
            "employee" => new(1, await db.Employees.Where(item => item.Id == (Guid)key).Select(item => item.Id + ":" + item.FirstName + ":" + item.IsActive + ":" + item.PinHash).SingleAsync()),
            "product-create" => new(await db.Products.CountAsync(), string.Join("|", await db.Products.Where(item => item.Code == (string)key).Select(item => item.Id + ":" + item.CurrentStock + ":" + item.IsActive).ToListAsync())),
            "product" => new(1, await db.Products.Where(item => item.Id == (Guid)key).Select(item => item.Code + ":" + item.SalePrice + ":" + item.IsActive + ":" + item.CurrentStock).SingleAsync()),
            "printer" => new(await db.Printers.CountAsync(), string.Join("|", await db.Printers.Where(item => item.Name == (string)key).Select(item => item.Id + ":" + item.IsDefault + ":" + item.IsActive).ToListAsync())),
            "inventory" => new(await db.InventoryMovements.CountAsync(item => item.ProductId == (Guid)key), await db.Products.Where(item => item.Id == (Guid)key).Select(item => item.CurrentStock + ":" + item.UpdatedAt).SingleAsync()),
            "supplier-create" => new(await db.Suppliers.CountAsync(), string.Join("|", await db.Suppliers.Where(item => item.Name == (string)key).Select(item => item.Id + ":" + item.IsActive).ToListAsync())),
            "supplier" => new(1, await db.Suppliers.Where(item => item.Id == (Guid)key).Select(item => item.Name + ":" + item.IsActive + ":" + item.CreditDays).SingleAsync()),
            "customer-create" => new(await db.Customers.CountAsync(), string.Join("|", await db.Customers.Where(item => item.Name == (string)key).Select(item => item.Id + ":" + item.IsActive).ToListAsync())),
            "customer" => new(1, await db.Customers.Where(item => item.Id == (Guid)key).Select(item => item.Name + ":" + item.IsActive).SingleAsync()),
            "order" or "order-existing" => await CaptureOrderAsync(db, (Guid)key, productId),
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
        };
    }

    private static async Task<DbState> CaptureOrderAsync(FerreteriaDbContext db, Guid key, Guid? productId)
    {
        var rows = await db.Orders.Where(item => item.Id == key || item.ClientRequestId == key).Select(item => item.Id + ":" + item.Status + ":" + item.ClientRequestId + ":" + item.CashSessionId).ToListAsync();
        var count = await db.Orders.CountAsync() + await db.OrderDetails.CountAsync() + await db.Payments.CountAsync();
        var stock = productId is null ? string.Empty : await db.Products.Where(item => item.Id == productId).Select(item => item.CurrentStock.ToString()).SingleAsync();
        return new(count, string.Join("|", rows) + ":" + stock);
    }

    private async Task<Guid> CreateEmployeeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var manager = await db.Employees.AsNoTracking().SingleAsync(item => item.Id == fixture.ManagerId);
        var employee = new Employee { Id = Guid.NewGuid(), FirstName = "QA", LastName = $"TMP-{Guid.NewGuid():N}", PositionId = manager.PositionId, DepartmentId = manager.DepartmentId, HireDate = DateTime.UtcNow.Date, ContractType = "PLAZO_FIJO", SalaryType = "MENSUAL", IsActive = true };
        db.Employees.Add(employee); await db.SaveChangesAsync(); return employee.Id;
    }

    private async Task<ProductReferences> LoadCatalogReferencesAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().Products.Select(item => new ProductReferences(item.FamilyId, item.MeasurementTypeId)).FirstAsync();
    }

    private async Task<Guid> CreateProductAsync(decimal stock = 2m)
    {
        var refs = await LoadCatalogReferencesAsync(); var code = $"QA-{Guid.NewGuid():N}"[..24]; await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var product = new Product { Id = Guid.NewGuid(), Code = code, Description = code, FamilyId = refs.FamilyId, MeasurementTypeId = refs.MeasurementTypeId, SalePrice = 1m, CostPrice = .5m, CurrentStock = stock, IsActive = true }; db.Products.Add(product); await db.SaveChangesAsync(); return product.Id;
    }

    private async Task<Product> LoadProductAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().Products.AsNoTracking().SingleAsync(item => item.Id == id);
    }

    private async Task<Guid> CreateSupplierAsync(string name)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var entity = new Supplier { Id = Guid.NewGuid(), Name = name, Country = "SV", IsActive = true }; db.Suppliers.Add(entity); await db.SaveChangesAsync(); return entity.Id;
    }

    private async Task<Guid> CreateCustomerAsync(string name)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var entity = new Customer { Id = Guid.NewGuid(), CustomerType = "CF", Name = name, IsActive = true }; db.Customers.Add(entity); await db.SaveChangesAsync(); return entity.Id;
    }

    private async Task<Guid> CreatePrinterAsync(string name)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var entity = new Printer { Id = Guid.NewGuid(), Name = name, ConnectionType = PrinterConfigurationRules.Usb, PaperWidth = 80, IsActive = true }; db.Printers.Add(entity); await db.SaveChangesAsync(); return entity.Id;
    }

    private async Task<Guid?> LoadDefaultPrinterIdAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().Printers
            .Where(item => item.IsDefault)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync();
    }

    private async Task<Guid> CreateInventoryOrderAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var order = new Order { Id = Guid.NewGuid(), EmployeeId = fixture.ManagerId, ClientRequestId = Guid.NewGuid(), OrderType = SalesDomainConstants.OrderTypes.CashRegisterSale, Status = SalesDomainConstants.OrderStatuses.Completed, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }; db.Orders.Add(order); await db.SaveChangesAsync(); return order.Id;
    }

    private async Task SetEmployeeActiveAsync(Guid id, bool value)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var employee = await db.Employees.SingleAsync(item => item.Id == id); employee.IsActive = value; await db.SaveChangesAsync();
    }

    private async Task SetProductActiveAsync(Guid id, bool value)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var product = await db.Products.SingleAsync(item => item.Id == id); product.IsActive = value; await db.SaveChangesAsync();
    }

    private async Task<Guid> LoadPositionIdAsync(string name)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().Positions.Where(item => item.Name == name).Select(item => item.Id).SingleAsync();
    }

    private async Task<Employee> LoadEmployeeAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>().Employees.AsNoTracking().SingleAsync(item => item.Id == id);
    }

    private async Task DeleteEmployeesAsync(params object[] keys)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var ids = keys.OfType<Guid>().ToArray(); var markers = keys.OfType<string>().ToArray(); var rows = await db.Employees.Where(item => ids.Contains(item.Id) || markers.Contains(item.LastName)).ToListAsync(); db.Employees.RemoveRange(rows); await db.SaveChangesAsync();
    }

    private async Task DeleteProductsAsync(params string[] codes)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var rows = await db.Products.Where(item => codes.Contains(item.Code)).ToListAsync(); var ids = rows.Select(item => item.Id).ToArray(); db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => ids.Contains(item.ProductId)).ToListAsync()); db.StockAlerts.RemoveRange(await db.StockAlerts.Where(item => ids.Contains(item.ProductId)).ToListAsync()); db.Products.RemoveRange(rows); await db.SaveChangesAsync();
    }

    private async Task DeleteProductAndOrderAsync(string code, Guid orderId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => item.OrderId == orderId).ToListAsync());
        var order = await db.Orders.SingleOrDefaultAsync(item => item.Id == orderId);
        if (order is not null) db.Orders.Remove(order);
        await db.SaveChangesAsync();
        await DeleteProductsAsync(code);
    }

    private async Task DeleteOrdersAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var row = await db.Orders.SingleOrDefaultAsync(item => item.Id == id); if (row is not null) { db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => item.OrderId == id).ToListAsync()); db.Orders.Remove(row); await db.SaveChangesAsync(); }
    }

    private async Task DeleteOrderByClientRequestAsync(Guid clientRequestId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        var row = await db.Orders.SingleOrDefaultAsync(item => item.ClientRequestId == clientRequestId);
        if (row is not null)
        {
            db.InventoryMovements.RemoveRange(await db.InventoryMovements.Where(item => item.OrderId == row.Id).ToListAsync());
            db.Orders.Remove(row);
            await db.SaveChangesAsync();
        }
    }

    private async Task DeleteSuppliersAsync(params string[] names)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var rows = await db.Suppliers.Where(item => names.Contains(item.Name)).ToListAsync(); db.Suppliers.RemoveRange(rows); await db.SaveChangesAsync();
    }

    private async Task DeleteCustomersAsync(params string[] names)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var rows = await db.Customers.Where(item => names.Contains(item.Name)).ToListAsync(); db.Customers.RemoveRange(rows); await db.SaveChangesAsync();
    }

    private async Task DeletePrintersAsync(string name)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var rows = await db.Printers.Where(item => item.Name == name).ToListAsync(); db.Printers.RemoveRange(rows); await db.SaveChangesAsync();
    }

    private async Task DeletePrinterAndRestoreDefaultAsync(string name, Guid? previousDefaultId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();
        db.Printers.RemoveRange(await db.Printers.Where(item => item.Name == name).ToListAsync());
        await db.SaveChangesAsync();
        if (previousDefaultId is Guid defaultId)
        {
            var previousDefault = await db.Printers.SingleOrDefaultAsync(item => item.Id == defaultId);
            if (previousDefault is not null)
            {
                previousDefault.IsDefault = true;
                await db.SaveChangesAsync();
            }
        }
    }

    private async Task CloseSessionDirectlyAsync(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>(); var session = await db.CashSessions.SingleOrDefaultAsync(item => item.Id == id); if (session is not null) { session.Status = SalesDomainConstants.CashSessionStatuses.Closed; session.ClosedAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
    }

    private string CurrentCashRegisterCode() => fixture.Services.GetRequiredService<IOptions<CashRegisterOptions>>().Value.Codigo;
    private static string UniqueCashRegisterCode() => $"QA-{Guid.NewGuid():N}"[..20];

    private sealed record AuthorizationProbe(Type ServiceType, Func<ServiceProvider, Task> Execute, Func<Task<DbState>> Snapshot, Func<Task>? Cleanup);
    private sealed record DbState(long Count, string Values);
    private sealed record ProductReferences(Guid FamilyId, Guid MeasurementTypeId);

    private sealed class SessionDouble(Guid employeeId) : ICurrentSessionService
    {
        public Employee? CurrentEmployee { get; private set; } = new() { Id = employeeId };
        public OperationalModule? ActiveModule => OperationalModule.Caja;
        public string? CurrentModule => ActiveModule?.ToString();
        public DateTime? StartedAtUtc => DateTime.UtcNow;
        public Guid? ActiveCashSessionId => null;
        public bool IsActive => true;
        public void StartSession(Employee employee, OperationalModule module, string initialSection) => CurrentEmployee = employee;
        public void SetActiveCashSession(Guid cashSessionId) { }
        public void ClearActiveCashSession() { }
        public string ResolveInitialSection() => NavSections.Productos;
        public void EndSession() => CurrentEmployee = null;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordChangeAsync(string action, string tableName, string recordId, object? oldData, object? newData, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
