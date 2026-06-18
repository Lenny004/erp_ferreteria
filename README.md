# FlexoCable SV — Sistema Integrado de Punto de Venta y Gestión

> **Código interno:** FCSV-2026 · **Versión:** 1.0.0-MVP · **Inicio:** Mayo 2026  
> **Cliente:** FlexoCable El Salvador, S.A. de C.V. · **Matriz:** FlexoCable Panamá.
> **Ubicación:** San Salvador, El Salvador.

Sistema integral para la sucursal salvadoreña de FlexoCable: punto de venta táctil con facturación electrónica DTE, control de inventario y gestión de planillas. Diseñado específicamente para personal mayor con experiencia tecnológica limitada.

---

## Índice

- [Contexto del Negocio](#contexto-del-negocio)
- [Arquitectura del Sistema](#arquitectura-del-sistema)
- [App de Escritorio (C# WPF)](#app-de-escritorio-c-wpf)
  - [Flujo de Navegación e Inicio](#flujo-de-navegación-e-inicio)
  - [Módulo Caja](#módulo-caja)
  - [Módulo Confección](#módulo-confección)
  - [Módulo Inventario (solo administración web)](#módulo-inventario-solo-administración-web)
- [Administración web (`FlexoCable-adminweb`)](#administración-web-flexocable-adminweb)
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

FlexoCable son **tres repositorios** con responsabilidades separadas. La base de datos PostgreSQL es **una sola estructura** (UUID, esquemas `public` / `sales` / `dte` / `hr` / `system`); las diferencias entre caja y administración se resuelven en **código**, no con tablas distintas por tecnología.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     FLEXOCABLE SV — ARQUITECTURA                         │
├─────────────────────────────┬────────────────────────────────────────────┤
│  CAJA (C# WPF)              │  ADMINISTRACIÓN (Node + Next.js)          │
│  FlexoCableSV.PuntoVenta    │  FlexoCable-backend  +  FlexoCable-adminweb│
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
        │  public/ sales/ dte/ hr/ system/      │
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
| **Local (ahora)** | PC de desarrollo | Aún no implementado | PostgreSQL en Docker (`FlexoCable-backend/docker-compose.yml`, puerto `55432`) |
| **Producción (después)** | PC en sucursal | Navegador → API en servidor | Supabase PostgreSQL (mismo esquema) |

La caja **no** implementa módulos de planilla, RRHH ni inventario administrativo. Eso vive **solo** en `FlexoCable-backend` + `FlexoCable-adminweb` (Node/Next), según `docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md`.

### Qué va en cada repositorio

| Repositorio | Tecnología | Responsabilidad |
|---|---|---|
| `FlexoCable` / `FlexoCableSV.PuntoVenta` | C# WPF, EF Core | Caja, confección, DTE, impresión, PIN |
| `FlexoCable-backend` | Node.js, Express, Prisma | API REST: empleados, planilla, inventario admin, reportes, Excel/PDF |
| `FlexoCable-adminweb` | Next.js | UI administrativa; consume **solo** la API Node |

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

| Módulo | Acción | PIN requerido |
|---|---|---|
| Caja | Ingresar al módulo | ✅ PIN del cajero |
| Caja | Facturar / emitir DTE | ✅ Mismo PIN del ingreso |
| Confección | Ingresar al módulo | ❌ No |
| Confección | Crear órdenes, ver historial, consultar códigos | ❌ No |

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

> **Nota de implementación:** `PinWindow.xaml` es un modal reutilizable que recibe el `employee_id` como parámetro, consulta `hr.employees.pin_hash` y valida con bcrypt. Se usa en el ingreso a Caja y al facturar.

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

| Ventana | Archivo | Propósito |
|---|---|---|
| Facturación | `FacturarWindow.xaml` | DTE, envío MH, sello, impresión |
| Historial de Facturas | *(nueva)* | Consulta y reimpresión de DTEs |
| Consultar Stock | *(nueva, solo lectura)* | Vista rápida de inventario |
| Nota de Crédito | *(nueva)* | Anulaciones DTE-05 |
| Corte de Caja | *(nueva)* | Cierre de turno |
| Impresoras | `ImpresorasWindow.xaml` | Configuración de impresión |
| PIN | `PinWindow.xaml` | Modal reutilizable (recibe `employee_id`) |

---

### Módulo Confección

Acceso directo sin PIN. Orientado a los técnicos de taller que fabrican cables custom. No manejan facturación ni efectivo.

```
Panel de Confección (sin PIN)
        │
        ├── HISTORIAL VENTAS
        │   ├── Tabla de órdenes registradas
        │   └── Resumen de ventas del día
        │
        ├── ÓRDENES CONFECCIÓN
        │   ├── Nueva orden de ensamble
        │   │   ├── Fecha/hora: automática
        │   │   ├── Técnico: Select (empleados activos)
        │   │   ├── Aplicación: Select (VT-01, VT-02, VT-03, RP-01)
        │   │   └── Cliente: Input con teclado virtual
        │   ├── Agregar códigos con cantidades
        │   └── Guardar borrador
        │
        └── VER CÓDIGOS
            ├── Catálogo completo de productos
            ├── Búsqueda por código o descripción
            ├── Auto-detección de tipo de medida
            └── Stock actual por producto
```

**Pantallas del módulo Confección:**

| Ventana | Archivo | Propósito |
|---|---|---|
| Historial de Ventas | `VentasWindow.xaml` | Lista de órdenes registradas |
| Órdenes Confección | `NuevaOrdenWindow.xaml` | Formulario de orden de ensamble |
| Ver Códigos | `SeleccionCodigoWindow.xaml` | Búsqueda y detalle de productos |

---

### Módulo Inventario (solo administración web)

El control **completo** de inventario (entradas, ajustes, alertas, reconciliación) se maneja desde **`FlexoCable-adminweb`** vía **`FlexoCable-backend`** (Node.js). La app de escritorio solo tiene **consulta rápida** de stock en Caja (`Consultar Stock`). Ver [Administración web](#administración-web-flexocable-adminweb).

**Reglas de inventario:**

| Regla | Descripción |
|---|---|
| R-INV-01 | Todo producto tiene tipo de medida fija: METRO, PIEZA, KIT, PESO |
| R-INV-02 | Cables se venden por metros con 2 decimales |
| R-INV-03 | Piezas/kits son unidades enteras |
| R-INV-04 | Stock no puede quedar negativo (validación antes de guardar) |
| R-INV-05 | Alerta automática cuando stock ≤ mínimo |
| R-INV-06 | Movimientos son inmutables — no se borran, solo se registran correcciones |
| R-INV-07 | Ajustes manuales requieren motivo obligatorio + PIN personal (vía web)

---

## Administración web (`FlexoCable-adminweb`)

Aplicación web separada (Next.js) que **no comparte código con WPF**. Se comunica **únicamente** con `FlexoCable-backend` por HTTP (`/api/v1/...`). No usa Prisma directamente desde el frontend.

> **Importante:** La administración (empleados, planilla, inventario, reportes) **no se desarrolla en C#**. El proyecto WPF no incluye login web ni módulos de RRHH.

### Módulos (implementación en Node + UI en Next)

| Módulo | Backend Node | Adminweb |
|---|---|---|
| Dashboard y KPIs | `reports/`, agregaciones | `/dashboard` |
| Empleados y PINs | `employees/` | `/empleados` |
| Expediente (bancos, documentos, ficha PDF) | `employee-bank-accounts/`, `employee-documents/` | `/empleados/[id]/...` |
| Planilla (quincenal, mensual, semanal) | `payroll-periods/`, `payroll-runs/` | `/planilla/...` |
| Aguinaldo, vacaciones, liquidaciones | `aguinaldo/`, `leave-requests/`, `employee-terminations/` | `/planilla/...` |
| Inventario administrativo | `inventory/` | `/inventario` |
| Reportes y exportación Excel/PDF | `payroll-exports.service.ts` (portado de Beraka) | `/reportes` |

Referencia funcional del motor de planilla: `beraka-core-api` (módulos `payroll-runs`, `employees`, etc.).

### Autenticación administrativa

| Aspecto | Detalle |
|---|---|
| Tabla | `system.WebUsers` (username, email, `PasswordHash` bcrypt, rol) |
| API | `POST /api/v1/auth/login` en **FlexoCable-backend** |
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
| `public` | `Products`, `InventoryMovements`, `StockAlerts`, … | Admin: entradas/ajustes (Node). Caja: descuento por venta (WPF) |
| `sales` | `Orders`, `OrderDetails`, `CashSessions`, `Payments` | WPF (caja) |
| `dte` | `DteConfig`, `DteIssued`, `DteContingency` | WPF (caja) |
| `hr` | `Employees`, `PayrollPeriods`, `PayrollRuns`, `PayrollDetails`, … | Admin (Node): RRHH y planilla. WPF: solo lectura de empleado/PIN |
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

Los PINs se asignan y cambian desde **`FlexoCable-adminweb`** (API Node). La app WPF **solo valida** — no crea empleados, no gestiona planilla ni usuarios `WebUsers`.

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

### Administración — API (`FlexoCable-backend`)

| Tecnología | Versión | Propósito |
|---|---|---|
| Node.js | 22+ | Runtime |
| Express | 5 | API REST `/api/v1/` |
| Prisma | 6 | ORM PostgreSQL (esquema único UUID) |
| Zod | Última | Validación HTTP |
| JWT + bcrypt | — | Auth admin (`WebUsers`) |
| ExcelJS + PDFKit | — | Export planilla (portado de Beraka) |

### Administración — UI (`FlexoCable-adminweb`)

| Tecnología | Versión | Propósito |
|---|---|---|
| Next.js | 15 | Framework React |
| React | 19 | UI |
| TypeScript | 5.x | Lenguaje |
| Tailwind CSS | 4 | Estilos |
| shadcn / Radix | — | Componentes |

El frontend **no** conecta a PostgreSQL; solo llama a `FlexoCable-backend`.

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
FlexoCable Sistema/
│
├── FlexoCable/                          ← Repo WPF (caja)
│   ├── FlexoCableSV.PuntoVenta/       ← App escritorio C#
│   │   ├── Squema.sql                 ← Esquema base + seeds
│   │   ├── Views/                     ← Caja, Confección, PIN
│   │   ├── Models/                    ← EF Core (solo dominio operativo)
│   │   ├── Services/                  ← DTE, inventario venta, PIN, impresión
│   │   └── Data/FlexoDbContext.cs
│   ├── tools/FlexoCable.DbApply/      ← Aplica Squema.sql + migraciones
│   └── docs/                          ← Plan, estándares, manuales
│
├── FlexoCable-backend/                ← API Node (RRHH, planilla, inventario admin)
│   ├── database/migrations/           ← SQL versionado (Fase 0 / 0b)
│   ├── docker-compose.yml             ← PostgreSQL local desarrollo
│   └── src/                           ← (pendiente) Express + Prisma
│
└── FlexoCable-adminweb/               ← UI Next.js (pendiente)
    └── src/app/                       ← Dashboard, empleados, planilla, …
```

**Modelos WPF (`Models/`):** dominio de caja — `Sales`, `Dte`, `Public` (lectura catálogo/stock), `Hr.Employee` (PIN y permisos). **No** incluir lógica de `WebUser`, planilla ni CRUD RRHH en vistas o servicios WPF; esas tablas existen en BD para el backend Node.

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

### 1. Base de Datos local (desarrollo WPF)

```bash
# Desde FlexoCable-backend/
docker compose up -d

# Aplicar Squema.sql + migraciones (desde repo FlexoCable/)
dotnet run --project tools/FlexoCable.DbApply
```

`FlexoCableSV.PuntoVenta/Config/appsettings.json` apunta al PostgreSQL local (puerto `55432` por defecto).

**Empleados demo (solo desarrollo)** — seeds en `Squema.sql`:

| PIN | Rol |
|-----|-----|
| `1234` | Administrador / caja |
| `5678` | Técnico confección |
| `0000` | Caja demo |

### 2. App de Escritorio (WPF)

```bash
cd FlexoCable/FlexoCableSV.PuntoVenta
dotnet restore
dotnet build
dotnet run
```

### 3. Administración web (cuando exista)

```bash
# API
cd FlexoCable-backend
npm install
npm run db:migrate
npm run dev

# UI
cd FlexoCable-adminweb
npm install
npm run dev
```

La adminweb usa `NEXT_PUBLIC_API_URL` hacia el backend Node, **no** una conexión directa a la BD desde el navegador.

1. Obtener NIT emisor y certificado `.p12` del Ministerio de Hacienda
2. Actualizar tabla `dte.dte_config` con datos reales del emisor
3. Subir certificado `.p12` al servidor en ruta segura
4. Probar con ambiente `00` (pruebas) antes de cambiar a `01` (producción)

### 5. Empleados y PINs (solo vía admin web — Node)

Cuando `FlexoCable-adminweb` esté disponible:

1. Crear empleado vía API Node (`employees/`)
2. Activar `can_sell` / `can_cashier` según rol
3. Asignar PIN — se guarda en `hr.Employees.PinHash` (bcrypt)
4. La caja WPF valida ese PIN; **no** hay pantalla de alta de empleados en C#

---

## Roadmap

### Fase 1 — MVP "La Caja Factura" (Semanas 1–6)

| Semana | Entregable | Estado |
|---|---|---|
| 1–2 | Setup BD, catálogo 500+ productos cargado, pantalla de inicio con CAJA / CONFECCIONES | 🔲 Pendiente |
| 3–4 | Módulo Caja: ingreso con PIN, facturación DTE, historial, consulta stock | 🔲 Pendiente |
| 5–6 | Módulo Confección: órdenes de ensamble, historial, ver códigos + impresión ESC/POS | 🔲 Pendiente |

### Fase 2 — Caja Avanzada (Semanas 7–10)

| Semana | Entregable | Estado |
|---|---|---|
| 7–8 | Corte de caja, notas de crédito (DTE-05), reimpresión de tickets | 🔲 Pendiente |
| 9–10 | Configuración de impresoras, multisesión por PIN, refinamiento UX | 🔲 Pendiente |

### Fase 3 — Administración web Node/Next (después de caja estable)

| Entregable | Repos | Estado |
|---|---|---|
| API base + auth JWT (`WebUsers`) | `FlexoCable-backend` | Pendiente |
| CRUD empleados, PINs, expediente | backend + adminweb | Pendiente |
| Planilla quincenal + exportes Beraka | backend | Pendiente |
| Inventario admin + reportes | backend + adminweb | Pendiente |

Plan detallado: `docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md` (Fases 8–10, sección 17).

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
