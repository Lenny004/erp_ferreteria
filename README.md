# FlexoCable SV — Sistema Integrado de Punto de Venta y Gestión

> **Código interno:** FCSV-2026 · **Versión:** 1.0.0-MVP · **Inicio:** Mayo 2026  
> **Cliente:** FlexoCable El Salvador, S.A. de C.V. · **Matriz:** FlexoCable Panamá  
> **Ubicación:** San Salvador, El Salvador

Sistema integral para la sucursal salvadoreña de FlexoCable: punto de venta táctil con facturación electrónica DTE, control de inventario y gestión de planillas. Diseñado específicamente para personal mayor con experiencia tecnológica limitada.

---

## Índice

- [Contexto del Negocio](#contexto-del-negocio)
- [Arquitectura del Sistema](#arquitectura-del-sistema)
- [App de Escritorio (C# WPF)](#app-de-escritorio-c-wpf)
  - [Flujo de Navegación e Inicio](#flujo-de-navegación-e-inicio)
  - [Módulo Ventas](#módulo-ventas)
  - [Módulo Inventario](#módulo-inventario)
  - [Módulo Impresoras](#módulo-impresoras)
- [WebApp (Next.js)](#webapp-nextjs)
- [Base de Datos (PostgreSQL)](#base-de-datos-postgresql)
- [Facturación Electrónica DTE](#facturación-electrónica-dte)
- [UX/UI — Diseño para Personas Mayores](#uxui--diseño-para-personas-mayores)
- [Stack Tecnológico](#stack-tecnológico)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Instalación y Configuración](#instalación-y-configuración)
- [Roadmap](#roadmap)

---

## Contexto del Negocio

FlexoCable es una empresa panameña con más de 20 años fabricando cables de control para vehículos comerciales e industriales. En 2026 abre sucursal en San Salvador combinando:

- **Venta de repuestos** — componentes sueltos (boquillas, terminales, resortes, cables)
- **Fabricación custom** — cables ensamblados a medida según especificación del cliente
- **Servicio de reparación** — reconstrucción de cables existentes

**Problemas que resuelve este sistema:**

| Problema | Solución |
|---|---|
| Sin control de inventario (500+ productos, 17 familias) | Inventario con deducción automática metros/piezas |
| Sin facturación electrónica (DTE obligatorio en SV desde 2022) | Integración directa con API del Ministerio de Hacienda |
| Planilla calculada manualmente en Excel | WebApp con cálculo automático ISSS/AFP/ISR |
| Personal 45+ años con baja experiencia tecnológica | UX táctil: botones grandes, un paso a la vez, sin gestos |

---

## Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────┐
│              FLEXOCABLE SV — ARQUITECTURA                   │
├──────────────────────────┬──────────────────────────────────┤
│  APP ESCRITORIO (C# WPF) │       WEBAPP (Next.js)           │
│  FlexoCableSV.PuntoVenta │       FlexoCableSV.Web           │
│                          │                                   │
│  • Pantalla táctil       │  • Dashboard + KPIs              │
│  • Ventas + DTE          │  • Planilla mensual              │
│  • Inventario            │  • Gestión de empleados          │
│  • Impresión ESC/POS     │  • Reportes y gráficas           │
│                          │                                   │
│  Stack: C# 12 + WPF      │  Stack: Next.js 14 + TypeScript  │
│  + .NET 8 + EF Core      │  + Prisma + Tailwind CSS         │
└──────────┬───────────────┴──────────────┬────────────────────┘
           │                              │
           └──────────────┬───────────────┘
                          │
               ┌──────────▼──────────┐
               │    POSTGRESQL 14+   │
               │                     │
               │  public/   → Catálogo e inventario
               │  sales/    → Órdenes y facturas
               │  dte/      → Facturación electrónica
               │  hr/       → Empleados y planilla
               │  system/   → Config y auditoría
               └──────────┬──────────┘
                          │
               ┌──────────▼──────────┐
               │  MINISTERIO DE      │
               │  HACIENDA (API DTE) │
               │  apifactura.mh.gob.sv
               └─────────────────────┘
```

**Principios arquitectónicos:**

| Principio | Implementación |
|---|---|
| Fuente única de verdad | PostgreSQL compartido entre WPF y WebApp |
| Sin capas innecesarias | WPF conecta directo a PostgreSQL vía EF Core + Npgsql |
| UX senior-friendly | Botones ≥90×90px, tipografía 16pt+, solo TAP, sin gestos |
| Escalabilidad futura | WebApp Next.js permite agregar módulos sin tocar la caja |

---

## App de Escritorio (C# WPF)

### Flujo de Navegación e Inicio

La pantalla de inicio **no es un login tradicional**. Es una pantalla de selección de módulo con dos botones grandes:

```
┌─────────────────────────────────────────┐
│          FLEXOCABLE EL SALVADOR         │
│              [Logo]                     │
│                                         │
│   ┌──────────────┐  ┌──────────────┐   │
│   │              │  │              │   │
│   │   VENTAS     │  │  INVENTARIO  │   │
│   │              │  │              │   │
│   └──────────────┘  └──────────────┘   │
│                                         │
└─────────────────────────────────────────┘
```

**Reglas de acceso por módulo:**

| Módulo | Acción | PIN requerido |
|---|---|---|
| Ventas | Entrar al módulo | ❌ No |
| Ventas | Crear / confirmar orden | ✅ PIN personal del técnico seleccionado |
| Inventario | Ver stock | ❌ No |
| Inventario | Registrar entrada de mercancía | ❌ No |
| Inventario | Ajuste manual (sumar/restar stock) | ✅ PIN personal del técnico que ajusta |

**Flujo de autenticación en Ventas:**

El módulo de ventas es accesible directamente. El PIN se solicita en el momento de **confirmar la orden**, no al entrar. El técnico se elige primero en el `Select`; al presionar "FACTURAR" se muestra el modal de PIN correspondiente a ese técnico. Esto evita que los técnicos tengan que autenticarse al inicio del turno y permite que varias personas trabajen desde la misma caja sin fricciones.

```
[Botón VENTAS] → Tabla de Ventas del Día (sin PIN)
                        │
                        ▼
                "+ NUEVA ORDEN" → Formulario de orden
                        │
                  [Elige técnico del Select]
                        │
                        ▼
                 Agrega items a la orden
                        │
                        ▼
                 Presiona "FACTURAR (DTE)"
                        │
                        ▼
              ┌─── Modal PIN ───┐
              │ Técnico: JUAN   │  ← Nombre del técnico seleccionado
              │ [_ _ _ _]       │  ← Teclado numérico grande
              │ [CONFIRMAR]     │
              └─────────────────┘
                        │
              PIN correcto → Continúa a facturación DTE
              PIN incorrecto → Mensaje de error, reintentar
```

**Flujo de autenticación en Inventario (ajustes):**

```
[Botón INVENTARIO] → Vista de Stock (sin PIN)
        │
        ├── "NUEVA ENTRADA" → Formulario de entrada (sin PIN)
        │
        └── "AJUSTE MANUAL" → Modal: elegir técnico que ajusta
                                      │
                                      ▼
                              Modal PIN del técnico
                                      │
                              PIN correcto → Formulario de ajuste
```

> **Nota de implementación:** `PinWindow.xaml` es un modal reutilizable que recibe el `employee_id` como parámetro, consulta `hr.employees.pin_hash` y valida con bcrypt. Se usa tanto en Ventas como en Inventario.

---

### Módulo Ventas

Flujo completo de una venta:

```
Tabla de Ventas del Día
        │
        ▼
"+ NUEVA ORDEN" (botón verde grande)
        │
        ▼
Nueva Orden de Confección
├── Fecha/hora: automática
├── Técnico: Select (lista de empleados activos)
├── Aplicación: Select (VT-01, VT-02, VT-03, RP-01)
├── Cliente: Input con teclado virtual
└── "+ AGREGAR CÓDIGO"
        │
        ▼
Selección de Código
├── Input código catálogo (ej: 02-AC-01)
├── Auto-búsqueda al escribir
├── Auto-detecta tipo de medida:
│   • METRO → input decimal (ej: 5.50)
│   • PIEZA → contador +/- entero
│   • KIT   → contador +/- entero
├── Muestra: descripción, stock actual, familia
└── "AGREGAR A LA ORDEN" (puede repetirse)
        │
        ▼
Resumen de Orden
├── Lista de items con subtotales
├── Total calculado automáticamente
├── "GUARDAR BORRADOR" (gris)
└── "FACTURAR (DTE)" (rojo grande)
        │
        ▼
[Modal PIN del técnico seleccionado]
        │ PIN correcto
        ▼
Facturación DTE
├── Tipo DTE: [01 Consumidor Final / 03 Crédito Fiscal]
├── Forma de pago: [Efectivo / Tarjeta / Transferencia]
├── NIT/DUI del cliente (si aplica)
├── "GENERAR Y ENVIAR DTE"
├── Spinner → Sello de recepción (éxito)
└── Si falla → Cola de contingencia, botón reintentar
        │
        ▼
Impresión de Ticket
├── Vista previa del ticket
├── Selector de impresora (configuradas en el sistema)
├── "IMPRIMIR" / "REIMPRIMIR"
└── Ticket: Logo, items, totales, QR DTE
        │
        ▼
[Automático] Deducción de inventario
├── Cable: descuenta METROS
└── Pieza/Kit: descuenta UNIDADES
    → Si stock < mínimo: genera alerta
```

**Pantallas del módulo Ventas:**

| Ventana | Archivo | Propósito |
|---|---|---|
| Tabla diaria | `VentasWindow.xaml` | Lista ventas del día, resumen superior |
| Nueva orden | `NuevaOrdenWindow.xaml` | Formulario fecha/técnico/aplicación/cliente |
| Selección código | `SeleccionCodigoWindow.xaml` | Búsqueda y cantidad por tipo de medida |
| PIN técnico | `PinWindow.xaml` | Modal reutilizable, recibe `employee_id` |
| Facturación | `FacturarWindow.xaml` | DTE, envío MH, sello, impresión |

---

### Módulo Inventario

Acceso directo sin PIN. Solo los **ajustes manuales** (sumar o restar stock arbitrariamente) requieren PIN del técnico que los realiza. Las entradas de mercancía (recepciones de compras o devoluciones de proveedor) son libres.

```
[Botón INVENTARIO] → Vista de Stock
        ├── Tabla productos con indicador de estado:
        │   🟢 Verde    → Stock OK (stock > mínimo)
        │   🟡 Naranja  → Bajo mínimo (stock ≤ mínimo)
        │   🔴 Rojo     → Agotado (stock = 0)
        ├── Filtros: Familia, Estado, Búsqueda por código
        │
        ├── "NUEVA ENTRADA" (sin PIN)
        │       ├── Input: código producto
        │       ├── Muestra: descripción, stock actual
        │       ├── Cantidad recibida
        │       ├── Proveedor (select o agregar nuevo)
        │       ├── Número de factura/documento del proveedor
        │       ├── Motivo: COMPRA / DEVOLUCIÓN
        │       └── "GUARDAR ENTRADA"
        │
        └── "AJUSTE MANUAL" (requiere PIN)
                ├── Select: técnico que realiza el ajuste
                ├── [Modal PIN del técnico seleccionado]
                ├── Código producto
                ├── Tipo: SUMAR o RESTAR
                ├── Cantidad
                ├── Motivo (obligatorio): DAÑO / PÉRDIDA / INVENTARIO_FISICO / CORRECCIÓN
                └── "GUARDAR AJUSTE"
```

**Reglas de inventario:**

| Regla | Descripción |
|---|---|
| R-INV-01 | Todo producto tiene tipo de medida fija: METRO, PIEZA, KIT, PESO |
| R-INV-02 | Cables se venden por metros con 2 decimales |
| R-INV-03 | Piezas/kits son unidades enteras |
| R-INV-04 | Stock no puede quedar negativo (validación antes de guardar) |
| R-INV-05 | Alerta automática cuando stock ≤ mínimo |
| R-INV-06 | Movimientos son inmutables — no se borran, solo se registran correcciones |
| R-INV-07 | Ajustes manuales requieren motivo obligatorio + PIN personal |

---

### Módulo Impresoras

Módulo dedicado accesible desde el menú de configuración. Permite:

- Ver impresoras instaladas en Windows
- Establecer impresora predeterminada para tickets
- Configurar ancho de papel (80mm / 58mm)
- Imprimir ticket de prueba
- Configurar conexión Ethernet (IP + puerto para impresoras de red)

No requiere PIN — es configuración técnica que normalmente solo toca el administrador.

---

## WebApp (Next.js)

Aplicación web separada, accesible desde cualquier dispositivo con navegador. Conecta a la **misma base de datos PostgreSQL** que la app de escritorio, usando Prisma como ORM. No duplica funcionalidad de la caja.

### Módulos

**1. Dashboard**
- KPIs del día: ventas totales, ticket promedio, DTEs enviados, alertas de stock
- Gráfica de barras: ventas por día (últimos 30 días)
- Gráfica circular: ventas por familia de producto
- Top 5 productos más vendidos
- Alertas activas de stock bajo

**2. Planilla y Nómina**
- CRUD de empleados (datos personales, puesto, salario base)
- Cálculo automático mensual con descuentos legales:
  - ISSS trabajador: 3% | ISSS patronal: 7.5%
  - AFP trabajador: 7.25% | AFP patronal: 8.75%
  - ISR según tabla progresiva vigente
- Registro de horas extras (200% salario base)
- Bonificaciones y descuentos adicionales
- Generación de recibo de pago en PDF
- Historial por mes/año — planillas cerradas son inmutables

**3. Reportes**
- Ventas por técnico (comisiones)
- Comparativo mensual/anual
- Rotación de inventario
- Exportación a Excel

### Autenticación WebApp

- Usuario + contraseña (no PIN)
- JWT con sesión de 8 horas
- Roles: Admin, Contador, Dueño (remoto)

> La WebApp también es donde se gestiona la creación de empleados y la asignación de sus PINs para la app de escritorio. El administrador crea al empleado en la WebApp, asigna su PIN inicial, y ese hash queda disponible para la validación en la caja.

---

## Base de Datos (PostgreSQL)

### Esquemas

Los identificadores en PostgreSQL y en los modelos C# están en **inglés** (`sales`, `hr`, `system`, etc.). La UI y los datos de negocio siguen en español.

| Esquema | Tablas principales | Propósito |
|---|---|---|
| `public` | `families`, `subfamilies`, `products`, `measurement_types`, `inventory_movements`, `stock_alerts` | Catálogo e inventario |
| `sales` | `applications`, `orders`, `order_details` | Operaciones diarias |
| `dte` | `dte_config`, `dte_issued`, `dte_contingency` | Facturación electrónica |
| `hr` | `employees`, `positions`, `departments`, `payroll`, `payroll_details` | Recursos humanos |
| `system` | `printers`, `settings`, `web_users`, `audit_log` | Config, seguridad y logs |

### Catálogo de Productos

```
FORMATO: XX-YY-ZZZ[A]

XX   = Familia     (ej: 02 → Boquillas)
YY   = Subfamilia  (ej: AC → Acelerador)
ZZZ  = Correlativo (ej: 01)
[A]  = Variante opcional

Ejemplos:
  02-AC-01      → Boquilla Acelerador #1
  01-Cga-03     → Cable Galvanizado Acero #3
  FLV-CCG-U-101 → Flexoindustrial VLD, CCG Universal #101
```

**Tipos de medida:**

| Tipo | Unidad | Decimales | Aplica a |
|---|---|---|---|
| METRO | metros | 2 | Cables de acero, fundas, mangueras |
| PIEZA | piezas | 0 | Boquillas, terminales, tuercas, resortes |
| KIT | kits | 0 | Kits pre-armados |
| PESO | kg | 3 | Materia prima a granel |

### Seguridad de PINs

Los PINs del personal que opera caja se almacenan como hash **bcrypt (12 rounds)** en `hr.employees.pin_hash`. No existe la tabla `tecnicos`: el mismo registro de empleado define permisos con `can_sell` (ventas/confección) y `can_cashier` (caja). Nunca se guarda el PIN en texto plano. La validación ocurre en la app de escritorio mediante `BCrypt.Net-Next`.

Los PINs se asignan y cambian desde la **WebApp** por el administrador. La app de escritorio solo valida — nunca crea ni modifica PINs.

---

## Facturación Electrónica DTE

| Aspecto | Detalle |
|---|---|
| Estándar | DTE v3.0 — Ministerio de Hacienda El Salvador |
| Formato | JSON con esquema validado |
| Firma | JWS con RSA 4096 bits, certificado PKCS#12 |
| URL Pruebas | `https://apifacturatest.mh.gob.sv` |
| URL Producción | `https://apifactura.mh.gob.sv` |
| Autenticación | JWT Bearer Token vía `/auth` |
| Contingencia | Cola local + reintento automático cada 15 min |

**Tipos de DTE soportados:**

| Código | Tipo | Cuándo se usa |
|---|---|---|
| 01 | Factura Consumidor Final | Ventas a personas naturales (sin NIT) |
| 03 | Comprobante Crédito Fiscal | Ventas a empresas (con NIT) |
| 05 | Nota de Crédito | Devoluciones o anulaciones |

**Flujo simplificado:**
1. Técnico finaliza orden → presiona "FACTURAR (DTE)" → ingresa su PIN
2. Sistema genera JSON DTE (emisor, receptor, items, totales, IVA 13%)
3. Firma con certificado `.p12`
4. POST a API MH → recibe sello de recepción
5. Venta marcada "CERRADA" → imprime ticket con QR
6. Si falla: guarda en `dte.dte_contingency`, reintenta automáticamente

---

## UX/UI — Diseño para Personas Mayores

El 70% del equipo operativo tiene 45+ años con experiencia tecnológica limitada. El diseño se llama internamente **"Flexo Simple"**.

### Principios

| Regla | Especificación | Por qué |
|---|---|---|
| Una acción por pantalla | Sin menús anidados ni tabs complejos | Reduce confusión |
| Botones grandes | Mínimo 90×90px — acción principal 200×60px | Dedos grandes, pantalla táctil |
| Solo TAP | Sin swipe, pinch ni zoom | Evita errores accidentales |
| Confirmación obligatoria | Modal antes de Facturar y Eliminar | Previene errores costosos |
| Retroalimentación inmediata | Cambio visible en <200ms tras cada toque | Seguridad de que "funcionó" |
| Tipografía grande | 16pt base, 20pt+ en títulos | Visión reducida común en +50 años |
| Timeout largo | 30 minutos de sesión | No cierra si se distraen |
| Colores semánticos | Verde=OK, Rojo=Acción principal, Naranja=Alerta | Intuitivo y universal |

### Paleta de Colores Oficial

```css
--flexo-rojo:        #D22533;  /* Botones principales, acciones críticas */
--flexo-negro:       #080808;  /* Headers, textos principales */
--flexo-blanco:      #FFFFFF;  /* Fondo de todas las pantallas */
--flexo-gris-claro:  #F5F5F5;  /* Inputs, áreas secundarias, filas tabla */
--flexo-gris-medio:  #9E9E9E;  /* Bordes, botones secundarios, deshabilitado */
--flexo-verde:       #4CAF50;  /* Guardar borrador, éxito, stock OK */
--flexo-naranja:     #FF9800;  /* DTE pendiente, stock bajo mínimo */
--flexo-rojo-claro:  #F44336;  /* Cancelar, stock agotado, error DTE */
```

### Tipografía

| Elemento | Fuente | Tamaño | Peso |
|---|---|---|---|
| Header / Logo | Segoe UI | 28–36pt | Bold |
| Título de pantalla | Segoe UI | 20–24pt | Bold |
| Label de campo | Segoe UI | 12–14pt | SemiBold |
| Valor / dato | Segoe UI | 14–16pt | Regular |
| Botón principal | Segoe UI | 16–18pt | Bold |
| Botón numérico +/- | Segoe UI | 24pt | Bold |
| Total / importe | Segoe UI | 18–24pt | Bold |

---

## Stack Tecnológico

### App de Escritorio (Punto de Venta)

| Tecnología | Versión | Propósito |
|---|---|---|
| C# | 12.0 | Lenguaje principal |
| WPF (.NET 8) | 8.0 LTS | Interfaz gráfica táctil |
| Entity Framework Core | 8.0 | ORM para PostgreSQL |
| Npgsql | 8.0 | Driver PostgreSQL para .NET |
| BCrypt.Net-Next | 4.0 | Hash y validación de PINs |
| Newtonsoft.Json | 13.0 | Serialización JSON para DTE |
| QRCoder | 1.4 | Generación de QR en tickets |
| ESC/POS.NET | 1.0 | Impresión térmica |

### WebApp (Gestión y RRHH)

| Tecnología | Versión | Propósito |
|---|---|---|
| Next.js | 14 | Framework React full-stack |
| Node.js | 20 LTS | Runtime JavaScript |
| TypeScript | 5.3 | Lenguaje principal |
| Prisma | 5.0 | ORM para PostgreSQL |
| Tailwind CSS | 3.4 | Estilos responsive |
| Recharts | Última | Gráficas y dashboards |
| NextAuth.js | 4.0 | Autenticación JWT |
| React Hook Form + Zod | Última | Formularios con validación |
| bcryptjs | 2.4 | Hash de PINs al crear/editar técnicos |

### Base de Datos e Infraestructura

| Componente | Especificación |
|---|---|
| Motor BD | PostgreSQL 14+ |
| PC Caja | Windows 10 Pro, 8GB RAM, monitor táctil 15" |
| Impresora | Térmica ESC/POS 80mm (USB o Ethernet) |
| Red | LAN local cableada |
| UPS | 600VA mínimo (30 min autonomía recomendado) |
| Backup | `pg_dump` automático diario, retención 30 días |

---

## Estructura del Proyecto

```
flexocable-sv/
│
├── README.md
├── LICENSE
├── .gitignore
│
├── FlexoCableSV.PuntoVenta/                ← APP ESCRITORIO C# WPF
│   ├── Squema.sql                          ← Esquema PostgreSQL completo + seed data
│   ├── FlexoCableSV.PuntoVenta.csproj
│   ├── App.xaml                            ← Recursos globales, colores, estilos
│   ├── App.xaml.cs                         ← Inicialización, contexto de BD
│   │
│   ├── Views/
│   │   ├── Inicio/
│   │   │   └── InicioWindow.xaml           ← 2 botones: VENTAS / INVENTARIO
│   │   ├── Compartidos/
│   │   │   └── PinWindow.xaml              ← Modal PIN reutilizable (recibe employee_id)
│   │   ├── Ventas/
│   │   │   └── VentasWindow.xaml           ← Tabla diaria de ventas
│   │   ├── Ordenes/
│   │   │   └── NuevaOrdenWindow.xaml       ← Formulario de orden
│   │   ├── Codigos/
│   │   │   └── SeleccionCodigoWindow.xaml  ← Búsqueda + auto-detección tipo medida
│   │   ├── Facturacion/
│   │   │   └── FacturarWindow.xaml         ← DTE, envío MH, sello, impresión
│   │   ├── Inventario/
│   │   │   └── InventarioWindow.xaml       ← Stock con estados de color
│   │   └── Configuracion/
│   │       └── ImpresorasWindow.xaml       ← Gestión de impresoras
│   │
│   ├── Models/                             ← EF Core (namespace Models; carpetas = schema PostgreSQL)
│   │   ├── Public/                         ← Product, Family, InventoryMovement, StockAlert, …
│   │   ├── Sales/                          ← Order, OrderDetail, Application
│   │   ├── Dte/                            ← DteConfig, DteIssued, DteContingency
│   │   ├── Hr/                             ← Employee (pin_hash, can_sell, can_cashier), Payroll, …
│   │   └── System/                         ← Setting, Printer, WebUser, AuditLog
│   │
│   ├── Data/
│   │   └── FlexoDbContext.cs               ← DbContext EF Core + Npgsql
│   │
│   ├── Services/
│   │   ├── DTEService.cs                   ← Genera JSON, firma, envía a MH
│   │   ├── ImpresionService.cs             ← ESC/POS tickets térmicos
│   │   ├── InventarioService.cs            ← Descuenta stock, valida, alertas
│   │   ├── PinService.cs                   ← Valida PIN con BCrypt por employee_id
│   │   └── ConfigService.cs                ← Lee appsettings.json
│   │
│   ├── Helpers/
│   │   └── NumberFormatter.cs              ← Formato moneda SV, decimales
│   │
│   ├── Resources/
│   │   ├── Styles/
│   │   │   └── FlexoStyles.xaml            ← Estilos reutilizables
│   │   └── Images/
│   │       └── logo_flexo.png
│   │
│   └── appsettings.json                    ← Cadena de conexión PostgreSQL
│
├── FlexoCableSV.Web/                       ← WEBAPP NEXT.JS
│   ├── package.json
│   ├── next.config.js
│   ├── tailwind.config.js
│   │
│   ├── src/
│   │   ├── app/
│   │   │   ├── layout.tsx
│   │   │   ├── page.tsx                    ← Dashboard principal
│   │   │   ├── empleados/
│   │   │   │   └── page.tsx                ← CRUD empleados + asignación de PINs
│   │   │   ├── planilla/
│   │   │   │   └── page.tsx                ← Cálculo planilla mensual
│   │   │   └── reportes/
│   │   │       └── page.tsx                ← Reportes y gráficas
│   │   │
│   │   ├── components/
│   │   │   ├── ui/
│   │   │   ├── charts/
│   │   │   └── forms/
│   │   │
│   │   ├── lib/
│   │   │   ├── prisma.ts
│   │   │   └── api.ts
│   │   │
│   │   └── types/
│   │       └── index.ts
│   │
│   ├── prisma/
│   │   └── schema.prisma
│   │
│   └── .env.example
│
└── docs/
    ├── manual_usuario.md
    ├── manual_admin.md
    └── dte_especificacion.md
```

---

## Instalación y Configuración

### Requisitos

**App de Escritorio:**

| Requisito | Versión mínima |
|---|---|
| Windows 10 | Versión 1909 o superior |
| .NET 8 SDK | 8.0.100+ |
| Visual Studio 2022 | Community Edition |
| PostgreSQL | 14+ |

**WebApp:**

| Requisito | Versión mínima |
|---|---|
| Node.js | 20 LTS |
| npm | 10+ |
| PostgreSQL | 14+ (misma instancia) |

---

### 1. Base de Datos PostgreSQL

```bash
psql -U postgres -c "CREATE DATABASE flexocable;"
psql -U postgres -c "CREATE USER flexo_user WITH PASSWORD 'tu_password_seguro';"
psql -U postgres -c "GRANT ALL PRIVILEGES ON DATABASE flexocable TO flexo_user;"
psql -U flexo_user -d flexocable -f FlexoCableSV.PuntoVenta/Squema.sql
```

### 2. App de Escritorio (WPF)

```bash
cd FlexoCableSV.PuntoVenta
dotnet restore
dotnet build
dotnet run
```

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "FlexoCableDB": "Host=localhost;Database=flexocable;Username=flexo_user;Password=tu_password_seguro"
  },
  "App": {
    "SessionTimeoutMinutes": 30
  }
}
```

> Los PINs de los técnicos se configuran desde la WebApp. La app de escritorio solo los valida.

### 3. WebApp (Next.js)

```bash
cd FlexoCableSV.Web
npm install
cp .env.example .env
npx prisma generate
npx prisma db push
npm run dev
```

`.env`:

```
DATABASE_URL="postgresql://flexo_user:password@localhost:5432/flexocable"
NEXTAUTH_SECRET="tu_secreto_seguro"
NEXTAUTH_URL="http://localhost:3000"
```

### 4. Configuración DTE

1. Obtener NIT emisor y certificado `.p12` del Ministerio de Hacienda
2. Actualizar tabla `dte.dte_config` con datos reales del emisor
3. Subir certificado `.p12` al servidor en ruta segura
4. Probar con ambiente `00` (pruebas) antes de cambiar a `01` (producción)

### 5. Crear empleados con acceso a caja y asignar PINs

Desde la WebApp (módulo Empleados):
1. Crear empleado en `hr.employees` con datos personales y puesto
2. Activar `can_sell` y/o `can_cashier` según el rol en caja
3. Asignar PIN de 4 dígitos — se guarda en `pin_hash` (bcrypt)
4. El empleado queda disponible en el `Select` de la app de escritorio

---

## Roadmap

### Fase 1 — MVP "La Caja Factura" (Semanas 1–6)

| Semana | Entregable | Estado |
|---|---|---|
| 1–2 | Setup BD, catálogo 500+ productos cargado, pantalla de inicio con dos módulos | 🔲 Pendiente |
| 3–4 | Módulo Ventas: orden completa, modal PIN por técnico, deducción de stock | 🔲 Pendiente |
| 5–6 | Integración DTE, impresión ESC/POS, módulo impresoras, pruebas con usuarios | 🔲 Pendiente |

### Fase 2 — Inventario Completo (Semanas 7–10)

| Semana | Entregable | Estado |
|---|---|---|
| 7–8 | Entradas de mercancía (libre), ajustes manuales con PIN, alertas automáticas | 🔲 Pendiente |
| 9–10 | Reconciliación física, reportes básicos de inventario | 🔲 Pendiente |

### Fase 3 — WebApp RRHH (Semanas 11–16)

| Semana | Entregable | Estado |
|---|---|---|
| 11–12 | Next.js setup, dashboard con KPIs y gráficas, gestión de empleados y PINs | 🔲 Pendiente |
| 13–14 | Cálculo automático de planilla con descuentos legales SV | 🔲 Pendiente |
| 15–16 | Reportes avanzados, testing completo, deploy producción | 🔲 Pendiente |

---

## Reglas de Negocio Críticas

### Ventas y DTE

| Regla | Descripción |
|---|---|
| R-VTA-01 | Toda venta facturada es inmutable (estado CERRADA, sin edición) |
| R-VTA-02 | El DTE debe confirmarse antes de imprimir el ticket |
| R-VTA-03 | Si DTE falla, la venta queda en CONTINGENCIA con reintento automático cada 15 min |
| R-VTA-04 | El ticket incluye QR con el código de generación DTE |
| R-VTA-05 | Anulación requiere Nota de Crédito (DTE 05) — no se borran registros |
| R-VTA-06 | El PIN que autoriza la venta corresponde al técnico seleccionado en la orden |

### Planilla El Salvador

| Concepto | Porcentaje |
|---|---|
| ISSS trabajador | 3% del salario |
| ISSS patronal | 7.5% del salario |
| AFP trabajador | 7.25% del salario |
| AFP patronal | 8.75% del salario |
| ISR | Tabla progresiva SV vigente |
| Horas extras | 200% del salario base por hora |

---

## Licencia

```
Copyright (c) 2026 FlexoCable El Salvador, S.A. de C.V.
Todos los derechos reservados.

Este software es propiedad exclusiva de FlexoCable El Salvador.
Queda estrictamente prohibida su reproducción, distribución
o uso sin autorización expresa por escrito del propietario.
```

---

## Contacto

| Departamento | Contacto |
|---|---|
| Soporte Técnico | soporte@flexocable.com.sv |
| Administración | admin@flexocable.com.sv |
| Desarrollo | dev@flexocable.com.sv |

**Dirección:** San Salvador, El Salvador
