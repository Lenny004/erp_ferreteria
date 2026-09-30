---
name: documentacion-codigo
description: "Invocar al documentar código C#, XAML o SQL del proyecto. Aplica el estándar oficial de XML comments de C# (/// <summary>, <param>, <returns>, <exception>, <remarks>, <inheritdoc/>). Cubre clases, métodos, propiedades, ViewModels, servicios, entidades EF Core y controles XAML cuando aplique."
---

# Documentación de código del proyecto

## Contexto del proyecto

**Ferreteria.PuntoVenta** — aplicación de escritorio para punto de venta y confección de cables.

- **Tecnología:** C# (.NET 10), WPF con arquitectura MVVM, Entity Framework Core 10.x, PostgreSQL (Npgsql).
- **Arquitectura:** Views (XAML) + ViewModels + Services + Models (entidades EF Core) + Data (DbContext).
- **Facturación DTE:** Generación y firma de documentos tributarios electrónicos (JSON con firma JWS RSA).
- **Impresión:** ESC/POS para tickets térmicos (80mm) con QR.
- **Autenticación:** PIN bcrypt para cajeros y técnicos (`hr.Employees.PinHash`).
- **Estado actual:** El proyecto tiene `GenerateDocumentationFile` habilitado — el compilador genera `Ferreteria.PuntoVenta.xml` y emite advertencia CS1591 en miembros públicos sin documentación XML.

## Objetivo

Recorrer cada archivo del proyecto (`.cs`, `.xaml`, `.sql`) y agregar/completar su documentación siguiendo el estándar **oficial de C#** (XML documentation comments), sin alterar la lógica, firmas de métodos ni comportamiento existente.

## Reglas generales (aplican a todo el proyecto)

1. No modificar lógica de negocio, nombres de variables, firmas de métodos ni dependencias inyectadas.
2. Si un bloque ya tiene documentación correcta, verificarla y solo actualizar lo que esté desactualizado o incompleto — no borrar lo válido.
3. No agregar comentarios redundantes (`/// <summary>Suma dos números</summary>` sobre un método `Sumar(int a, int b)`). Solo documentar lo que no es evidente por el nombre o la firma.
4. Idioma de los comentarios: **español**.
5. Mantener el mismo estilo de documentación en todos los archivos de un mismo tipo (consistencia de formato, orden de etiquetas, puntuación).
6. Al terminar cada archivo, indicar en una línea qué se documentó (no reescribir el archivo completo en la respuesta si no es necesario).

## Estándar C# — XML Documentation Comments (Microsoft Docs)

Referencia oficial: [XML documentation comments (C# programming guide)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)

### Encabezado de archivo (opcional)

Solo si el archivo agrupa múltiples clases no relacionadas o requiere contexto global no evidente por los nombres. En la mayoría de los casos, **omitir** este encabezado y documentar directamente las clases.

### Por cada clase, struct, enum, interface, record

```csharp
/// <summary>
/// Descripción concisa de la responsabilidad de la clase (una o dos líneas).
/// </summary>
/// <remarks>
/// (Opcional) Contexto adicional: patrón que implementa (Service, Repository, ViewModel, DTO), cuándo usarla, dependencias clave.
/// Si la clase participa en transacciones EF Core o en el flujo de DTE, mencionarlo aquí.
/// </remarks>
public class MiClase
{
    // ...
}
```

**Ejemplo:**

```csharp
/// <summary>
/// Servicio de autenticación por PIN para empleados de caja y confección.
/// </summary>
/// <remarks>
/// Valida PINs con bcrypt (12 rounds) almacenados en hr.Employees.PinHash.
/// Registra intentos fallidos en hr.PinAttempts para bloqueo temporal tras 5 fallos consecutivos.
/// </remarks>
public class PinAuthService : IPinAuthService
{
    // ...
}
```

### Por cada método/función

```csharp
/// <summary>
/// Descripción concisa de qué hace el método (una línea si es posible).
/// </summary>
/// <param name="nombreParametro">Descripción breve del parámetro.</param>
/// <returns>Qué devuelve y en qué casos (si el tipo no es evidente).</returns>
/// <exception cref="TipoExcepcion">Cuándo se lanza esta excepción.</exception>
/// <remarks>
/// (Opcional) Contexto adicional: si el método ejecuta dentro de una transacción, si debe llamarse desde un flujo específico,
/// si participa en idempotencia o manejo de contingencia DTE.
/// </remarks>
public async Task<ResultadoDto> MiMetodo(string nombreParametro)
{
    // ...
}
```

**Ejemplo:**

```csharp
/// <summary>
/// Valida el PIN de un empleado y registra el intento.
/// </summary>
/// <param name="pin">PIN de 4 dígitos introducido por el usuario.</param>
/// <param name="moduleType">Módulo al que intenta acceder (CAJA o CONFECCION).</param>
/// <returns>
/// El empleado si el PIN es válido y el empleado tiene permisos para el módulo; 
/// <c>null</c> si el PIN es incorrecto o el empleado no tiene permisos.
/// </returns>
/// <exception cref="AccountLockedException">
/// El empleado está bloqueado temporalmente por exceso de intentos fallidos.
/// </exception>
/// <remarks>
/// Ejecuta dentro de una transacción explícita para garantizar consistencia entre validación y registro de intento.
/// Bloquea temporalmente al empleado tras 5 intentos fallidos consecutivos (30 minutos).
/// </remarks>
public async Task<Employee?> ValidatePinAsync(string pin, ModuleType moduleType)
{
    // ...
}
```

### Por cada propiedad

```csharp
/// <summary>
/// Descripción breve si el nombre no es autoexplicativo o si tiene lógica no trivial en getter/setter.
/// </summary>
public string MiPropiedad { get; set; }
```

**Si la propiedad es trivial (nombre autoexplicativo, getter/setter automático), no requiere documentación XML.**

**Ejemplo que SÍ requiere documentación:**

```csharp
/// <summary>
/// Identificador único del empleado autenticado en la sesión actual.
/// <c>null</c> si no hay sesión activa.
/// </summary>
public Guid? CurrentEmployeeId { get; private set; }
```

### Por cada campo público o protegido

```csharp
/// <summary>
/// Descripción breve del campo.
/// </summary>
protected readonly ILogger<MiClase> _logger;
```

**Campos privados de backing no requieren documentación XML.**

### Por cada enum y sus valores

```csharp
/// <summary>
/// Estados posibles de una orden de venta.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Orden creada pero no facturada. Stock no descontado.
    /// </summary>
    PENDIENTE,

    /// <summary>
    /// Orden facturada y pagada. Stock descontado, DTE emitido.
    /// </summary>
    COMPLETADA,

    /// <summary>
    /// Orden cancelada antes de facturar. Stock no afectado.
    /// </summary>
    CANCELADA
}
```

### Herencia de documentación (`<inheritdoc/>`)

Si un método implementa una interfaz o sobrescribe un método de clase base **y no añade comportamiento adicional**, usar:

```csharp
/// <inheritdoc/>
public async Task<Employee?> ValidatePinAsync(string pin, ModuleType moduleType)
{
    // ...
}
```

**Solo usar `<inheritdoc/>` si la documentación del contrato base es completa.** Si el método añade comportamiento específico, **documentarlo explícitamente**.

### Etiquetas comunes adicionales

- `<c>código</c>` — formato inline de código (`null`, `true`, nombres de parámetros).
- `<code>bloque</code>` — bloque de código multilínea (raramente necesario en XML comments).
- `<para>` — nuevo párrafo dentro de `<summary>` o `<remarks>`.
- `<see cref="Tipo"/>` — referencia a otro tipo o miembro.
- `<seealso cref="Tipo"/>` — relacionado con (se muestra aparte en docs generados).

## Documentación de ViewModels (MVVM)

Los ViewModels implementan `INotifyPropertyChanged` o heredan de `ObservableObject` (CommunityToolkit.Mvvm). Documentar:

- **Propiedades enlazables** (las que notifican cambios): solo si no son triviales o si tienen validación/lógica compleja.
- **Comandos** (`ICommand`, `RelayCommand`): documentar qué acción ejecutan y bajo qué condiciones (`CanExecute`).
- **Constructor**: documentar servicios inyectados si no es evidente su uso.

**Ejemplo:**

```csharp
/// <summary>
/// ViewModel para la vista de facturación (caja).
/// </summary>
/// <remarks>
/// Gestiona la creación de órdenes de venta, selección de productos, cálculo de totales,
/// generación de DTE y envío al Ministerio de Hacienda.
/// </remarks>
public class FacturacionViewModel : ObservableObject
{
    /// <summary>
    /// Comando para agregar un producto a la orden actual.
    /// </summary>
    /// <remarks>
    /// Se deshabilita si no hay un producto seleccionado o si la cantidad es <= 0.
    /// </remarks>
    public ICommand AgregarProductoCommand { get; }

    /// <summary>
    /// Comando para facturar la orden actual.
    /// </summary>
    /// <remarks>
    /// Valida que la orden tenga al menos un detalle, genera el DTE, lo envía al MH,
    /// descuenta el stock y registra el pago. Si MH no responde, marca el DTE en contingencia.
    /// Ejecuta dentro de una transacción EF Core.
    /// </remarks>
    public ICommand FacturarCommand { get; }
}
```

## Documentación de entidades EF Core (Models)

Las entidades mapeadas a tablas PostgreSQL requieren documentación solo en:

- **Clases** (tabla que representa).
- **Propiedades navegación** (relaciones FK) si no son evidentes por el nombre.
- **Propiedades calculadas** (no mapeadas, con lógica).

**No** documentar propiedades triviales (PK, FK simples, timestamps automáticos) cuyo nombre es autoexplicativo.

**Ejemplo:**

```csharp
/// <summary>
/// Representa una orden de venta en el sistema (tabla sales.Orders).
/// </summary>
/// <remarks>
/// Una orden puede estar PENDIENTE (confección), COMPLETADA (facturada y pagada) o CANCELADA.
/// Solo las órdenes COMPLETADAS desuentan stock y generan DTE.
/// </remarks>
public class Order
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tipo de orden: VENTA_MOSTRADOR u ORDEN_CONFECCION.
    /// </summary>
    public string OrderType { get; set; } = null!;

    /// <summary>
    /// Estado actual: PENDIENTE, COMPLETADA, CANCELADA.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Detalles de la orden (productos y cantidades).
    /// </summary>
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    /// <summary>
    /// Indica si la orden tiene DTE emitido (solo órdenes COMPLETADAS).
    /// </summary>
    [NotMapped]
    public bool HasDte => Status == "COMPLETADA";
}
```

## Documentación de DbContext

```csharp
/// <summary>
/// Contexto de base de datos para Ferreteria.PuntoVenta (PostgreSQL).
/// </summary>
/// <remarks>
/// Incluye DbSets para ventas (Orders, OrderDetails, Payments), DTE (DteIssued, DteContingency),
/// inventario (Products, InventoryMovements), empleados (Employees, PinAttempts) y configuración (Settings, Printers).
/// </remarks>
public class FerreteriaDbContext : DbContext
{
    // ...
}
```

## Documentación de controles XAML y code-behind

- **Code-behind** (`.xaml.cs`): documentar solo si contiene lógica no trivial (validación, animaciones, manejo de eventos complejos). El code-behind típico (`InitializeComponent()`, `DataContext`) **no requiere documentación**.
- **UserControls/CustomControls**: documentar la clase si el control tiene propósito no evidente o DependencyProperties personalizadas.

**Ejemplo:**

```csharp
/// <summary>
/// Control de entrada numérica con botones +/- para incremento/decremento.
/// </summary>
/// <remarks>
/// Usado en facturación para cantidades de productos tipo PIEZA o KIT.
/// Soporta valor mínimo, máximo y paso configurable.
/// </remarks>
public partial class NumericUpDownControl : UserControl
{
    /// <summary>
    /// Propiedad de dependencia para el valor actual.
    /// </summary>
    public static readonly DependencyProperty ValueProperty = ...;

    // ...
}
```

## Comentarios inline (dentro del cuerpo del código)

Aplican las mismas reglas que el estándar general:

- Solo donde el código no se explica por sí mismo (reglas de negocio no obvias, decisiones no evidentes, workarounds).
- Máximo una línea por bloque de control (`if`, `for`, `while`, `switch`, `try/catch`).
- Explicar el **por qué**, no el **qué** (el código ya dice qué hace).
- Variables: comentar solo si el nombre no es descriptivo, o si el valor tiene un formato/unidad no evidente (ej. montos en centavos, timestamps en UTC, estados numéricos de DTE).
- No comentar líneas triviales (`i++`, `count++`, etc.).

**Ejemplo de comentario inline justificado:**

```csharp
// Usamos bcrypt con factor de costo 12 (recomendación OWASP para 2026)
var hashedPin = BCrypt.Net.BCrypt.HashPassword(pin, workFactor: 12);

// Bloqueamos temporalmente tras 5 intentos fallidos para prevenir fuerza bruta
if (failedAttempts >= 5)
{
    employee.LockedUntil = DateTime.UtcNow.AddMinutes(30);
}

// Stock no se descuenta aquí porque la orden aún está PENDIENTE
// Solo se descuenta al facturar (status = COMPLETADA)
await _dbContext.Orders.AddAsync(order);
```

## Mencionar transacciones cuando aplique

Si un método ejecuta dentro de una transacción EF Core explícita (`BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`), mencionarlo en `<remarks>`:

```csharp
/// <remarks>
/// Ejecuta dentro de una transacción explícita: crea la orden, genera el DTE, descuenta el stock y registra el pago.
/// Si cualquier paso falla, se revierte toda la operación (rollback automático).
/// </remarks>
```

## Restricciones específicas del proyecto

- **Nullable reference types** habilitado (`<Nullable>enable</Nullable>`): preferir `?` en tipos y `null!` en inicializadores obligatorios.
- **Código asíncrono**: preferir `async`/`await` y `Task<T>` sobre `.Result` o `.Wait()`.
- **Inyección de dependencias**: documentar servicios inyectados en constructores solo si no es evidente su uso.

## Formato de entrega esperado por archivo

1. Ruta del archivo.
2. Resumen de una línea de qué se agregó o corrigió.
3. Diff o código final con la documentación aplicada (solo los bloques modificados, salvo que se pida el archivo completo).

---

## Resumen de etiquetas XML más comunes

| Etiqueta | Uso |
|----------|-----|
| `<summary>` | Descripción breve de clase, método, propiedad, enum |
| `<param name="x">` | Descripción de parámetro |
| `<returns>` | Qué devuelve el método |
| `<exception cref="T">` | Excepción que puede lanzar |
| `<remarks>` | Contexto adicional, detalles de implementación, transacciones |
| `<inheritdoc/>` | Hereda documentación del contrato base |
| `<c>code</c>` | Código inline (nombres de parámetros, `null`, `true`, etc.) |
| `<see cref="T"/>` | Referencia a otro tipo o miembro |
| `<seealso cref="T"/>` | Relacionado con (se muestra aparte) |
| `<para>` | Nuevo párrafo dentro de `<summary>` o `<remarks>` |

---

## Ejemplo completo de archivo documentado

```csharp
namespace Ferreteria.PuntoVenta.Services;

/// <summary>
/// Servicio de autenticación por PIN para empleados de caja y confección.
/// </summary>
/// <remarks>
/// Valida PINs con bcrypt (12 rounds) almacenados en hr.Employees.PinHash.
/// Registra intentos fallidos en hr.PinAttempts para bloqueo temporal tras 5 fallos consecutivos.
/// </remarks>
public class PinAuthService : IPinAuthService
{
    private readonly FerreteriaDbContext _dbContext;
    private readonly ILogger<PinAuthService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PinAuthService"/>.
    /// </summary>
    /// <param name="dbContext">Contexto de base de datos.</param>
    /// <param name="logger">Logger para registro de eventos de autenticación.</param>
    public PinAuthService(FerreteriaDbContext dbContext, ILogger<PinAuthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Valida el PIN de un empleado y registra el intento.
    /// </summary>
    /// <param name="pin">PIN de 4 dígitos introducido por el usuario.</param>
    /// <param name="moduleType">Módulo al que intenta acceder (CAJA o CONFECCION).</param>
    /// <returns>
    /// El empleado si el PIN es válido y el empleado tiene permisos para el módulo; 
    /// <c>null</c> si el PIN es incorrecto o el empleado no tiene permisos.
    /// </returns>
    /// <exception cref="AccountLockedException">
    /// El empleado está bloqueado temporalmente por exceso de intentos fallidos.
    /// </exception>
    /// <remarks>
    /// Ejecuta dentro de una transacción explícita para garantizar consistencia entre validación y registro de intento.
    /// Bloquea temporalmente al empleado tras 5 intentos fallidos consecutivos (30 minutos).
    /// </remarks>
    public async Task<Employee?> ValidatePinAsync(string pin, ModuleType moduleType)
    {
        // ... implementación ...
    }
}
```
