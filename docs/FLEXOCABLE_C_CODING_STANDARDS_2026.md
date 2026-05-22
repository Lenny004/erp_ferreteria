# FlexoCable SV — Estándares de Codificación C# 2026

> **Documento:** FLEXO-DEV-STD-001  
> **Versión:** 1.0  
> **Fecha:** Mayo 2026  
> **Responsable:** Equipo de Desarrollo  
> **Última actualización:** 2026-05-22

**Documentos relacionados:** [GIT, PR y revisión](FLEXOCABLE_GIT_PR_2026.md) (FLEXO-DEV-STD-002) · [Índice de docs](README.md) · Skill Cursor: `.cursor/skills/flexocable-dev/`

---

## 📋 Índice

1. [Autoridades y Estándares Normalizados](#autoridades-y-estándares-normalizados)
2. [Convenciones de Nomenclatura](#convenciones-de-nomenclatura)
3. [Formato y Estructura de Código](#formato-y-estructura-de-código)
4. [Modelos y Entidades EF Core](#modelos-y-entidades-ef-core)
5. [Patrones de Servicios](#patrones-de-servicios)
6. [Validación y Manejo de Errores](#validación-y-manejo-de-errores)
7. [Async/Await y Concurrencia](#asyncawait-y-concurrencia)
8. [Herramientas de Enforcing](#herramientas-de-enforcing)
9. [Checklist de Calidad](#checklist-de-calidad)
10. [Ejemplos Aplicados a FlexoCable](#ejemplos-aplicados-a-flexocable)
11. [Git, PR y revisión](#git-pr-y-revisión)

---

## 📚 Autoridades y Estándares Normalizados

En 2026, **no existe una entidad única reguladora obligatoria** para C#, pero hay autoridades reconocidas internacionalmente que definen estándares de facto:

| Autoridad | Tipo | Descripción | Enlace |
|---|---|---|---|
| **Microsoft .NET Team** | Oficial | Convenciones oficiales C# + Framework Design Guidelines | `learn.microsoft.com/csharp` |
| **ECMA-334** | Estándar ISO | Especificación del lenguaje C# aprobada internacionalmente (2006, actualizada regularmente) | `www.ecma-international.org` |
| **Google C# Style Guide** | Corporativo | Estándares internos de Google, adoptados en muchas empresas | `google.github.io/styleguide/csharp-style.html` |
| **C# Coding Guidelines** | Comunitario | Juval Löwy (IDesign), 240+ guidelines verificables con herramientas | `csharpcodingguidelines.com` |
| **.NET Core Runtime Team** | Oficial | Estándares usados en los repositorios oficiales de Microsoft | `github.com/dotnet/runtime` |
| **StyleCop / Roslynator** | Herramientas | Analizadores estáticos que implementan las reglas anteriores | Nuget packages |

---

### Jerarquía de Adoptabilidad para FlexoCable

```
┌─────────────────────────────────────────────────┐
│ 1. Microsoft Conventions (Obligatorio)           │ ← Base legal/técnica
├─────────────────────────────────────────────────┤
│ 2. C# Coding Guidelines by Juval Löwy (Adoptar) │ ← Prácticas probadas +20 años
├─────────────────────────────────────────────────┤
│ 3. .NET Runtime Team practices (Inspiración)    │ ← Code en repos oficiales
├─────────────────────────────────────────────────┤
│ 4. Estándares internos FlexoCable (Aplicar)     │ ← Este documento
└─────────────────────────────────────────────────┘
```

---

## 🏷️ Convenciones de Nomenclatura

### Regla 1: Casing y Convenciones Oficiales

**Regla:** Las siguientes reglas son de Microsoft y están documentadas en `learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`

| Elemento | Convención | Ejemplo | ¿Por qué? |
|---|---|---|---|
| **Clases** | `PascalCase` | `public class EmployeePayroll` | Estándar Microsoft |
| **Métodos** | `PascalCase` | `public void CalculateSalary()` | Estándar Microsoft |
| **Propiedades** | `PascalCase` | `public string FirstName { get; set; }` | Estándar Microsoft |
| **Interfaces** | `IPascalCase` | `public interface IEmployeeService` | ECMA-334 + Microsoft |
| **Enums** | `PascalCase` (sing.) | `public enum PaymentMethod` | Estándar Microsoft |
| **Enum values** | `PascalCase` | `PaymentMethod.BankTransfer` | Estándar Microsoft |
| **Variables locales** | `camelCase` | `var employeeCount = 0;` | Estándar Microsoft |
| **Parámetros métodos** | `camelCase` | `public void Process(string firstName)` | Estándar Microsoft |
| **Constantes** | `PascalCase` | `public const decimal IVA_RATE = 0.13m;` | Microsoft recomienda |
| **Private fields** | `_camelCase` | `private string _firstName;` | Moderna (mejor que prefijo m_) |
| **Static fields** | `_camelCase` (private) o `PascalCase` (public) | `private static DateTime _lastSync;` | Consistencia |

**❌ NUNCA hacer:**
```csharp
// ❌ Notación húngara (obsoleta desde 2010)
public class Employee {
    private string strFirstName;  // ❌ NO
    private int intAge;           // ❌ NO
    private bool bIsActive;       // ❌ NO
}

// ❌ Abreviaturas
public class Emp { }                    // ❌ NO — usar Employee
public void CalcSal() { }               // ❌ NO — usar CalculateSalary
public string FN { get; set; }          // ❌ NO — usar FirstName

// ❌ Todos mayúsculas (solo para constantes muy antiguas)
private string FIRSTNAME;               // ❌ NO — usar firstName o _firstName
```

**✅ CORRECTO:**
```csharp
public class Employee
{
    private string _firstName;          // ✅ Private field
    public string FirstName { get; set; }  // ✅ Property
    
    public const decimal IVA_RATE = 0.13m;  // ✅ Constant
    
    public void CalculateSalary()       // ✅ Method
    {
        var employeeCount = 0;          // ✅ Local variable
    }
}

public interface IEmployeeService      // ✅ Interface
{
    void ProcessPayroll(Employee emp);  // ✅ Parameter
}

public enum PaymentMethod              // ✅ Enum (singular)
{
    Cash,                               // ✅ Enum value
    BankTransfer,
    Check
}
```

---

### Regla 2: Nombres Claros y Sin Abreviaturas

**Estándar:** Microsoft Framework Design Guidelines + C# Coding Guidelines

**Principio:** El código se lee 10 veces más de lo que se escribe.

| ❌ Evitar | ✅ Usar | Motivo |
|---|---|---|
| `emp`, `addr`, `calc` | `employee`, `address`, `calculate` | Claridad > brevedad |
| `GetInfo()` | `GetEmployeeDetails()` | Sé específico |
| `var x = obj.M();` | `var salary = employee.CalculateSalary();` | Auto-documentación |
| `db.SaveChanges()` en lógica | `await _dbContext.SaveChangesAsync()` | Claridad de async |

**Excepción permitida:** Variables de bucle muy cortas
```csharp
// ✅ Permitido en bucles simples
for (int i = 0; i < count; i++) { }

// ✅ Permitido en LINQ
var results = items.Select((x, index) => x.Value * index);

// ❌ NO permitido en lógica compleja
foreach (var x in employees)
{
    x.ProcessPayroll();  // ¿Qué es x?
}

// ✅ CORRECTO
foreach (var employee in employees)
{
    employee.ProcessPayroll();
}
```

---

### Regla 3: Nombres de Métodos por Verbo (Semántica)

**Estándar:** Microsoft Framework Design Guidelines

El verbo debe reflejar **exactamente** lo que hace el método:

| Verbo | Cuándo usar | Ejemplo |
|---|---|---|
| `Get` | Retorna una propiedad sin efectos secundarios | `GetEmployeeName()` |
| `Set` | Asigna un valor | `SetSalary(decimal amount)` |
| `Fetch` | Obtiene datos de BD (síncrono) | `FetchProductsByFamily()` |
| `Retrieve` | Obtiene datos remotos | `RetrieveFromMH()` → DTE |
| `Calculate` | Realiza cálculos | `CalculateNetSalary()` |
| `Validate` | Valida sin efectos secundarios | `ValidatePin(string pin)` |
| `Process` | Ejecuta lógica compleja | `ProcessPayroll()` |
| `Create` / `Make` | Instancia objetos | `CreateOrder()` |
| `Delete` / `Remove` | Elimina | `DeleteOrder()` |
| `Update` / `Modify` | Cambia estado | `UpdateEmployeeSalary()` |
| `Is` / `Has` | Predicados booleanos | `IsActive()`, `HasPermission()` |

```csharp
// ❌ MALO
public void Order() { }                 // ¿Qué hace? ¿Crear? ¿Procesar?
public bool Check() { }                 // ¿Qué valida?
public void Data() { }                  // ¿Qué datos?

// ✅ CORRECTO
public Order CreateOrder() { }
public bool ValidatePin(string pin) { }
public async Task<List<Product>> FetchProductDataAsync() { }
```

---

## 📝 Formato y Estructura de Código

### Regla 4: Indentación y Llaves

**Estándar:** Microsoft Conventions + .NET Runtime Team

```csharp
// ✅ CORRECTO: Allman style (llave en nueva línea)
public class Employee
{
    private string _firstName;
    
    public void CalculateSalary()
    {
        if (_firstName != null)
        {
            // lógica
        }
        else
        {
            // lógica alternativa
        }
    }
}

// ❌ NO PERMITIDO en FlexoCable: K&R style
public class Employee {
    public void CalculateSalary() {
        if (_firstName != null) { }
    }
}
```

**Indentación:**
- **4 espacios** por nivel (nunca tabs)
- Máximo **120 caracteres por línea** (120 es el estándar moderno)

```csharp
// ❌ Demasiado largo (>120 chars)
public class EmployeePayrollCalculator
{
    public decimal CalculateNetSalaryAfterDeductionsAndTaxesConsideringInsuranceAndBenefits(Employee employee) { }
}

// ✅ CORRECTO: Rompe línea correctamente
public class EmployeePayrollCalculator
{
    public decimal CalculateNetSalaryAfterDeductions(
        Employee employee,
        InsurancePolicy policy)
    {
        // ...
    }
}
```

---

### Regla 5: Organización de Miembros de Clase

**Estándar:** Microsoft + IDesign Coding Guidelines (orden óptima de lectura)

```csharp
public class Employee
{
    // 1. Constantes (campos static const)
    public const decimal MONTHLY_WORKING_HOURS = 160m;
    private const string PIN_HASH_PREFIX = "bcrypt_";
    
    // 2. Variables static (fields)
    private static DateTime _lastSyncTime;
    
    // 3. Variables de instancia (fields privados)
    private string _firstName;
    private string _lastName;
    
    // 4. Propiedades
    public string FirstName
    {
        get { return _firstName; }
        set { _firstName = value; }
    }
    
    public string FullName => $"{_firstName} {_lastName}";  // ✅ Expression-bodied
    
    // 5. Constructor(es)
    public Employee(string firstName, string lastName)
    {
        _firstName = firstName;
        _lastName = lastName;
    }
    
    // 6. Métodos públicos (por orden lógico, no alfabético)
    public void CalculateSalary() { }
    public void ApplyDeductions() { }
    public bool ValidatePin(string pin) { }
    
    // 7. Métodos privados (helpers)
    private decimal GetBaseSalary() { }
    private decimal CalculateIsss() { }
}
```

---

### Regla 6: Espacios en Blanco Semánticos

**Estándar:** Microsoft Conventions

```csharp
public class PayrollService
{
    private readonly IEmployeeRepository _employeeRepo;
    
    // ✅ Separator entre secciones lógicas
    public PayrollService(IEmployeeRepository employeeRepo)
    {
        _employeeRepo = employeeRepo;
    }

    // ✅ Una línea en blanco entre métodos
    public async Task<PayrollSummary> CalculateMonthlyPayrollAsync(
        int month,
        int year)
    {
        var employees = await _employeeRepo.GetAllActiveAsync();
        
        // ✅ Línea en blanco antes de return
        return new PayrollSummary { /* ... */ };
    }

    // ✅ Una línea en blanco antes del siguiente método
    public void ProcessPayment(PayrollDetail detail)
    {
        // ...
    }
}
```

**NUNCA:**
```csharp
// ❌ Líneas en blanco innecesarias
public void Foo()
{


    var x = 1;


}

// ❌ Ningún espaciado
public void Bar(){var x=1;if(x==1){}}
```

---

## 🛢️ Modelos y Entidades EF Core

### Regla 7: Estructura de Entidades (Data Annotations + Fluent API)

**Estándar:** Microsoft EF Core Best Practices + Entity Framework Core 8.0

En FlexoCable, el esquema PostgreSQL es la **fuente de verdad**. Los modelos deben reflejar exactamente la BD:

```csharp
// ✅ CORRECTO: Completo con validaciones
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models
{
    /// <summary>
    /// Representa un empleado con acceso a sistema POS.
    /// Mapea tabla: hr.employees
    /// </summary>
    [Table("employees", Schema = "hr")]
    public class Employee
    {
        // 1. Clave primaria
        [Key]
        public int Id { get; set; }
        
        // 2. Datos de identidad
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }
        
        [Required]
        [StringLength(100)]
        public string LastName { get; set; }
        
        [StringLength(15)]
        public string DUI { get; set; }
        
        [StringLength(20)]
        public string NIT { get; set; }
        
        // 3. Relaciones
        [ForeignKey(nameof(Position))]
        public int? PositionId { get; set; }
        
        [ForeignKey(nameof(Department))]
        public int? DepartmentId { get; set; }
        
        public virtual Position Position { get; set; }
        public virtual Department Department { get; set; }
        
        // 4. Datos operativos
        [Required]
        [Column(TypeName = "date")]
        public DateTime HireDate { get; set; }
        
        [Column(TypeName = "numeric(10,2)")]
        public decimal BaseSalary { get; set; }
        
        // 5. Datos de seguridad
        public string PinHash { get; set; }  // bcrypt(PIN, cost=12)
        
        [Required]
        public bool CanSell { get; set; } = false;
        
        [Required]
        public bool CanCashier { get; set; } = false;
        
        // 6. Auditoría
        [Required]
        public bool IsActive { get; set; } = true;
        
        [Column(TypeName = "timestamptz")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Column(TypeName = "timestamptz")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // 7. Propiedades calculadas (NO se mapean a BD)
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
        
        /// <summary>
        /// Verifica si el empleado puede operar caja.
        /// </summary>
        [NotMapped]
        public bool CanOperatePOS => CanSell || CanCashier;
    }
}
```

---

### Regla 8: Configuración con Fluent API

**En DbContext, nunca uses Data Annotations para relaciones complejas:**

```csharp
// ✅ CORRECTO: Fluent API en OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Employee → Position
    modelBuilder.Entity<Employee>()
        .HasOne(e => e.Position)
        .WithMany(p => p.Employees)
        .HasForeignKey(e => e.PositionId)
        .OnDelete(DeleteBehavior.SetNull);
    
    // Order → OrderDetails (cascada)
    modelBuilder.Entity<Order>()
        .HasMany(o => o.Details)
        .WithOne(od => od.Order)
        .HasForeignKey(od => od.OrderId)
        .OnDelete(DeleteBehavior.Cascade);
    
    // Índices
    modelBuilder.Entity<Product>()
        .HasIndex(p => p.Code)
        .IsUnique();
    
    // Check constraints
    modelBuilder.Entity<PayrollDetail>()
        .HasCheckConstraint("ck_monthly_valid", "period_month BETWEEN 1 AND 12");
}
```

---

## 🧩 Patrones de Servicios

### Regla 9: Repository Pattern + Dependency Injection

**Estándar:** Microsoft ASP.NET Core Best Practices + EF Core

En FlexoCable, **SIEMPRE** usar inyección de dependencias:

```csharp
// ✅ CORRECTO: Interfaz y servicio desacoplados

// 1. Interfaz del repositorio
public interface IEmployeeRepository
{
    Task<Employee> GetByIdAsync(int id);
    Task<List<Employee>> GetAllActiveAsync();
    Task<Employee> GetByPinHashAsync(string pinHash);
    Task AddAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task DeleteAsync(int id);
}

// 2. Implementación concreta
public class EmployeeRepository : IEmployeeRepository
{
    private readonly FlexoDbContext _dbContext;
    
    public EmployeeRepository(FlexoDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }
    
    public async Task<Employee> GetByIdAsync(int id)
    {
        return await _dbContext.Employees
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == id);
    }
    
    public async Task<List<Employee>> GetAllActiveAsync()
    {
        return await _dbContext.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.FirstName)
            .ToListAsync();
    }
}

// 3. Servicio de lógica de negocio
public interface IPayrollService
{
    Task<PayrollSummary> CalculateMonthlyAsync(int month, int year);
    Task<PayrollDetail> CalculateEmployeePayrollAsync(Employee employee, int month, int year);
}

public class PayrollService : IPayrollService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly PayrollCalculator _calculator;
    private readonly ILogger<PayrollService> _logger;
    
    public PayrollService(
        IEmployeeRepository employeeRepository,
        PayrollCalculator calculator,
        ILogger<PayrollService> logger)
    {
        _employeeRepository = employeeRepository ?? 
            throw new ArgumentNullException(nameof(employeeRepository));
        _calculator = calculator ?? 
            throw new ArgumentNullException(nameof(calculator));
        _logger = logger ?? 
            throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<PayrollSummary> CalculateMonthlyAsync(int month, int year)
    {
        try
        {
            var employees = await _employeeRepository.GetAllActiveAsync();
            var summary = new PayrollSummary();
            
            foreach (var employee in employees)
            {
                var detail = await CalculateEmployeePayrollAsync(employee, month, year);
                summary.AddDetail(detail);
            }
            
            _logger.LogInformation(
                "Payroll calculated for {Month}/{Year}: {EmployeeCount} employees",
                month, year, employees.Count);
            
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating payroll for {Month}/{Year}", month, year);
            throw;
        }
    }
    
    public async Task<PayrollDetail> CalculateEmployeePayrollAsync(
        Employee employee,
        int month,
        int year)
    {
        var detail = new PayrollDetail
        {
            EmployeeId = employee.Id,
            BaseSalary = employee.BaseSalary,
            Month = month,
            Year = year
        };
        
        detail.IsssEmployee = _calculator.CalculateIsss(detail.BaseSalary);
        detail.AfpEmployee = _calculator.CalculateAfp(detail.BaseSalary);
        detail.NetSalary = _calculator.CalculateNetSalary(detail);
        
        return detail;
    }
}

// 4. Registro en Program.cs (Dependency Injection Container)
var builder = WebApplicationBuilder.CreateBuilder(args);

builder.Services.AddDbContext<FlexoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FlexoCableDB")));

builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<PayrollCalculator>();

var app = builder.Build();
// ...
```

---

## ✅ Validación y Manejo de Errores

### Regla 10: Guard Clauses y Validación Temprana

**Estándar:** IDesign C# Coding Guidelines + Microsoft Best Practices

```csharp
// ❌ MALO: Validación anidada
public void ProcessOrder(Order order)
{
    if (order != null)
    {
        if (order.Items.Count > 0)
        {
            if (order.Total > 0)
            {
                // Lógica real (36 espacios de indentación)
            }
        }
    }
}

// ✅ CORRECTO: Guard clauses (fail-fast)
public void ProcessOrder(Order order)
{
    if (order == null)
        throw new ArgumentNullException(nameof(order));
    
    if (order.Items.Count == 0)
        throw new InvalidOperationException("Order must contain at least one item.");
    
    if (order.Total <= 0)
        throw new InvalidOperationException("Order total must be positive.");
    
    // Lógica real (solo 4 espacios de indentación)
}
```

---

### Regla 11: Excepciones Específicas y Personalizadas

**Estándar:** Microsoft Framework Design Guidelines

```csharp
// ❌ MALO
catch (Exception ex)
{
    throw new Exception("Something went wrong");
}

// ✅ CORRECTO
public class PinValidationException : Exception
{
    public PinValidationException(string message) : base(message) { }
    public PinValidationException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class PinService
{
    public bool ValidatePin(Employee employee, string inputPin)
    {
        if (employee == null)
            throw new ArgumentNullException(nameof(employee));
        
        if (string.IsNullOrWhiteSpace(inputPin))
            throw new ArgumentException("PIN cannot be empty.", nameof(inputPin));
        
        if (employee.PinHash == null)
            throw new InvalidOperationException(
                $"Employee {employee.Id} has not been assigned a PIN.");
        
        try
        {
            return BCrypt.Net.BCrypt.Verify(inputPin, employee.PinHash);
        }
        catch (FormatException ex)
        {
            throw new PinValidationException(
                "Invalid PIN format in database.",
                ex);
        }
    }
}
```

---

### Regla 12: Logging Estructurado

**Estándar:** Microsoft ILogger + Serilog (recomendado)

```csharp
// ✅ CORRECTO: Logging con contexto
public class OrderService
{
    private readonly ILogger<OrderService> _logger;
    
    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }
    
    public async Task<Order> CreateOrderAsync(Order order, int employeeId)
    {
        _logger.LogInformation(
            "Creating order for employee {EmployeeId} with {ItemCount} items",
            employeeId,
            order.Items.Count);
        
        try
        {
            // Guardar orden
            var savedOrder = await _dbContext.Orders.AddAsync(order);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation(
                "Order {OrderId} created successfully by employee {EmployeeId}",
                savedOrder.Entity.Id,
                employeeId);
            
            return savedOrder.Entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create order for employee {EmployeeId}",
                employeeId);
            throw;
        }
    }
}
```

---

## ⚡ Async/Await y Concurrencia

### Regla 13: Convenciones de Nombres Async

**Estándar:** Microsoft Framework Design Guidelines

**Regla:** Los métodos asincronos **DEBEN** terminar en `Async`

```csharp
// ✅ CORRECTO
public async Task<Order> GetOrderAsync(int id)
{
    return await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
}

public async Task<List<Employee>> GetAllActiveEmployeesAsync()
{
    return await _dbContext.Employees
        .Where(e => e.IsActive)
        .ToListAsync();
}

public async Task SaveOrderAsync(Order order)
{
    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync();
}

// ❌ INCORRECTO
public Task<Order> GetOrder(int id) { }           // ❌ Falta Async
public async Task GetEmployees() { }              // ❌ Falta Async + inconsistencia
public Task<Order> GetOrderAsync() { }            // ❌ Task sin await en algún lugar
```

---

### Regla 14: Evitar Deadlocks con Async

**Estándar:** Stephen Cleary — Async Await Best Practices

```csharp
// ❌ MALO: Blocking en UI thread
public class VentasWindow : Window
{
    public VentasWindow()
    {
        // ❌ NUNCA bloquear async en constructor
        var orders = _orderService.GetAllOrdersAsync().Result;
        dataGrid.ItemsSource = orders;
    }
}

// ✅ CORRECTO: Async todo el camino
public partial class VentasWindow : Window
{
    public VentasWindow()
    {
        InitializeComponent();
    }
    
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var orders = await _orderService.GetAllOrdersAsync();
            dataGrid.ItemsSource = orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load orders");
            MessageBox.Show("Error cargando órdenes: " + ex.Message);
        }
    }
}
```

---

## 🛠️ Herramientas de Enforcing

### Regla 15: EditorConfig + StyleCop + Roslynator

FlexoCable debe usar herramientas **automáticas** para enforcing:

**1. .editorconfig** (raíz del proyecto)

```ini
# ✅ FlexoCable SV — EditorConfig Rules
# Archivo: .editorconfig (raíz del repositorio)

root = true

[*.cs]
# Indentation
indent_style = space
indent_size = 4
tab_size = 4

# Naming conventions
csharp_style_var_for_built_in_types = false:warning
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:warning

# Code blocks
csharp_prefer_braces = true:silent
csharp_prefer_simple_using_statement = true:suggestion

# Code block preferences
csharp_prefer_simple_default_expression = true:suggestion

# Async
csharp_style_inlined_variable_declaration = true:suggestion

# Naming
dotnet_naming_style.pascal_case_style.required_prefix = 
dotnet_naming_style.pascal_case_style.required_suffix = 
dotnet_naming_style.pascal_case_style.word_separator = 
dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# Rule: Private fields should be _camelCase
dotnet_naming_rule.private_fields_should_be_camel_case_with_underscore.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case_with_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case_with_underscore.style = camel_case_with_underscore_style

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_with_underscore_style.required_prefix = _
dotnet_naming_style.camel_case_with_underscore_style.capitalization = camel_case
```

**2. Install NuGet Packages**

```bash
# En el archivo .csproj
<ItemGroup>
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    
    <PackageReference Include="Roslynator.Analyzers" Version="4.5.0">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
</ItemGroup>
```

**Verificar en build:**
```bash
dotnet build --verbosity diagnostic
# Verá advertencias/errores de StyleCop y Roslynator
```

---

## ✔️ Checklist de Calidad

### Pre-commit en FlexoCable

Antes de hacer `git commit`, seguir también [FLEXOCABLE_GIT_PR_2026.md](FLEXOCABLE_GIT_PR_2026.md) (protocolo git, formato de mensaje y PR). Verificar código con:

```markdown
## Pre-Commit Checklist

### Nomenclatura (☑ Obligatorio)
- [ ] Clases en PascalCase
- [ ] Métodos en PascalCase
- [ ] Variables locales en camelCase
- [ ] Private fields con prefijo _
- [ ] Interfaces comienzan con I
- [ ] NO hay abreviaturas (emp → employee)
- [ ] Nombres reflejan semántica (GetOrder, CalculateSalary, ValidatePin)

### Formato (☑ Obligatorio)
- [ ] Indentación de 4 espacios (NO tabs)
- [ ] Máximo 120 caracteres por línea
- [ ] Llaves en nueva línea (Allman style)
- [ ] Una línea en blanco entre métodos

### Estructuras (☑ Obligatorio)
- [ ] Guard clauses antes de lógica compleja
- [ ] Excepciones específicas, no genéricas Exception
- [ ] Métodos async terminan en "Async"
- [ ] NO bloquear async con .Result o .Wait()

### Testing (☑ Recomendado pero no obligatorio en MVP)
- [ ] Métodos públicos tienen comentarios XML (///)
- [ ] Casos de error están cubiertos en tests
- [ ] Performance: <200ms en operaciones críticas

### Tools (☑ Automatizado)
- [ ] `dotnet build` sin warnings críticos
- [ ] StyleCop warnings < 10
- [ ] Roslynator warnings < 5
- [ ] EditorConfig compliance = 100%

### Code Review (☑ Por equipo)
- [ ] Peer review aprobado
- [ ] Al menos 1 otro dev revisó
- [ ] No merge sin 👍
```

---

## Git, PR y revisión

El protocolo git, emojis en mensajes de commit, plantillas de PR y checklist de revisión por dominio (DTE, inventario, `Squema.sql`) están en **[FLEXOCABLE_GIT_PR_2026.md](FLEXOCABLE_GIT_PR_2026.md)**. Este documento (STD-001) cubre solo calidad de código C#.

---

## 📖 Ejemplos Aplicados a FlexoCable

### Ejemplo 1: Servicio de Validación de PIN

```csharp
// ✅ COMPLETO Y CORRECTO
using System;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

namespace FlexoCableSV.PuntoVenta.Services
{
    /// <summary>
    /// Servicio de validación de PINs de empleados.
    /// Utiliza bcrypt (cost=12) para hash seguro.
    /// </summary>
    public interface IPinService
    {
        /// <summary>
        /// Valida un PIN ingresado contra el hash del empleado.
        /// </summary>
        /// <param name="employee">Empleado a validar</param>
        /// <param name="inputPin">PIN de 4 dígitos ingresado</param>
        /// <returns>true si el PIN es correcto; false si es incorrecto</returns>
        /// <exception cref="ArgumentNullException">Si employee es null</exception>
        /// <exception cref="InvalidOperationException">Si empleado no tiene PIN asignado</exception>
        Task<bool> ValidatePinAsync(Employee employee, string inputPin);
        
        /// <summary>
        /// Genera un nuevo hash de PIN para un empleado.
        /// </summary>
        Task<string> CreatePinHashAsync(string plainPin);
    }
    
    public class PinService : IPinService
    {
        private readonly ILogger<PinService> _logger;
        private const int BCRYPT_COST = 12;
        
        public PinService(ILogger<PinService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<bool> ValidatePinAsync(Employee employee, string inputPin)
        {
            if (employee == null)
                throw new ArgumentNullException(nameof(employee));
            
            if (string.IsNullOrWhiteSpace(inputPin))
                throw new ArgumentException("PIN cannot be empty.", nameof(inputPin));
            
            if (employee.PinHash == null)
                throw new InvalidOperationException(
                    $"Employee {employee.Id} ({employee.FullName}) has no PIN assigned.");
            
            // Validación en background para no bloquear UI
            return await Task.Run(() =>
            {
                try
                {
                    var isValid = BCrypt.Net.BCrypt.Verify(inputPin, employee.PinHash);
                    
                    _logger.LogInformation(
                        "PIN validation for employee {EmployeeId} ({EmployeeName}): {Result}",
                        employee.Id,
                        employee.FullName,
                        isValid ? "SUCCESS" : "FAILED");
                    
                    return isValid;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error validating PIN for employee {EmployeeId}",
                        employee.Id);
                    throw;
                }
            });
        }
        
        public async Task<string> CreatePinHashAsync(string plainPin)
        {
            if (string.IsNullOrWhiteSpace(plainPin))
                throw new ArgumentException("PIN cannot be empty.", nameof(plainPin));
            
            if (plainPin.Length != 4 || !int.TryParse(plainPin, out _))
                throw new ArgumentException("PIN must be exactly 4 digits.", nameof(plainPin));
            
            return await Task.Run(() =>
            {
                try
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(plainPin, BCRYPT_COST);
                    _logger.LogInformation("New PIN hash created successfully.");
                    return hash;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating PIN hash");
                    throw;
                }
            });
        }
    }
}
```

---

### Ejemplo 2: Servicio de Cálculo de Nómina

```csharp
// ✅ COMPLETO Y CORRECTO
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FlexoCableSV.PuntoVenta.Services
{
    /// <summary>
    /// Servicio de cálculo de nómina mensual con deducciones legales El Salvador.
    /// Aplica: ISSS 3%, AFP 7.25%, ISR según tabla vigente.
    /// </summary>
    public interface IPayrollCalculatorService
    {
        Task<PayrollMonthlyResult> CalculateMonthlyPayrollAsync(
            int year,
            int month);
    }
    
    public class PayrollCalculatorService : IPayrollCalculatorService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly DeductionCalculator _deductionCalculator;
        private readonly ILogger<PayrollCalculatorService> _logger;
        
        public PayrollCalculatorService(
            IEmployeeRepository employeeRepository,
            DeductionCalculator deductionCalculator,
            ILogger<PayrollCalculatorService> logger)
        {
            _employeeRepository = employeeRepository ??
                throw new ArgumentNullException(nameof(employeeRepository));
            _deductionCalculator = deductionCalculator ??
                throw new ArgumentNullException(nameof(deductionCalculator));
            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<PayrollMonthlyResult> CalculateMonthlyPayrollAsync(
            int year,
            int month)
        {
            ValidateYearMonth(year, month);
            
            try
            {
                var employees = await _employeeRepository.GetAllActiveAsync();
                
                if (employees.Count == 0)
                {
                    _logger.LogWarning(
                        "No active employees found for payroll {Year}-{Month:00}",
                        year,
                        month);
                    return new PayrollMonthlyResult();
                }
                
                var result = new PayrollMonthlyResult
                {
                    Year = year,
                    Month = month,
                    ProcessedDate = DateTime.UtcNow
                };
                
                foreach (var employee in employees)
                {
                    var detail = CalculateEmployeePayroll(employee, year, month);
                    result.AddDetail(detail);
                }
                
                _logger.LogInformation(
                    "Payroll calculated for {Month}/{Year}: {EmployeeCount} employees, " +
                    "Total gross: ${TotalGross:F2}, Total net: ${TotalNet:F2}",
                    month, year, employees.Count,
                    result.TotalGrossSalary,
                    result.TotalNetSalary);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calculating monthly payroll for {Year}-{Month:00}",
                    year,
                    month);
                throw;
            }
        }
        
        /// <summary>
        /// Calcula la nómina individual de un empleado.
        /// </summary>
        private PayrollDetail CalculateEmployeePayroll(
            Employee employee,
            int year,
            int month)
        {
            var detail = new PayrollDetail
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.FullName,
                Month = month,
                Year = year,
                BaseSalary = employee.BaseSalary
            };
            
            // Deducciones trabajador
            detail.IsssDeduction = _deductionCalculator.CalculateIsss(
                detail.BaseSalary);  // 3%
            detail.AfpDeduction = _deductionCalculator.CalculateAfp(
                detail.BaseSalary);  // 7.25%
            detail.IsrDeduction = _deductionCalculator.CalculateIsr(
                detail.BaseSalary);  // Tabla progresiva
            
            detail.TotalDeductions = detail.IsssDeduction +
                                     detail.AfpDeduction +
                                     detail.IsrDeduction;
            
            detail.NetSalary = detail.BaseSalary - detail.TotalDeductions;
            
            // Costo patronal (NO se deduce del empleado)
            detail.IsssEmployerCost = _deductionCalculator.CalculateIsssEmployer(
                detail.BaseSalary);  // 7.5%
            detail.AfpEmployerCost = _deductionCalculator.CalculateAfpEmployer(
                detail.BaseSalary);  // 8.75%
            
            return detail;
        }
        
        private void ValidateYearMonth(int year, int month)
        {
            if (year < 2020 || year > DateTime.UtcNow.Year)
                throw new ArgumentException(
                    $"Year must be between 2020 and {DateTime.UtcNow.Year}.",
                    nameof(year));
            
            if (month < 1 || month > 12)
                throw new ArgumentException(
                    "Month must be between 1 and 12.",
                    nameof(month));
        }
    }
}
```

---

## 📋 Resumen de Autoridades y Referencias

| Autoridad | Aplicabilidad | Referencia |
|---|---|---|
| **Microsoft Conventions** | ✅ **OBLIGATORIO** | `learn.microsoft.com/dotnet/csharp` |
| **ECMA-334** | ✅ **LEGAL (ISO)** | `ecma-international.org/publications/standards/ecma-334` |
| **C# Coding Guidelines (Juval Löwy)** | ✅ **RECOMENDADO** | `csharpcodingguidelines.com` |
| **StyleCop + Roslynator** | ✅ **AUTOMATIZADO** | NuGet + build validation |
| **.editorconfig** | ✅ **EN EL PROYECTO** | Root of repository |

---

## 📞 Contacto para Actualizaciones

- **Microsoft Docs:** `learn.microsoft.com` (actualiza mensualmente)
- **C# Language Features:** `github.com/dotnet/csharplang` (RFCs públicas)
- **FlexoCable Dev Team:** Revisar este estándar cada 6 meses o cuando haya cambios en .NET

---

**Documento aprobado:**  
Firma: _________________________ Fecha: _________________

