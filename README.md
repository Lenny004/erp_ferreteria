# erp_ferreteria

> **Nombre:** FlexoCable SV — Sistema Integrado de Punto de Venta y Gestión  
> **Descripción:** Aplicación de escritorio (C# WPF) para punto de venta táctil con facturación electrónica DTE, confección de cables y consulta de stock. Forma parte del ecosistema ERP Ferretería junto con `ferreteria_backend` y `ferreteria_adminweb`.

> **Código interno:** FCSV-2026 · **Versión:** 1.0.0-MVP · **Plan:** v3.0 (Junio 2026)  
> **Cliente:** FlexoCable El Salvador, S.A. de C.V. · **Matriz:** FlexoCable Panamá  
> **Ubicación:** San Salvador, El Salvador  
> **Plan de desarrollo:** [`docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md`](docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md)

Sistema integral para la sucursal salvadoreña de FlexoCable: punto de venta táctil con facturación electrónica DTE, control de inventario y gestión de planillas. Diseñado específicamente para personal mayor con experiencia tecnológica limitada.

---

## Índice

- [Contexto del Negocio](#contexto-del-negocio)
- [Arquitectura del Sistema](#arquitectura-del-sistema)
- [Estado del desarrollo](#estado-del-desarrollo)
- [App de Escritorio (C# WPF)](#app-de-escritorio-c-wpf)
  - [Flujo de Navegación e Inicio](#flujo-de-navegación-e-inicio)
  - [Módulo Caja](#módulo-caja)
  - [Módulo Confección](#módulo-confección)
  - [Módulo Inventario (solo administración web)](#módulo-inventario-solo-administración-web)
- [Administración web (`ferreteria_adminweb`)](#administración-web-ferreteria_adminweb)
- [Base de Datos (PostgreSQL)](#base-de-datos-postgresql)
- [Facturación Electrónica DTE](#facturación-electrónica-dte)
- [UX/UI — Diseño para Personas Mayores](#uxui--diseño-para-personas-mayores)
- [Stack Tecnológico](#stack-tecnológico)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Instalación y Configuración](#instalación-y-configuración)
- [Roadmap por fases](#roadmap-por-fases)
- [Repositorios relacionados](#repositorios-relacionados)
- [Reglas de Negocio Críticas](#reglas-de-negocio-críticas)

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

FlexoCable son **tres repositorios** con responsabilidades separadas. La base de datos PostgreSQL es **una sola estructura** (UUID, esquemas `public` / `purchasing` / `sales` / `dte` / `fiscal` / `hr` / `system`); las diferencias entre caja y administración se resuelven en **código**, no con tablas distintas por tecnología.

**Fuente de verdad del esquema (v3.0):** `ferreteria_backend/prisma/schema.prisma`. WPF (EF Core) y el API Node consumen la misma BD.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     FLEXOCABLE SV — ARQUITECTURA                         │
├─────────────────────────────┬────────────────────────────────────────────┤
│  CAJA (C# WPF)              │  ADMINISTRACIÓN (Node + Next.js)          │
│  FlexoCableSV.PuntoVenta    │  ferreteria_backend  +  ferreteria_adminweb│
│                             │                                            │
│  • Pantalla táctil          │  • Dashboard, KPIs, reportes               │
│  • Caja + DTE + impresión   │  • RRHH: empleados, expediente, PINs       │
│  • Confección (órdenes)     │  • Planilla quincenal/mensual/semanal      │
│  • PIN de empleado (caja)   │  • Inventario admin (entradas, ajustes)    │
│  • Consulta stock (lectura) │  • Login admin (system.WebUsers) + JWT     │
│                             │                                            │
│  Stack: C# + WPF + EF Core  │  API: Node 22 + Express 5 + Prisma 6       │
│                             │  UI: Next.js 15 + React 19 + Tailwind 4    │
└──────────────┬──────────────┴────────────────────┬─────────────────────┘
               │                                     │
               │         ┌───────────────────────────┘
               │         │  adminweb → HTTP → backend (no Prisma en browser)
               ▼         ▼
        ┌──────────────────────────────────────┐
        │         POSTGRESQL (esquema único)    │
        │  PKs/FKs: UUID (gen_random_uuid())    │
        │  public/ purchasing/ sales/ dte/       │
        │  fiscal/ hr/ system/                   │
        └──────────────────┬───────────────────┘
                           │
                ┌──────────▼──────────┐
                │  MINISTERIO DE      │
                │  HACIENDA (API DTE) │
                └─────────────────────┘
```

### Desarrollo local vs producción

| Entorno | Caja WPF | Admin web | Base de datos |
|---|---|---|---|
| **Local (ahora)** | PC de desarrollo | Aún no implementado | PostgreSQL en Docker (`ferreteria_backend/docker-compose.yml`, puerto `55432`) |
| **Producción (después)** | PC en sucursal | Navegador → API en servidor | Supabase PostgreSQL (mismo esquema) |

La caja **no** implementa módulos de planilla, RRHH ni inventario administrativo. Eso vive **solo** en `ferreteria_backend` + `ferreteria_adminweb` (Node/Next), según `docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md`.

### Qué va en cada repositorio

| Repositorio | Tecnología | Responsabilidad |
|---|---|---|
| `erp_ferreteria` / `FlexoCableSV.PuntoVenta` | C# WPF, EF Core | Caja, confección, DTE, impresión, PIN |
| [`ferreteria_backend`](../ferreteria_backend/README.md) | Node.js, Express, Prisma | API REST: empleados, planilla, compras, libros IVA, BI, Excel/PDF |
| [`ferreteria_adminweb`](../ferreteria_adminweb/README.md) | Next.js | UI administrativa; consume **solo** la API Node |

**Principios arquitectónicos:**

| Principio | Implementación |
|---|---|
| Esquema BD único | Misma estructura PostgreSQL (UUID) para WPF y admin; referencia funcional: `beraka-core-api` |
| Sin admin en C# | No hay WebApp ni login administrativo en el proyecto WPF |
| Caja operativa | WPF escribe ventas/DTE/caja; lee catálogo, empleados (PIN) y stock |
| Admin centralizado | CRUD empleados, planilla, bancos, documentos, ajustes de inventario → **solo Node** |
| UX senior-friendly | Botones ≥90×90px, tipografía 16pt+, solo TAP en WPF |

---

## App de Escritorio (C# WPF)

### Flujo de Navegación e Inicio

La pantalla de inicio **no es un login tradicional**. Es una pantalla de selección de módulo con dos botones grandes que determinan el rol del usuario:

```
┌─────────────────────────────────────────┐
│          FLEXOCABLE EL SALVADOR         │
│              [Logo]                     │
│                                         │
│   ┌──────────────┐  ┌──────────────┐   │
│   │              │  │              │   │
│   │     CAJA     │  │ CONFECCIONES │   │
│   │              │  │              │   │
│   └──────────────┘  └──────────────┘   │
│                                         │
└─────────────────────────────────────────┘
```

El sistema se instala igual en todas las PCs. La pantalla de inicio presenta ambos caminos; cada usuario ingresa al módulo que corresponde a su función.

**Reglas de acceso por módulo:**

| Módulo (botón inicio) | Permiso en BD | PIN al entrar | Secciones visibles en sidenav |
|---|---|---|---|
| **VENTAS** (caja) | `can_cashier = true` | ✅ PIN 4 dígitos | Solo bloque **CAJA** (6 ítems) |
| **CONFECCION** (taller) | `can_sell = true` | ✅ PIN 4 dígitos | Solo bloque **CONFECCION** (3 ítems) |

El panel lateral **no muestra ambos bloques a la vez**. La visibilidad depende del módulo elegido en inicio y validado con PIN.

**Ítems del sidenav — módulo CAJA (`can_cashier`):**

| Botón sidenav | Vista | Descripción |
|---|---|---|
| Consultar Stock | `ConsultarStockView` | Búsqueda rápida de inventario (solo lectura) |
| Facturacion | `FacturacionView` | Venta mostrador + DTE |
| Historial Facturas | `HistorialFacturasView` | DTEs emitidos y reimpresión |
| Impresoras | `ImpresorasView` | Configuración de impresión |
| Devoluciones | `DevolucionesView` | Nota de crédito DTE-05 |
| Corte de caja | `CorteCajaView` | Cierre de turno |

**Ítems del sidenav — módulo CONFECCION (`can_sell`):**

| Botón sidenav | Vista | Descripción |
|---|---|---|
| Historial Ventas | `HistorialVentasView` | Órdenes y ventas del taller |
| Ordenes Confeccion | `OrdenesConfeccionView` | Crear órdenes pendientes |
| Ver Codigos | `VerCodigosView` | Catálogo y stock por producto |

| Módulo | Acción | PIN requerido |
|---|---|---|
| Caja (VENTAS) | Ingresar al módulo | ✅ PIN del cajero (`can_cashier`) |
| Caja | Facturar / emitir DTE | ✅ Mismo PIN de la sesión activa (sin segundo PIN) |
| Confección | Ingresar al módulo | ✅ PIN del técnico (`can_sell`) |
| Confección | Crear órdenes, ver historial, consultar códigos | ✅ Misma sesión del ingreso |

**Flujo de autenticación en Caja:**

El módulo de Caja requiere PIN para ingresar. Esto identifica al cajero que opera la máquina y queda registrado en cada factura que emita durante su turno. Al abrir la aplicación por primera vez o al cambiar de turno, se solicita el PIN.

```
[Botón CAJA] → Modal PIN (empleados con can_cashier)
                     │
               PIN correcto → Panel de Caja
                     │
                     ├── Facturación → Crear orden + DTE
                     ├── Historial de Facturas → Consulta y reimpresión
                     ├── Consultar Stock → Vista rápida de inventario
                     ├── Nota de Crédito → Devoluciones / anulaciones (DTE-05)
                     ├── Corte de Caja → Cierre de turno
                     └── Impresoras → Configuración de impresión
```

**Flujo de acceso a Confección:**

El módulo de Confección es de acceso directo sin PIN, pues los técnicos de taller no facturan. Pueden crear órdenes de ensamble, consultar el historial y buscar códigos del catálogo.

```
[Botón CONFECCIONES] → Panel de Confección (sin PIN)
        │
        ├── Historial Ventas → Órdenes registradas
        ├── Órdenes Confección → Nueva orden de ensamble
        └── Ver Códigos → Catálogo de productos y códigos
```

> **Nota de implementación:** `PinWindow` valida el PIN según el módulo elegido: `can_cashier` para VENTAS, `can_sell` para CONFECCION. Tras el ingreso, `MainShellWindow` oculta las secciones del otro módulo vía `NavSections` y `ICurrentSessionService.ActiveModule`.

---

### Módulo Caja

Módulo protegido con PIN — solo personal autorizado (`can_cashier = true`) puede acceder. Agrupa todas las funciones de facturación y cierre financiero.

```
Panel de Caja (requiere PIN para entrar)
        │
        ├── FACTURACIÓN
        │   ├── Crear orden con productos del catálogo
        │   ├── Selección de código con auto-detección de medida
        │   │   • METRO → input decimal (ej: 5.50)
        │   │   • PIEZA → contador +/- entero
        │   │   • KIT   → contador +/- entero
        │   ├── Resumen de orden + total calculado
        │   ├── Facturación DTE:
        │   │   ├── Tipo: 01 Consumidor Final / 03 Crédito Fiscal
        │   │   ├── Forma de pago: Efectivo / Tarjeta / Transferencia
        │   │   ├── Generar y enviar a MH
        │   │   └── Si falla → cola de contingencia
        │   └── Impresión de ticket con QR DTE
        │
        ├── HISTORIAL DE FACTURAS
        │   ├── Consulta de DTEs emitidos por fecha/rango
        │   ├── Reimpresión de tickets
        │   └── Visualización de facturas en contingencia
        │
        ├── CONSULTAR STOCK
        │   ├── Búsqueda rápida de productos
        │   ├── Indicador visual: 🟢 OK / 🟡 Bajo mínimo / 🔴 Agotado
        │   └── Stock en tiempo real (solo consulta)
        │
        ├── NOTA DE CRÉDITO (DTE-05)
        │   ├── Anulación de facturas emitidas
        │   └── Devoluciones
        │
        ├── CORTE DE CAJA
        │   ├── Resumen del turno: ventas, métodos de pago, DTEs
        │   └── Cierre de turno del cajero
        │
        └── IMPRESORAS
            ├── Ver impresoras instaladas en Windows
            ├── Establecer predeterminada para tickets
            ├── Configurar ancho de papel (80mm / 58mm)
            ├── Impresión de prueba
            └── Configurar Ethernet (IP + puerto)
```

**Pantallas del módulo Caja:**

| Vista | Archivo | Propósito | Estado |
|---|---|---|---|
| Facturación | `Views/Caja/FacturacionView.xaml` | Venta mostrador, DTE, pago | UI + servicios parciales |
| Historial de Facturas | `Views/Caja/HistorialFacturasView.xaml` | Consulta y reimpresión de DTEs | UI shell |
| Consultar Stock | `Views/Caja/ConsultarStockView.xaml` | Vista rápida de inventario | Conectado a `InventoryService` |
| Nota de Crédito | `Views/Caja/DevolucionesView.xaml` | Devoluciones DTE-05 | UI shell |
| Corte de Caja | `Views/Caja/CorteCajaView.xaml` | Cierre de turno | UI shell |
| Impresoras | `Views/Caja/ImpresorasView.xaml` | Configuración de impresión | UI shell |
| PIN | `Views/PIN/PinWindow.xaml` | Modal de autenticación cajero | ✅ `PinAuthService` + bcrypt |

---

### Módulo Confección

Acceso directo sin PIN. Orientado a los técnicos de taller que fabrican cables custom. No manejan facturación ni efectivo.

### Flujo de confección (taller)

Las órdenes pendientes del taller son una consulta sobre `sales."Orders"` con `orderType = 'ORDEN_CONFECCION'` y `status = 'PENDIENTE'` — no hay tabla separada de cola.

```
Cliente llega → crear Order PENDIENTE (datos cliente opcionales)
    → técnicos trabajan (orden sigue PENDIENTE, sin descontar stock)
    → cliente regresa → cajera factura desde bandeja pendientes
    → Order pasa a COMPLETADA + DTE + descuento inventario
```

Si el cliente no proporciona datos, `customerId` apunta al registro sistema **"Consumidor Final"** (`system.Settings.DefaultCustomerId`).

```
Panel de Confección (sin PIN)
        │
        ├── HISTORIAL VENTAS
        │   ├── Tabla de órdenes registradas
        │   └── Resumen de ventas del día
        │
        ├── ÓRDENES CONFECCIÓN
        │   ├── Nueva orden de ensamble (queda PENDIENTE)
        │   │   ├── Fecha/hora: automática
        │   │   ├── Técnico: empleado activo con can_sell
        │   │   ├── Aplicación: catálogo VT/RP
        │   │   └── Cliente: nombre/teléfono opcionales
        │   ├── Agregar códigos con cantidades
        │   └── Guardar orden pendiente (sin facturar)
        │
        └── VER CÓDIGOS
            ├── Catálogo completo de productos
            ├── Búsqueda por código o descripción
            ├── Auto-detección de tipo de medida
            └── Stock actual por producto
```

**Pantallas del módulo Confección:**

| Vista | Archivo | Propósito | Estado |
|---|---|---|---|
| Historial de Ventas | `Views/Confeccion/HistorialVentasView.xaml` | Órdenes y ventas completadas | UI + `OrderService` parcial |
| Órdenes Confección | `Views/Confeccion/OrdenesConfeccionView.xaml` | Crear y listar órdenes pendientes | UI + `OrderService` parcial |
| Ver Códigos | `Views/Confeccion/VerCodigosView.xaml` | Búsqueda de catálogo | Conectado a `InventoryService` |

---

### Módulo Inventario (solo administración web)

El control **completo** de inventario (entradas, ajustes, alertas, reconciliación) se maneja desde **`ferreteria_adminweb`** vía **`ferreteria_backend`** (Node.js). La app de escritorio solo tiene **consulta rápida** de stock en Caja (`Consultar Stock`). Ver [Administración web](#administración-web-ferreteria_adminweb).

**Reglas de inventario:**

| Regla | Descripción |
|---|---|
| R-INV-01 | Todo producto tiene tipo de medida fija: METRO, PIEZA, KIT, PESO |
| R-INV-02 | Cables se venden por metros con 2 decimales |
| R-INV-03 | Piezas/kits son unidades enteras |
| R-INV-04 | Stock no puede quedar negativo (validación antes de guardar) |
| R-INV-05 | Alerta automática cuando stock ≤ mínimo |
| R-INV-06 | Movimientos son inmutables — no se borran, solo se registran correcciones |
| R-INV-07 | Ajustes manuales requieren motivo obligatorio + rol autorizado (vía adminweb) |

---

## Administración web (`ferreteria_adminweb`)

Aplicación web separada (Next.js) documentada en [`../ferreteria_adminweb/README.md`](../ferreteria_adminweb/README.md). Se comunica **únicamente** con [`ferreteria_backend`](../ferreteria_backend/README.md) por HTTP (`/api/v1/...`).

> **Importante:** La administración (empleados, planilla, compras, libros IVA, BI) **no se desarrolla en C#**. El proyecto WPF no incluye login web ni módulos de RRHH.

### Módulos administrativos (Node + Next)

| Módulo | Backend Node | Adminweb | Fase |
|---|---|---|---|
| Dashboard BI (KPIs) | `dashboard/` | `/dashboard` | 11 |
| Empleados y PINs | `employees/` | `/empleados` | 8 |
| Expediente (bancos, documentos, ficha PDF) | `employee-*` | `/empleados/[id]/...` | 8–10 |
| Clientes fiscal (CF/CCF) | `customers/` | `/clientes` | 8 |
| Planilla quincenal/mensual/semanal | `payroll-runs/` | `/planilla/...` | 10 |
| Aguinaldo, vacaciones, liquidaciones | `aguinaldo/`, `leave-requests/`, … | `/planilla/...` | 10b–10c |
| Inventario administrativo | `inventory/` | `/inventario` | 9 |
| Compras, proveedores, Kardex valorado | `purchase-orders/`, `suppliers/` | `/compras/...` | 9b |
| Libros de IVA | `fiscal/iva-reports/` | `/fiscal/libros-iva` | 10d |
| Import/export Excel | `imports/`, `reports/` | `/importaciones`, `/reportes` | 9–10 |

Referencia funcional del motor de planilla: `beraka-core-api` (módulos `payroll-runs`, `employees`, etc.).

### Autenticación administrativa

| Aspecto | Detalle |
|---|---|
| Tabla | `system.WebUsers` (username, email, `PasswordHash` bcrypt, rol) |
| API | `POST /api/v1/auth/login` en **ferreteria_backend** |
| Token | JWT (sesión admin); roles: `ADMIN`, `ACCOUNTANT`, `OWNER` |
| WPF | **No usa** `WebUsers` — la caja autentica con PIN en `hr.Employees` |

> El administrador crea empleados y asigna PINs desde **adminweb** (API Node). El hash queda en `hr.Employees.PinHash` y la caja WPF **solo valida** ese PIN; nunca crea usuarios web ni gestiona planilla.

---

## Base de Datos (PostgreSQL)

### Esquema único (UUID)

Todos los identificadores de negocio usan **`UUID`** (`gen_random_uuid()`), alineado a la referencia Beraka. WPF (EF Core) y el backend (Prisma) consumen **el mismo esquema**; cualquier adaptación es en la capa de aplicación (`Guid` en C#, `String @db.Uuid` en Prisma).

Los nombres de tablas/columnas en PostgreSQL y modelos C# están en **inglés** (`sales`, `hr`, `system`, etc.). La UI y los datos de negocio siguen en español.

| Esquema | Tablas principales | Quién escribe |
|---|---|---|
| `public` | `Products`, `Customers`, `InventoryMovements`, `StockAlerts` | Admin: entradas/ajustes (Node). Caja: descuento por venta (WPF) |
| `purchasing` | `Suppliers`, `PurchaseOrders`, `PurchaseOrderDetails` | Solo admin (Node) — Fase 9b |
| `sales` | `Orders`, `OrderDetails`, `CashSessions`, `Payments` | WPF (caja) |
| `dte` | `DteConfig`, `DteIssued`, `DteContingency` | WPF (caja) |
| `fiscal` | `IvaReports` | Solo admin (Node) — Fase 10d |
| `hr` | `Employees`, `PayrollPeriods`, `PayrollRuns`, `PayrollDetails`, … | Admin (Node): RRHH y planilla. WPF: solo lectura empleado/PIN |
| `system` | `Settings`, `Printers`, `WebUsers`, `AuditLog` | `WebUsers`: solo admin Node. `Printers`: WPF. Resto según módulo |

Detalle completo de planilla/RRHH: `docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md` (sección 17).

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

Los PINs se asignan y cambian desde **`ferreteria_adminweb`** (API Node). La app WPF **solo valida** — no crea empleados, no gestiona planilla ni usuarios `WebUsers`.

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
1. Cajero completa venta → orden pasa a `COMPLETADA`
2. Sistema genera JSON DTE (emisor, receptor, items, totales, IVA 13%)
3. Firma con certificado `.p12`
4. POST a API MH → recibe sello de recepción → `MhStatus = PROCESADO`
5. Imprime ticket con QR
6. Si falla MH por red: `MhStatus = CONTINGENCIA` en `dte.DteIssued`, cola de reintento

**Estados alineados al esquema:**

| Entidad | Campo | Valores | Nota |
|---|---|---|---|
| `sales.Orders` | `Status` | `PENDIENTE`, `COMPLETADA`, `CANCELADA` | La contingencia fiscal **no** es estado de orden |
| `dte.DteIssued` | `MhStatus` | `PENDIENTE`, `PROCESADO`, `RECHAZADO`, `CONTINGENCIA` | Una orden `COMPLETADA` puede tener DTE en contingencia |

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

### App de Escritorio — Caja (`FlexoCableSV.PuntoVenta`)

| Tecnología | Versión | Propósito |
|---|---|---|
| C# | 12+ | Lenguaje principal |
| WPF (.NET) | `net10.0-windows` | Interfaz táctil |
| Entity Framework Core | 10.x | ORM → PostgreSQL (`Guid` / UUID) |
| Npgsql | 10.x | Driver PostgreSQL |
| BCrypt.Net-Next | 4.x | Validación de PIN |
| Newtonsoft.Json | 13.x | JSON DTE |
| QRCoder | 1.4+ | QR en tickets |
| ESC/POS.NET | 1.0+ | Impresión térmica |

**Fuera de alcance WPF:** planilla, RRHH CRUD, `WebUsers`, inventario admin, reportes Excel — todo en Node/Next.

### Administración — API (`ferreteria_backend`)

| Tecnología | Versión | Propósito |
|---|---|---|
| Node.js | 22+ | Runtime |
| Express | 5 | API REST `/api/v1/` |
| Prisma | 6 | ORM PostgreSQL (esquema único UUID) |
| Zod | Última | Validación HTTP |
| JWT + bcrypt | — | Auth admin (`WebUsers`) |
| ExcelJS + PDFKit | — | Export planilla (portado de Beraka) |

### Administración — UI (`ferreteria_adminweb`)

| Tecnología | Versión | Propósito |
|---|---|---|
| Next.js | 15 | Framework React |
| React | 19 | UI |
| TypeScript | 5.x | Lenguaje |
| Tailwind CSS | 4 | Estilos |
| shadcn / Radix | — | Componentes |

El frontend **no** conecta a PostgreSQL; solo llama a `ferreteria_backend`.

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
Ferreteria/
│
├── erp_ferreteria/                      ← Repo WPF (caja) — este README
│   ├── FlexoCableSV.PuntoVenta/
│   │   ├── Views/                       ← Shell, Inicio, Caja, Confección, PIN
│   │   ├── Models/                      ← EF Core (dominio operativo)
│   │   ├── Services/                    ← PIN, inventario, órdenes, DTE, impresión
│   │   ├── Data/FlexoDbContext.cs
│   │   └── Config/appsettings.json
│   ├── tools/FlexoCable.DbApply/        ← Legacy/diagnóstico (no fuente de verdad)
│   └── docs/
│       └── FLEXOCABLE_PLAN_FINALIZACION_APP.md
│
├── ferreteria_backend/                  ← API Node — ver README propio
│   ├── prisma/schema.prisma             ← Fuente de verdad BD v3.0
│   ├── prisma/seed.ts
│   ├── docker-compose.yml
│   └── src/                             ← (pendiente Fase 8)
│
└── ferreteria_adminweb/                 ← UI Next.js — ver README propio
    └── src/app/                         ← (pendiente Fase 8)
```

### Servicios WPF (`Services/`)

| Servicio | Archivo | Estado | Fase |
|---|---|---|---|
| PIN / autenticación caja | `PinAuthService`, `PinAttemptService` | ✅ Implementado | 1 |
| Sesión de cajero | `CurrentSessionService` | ✅ Implementado | 1 |
| Conectividad | `ConnectivityService` | ✅ Implementado | 1 |
| Auditoría | `AuditService` | ✅ Implementado | 1 |
| Inventario (consulta + descuento) | `InventoryService` | ✅ Implementado | 2 |
| Órdenes y ventas | `OrderService` | ✅ Implementado | 3 |
| DTE | `DTEService` | 🔲 Vacío | 4 |
| Impresión | `ImpresionService` | 🔲 Vacío | 5 |
| Corte de caja | — | 🔲 Pendiente | 6 |
| Configuración | `ConfigService` | Parcial | — |

**Modelos WPF (`Models/`):** dominio de caja — `Sales`, `Dte`, `Public` (catálogo/stock), `Hr.Employee` (PIN y permisos). **No** incluir `WebUser`, planilla ni CRUD RRHH en WPF.

---

## Instalación y Configuración

### Requisitos

**App de Escritorio:**

| Requisito | Versión mínima |
|---|---|
| Windows 10 | Versión 1909 o superior |
| .NET SDK | 10.0+ (`net10.0-windows`) |
| Visual Studio 2022 | Community Edition |
| PostgreSQL | 14+ (o Docker vía backend) |

**Administración web (cuando exista):**

| Requisito | Versión mínima |
|---|---|
| Node.js | 22+ |
| npm | 10+ |
| Docker Desktop | Para PostgreSQL local |

---

### 1. Base de Datos local (desarrollo)

```bash
# Desde ferreteria_backend/
docker compose up -d
cp .env.example .env
npm install
npm run db:push
npm run db:seed
```

`FlexoCableSV.PuntoVenta/Config/appsettings.json` apunta al PostgreSQL local (puerto **55432** por defecto).

`tools/FlexoCable.DbApply` queda como herramienta legacy/diagnóstico. **No** es la fuente de verdad: usar Prisma (`ferreteria_backend/prisma/schema.prisma`).

**Empleados demo (solo desarrollo)** — ver [`ferreteria_backend/README.md`](../ferreteria_backend/README.md):

| PIN | Rol |
|-----|-----|
| `1234` | Administrador / caja |
| `5678` | Técnico confección |
| `0000` | Caja demo |

### 2. App de Escritorio (WPF)

```bash
cd erp_ferreteria/FlexoCableSV.PuntoVenta
dotnet restore
dotnet build
dotnet run
```

### 3. Administración web (cuando exista)

```bash
# API
cd ferreteria_backend
npm install
npm run db:migrate
npm run dev

# UI
cd ferreteria_adminweb
npm install
npm run dev
```

La adminweb usa `NEXT_PUBLIC_API_URL` hacia el backend Node, **no** una conexión directa a la BD desde el navegador.

1. Obtener NIT emisor y certificado `.p12` del Ministerio de Hacienda
2. Actualizar tabla `dte.dte_config` con datos reales del emisor
3. Subir certificado `.p12` al servidor en ruta segura
4. Probar con ambiente `00` (pruebas) antes de cambiar a `01` (producción)

### 5. Empleados y PINs (solo vía admin web — Node)

Cuando `ferreteria_adminweb` esté disponible:

1. Crear empleado vía API Node (`employees/`)
2. Activar `can_sell` / `can_cashier` según rol
3. Asignar PIN — se guarda en `hr.Employees.PinHash` (bcrypt)
4. La caja WPF valida ese PIN; **no** hay pantalla de alta de empleados en C#

---

## Estado del desarrollo

Resumen alineado al plan v3.0 (`docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md`):

### WPF — vistas existentes

| Área | Vista | Estado |
|---|---|---|
| Shell | `MainShellWindow` | Navegación ✅ — falta inyectar sesión completa en todas las vistas |
| Inicio | `InicioWindow` | UI ✅ |
| Seguridad | `PinWindow` | ✅ bcrypt real (`PinAuthService`) |
| Caja | `FacturacionView` | UI ✅ — integración DTE pendiente |
| Caja | `HistorialFacturasView` | UI shell |
| Caja | `ConsultarStockView` | ✅ conectado a inventario |
| Caja | `CorteCajaView` | UI shell |
| Caja | `DevolucionesView` | UI shell |
| Caja | `ImpresorasView` | UI shell |
| Confección | `HistorialVentasView` | UI + servicios parciales |
| Confección | `OrdenesConfeccionView` | UI + servicios parciales |
| Confección | `VerCodigosView` | ✅ conectado a inventario |

`dotnet build` compila con 0 errores. Los flujos de negocio completos (DTE, corte, devoluciones) aún no están operativos en producción.

### Base de datos y repos administrativos

| Componente | Estado |
|---|---|
| Prisma schema v3.0 (7 esquemas) | ✅ |
| Seeds idempotentes | ✅ |
| Docker PostgreSQL local | ✅ |
| API Express (`src/`) | 🔲 Fase 8 |
| Adminweb Next.js | 🔲 Fase 8 |

### Decisiones MVP confirmadas

| Tema | Decisión |
|---|---|
| CxC (cuentas por cobrar) | Descartada — ventas al contado |
| Multisucursal | Pospuesta |
| Modo offline MVP | Cache catálogo + cola local (Fase 1) |
| PIN al facturar | Solo cajero de sesión |
| Planilla | Quincenal principal; referencia Beraka |

---

## Roadmap por fases

Roadmap completo según `FLEXOCABLE_PLAN_FINALIZACION_APP.md`. Las fases WPF (0–7) preceden o corren en paralelo a la administración web (8–11).

### Caja WPF

| Fase | Objetivo | Estado |
|---|---|---|
| **0** | Schema Prisma v3.0, seeds, alinear EF Core | 🟡 En progreso |
| **0b** | Esquema `hr` Periodo+Corrida (planilla Beraka) | 🟡 Schema listo |
| **1** | PIN real, sesión cajero, conectividad, auditoría | 🟡 Parcial |
| **2** | Catálogo e inventario transaccional | 🟡 `InventoryService` listo |
| **3** | Órdenes, ventas, pagos, confección | 🟡 `OrderService` listo |
| **4** | DTE 01/03/05, firma, MH, contingencia | 🔲 Pendiente |
| **5** | Impresión tickets con QR | 🔲 Pendiente |
| **6** | Corte de caja y turnos | 🔲 Pendiente |
| **7** | Devoluciones y nota de crédito DTE-05 | 🔲 Pendiente |

### Administración web

| Fase | Objetivo | Repos | Estado |
|---|---|---|---|
| **8** | API base, auth JWT, CRUD empleados/clientes/catálogo | backend + adminweb | 🔲 Pendiente |
| **9** | Inventario administrativo (entradas, ajustes, alertas) | backend + adminweb | 🔲 Pendiente |
| **9b** | Compras, proveedores, Kardex valorado, costo promedio | backend + adminweb | 🔲 Pendiente |
| **10** | Planilla quincenal + Excel/PDF (port Beraka) | backend | 🔲 Pendiente |
| **10b** | Aguinaldo y vacaciones | backend + adminweb | 🔲 Pendiente |
| **10c** | Liquidaciones | backend + adminweb | 🔲 Pendiente |
| **10d** | Libros de IVA | backend + adminweb | 🔲 Pendiente |
| **11** | Dashboard BI (ventas, inventario, compras, RRHH) | backend + adminweb | 🔲 Pendiente |

---

## Repositorios relacionados

| Repositorio | README | Contenido |
|---|---|---|
| `ferreteria_backend` | [`../ferreteria_backend/README.md`](../ferreteria_backend/README.md) | API Node, Prisma, esquema BD, módulos planificados |
| `ferreteria_adminweb` | [`../ferreteria_adminweb/README.md`](../ferreteria_adminweb/README.md) | UI Next.js, rutas, auth admin, módulos por fase |
| Plan maestro | [`docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md`](docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md) | Especificación completa v3.0 |

---

## Reglas de Negocio Críticas

### Ventas y DTE

| Regla | Descripción |
|---|---|
| R-VTA-01 | Toda venta facturada es inmutable (`Orders.Status = COMPLETADA`, sin edición) |
| R-VTA-02 | El DTE debe confirmarse o quedar en contingencia válida antes de imprimir ticket |
| R-VTA-03 | Si MH no responde, `DteIssued.MhStatus = CONTINGENCIA` con reintento automático |
| R-VTA-04 | El ticket incluye QR con código de generación DTE |
| R-VTA-05 | Anulación requiere Nota de Crédito (DTE-05) — no se borran registros |
| R-VTA-06 | El PIN de la sesión de caja identifica al cajero en cada factura (sin segundo PIN de técnico) |
| R-VTA-07 | Stock se descuenta solo al completar la venta, no al crear orden de confección pendiente |
| R-VTA-08 | `customerId` nunca es null en confección — usar Consumidor Final si no hay datos del cliente |

### Planilla El Salvador

Motor de cálculo a portar desde `beraka-core-api` (AFP/ISSS solo sobre salario ordinario):

| Concepto | Regla |
|---|---|
| AFP empleado | 7.25% sobre salario ordinario del periodo |
| AFP patronal | 7.75% sobre salario ordinario |
| ISSS empleado | 3% sobre salario ordinario, tope $1,000/mes prorrateado |
| ISSS patronal | 7.5% sobre misma base ISSS |
| ISR | Tabla progresiva `IsrBrackets` por año y tipo de periodo |
| Horas extra diurnas/feriadas | ×2.0 del valor hora ordinaria |
| Horas extra nocturnas | ×2.5 |
| Honorarios | Retención ISR 10%; sin AFP/ISSS |

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
