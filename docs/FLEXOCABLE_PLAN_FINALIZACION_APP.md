# FlexoCable SV - Plan Validado de Finalizacion de la Aplicacion

> **Documento:** FLEXO-PLAN-001  
> **Version:** 3.0  
> **Fecha:** Junio 2026  
> **Estado:** v3.0 — schema Prisma reescrito desde cero; módulos Compras/Kardex, Clientes, Libros IVA y BI incorporados; CxC descartada para MVP; multisucursal pospuesta  
> **Base revisada:** `README.md`, `schema.prisma` v3.0, vistas WPF existentes, modelos EF Core, estandares `FLEXOCABLE_C_CODING_STANDARDS_2026.md`, modulo empleados/planilla de `beraka-core-api` y análisis comparativo propuesta ERP FERRESAL (COT-9520).

---

## 1. Resumen Ejecutivo

El plan original es una buena base conceptual: identifica los bloques correctos de caja, inventario, DTE, impresion, WebApp, planilla y reportes. Sin embargo, para que el sistema funcione en un entorno real y compatible con Supabase, debe reforzarse antes de implementar logica de negocio.

Las correcciones principales son:

- Antes de programar servicios, hay que estabilizar el esquema PostgreSQL y validar compatibilidad con Supabase.
- El flujo de caja necesita tablas y estados que actualmente no estan completos en `Squema.sql`.
- El plan menciona estados como `CERRADA`, `CONTINGENCIA` y `ANULADA`, pero el esquema actual solo permite `PENDIENTE`, `COMPLETADA` y `CANCELADA`.
- El esquema actual usa `DEFAULT "UuidGenerateV4"()`, que no es una funcion valida de PostgreSQL/Supabase. Debe cambiarse a `gen_random_uuid()` o `uuid_generate_v4()` correctamente.
- Los settings seed tienen claves con comillas embebidas, por ejemplo `'"IvaPercentage"'`. Deben normalizarse a `IvaPercentage` o `iva_percentage`.
- No debe asumirse que una app WPF instalada en caja puede usar credenciales privilegiadas directas a Supabase en produccion.
- Las vistas WPF existen, pero casi todas estan solo como UI/shell. Falta integracion real con servicios, validaciones y persistencia.
- El DTE requiere una fase especifica de certificacion, validacion de JSON, firma, token MH, contingencia, QR y pruebas contra el ambiente oficial.
- El esquema `hr` actual **no soporta planilla quincenal ni expediente completo**; requiere refactorizacion Fase 0b antes del modulo administrativo (seccion 17).

Resultado recomendado: primero cerrar la arquitectura de datos y seguridad (**incluyendo RRHH/planilla**), luego implementar inventario y ventas de forma transaccional, despues DTE, impresion y corte de caja, y finalmente crear la WebApp administrativa con planilla, compras, libros IVA y dashboard BI.

**v3.0 — Cambios incorporados del análisis comparativo ERP FERRESAL:**

- **Módulos nuevos agregados al plan:** Compras y Proveedores (Fase 9b), Kardex valorado con costo promedio ponderado (integrado en Fases 2 y 9b), Maestro de Clientes fiscal (Fase 8), Libros de IVA (Fase 10d) y Dashboard BI gerencial (Fase 11).
- **Módulos descartados para MVP:** Cuentas por Cobrar (CxC) — FlexoCable opera principalmente al contado; puede incorporarse en una fase futura si se requiere crédito a clientes.
- **Multisucursal:** descartado para el alcance actual. La arquitectura de BD no impide crecer a multisucursal en el futuro (el campo `CashRegisterCode` en `CashSessions` es el punto de extensión), pero no se planifica ahora.
- **Schema Prisma:** reescrito desde cero (v3.0) con esquemas `public`, `purchasing`, `sales`, `dte`, `fiscal`, `hr`, `system`. Migraciones SQL manuales eliminadas; Prisma gestiona la BD via `prisma migrate dev`.

### 1.1 Decisiones confirmadas por el equipo

| Tema | Decision |
|---|---|
| Infraestructura BD | Supabase hibrido: WPF y administracion comparten PostgreSQL en Supabase |
| PIN al facturar | Solo cajero de sesion (sin segundo PIN del tecnico) |
| .NET objetivo | Mantener `net10.0-windows` en WPF |
| Repos administracion | `FlexoCable-backend` (API) + `FlexoCable-adminweb` (frontend) |
| Integracion Excel | Bidireccional: importar catalogo y entradas; exportar reportes/planilla |
| Modo offline | **MVP robusto:** cache de catalogo + cola local de operaciones desde Fase 1 (seccion 13) |
| Planilla RRHH | **Quincenal como operacion principal** + soporte mensual y semanal; honorarios con retencion 10%; aguinaldo, vacaciones y liquidaciones en primera fase administrativa |
| Modelo de planilla | Reemplazar `hr."Payroll"` simplificado por **Periodo + Corrida** (referencia Beraka); motor de calculo y exportes Excel/PDF portados desde `payroll-exports.service.ts` |
| IDs en BD | **UUID (`gen_random_uuid()`) en todas las tablas de negocio**, misma estructura que Beraka; sin esquemas distintos por app. WPF adapta en EF Core (`Guid`); Prisma en backend Node |
| Bancos | Catalogo **editable** desde adminweb (CRUD), con seed inicial de bancos SV como en Beraka |
| Documentos empleado | Mismos tipos requeridos que Beraka (seed idempotente); modulo administrable |
| **CxC (Cuentas por Cobrar)** | **Descartada para MVP.** FlexoCable opera al contado. El modelo `Customer` existe como base si se requiere credito en el futuro. |
| **Multisucursal** | **Pospuesto indefinidamente.** Sin planificacion activa. `CashRegisterCode` en `CashSessions` es el punto de extension futuro. |
| **Compras y Proveedores** | **Incluido — Fase 9b.** Esquema `purchasing`: `Suppliers`, `PurchaseOrders`, `PurchaseOrderDetails`. Costeo promedio ponderado integrado. |
| **Libros de IVA** | **Incluido — Fase 10d.** Esquema `fiscal`: `IvaReports`. Generados desde `dte.DteIssued` y `purchasing.PurchaseOrders`. |
| **Dashboard BI** | **Incluido — Fase 11.** KPIs de ventas, inventario, compras y planilla. API de agregacion sobre datos existentes. |
| **Schema Prisma** | Reescrito v3.0 desde cero. `prisma migrate dev` gestiona la BD. Migraciones SQL manuales eliminadas. |

---

## 2. Estado Actual del Proyecto

### 2.1 Vistas WPF Existentes

Estas vistas existen fisicamente en `FlexoCableSV.PuntoVenta/Views`:

| Area | Vista | Archivo | Estado actual | Accion requerida |
|---|---|---|---|---|
| Shell | MainShell | `Views/Shell/MainShellWindow.xaml` | Existe navegacion | Inyectar sesion, permisos y servicios |
| Inicio | Inicio | `Views/Inicio/InicioWindow.xaml` | Existe | Ajustar nombres Caja/Confeccion y flujo real |
| Seguridad | PIN | `Views/PIN/PinWindow.xaml` | Existe con PINs hardcoded | Reemplazar por `PinService` y bcrypt |
| Caja | Facturacion | `Views/Caja/FacturacionView.xaml` | UI sin logica real | Conectar productos, orden, pago, DTE e impresion |
| Caja | Historial facturas | `Views/Caja/HistorialFacturasView.xaml` | UI sin logica real | Consultar DTEs, filtros, reimpresion |
| Caja | Consultar stock | `Views/Caja/ConsultarStockView.xaml` | UI sin logica real | Conectar busqueda de inventario |
| Caja | Corte caja | `Views/Caja/CorteCajaView.xaml` | UI sin logica real | Requiere modelo de turnos/cortes |
| Caja | Devoluciones | `Views/Caja/DevolucionesView.xaml` | UI sin logica real | Requiere DTE-05 y relacion con factura original |
| Caja | Impresoras | `Views/Caja/ImpresorasView.xaml` | UI sin logica real | Conectar impresoras Windows/Ethernet |
| Confeccion | Historial ventas | `Views/Confeccion/HistorialVentasView.xaml` | UI sin logica real | Consultar ordenes reales |
| Confeccion | Ordenes confeccion | `Views/Confeccion/OrdenesConfeccionView.xaml` | UI sin logica real | Crear ordenes y detalles reales |
| Confeccion | Ver codigos | `Views/Confeccion/VerCodigosView.xaml` | UI sin logica real | Consultar catalogo real |

Conclusion: no faltan muchas vistas WPF base; falta la capa de aplicacion, servicios, validaciones y persistencia.

### 2.2 Servicios Existentes

| Servicio | Archivo | Estado actual | Prioridad |
|---|---|---|---|
| DTE | `Services/DTEService.cs` | Vacio | Critica |
| Inventario | `Services/InventarioService.cs` | Vacio | Critica |
| Impresion | `Services/ImpresionService.cs` | Vacio | Alta |
| Configuracion | `Services/ConfigService.cs` | Existe | Media |
| PIN | No existe como servicio dedicado | Logica en UI hardcoded | Critica |
| Ordenes/Ventas | No existe | Necesario | Critica |
| Corte caja | No existe | Necesario | Alta |
| Auditoria | No existe | Necesario | Alta |

### 2.3 Verificacion de Build

`dotnet build FlexoCableSV.PuntoVenta/FlexoCableSV.PuntoVenta.csproj` compila correctamente con 0 errores y 0 advertencias.

Esto confirma que el proyecto esta en buen estado estructural, pero no valida que los flujos de negocio funcionen.

---

## 3. Brechas Criticas Detectadas

### 3.1 Brechas de Base de Datos

| Brecha | Impacto | Accion requerida |
|---|---|---|
| Estados de orden no alineados | El plan usa estados que el SQL no permite | Definir estados finales y migrar constraint |
| `DEFAULT "UuidGenerateV4"()` invalido | El schema puede fallar al ejecutarse | Usar `gen_random_uuid()` o `uuid_generate_v4()` valido |
| No hay tabla de cortes/turnos | Corte de caja no puede persistir cierres | Agregar `sales.CashSessions` y/o `sales.CashClosings` |
| No hay tabla de pagos separada | Dificulta pagos mixtos y conciliacion | Agregar `sales.Payments` si se requiere pago mixto |
| DTE-05 no tiene relacion formal con DTE original | Devoluciones quedan poco trazables | Agregar `RelatedDteId` o tabla de relaciones DTE |
| `system.Settings` tiene claves con comillas embebidas | Consultas por llave fallaran o seran inconsistentes | Normalizar keys seed |
| `GRANT ... TO flexo_user` no aplica directo en Supabase | Supabase maneja roles propios | Crear estrategia de roles/RLS/migraciones |
| Tablas PascalCase entre comillas | Funciona en PostgreSQL, pero complica Prisma/PostgREST | Decidir mantener con `@@map`/`@@schema` o migrar a snake_case |

### 3.2 Brechas de Seguridad

| Brecha | Impacto | Accion requerida |
|---|---|---|
| PINs hardcoded | Riesgo critico de operacion real | Implementar `PinService` con bcrypt |
| WPF directo a Supabase con credenciales DB | Riesgo de exponer credenciales y permisos | Definir API intermedia o rol limitado con RLS/RPC |
| Certificado DTE `.p12` en ruta local sin estrategia | Riesgo fiscal y de seguridad | Definir almacenamiento seguro y acceso restringido |
| No hay modelo de sesion de caja | Auditoria incompleta | Implementar `CurrentSession` y registro de cajero |
| No hay auditoria de acciones criticas | Riesgo operativo | Implementar `AuditService` |

### 3.3 Brechas de DTE

| Brecha | Impacto | Accion requerida |
|---|---|---|
| Falta generador JSON oficial | MH puede rechazar documentos | Implementar modelos por tipo DTE y validacion schema |
| Falta firma JWS | DTE no sera aceptado | Implementar firma con certificado valido |
| Falta manejo de token MH | Fallos intermitentes | Cachear token y renovar por expiracion |
| Falta validacion receptor | Rechazos para credito fiscal | Validar NIT/NRC/nombre segun tipo DTE |
| Falta control de correlativos | Riesgo de duplicados | Generar numero de control de forma transaccional |
| Falta contingencia robusta | Ventas pueden quedar inconsistentes | Cola con reintentos, errores y resolucion |
| Falta QR segun formato oficial | Ticket no cumple consulta publica | Generar QR con ambiente, codigo generacion y fecha |

### 3.4 Brechas de RRHH y Planilla (nueva — v2.3)

El esquema actual `hr` cubre empleados basicos y una planilla mensual simplificada, pero **no soporta** el control de planilla que FlexoCable requiere en `FlexoCable-adminweb`. Comparado con la referencia `beraka-core-api`:

| Brecha | Estado actual (`Squema.sql`) | Impacto | Accion requerida |
|---|---|---|---|
| Modelo planilla | `hr."Payroll"` (mes/anio) + `hr."PayrollDetails"` plano | No hay periodos con fechas, corridas multiples ni revision/aprobacion | Reemplazar por `PayrollPeriods` + `PayrollRuns` + detalle ampliado (seccion 17.5) |
| Calculo legal incorrecto | Comentarios SQL calculan ISSS/AFP sobre `TotalIncome` | Montos legales incorrectos vs normativa SV | Motor tipo `payroll.calculator.ts`: AFP/ISSS solo sobre salario ordinario; tope ISSS; ISR progresivo |
| Tipos de periodo | Solo mensual implicito | No soporta quincenal ni semanal | `PeriodType`: MENSUAL, QUINCENAL, SEMANAL |
| Honorarios | `ContractType` = `HONORARIOS` sin logica | Sin retencion 10% ni exporte separado | Flujo `buildHonorariosDetail` + retencion ISR honorarios |
| Cuentas bancarias | No existe | No se puede generar lote de transferencias ni comprobantes con banco | `Banks` + `EmployeeBankAccounts` |
| Documentos empleado | No existe | Sin expediente ni control de vencimientos | `RequiredDocumentTypes` + `EmployeeDocuments` |
| Historial salarial | No existe | Sin trazabilidad de aumentos | `SalaryHistory` |
| Lineas de devengo/deduccion | Columnas fijas en detalle | No desglosa rubros ni auditoria por concepto | `PayrollEarningLines` + `PayrollDeductionLines` |
| Tabla ISR | No existe | ISR no versionable por anio/periodo | `IsrBrackets` |
| Aguinaldo | No existe | Requerido en primera fase | `AguinaldoRuns` + `AguinaldoDetails` |
| Vacaciones | No existe | Requerido en primera fase | `LeaveTypes`, `LeaveRequests`, `VacationBalances` |
| Liquidaciones | No existe | Requerido en primera fase | `EmployeeTerminations` |
| Feriados | No existe | Horas extra feriadas y dias no laborables | `Holidays` |
| Ficha / comprobantes PDF | No existe | Entregable operativo para RRHH | Servicios PDF (ficha empleado + boleta + Excel planilla) |
| Empleado incompleto | ~25 campos basicos | Faltan datos legales, pago, prueba, supervisor | Ampliar `hr."Employees"` (seccion 17.4) |
| Backend / adminweb | Repos vacios | Cero implementacion RRHH | Planeacion en secciones 7, 8, 10 y 17 (sin codigo aun) |

**Nota:** esta refactorizacion de BD debe ejecutarse en **Fase 0** (migracion `20260616_0002` o posterior), antes de implementar modulos de planilla en backend. No eliminar tablas antiguas sin migracion de datos si ya hubiera registros en staging.

---

## 4. Principios de Desarrollo Obligatorios

Estos principios deben cumplirse en todas las fases.

### 4.1 Estandares C# del Proyecto

Basado en `FLEXOCABLE_C_CODING_STANDARDS_2026.md`:

- Usar inyeccion de dependencias para servicios y repositorios.
- Usar interfaces para servicios de negocio: `IPinService`, `IInventoryService`, `IOrderService`, `IDteService`.
- Usar metodos async con sufijo `Async`.
- No bloquear el UI thread con `.Result` ni `.Wait()`.
- Aplicar guard clauses antes de logica compleja.
- Usar excepciones especificas para dominio: `PinValidationException`, `InsufficientStockException`, `DteRejectedException`.
- Usar `ILogger<T>` con logging estructurado.
- Mantener nombres claros, PascalCase en clases/metodos y `_camelCase` en campos privados.
- Ejecutar `dotnet build` antes de cada commit funcional.

### 4.2 Validaciones por Capa

| Capa | Validaciones requeridas |
|---|---|
| UI WPF | Campos obligatorios, formato visible, mensajes simples, no permitir accion si falta informacion |
| Servicio de aplicacion | Reglas de negocio, permisos, estado permitido, stock, totales, idempotencia |
| Base de datos | FK, CHECK, UNIQUE, transacciones, constraints de estado, indices |
| DTE | JSON schema, firma, token, ambiente, receptor, emisor, totales, QR, contingencia |
| WebApp | Zod/React Hook Form, autorizacion por rol, sanitizacion, CSRF/session handling |
| Supabase | RLS, roles, RPC segura, secrets fuera del cliente, backups y migraciones |

### 4.3 Reglas Transaccionales

- Crear orden, detalle, pago, descuento de inventario y DTE deben coordinarse con transacciones claras.
- El stock no puede quedar negativo bajo concurrencia. Usar transaccion con bloqueo de fila o update condicional.
- Una venta facturada no se edita; solo se corrige con nota de credito.
- Una nota de credito nunca borra la factura original.
- El DTE confirmado o en contingencia debe quedar trazable desde la orden.
- La impresion no debe ser la fuente de verdad; solo refleja lo persistido.

---

## 5. Compatibilidad PostgreSQL y Supabase

### 5.1 Decision Arquitectonica Pendiente

Para produccion con Supabase hay dos opciones.

| Opcion | Descripcion | Recomendacion |
|---|---|---|
| WPF directo a PostgreSQL/Supabase | La caja usa Npgsql directo contra la base | Aceptable solo con rol muy limitado, RLS y red controlada |
| API intermedia | WPF y WebApp consumen una API que aplica reglas de negocio | Recomendada para produccion real |

Recomendacion: crear una API intermedia o capa RPC segura antes de produccion. La conexion directa desde WPF a Supabase expone demasiado si usa credenciales con permisos amplios.

### 5.2 Ajustes Requeridos para Supabase

| Tema | Validacion |
|---|---|
| Extensiones | Usar `pgcrypto` para `gen_random_uuid()` y bcrypt; validar si `uuid-ossp` esta habilitada |
| UUID | Cambiar `DEFAULT "UuidGenerateV4"()` por `DEFAULT gen_random_uuid()` |
| Roles | Reemplazar `GRANT ... TO flexo_user` por estrategia Supabase (`authenticated`, `service_role`, roles dedicados o API) |
| RLS | Definir politicas para WebApp; no exponer tablas sensibles como `DteConfig` o `WebUsers` |
| Schemas | Exponer solo schemas necesarios; revisar `public`, `sales`, `dte`, `hr`, `system` en Supabase API |
| Prisma | Usar `multiSchema` y `@@schema`, o mapear tablas PascalCase con `@@map` |
| PostgREST | Tablas con comillas y PascalCase son mas incomodas; preferir API/Prisma para evitar errores |
| Secrets | Certificados `.p12`, passwords y tokens MH no deben estar en tablas accesibles al cliente |
| Backups | Confirmar plan de backups Supabase y exportacion adicional si se requiere control local |
| Latencia/offline | Caja debe manejar caida de internet con contingencia DTE y politica de operacion local |

### 5.3 Migraciones Necesarias Antes de Servicios

Antes de implementar servicios, crear una migracion SQL de saneamiento:

- Corregir generacion UUID.
- Normalizar keys de `system.Settings`.
- Definir estados finales de `sales.Orders`.
- Agregar tablas de caja: `sales.CashSessions`, `sales.CashClosings` o equivalente.
- Agregar tabla `sales.Payments` si se aceptaran pagos mixtos.
- Agregar relacion de notas de credito con DTE original.
- Revisar constraints de DTE segun tipos realmente soportados: `01`, `03`, `05`.
- Agregar campos necesarios para numero de control, codigo generacion, sello, ambiente, version DTE y lote/contingencia si aplica.
- **Refactorizar esquema `hr` para planilla completa** (seccion 17): reemplazar `hr."Payroll"`/`hr."PayrollDetails"` por modelo Periodo + Corrida; ampliar `hr."Employees"`; agregar bancos, documentos, ISR, aguinaldo, vacaciones y liquidaciones.
- Seed idempotente de bancos SV, tipos de documento requeridos, brackets ISR por anio y feriados nacionales.
- Actualizar modelos EF Core WPF (`Employee`, eliminar/deprecar `Payroll`/`PayrollDetail` antiguos) en la misma migracion o commit posterior documentado.

## 6. Modulos y Orden Correcto de Implementacion

### Fase 0 - Saneamiento de Esquema y Arquitectura

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Tener una base compatible con PostgreSQL/Supabase antes de conectar servicios.

**Implementar:**

- Migracion de correcciones SQL.
- Decision de arquitectura Supabase: API intermedia vs WPF directo con rol limitado.
- Estrategia de migraciones versionadas.
- Seeds idempotentes.
- Validacion de `FlexoDbContext` contra schema real.
- Configuracion de ambientes: local, test, produccion.

**Validaciones:**

- El SQL ejecuta limpio en PostgreSQL local.
- El SQL ejecuta limpio en Supabase staging.
- No hay funciones inexistentes.
- Los estados del plan coinciden con CHECK constraints.
- Los seeds pueden ejecutarse mas de una vez sin duplicar datos o fallar.

**Criterios de cierre:**

- `dotnet build` exitoso.
- Conexion a DB validada al arrancar.
- Migracion probada en ambiente limpio.
- Decision Supabase documentada.
- Esquema `hr` refactorizado y validado contra checklist de seccion 17.6 (tablas creadas, seeds, UUID en PKs/FKs).

**Subfase 0b — Refactorizacion RRHH/Planilla (critica para adminweb):**

| Entregable | Descripcion |
|---|---|
| Migracion SQL `hr` v2 | Nuevas tablas + alter `Employees` + deprecacion `Payroll` legacy |
| Seeds RRHH | Bancos, documentos, ISR, feriados, puestos/departamentos FlexoCable |
| Prisma schema | `multiSchema` con `@@map` PascalCase en `FlexoCable-backend` |
| EF Core | Modelos C# con `Guid` para PKs/FKs; WPF solo consulta/escribe datos operativos de caja (ver 17.12) |
| Documentacion | Mapeo Beraka → FlexoCable en seccion 17.5 |

**Regla operativa Fase 0 — fuente de verdad BD:** desde v3.0, `FlexoCable-backend/prisma/schema.prisma` es la fuente principal del schema. `FlexoCableSV.PuntoVenta/Squema.sql` queda como referencia legacy hasta eliminarlo o convertirlo en migraciones raw puntuales. No ejecutar ambos caminos sobre la misma BD.

**Pendiente antes de servicios WPF:** alinear modelos EF Core contra Prisma v3.0 y documentar cualquier SQL raw requerido por PostgreSQL que Prisma no pueda expresar, por ejemplo indices parciales, triggers y politicas RLS.

---

### Fase 1 - Seguridad, Sesion y PIN Real

**Prioridad:** Critica  
**Esfuerzo:** Medio  
**Objetivo:** Eliminar PINs hardcoded y registrar correctamente quien opera caja.

**Implementar:**

- `IPinService` y `PinService`.
- `ICurrentSessionService` para empleado autenticado, modulo y hora de inicio.
- `IConnectivityService` y banner de estado en `MainShellWindow`.
- Cache local SQLite de catalogo (solo lectura cuando Supabase falla).
- Cola local de operaciones pendientes con `ClientRequestId` para idempotencia al reconectar.
- Reintentos Npgsql con Polly en servicios criticos.
- Validacion bcrypt contra `hr.Employees.PinHash`.
- Filtro por `IsActive`, `CanCashier` y `CanSell`.
- Manejo de intentos fallidos y bloqueo temporal si se requiere.
- Registro de auditoria para ingreso/salida.

**Validaciones:**

- PIN tiene 4 digitos.
- PIN nunca se guarda ni se loguea en texto plano.
- Empleado inactivo no puede operar.
- Empleado sin `CanCashier` no entra a Caja.
- Todas las acciones de caja reciben `EmployeeId` desde sesion, no desde UI editable.

**Criterios de cierre:**

- `PinWindow` no contiene PINs hardcoded.
- Un cajero valido entra a Caja.
- Un PIN invalido es rechazado.
- La sesion se propaga a facturacion, corte y devoluciones.

---

### Fase 2 - Catalogo e Inventario Transaccional

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Consultar y modificar stock real con seguridad ante concurrencia.

**Implementar:**

- `IInventoryService`.
- Busqueda de productos para `ConsultarStockView` y `VerCodigosView`.
- Validacion de unidades por `MeasurementType.Decimals`.
- Descuento de stock con transaccion.
- Movimiento inmutable en `InventoryMovements`.
- Alertas de stock bajo.
- Ajustes con motivo obligatorio para WebApp futura.

**Validaciones:**

- Cantidad mayor que 0.
- `PIEZA` y `KIT` no aceptan decimales.
- `METRO` acepta maximo 2 decimales.
- `PESO` acepta maximo 3 decimales.
- Stock no puede quedar negativo.
- Movimiento de inventario debe tener referencia de documento/orden.
- La actualizacion de stock y el movimiento se guardan en la misma transaccion.

**Criterios de cierre:**

- Caja y Confeccion consultan productos reales.
- Stock bajo se muestra correctamente.
- No se puede vender mas de lo disponible.
- Los movimientos quedan auditables.

---

### Fase 3 - Ordenes, Ventas y Pagos

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Crear ventas reales antes de facturar electronicamente.

**Implementar:**

- `IOrderService`.
- `IPaymentService` si se agrega tabla de pagos.
- Flujo de orden desde `OrdenesConfeccionView`.
- Flujo de venta desde `FacturacionView`.
- Calculo de subtotal, IVA 13% y total.
- Persistencia de `sales.Orders` y `sales.OrderDetails`.
- Estados finales alineados al SQL.
- Historial de ventas/facturas.

**Estados alineados al esquema actual (obligatorio):**

| Tabla | Campo | Valores permitidos | Uso |
|---|---|---|---|
| `sales.Orders` | `Status` | `PENDIENTE`, `COMPLETADA`, `CANCELADA` | Orden en taller o venta cerrada/cancelada |
| `dte.DteIssued` | `MhStatus` | `PENDIENTE`, `PROCESADO`, `RECHAZADO`, `CONTINGENCIA` | Estado fiscal ante MH |

**Nota:** no agregar `CONTINGENCIA` ni `CERRADA` a `Orders.Status` sin migracion SQL. La contingencia fiscal vive en `DteIssued.MhStatus`. Una orden `COMPLETADA` puede tener DTE en `CONTINGENCIA` si MH no respondio.

**Extension opcional en Fase 0 (si se requiere borrador de confeccion):** agregar `BORRADOR` al CHECK de `Orders.Status` mediante migracion, o usar `PENDIENTE` como borrador hasta facturar.

**Validaciones:**

- Orden debe tener al menos un detalle.
- Producto debe estar activo.
- Precio unitario debe congelarse al momento de venta.
- Total calculado en servicio, no confiado desde UI.
- Metodo de pago obligatorio.
- Receptor obligatorio para credito fiscal.
- Orden cerrada no puede editarse.

**Criterios de cierre:**

- Se crea una orden real con detalle.
- Totales coinciden con DB.
- Stock se descuenta solo una vez.
- Historial muestra datos reales.

---

### Fase 4 - DTE 2026 y Ministerio de Hacienda

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Emitir DTE valido en ambiente de pruebas MH y dejar listo el camino a produccion.

**Implementar:**

- `IDteService`.
- Modelos DTE por tipo: `01`, `03`, `05`.
- Generador JSON segun especificacion oficial vigente del MH.
- Validacion local del JSON antes de firmar.
- Firma JWS con certificado `.p12`.
- Cliente HTTP para autenticacion y envio a MH.
- Cache/renovacion de token MH.
- Persistencia en `dte.DteIssued`.
- Cola `dte.DteContingency`.
- Reintento automatico con backoff controlado.
- QR para ticket.

**Validaciones DTE criticas:**

- Ambiente `00` para pruebas y `01` para produccion.
- NIT/NRC emisor configurados y validos.
- Codigo de actividad economica configurado.
- Direccion, municipio y departamento del emisor completos.
- Receptor requerido segun tipo DTE.
- Tipo `03` requiere datos fiscales del receptor.
- Tipo `05` debe referenciar documento original.
- Totales, IVA y redondeos deben cuadrar exactamente.
- Numero de control no puede duplicarse.
- Codigo de generacion UUID no puede duplicarse.
- Fecha/hora debe estar sincronizada; usar NTP en caja/servidor.
- No loguear certificado, password, token ni JSON con secretos.
- Si MH rechaza, guardar codigo/mensaje y no marcar como procesado.
- Si hay error de red, marcar contingencia segun politica fiscal.

**Punto obligatorio:** validar esta fase contra la documentacion oficial vigente del MH antes de codificar. El README describe DTE v3.0, pero la implementacion debe confirmarse con los documentos tecnicos activos en 2026.

**Criterios de cierre:**

- DTE tipo `01` aceptado en ambiente test.
- DTE tipo `03` aceptado en ambiente test.
- DTE tipo `05` aceptado en ambiente test.
- Rechazo MH queda registrado y visible.
- Contingencia puede reintentarse y resolverse.
- QR del ticket apunta a consulta valida.

---

### Fase 5 - Impresion, Reimpresion y Tickets

**Prioridad:** Alta  
**Esfuerzo:** Medio  
**Objetivo:** Imprimir comprobantes consistentes con la venta y DTE.

**Implementar:**

- `IPrintService`.
- Configuracion de impresoras USB/Ethernet.
- Persistencia de impresora predeterminada.
- Ticket de prueba.
- Ticket de venta con QR.
- Ticket de contingencia si la politica lo permite.
- Reimpresion desde historial.
- Incremento de contador `Reprints`.

**Validaciones:**

- Solo ventas cerradas o en contingencia valida pueden imprimir.
- Reimpresion no modifica venta.
- Ticket muestra fecha, cajero, productos, totales, DTE, sello si existe y QR.
- Errores de impresora no deben revertir la venta ya aceptada.

**Criterios de cierre:**

- Se imprime ticket real.
- Se reimprime desde historial.
- La impresora predeterminada se guarda y recupera.

---

### Fase 6 - Corte de Caja y Turnos

**Prioridad:** Alta  
**Esfuerzo:** Medio  
**Objetivo:** Cerrar la operacion diaria por cajero/turno.

**Implementar:**

- Modelo SQL de turnos/cortes si aun no existe.
- `ICashSessionService`.
- Apertura y cierre de turno.
- Resumen por metodo de pago.
- Total de DTE procesados, rechazados y en contingencia.
- Monto declarado por cajero.
- Diferencia calculada.
- Impresion de corte.

**Validaciones:**

- No puede haber dos turnos abiertos para el mismo cajero/caja, salvo decision explicita.
- Corte requiere PIN o sesion activa.
- Ventas del corte deben estar asociadas al turno.
- Un corte cerrado no se modifica.

**Criterios de cierre:**

- Se abre y cierra turno.
- Totales coinciden con ventas reales.
- Corte queda persistido y auditable.

---

### Fase 7 - Devoluciones y Nota de Credito

**Prioridad:** Alta  
**Esfuerzo:** Alto  
**Objetivo:** Resolver devoluciones sin borrar historico.

**Implementar:**

- Flujo `DevolucionesView`.
- Busqueda de factura original.
- Seleccion parcial o total de productos devueltos.
- DTE tipo `05`.
- Relacion con DTE original.
- Movimiento `ENTRADA_DEVOLUCION`.
- Auditoria.

**Validaciones:**

- Factura original debe estar procesada.
- No se puede devolver mas cantidad que la vendida.
- No se puede duplicar una devolucion ya aplicada.
- Nota de credito debe referenciar documento original.
- Stock se reingresa solo si aplica fisicamente.

**Criterios de cierre:**

- Nota de credito aceptada en ambiente test.
- Inventario se ajusta correctamente.
- Historial muestra factura y nota relacionadas.

---

### Fase 8 - WebApp Administrativa Base

**Prioridad:** Alta  
**Esfuerzo:** Alto  
**Objetivo:** Crear el sistema web publico/administrativo de FlexoCable.

**Implementar:**

- Repositorios separados para backend administrativo y frontend administrativo.
- Frontend: Next.js 15, React 19, TypeScript, Tailwind CSS 4, Radix UI / shadcn.
- Backend: Node.js 22+, Express 5, Prisma 6, PostgreSQL/Supabase.
- Login administrativo.
- API REST versionada para administracion.
- Roles: `ADMIN`, `ACCOUNTANT`, `OWNER`.
- Dashboard inicial.
- CRUD empleados.
- Asignacion/cambio de PIN.
- CRUD catalogo basico.
- Gestion de usuarios web.
- CRUD **Clientes** (public.Customers): nombre, tipo (CF/CCF), DUI/NIT/NRC, datos de contacto y direccion fiscal. Base para receptor DTE y trazabilidad de ventas.
- **Planeacion RRHH (sin implementar en esta fase):** estructura de modulos backend y rutas adminweb para expediente completo del empleado (seccion 17.8–17.9).
- CRUD catalogo **bancos** (editable; seed inicial SV).
- CRUD **tipos de documento requerido** (editable; seed inicial Beraka).
- Ficha empleado: datos personales, laborales, bancarios y documentos (UI shell; logica en Fase 10).

**Validaciones:**

- Passwords y PINs siempre hasheados con bcrypt.
- Autenticacion admin con JWT.
- PIN de empleados solo para acciones operativas autorizadas, no como login web principal.
- Rol requerido por ruta.
- Validacion compartida con Zod en frontend y backend.
- No exponer `DteConfig.CertificateKey` al frontend.
- No permitir modificar ventas cerradas desde WebApp.
- Prisma no debe permitir operaciones que salten reglas transaccionales de inventario.

**Criterios de cierre:**

- Admin inicia sesion.
- Admin crea empleado y asigna PIN.
- Ese PIN funciona en WPF.
- WebApp consulta la misma base o API oficial.

---

### Fase 9 - Inventario Administrativo Web

**Prioridad:** Media-Alta  
**Esfuerzo:** Alto  
**Objetivo:** Administrar stock completo fuera de caja.

**Implementar:**

- Entradas de compra.
- Ajustes con motivo obligatorio.
- Proveedores.
- Reconciliacion fisica.
- Alertas.
- Historial de movimientos.
- Exportacion de inventario.

**Validaciones:**

- Ajuste requiere usuario autorizado.
- Motivo obligatorio.
- Cantidad positiva.
- Movimiento inmutable.
- Stock resultante no negativo.

**Criterios de cierre:**

- El stock coincide entre WPF y WebApp.
- Movimientos quedan trazables.
- Alertas se resuelven correctamente.

---

### Fase 9b - Compras, Proveedores y Kardex Valorado

**Prioridad:** Alta
**Esfuerzo:** Alto
**Objetivo:** Gestionar el abastecimiento de productos con trazabilidad de costo y actualizar el inventario valorado (Kardex con costo promedio ponderado).

**Implementar:**

- Maestro de proveedores (purchasing.Suppliers): nombre, NIT/NRC, condiciones de credito, pais (para diferenciar nacional vs importacion).
- Ordenes de compra (purchasing.PurchaseOrders): flujo BORRADOR → CONFIRMADA → RECIBIDA → CANCELADA.
- Detalle de OC (purchasing.PurchaseOrderDetails): cantidad, costo unitario, tasa de impuesto, totales.
- Al recibir OC: generar InventoryMovement tipo ENTRADA_COMPRA con unitCost, 	otalCost, stockBefore, stockAfter.
- Actualizar costPrice del producto con **costo promedio ponderado**: (stockActual * costoActual + cantidadRecibida * costoNuevo) / (stockActual + cantidadRecibida).
- Registro del documento de compra (CCF/FAC del proveedor) para libro de compras.
- CRUD de proveedores en FlexoCable-adminweb.
- Lista de ordenes de compra con estado, proveedor y totales.
- Reporte de Kardex valorado por producto (movimientos con costo y saldo valorado).
- Reporte de valuacion de inventario total (stock × costo promedio).

**Validaciones:**

- Costo unitario > 0 al recibir OC.
- Stock no puede quedar negativo al ajustar.
- OC recibida no puede editarse; solo cancelarse antes de recibir.
- costPrice del producto siempre refleja el ultimo costo promedio calculado.
- Movimiento de inventario referencia la OC de origen (purchaseOrderId).

**Criterios de cierre:**

- Se crea un proveedor y una OC.
- Al marcar OC como RECIBIDA, el inventario sube y el costPrice del producto se actualiza.
- El Kardex valorado muestra historial de movimientos con costos correctos.
- El reporte de valuacion de inventario muestra el valor total del stock.

---
### Fase 10 - Planilla, Reportes y Operacion

**Prioridad:** Media-Alta (elevada por requerimiento RRHH confirmado v2.3)  
**Esfuerzo:** Muy alto  
**Objetivo:** Completar administracion financiera y operativa, con **modulo de planilla equivalente a Beraka** para control quincenal/mensual/semanal, honorarios, aguinaldo, vacaciones y liquidaciones.

**Referencia funcional obligatoria:** `beraka-core-api` — en especial:

| Artefacto Beraka | Uso en FlexoCable |
|---|---|
| `src/modules/payroll-runs/payroll.calculator.ts` | Motor legal SV (AFP, ISSS, ISR, horas extra) |
| `src/modules/payroll-runs/payroll.builder.ts` | Armado de lineas planilla vs honorarios |
| `src/modules/payroll-runs/payroll-runs.service.ts` | Generar, editar, aprobar y pagar corridas |
| `src/modules/payroll-runs/payroll-exports.service.ts` | **Excel de planilla + comprobantes PDF** (funcion principal de exporte) |
| `src/modules/payroll-periods/` | CRUD periodos con fechas y cierre |
| `src/modules/employees/employee-ficha-pdf.service.ts` | PDF ficha del empleado |
| `src/modules/aguinaldo/` | Corrida anual de aguinaldo |
| `src/modules/employee-terminations/` | Liquidaciones |
| `prisma/schema/leaves.prisma` | Vacaciones y permisos ligados a planilla |

**Implementar:**

- Planilla **quincenal** (operacion principal FlexoCable) + soporte **mensual** y **semanal**.
- Periodos de planilla (`PayrollPeriods`) con `startDate`, `endDate`, `paymentDate`, tipo y cierre.
- Corridas de planilla (`PayrollRuns`) con estados `EN_REVISION` → `APROBADA` → `PAGADA` → `ANULADA`.
- Generacion automatica de detalle por empleado activo en el periodo.
- Honorarios con **retencion ISR 10%** (`buildHonorariosDetail`).
- ISSS, AFP, ISR progresivo (`IsrBrackets` por anio y tipo de periodo).
- Horas extra: diurnas (×2), nocturnas (×2.5), feriadas (×2).
- Lineas de devengo y deduccion desglosadas (`PayrollEarningLines`, `PayrollDeductionLines`).
- **Aguinaldo:** corrida anual con calculo proporcional y retencion ISR si aplica.
- **Vacaciones:** saldos, solicitudes aprobadas, impacto en dias trabajados y pago en planilla.
- **Liquidaciones:** finiquito al terminar relacion laboral (`EmployeeTerminations`).
- Cuentas bancarias por empleado para lote de transferencias.
- **Ficha empleado PDF** y **comprobante de pago PDF** por linea de planilla.
- **Exportacion Excel de planilla** — portar logica de `payroll-exports.service.ts` (hojas: resumen, detalle, transferencias bancarias, honorarios).
- Reportes de ventas, inventario, DTE y tecnicos.
- Exportacion Excel general (otros reportes).
- Backups.
- Auditoria completa.
- Manuales de operacion.

**Validaciones:**

- Planilla cerrada (`PAGADA`) es inmutable.
- Parametros legales versionados (`IsrBrackets`, tasas AFP/ISSS en constantes o `system.Settings`).
- AFP e ISSS calculados **solo sobre salario ordinario devengado**, no sobre bonos ni horas extra.
- Tope ISSS mensual $1,000 prorrateado por dias del periodo.
- Reportes coinciden con queries auditables.
- Backup puede restaurarse.
- Excel generado coincide con totales de corrida en BD (misma fuente que PDF).
- Honorarios no mezclan deducciones de planilla regular en la misma linea.

**Criterios de cierre:**

- Planilla quincenal se calcula, revisa, aprueba y cierra.
- Comprobantes PDF y Excel exportan correctamente por corrida.
- Aguinaldo anual calculable y exportable.
- Vacaciones aprobadas impactan planilla del periodo correspondiente.
- Liquidacion genera detalle de finiquito vinculado a empleado inactivo.
- Reportes exportan correctamente.
- Backup diario probado.
- Caja puede operar un dia completo sin intervencion tecnica.

**Alcance MVP planilla (primera entrega operativa):**

1. Periodos + corridas + calculo automatico.
2. Edicion manual de lineas antes de aprobar.
3. Excel + PDF comprobante (via `payroll-exports` portado).
4. Ficha empleado PDF.
5. Aguinaldo, vacaciones y liquidaciones en la misma fase (pueden liberarse en sub-entregas 10a/10b/10c si el equipo lo prefiere, pero **deben estar planificadas desde Fase 0** en BD).

---

### Fase 10d - Libros de IVA y Cumplimiento Fiscal

**Prioridad:** Alta (requerimiento legal)
**Esfuerzo:** Medio
**Objetivo:** Generar los libros de IVA obligatorios a partir de los DTEs ya emitidos y las compras registradas, sin doble digitacion.

**Implementar:**

- **Libro de ventas a consumidor final:** agregar desde dte.DteIssued tipo  1 del mes.
- **Libro de ventas a contribuyentes (CCF):** desde dte.DteIssued tipo  3 del mes.
- **Libro de compras:** desde purchasing.PurchaseOrders recibidas con documento del proveedor.
- Cuadre automatico entre DTEs emitidos/recibidos y los libros (verificacion de totales).
- Generacion de archivo Excel en formato compatible con declaracion mensual Hacienda.
- Registro del libro generado en iscal.IvaReports con estado BORRADOR → CERRADO.
- Endpoint GET /api/v1/fiscal/iva-reports/:year/:month/:type para consulta y descarga.

**Validaciones:**

- Solo se puede cerrar un libro si el cuadre con DTEs es correcto.
- Libro cerrado es inmutable.
- Periodo sin DTEs genera libro vacio (no falla).

**Criterios de cierre:**

- Libro de ventas CF generado para un mes con ventas reales.
- Libro de compras generado para un mes con OCs recibidas.
- Exportacion Excel descargable desde adminweb.

---

### Fase 11 - Dashboard de Inteligencia de Negocios (BI)

**Prioridad:** Media
**Esfuerzo:** Medio
**Objetivo:** Tablero gerencial centralizado con KPIs operativos, comerciales y de RRHH para toma de decisiones.

**Implementar:**

**Ventas:**
- Total de ventas del dia / semana / mes con comparativo vs periodo anterior.
- Ventas por cajero, por tipo de orden (VENTA_CAJA vs ORDEN_CONFECCION).
- Productos mas vendidos por cantidad y por monto.
- Ticket promedio y cantidad de transacciones.

**Inventario:**
- Valor total del inventario (stock × costo promedio).
- Productos bajo stock minimo (alertas de reabastecimiento).
- Rotacion de inventario por familia/subfamilia.
- Movimientos del dia (entradas, salidas, ajustes).

**Compras:**
- OCs pendientes de recibir.
- Total comprado por proveedor en el mes.
- Comparativo costo promedio historico por producto.

**RRHH:**
- Headcount activo por tipo de contrato.
- Proximas planillas por pagar.
- Empleados con documentos por vencer.

**Implementacion:**
- Endpoints de agregacion en FlexoCable-backend: GET /api/v1/dashboard/sales, /inventory, /purchases, /hr.
- Sin tablas adicionales; todas las consultas sobre datos existentes con indices adecuados.
- Filtros por rango de fechas.
- Frontend: pagina /dashboard en FlexoCable-adminweb con tarjetas, graficas (recharts o chart.js) y tablas.

**Criterios de cierre:**

- Dashboard muestra datos reales del dia y del mes.
- Las 4 secciones (ventas, inventario, compras, RRHH) tienen al menos 3 KPIs cada una.
- Exportacion de datos en Excel desde cada seccion.

---
## 7. Estructura Inicial Recomendada para el Sistema Web

### 7.1 Repositorios confirmados

```text
FlexoCable-backend/          # Node.js 22+ + Express 5 + Prisma 6 — API administrativa
FlexoCable-adminweb/         # Next.js 15 + React 19 + Tailwind CSS 4 — UI administrativa
FlexoCableSV.PuntoVenta/     # WPF caja — EF Core directo a Supabase (decision hibrida)
```

**Decision actualizada:** el sistema administrativo usa backend Node.js. Cualquier referencia a una API `.NET` debe considerarse opcion futura separada o parte de otro servicio, no la base del admin.

### 7.2 Estructura `FlexoCable-backend`

```text
FlexoCable-backend/
+-- package.json
+-- tsconfig.json
+-- prisma/
|   +-- schema.prisma
|   +-- migrations/
+-- src/
|   +-- server.ts                    # Bootstrap Express 5
|   +-- app.ts                       # Middlewares, rutas, errores
|   +-- config/                      # Env, Supabase/PostgreSQL, JWT
|   +-- modules/
|   |   +-- auth/                    # Login admin JWT, refresh si aplica
|   |   +-- employees/               # CRUD empleados + PIN hash + ficha PDF
|   |   +-- employee-bank-accounts/  # Cuentas bancarias por empleado
|   |   +-- employee-documents/      # Expediente y archivos
|   |   +-- banks/                   # Catalogo editable de bancos
|   |   +-- required-document-types/ # Tipos de documento requerido (CRUD)
|   |   +-- salary-history/          # Historial de salarios
|   |   +-- payroll-periods/         # Periodos de planilla
|   |   +-- payroll-runs/            # Corridas, calculo, aprobacion, exportes
|   |   +-- aguinaldo/               # Corrida anual aguinaldo
|   |   +-- employee-terminations/   # Liquidaciones / finiquitos
|   |   +-- leave-requests/          # Permisos y vacaciones
|   |   +-- vacation-balances/       # Saldos vacacionales
|   |   +-- holidays/                # Feriados nacionales
|   |   +-- isr-declarations/        # Declaraciones ISR (opcional fase 10+)
|   |   +-- products/                # Catalogo
|   |   +-- inventory/               # Entradas, ajustes, movimientos
|   |   +-- imports/                 # Excel import/export
|   |   +-- payroll/                 # Planilla (periodos, corridas, exportes)
|   |   +-- reports/                 # KPIs y exportaciones
|   |   +-- dte/                     # Consulta/configuracion DTE sin exponer secretos
|   +-- middleware/                  # JWT, roles, rate limit, errores
|   +-- lib/                         # Prisma, logger, crypto, Excel
|   +-- schemas/                     # Zod compartible por modulo
|   +-- types/
+-- tests/
+-- Dockerfile
+-- README.md
```

**Stack backend:** Node.js 22+, Express 5, Prisma 6, PostgreSQL/Supabase, JWT, bcrypt, Zod.

**Modulos API v1:** auth, empleados, bancos, documentos, historial salarial, periodos planilla, corridas planilla, aguinaldo, vacaciones, liquidaciones, feriados, productos, inventario, importacion/exportacion Excel, reportes, dashboard KPIs, consulta DTE.

**Validaciones backend obligatorias:**

- Todas las entradas HTTP se validan con Zod antes de tocar Prisma.
- JWT obligatorio para rutas admin, excepto `POST /api/v1/auth/login`.
- Roles revisados por middleware, no por componentes frontend.
- PIN de empleado se hashea en backend; nunca se devuelve al frontend.
- Prisma debe ejecutarse dentro de transacciones para inventario, ajustes e importaciones.
- Errores deben responder con formato consistente: `code`, `message`, `details`, `requestId`.

### 7.3 Estructura `FlexoCable-adminweb`

```text
FlexoCable-adminweb/
+-- package.json
+-- next.config.ts
+-- tsconfig.json
+-- tailwind.config.ts
+-- components.json              # shadcn/ui
+-- src/
|   +-- app/
|   |   +-- login/
|   |   +-- dashboard/
|   |   +-- empleados/
|   |   |   +-- [id]/ficha/          # Vista expediente + descarga PDF
|   |   |   +-- [id]/bancos/
|   |   |   +-- [id]/documentos/
|   |   +-- rrhh/
|   |   |   +-- bancos/              # Catalogo editable
|   |   |   +-- tipos-documento/
|   |   |   +-- feriados/
|   |   +-- planilla/
|   |   |   +-- periodos/
|   |   |   +-- corridas/
|   |   |   +-- aguinaldo/
|   |   |   +-- liquidaciones/
|   |   |   +-- vacaciones/
|   |   +-- inventario/
|   |   +-- importaciones/       # Carga Excel catalogo y entradas
|   |   +-- reportes/            # Exportacion Excel
|   +-- components/
|   |   +-- ui/                  # shadcn/Radix
|   |   +-- layout/
|   |   +-- forms/
|   +-- features/
|   +-- lib/
|   |   +-- api.ts               # Cliente REST hacia FlexoCable-backend
|   |   +-- auth.ts              # Manejo JWT/session client-side
|   |   +-- validators.ts        # Zod reusado en formularios
+-- .env.example                 # NEXT_PUBLIC_API_URL
```

**Stack frontend:** Next.js 15, React 19, TypeScript, Tailwind CSS 4, Radix UI / shadcn.

**No usar Prisma directo desde adminweb** si el backend centraliza reglas, auditoria e importaciones Excel. El frontend debe consumir `FlexoCable-backend` por HTTP.

### 7.4 Reutilizacion de logica entre WPF y backend

| Capa | WPF | Backend | Estrategia |
|---|---|---|---|
| Lectura catalogo/stock | EF directo | Prisma | Misma BD Supabase; validar reglas identicas en tests |
| Escritura inventario admin | No (solo consulta) | Express + Prisma | Reglas solo en backend Node |
| Ventas/DTE | EF + servicios WPF | Solo lectura reportes | Extraer contratos/reglas puras a paquete compartido si crece la divergencia |
| Import Excel | No | Express + libreria Excel Node | Unico punto de importacion |

Riesgo aceptado en MVP: WPF y backend pueden divergir en reglas de venta. Mitigacion: tests de aceptacion compartidos y, si se requiere, paquete compartido de contratos/validadores para reglas no acopladas a UI.

---

## 8. Estructura de Commits Propuesta

Cada commit debe ser atomico, verificable y alineado con `FLEXOCABLE_GIT_PR_2026.md`.

### 8.1 Convencion Gitmoji para FlexoCable

Usar Gitmoji de forma consistente. No elegir emojis por gusto visual; el emoji debe describir el tipo principal del cambio.

| Emoji | Uso en FlexoCable | Ejemplo |
|---|---|---|
| `🎉` | Inicio de proyecto, scaffold inicial o primer commit de repositorio | `🎉 Se crea la base del repositorio administrativo Node/Next.` |
| `✨` | Nueva funcionalidad de negocio o modulo completo | `✨ Se implementa flujo transaccional de ordenes y ventas.` |
| `🐛` | Correccion de bug o comportamiento incorrecto | `🐛 Se corrige validacion de stock para productos por metro.` |
| `🔐` | Seguridad, autenticacion, permisos, PIN, JWT, secretos | `🔐 Se implementa autenticacion JWT y roles administrativos.` |
| `🗃️` | Cambios de base de datos, migraciones, Prisma, SQL, indices | `🗃️ Se sanea Squema.sql para compatibilidad Supabase.` |
| `🔧` | Configuracion, tooling, variables, ajustes operativos | `🔧 Se configura conexion por ambiente y healthcheck.` |
| `♻️` | Refactor sin cambio funcional visible | `♻️ Se extraen servicios WPF para usar inyeccion de dependencias.` |
| `✅` | Tests unitarios, integracion, fixtures o cobertura | `✅ Se agregan pruebas de inventario transaccional.` |
| `📝` | Documentacion, planes, manuales, README | `📝 Se actualiza plan de finalizacion y roadmap.` |
| `💄` | UI, estilos, componentes visuales, shadcn/Radix | `💄 Se crea layout administrativo con shadcn.` |
| `📦` | Dependencias, package manager, NuGet/npm | `📦 Se agregan Prisma 6 y Zod al backend.` |
| `⬆️` | Actualizacion de dependencias existentes | `⬆️ Se actualiza Next.js a version 15.` |
| `⚡` | Performance, optimizacion o cache | `⚡ Se agrega cache de catalogo para modo degradado.` |
| `🚨` | Correcciones de lint, analyzer, warnings o formato | `🚨 Se corrigen advertencias de StyleCop y ESLint.` |
| `💚` | CI/CD, pipelines, checks automaticos | `💚 Se agrega pipeline de build y tests.` |
| `🚀` | Deploy, release, infraestructura de publicacion | `🚀 Se documenta despliegue de backend y adminweb.` |

Regla: si un commit toca dos categorias, dividirlo. Si no es posible, usar el emoji del riesgo mayor: seguridad > base de datos > funcionalidad > UI > documentacion.

### 8.2 Secuencia propuesta de commits

| Commit | Mensaje sugerido | Contenido |
|---|---|---|
| 1 | `📝 Se actualiza el plan de finalizacion con validaciones Supabase, DTE y roadmap por fases.` | Solo documentacion del plan |
| 2 | `🗃️ Se sanea Squema.sql para compatibilidad PostgreSQL y Supabase.` | UUID, settings, estados, seeds idempotentes |
| 2b | `🗃️ Se refactoriza esquema hr para planilla Periodo+Corrida y expediente empleado.` | Migracion Fase 0b — seccion 17 |
| 3 | `🗃️ Se agregan tablas de turnos, pagos y relaciones de notas de credito.` | Migracion caja/DTE |
| 4 | `🔐 Se implementa PinService con bcrypt y sesion de cajero.` | Seguridad WPF |
| 5 | `♻️ Se conecta la navegacion WPF a servicios mediante inyeccion de dependencias.` | DI, session, service registration |
| 6 | `✨ Se implementa consulta de catalogo e inventario real.` | `InventarioService`, stock, busquedas |
| 7 | `✨ Se implementa flujo transaccional de ordenes y ventas.` | Orden, detalles, totales, estados |
| 8 | `✨ Se integra descuento de inventario y movimientos inmutables.` | Stock transaccional |
| 9 | `✨ Se implementa historial real de ventas y facturas.` | Consultas y filtros |
| 10 | `✨ Se implementa generacion inicial de DTE tipo 01 en ambiente de pruebas.` | JSON, persistencia DTE |
| 11 | `🔐 Se agrega firma, autenticacion y envio DTE al Ministerio de Hacienda.` | JWS, token, HTTP client |
| 12 | `✨ Se implementa contingencia y reintentos para DTE.` | Cola y estados |
| 13 | `✨ Se implementa impresion de tickets con QR DTE.` | `ImpresionService` |
| 14 | `✨ Se implementa corte de caja por turno.` | Apertura/cierre/resumen |
| 15 | `✨ Se implementan devoluciones con nota de credito DTE-05.` | Devoluciones y stock |
| 16 | `🎉 Se crea la base de los repositorios FlexoCable-backend y FlexoCable-adminweb.` | Scaffold Node/Next |
| 17 | `📦 Se configura stack administrativo con Next.js 15, Express 5, Prisma 6 y Zod.` | Dependencias y tooling |
| 18 | `🔐 Se implementa autenticacion JWT y roles en backend administrativo.` | Login y permisos |
| 19 | `💄 Se crea layout administrativo con Tailwind CSS 4 y shadcn/Radix.` | UI base admin |
| 20 | `✨ Se implementa gestion de empleados y PINs desde WebApp.` | CRUD empleados |
| 21 | `✨ Se implementa inventario administrativo Web.` | Entradas, ajustes, proveedores |
| 22 | `✨ Se implementan reportes y exportaciones Excel generales.` | Reportes + ExcelJS |
| 22b | `✨ Se implementa modulo planilla con calculo legal y exportes Beraka.` | payroll-runs + payroll-exports portados |
| 22c | `✨ Se implementan aguinaldo, vacaciones y liquidaciones.` | Sub-fases 10b/10c |
| 23 | `✨ Se implementa importacion bidireccional Excel (catalogo y entradas).` | Endpoints import + plantillas |
| 24 | `⚡ Se agrega resiliencia de conexion y cola local de operaciones pendientes.` | Offline parcial |
| 25 | `✅ Se agregan pruebas unitarias e integracion para inventario, ventas y DTE.` | Tests criticos |
| 26 | `💚 Se agrega pipeline de build y tests para WPF, backend y adminweb.` | CI |
| 27 | `🚀 Se agregan backups, auditoria operativa y documentacion de despliegue.` | Operacion |

Reglas de granularidad:

- No mezclar cambios SQL, UI y DTE en el mismo commit salvo que sean inseparables.
- Cada commit funcional debe compilar.
- Cada commit SQL debe incluir prueba de migracion o instrucciones de verificacion.
- Commits con secretos, certificados o `.env` reales estan prohibidos.

---

## 9. Checklist de Validaciones por Modulo

| Modulo | Validaciones minimas |
|---|---|
| PIN | 4 digitos, bcrypt, empleado activo, permiso correcto, no log de PIN |
| Productos | activo, codigo unico, unidad valida, precio >= 0, stock >= 0 |
| Venta | detalle no vacio, cantidades validas, total recalculado, stock disponible |
| Inventario | transaccion, movimiento inmutable, motivo en ajustes, no negativo |
| DTE | schema oficial, firma, token, emisor, receptor, totales, QR, contingencia |
| Impresion | venta valida, impresora configurada, reimpresion auditada |
| Corte | turno abierto, cajero valido, totales por pago, cierre inmutable |
| Devolucion | factura original, cantidades no excedidas, DTE-05, stock restaurado |
| Backend admin | JWT, roles por middleware, Zod por endpoint, Prisma transaccional, no exposicion de secretos |
| WebApp | rol por ruta, formularios Zod, JWT seguro, shadcn accesible, no log de datos sensibles |
| Supabase | RLS, roles, migrations, extensions, backups, secrets protegidos |
| Planilla | periodo unico por fechas; corrida unica por empleado; estados validos; inmutable si PAGADA |
| Calculo planilla | AFP/ISSS solo ordinario; tope ISSS; ISR por bracket; HE diurna/nocturna/feriada |
| Honorarios | retencion 10%; sin AFP/ISSS; exporte separado en Excel |
| Empleado RRHH | DUI/NIT/NUP/ISSS unicos si presentes; cuenta bancaria primaria; documentos obligatorios |
| Bancos | codigo unico si presente; no borrar banco con cuentas activas |
| Aguinaldo | un run por anio; proporcional por meses trabajados |
| Liquidacion | empleado con fecha baja; totales finiquito auditables |
| Vacaciones | solicitud aprobada antes de impactar planilla; saldo no negativo |

---

## 10. Riesgos Tecnicos

| Riesgo | Severidad | Mitigacion |
|---|---|---|
| DTE rechazado por schema o firma incorrecta | Alta | Certificacion temprana en ambiente MH test |
| Stock negativo por concurrencia | Alta | Transacciones y bloqueo/update condicional |
| Credenciales Supabase expuestas en WPF | Alta | API intermedia o rol minimo con RLS |
| Duplicidad de reglas entre WPF y WebApp | Alta | Backend unico o RPC transaccional |
| Estados SQL no coinciden con UI | Alta | Saneamiento de esquema en Fase 0 |
| Internet caido en caja | Alta | Ver seccion 13 — contingencia DTE no basta si Supabase no responde |
| Sin catalogo importado | Alta | Script/plantilla Excel en Fase 0 seed + API import en Fase 9 |
| Divergencia reglas WPF vs backend | Alta | Tests compartidos + paquete compartido de contratos/validadores si aplica |
| Certificado DTE mal protegido | Alta | Secret storage y permisos restringidos |
| Tablas PascalCase dificultan Prisma/PostgREST | Media | Mapear con Prisma o migrar naming antes de WebApp |
| Planilla legal desactualizada | Media | Parametros versionados por vigencia (`IsrBrackets`, constantes en `payroll.constants`) |
| Divergencia calculo Excel vs BD | Media | Un solo motor (`payroll.calculator`) alimenta persistencia y `payroll-exports.service` |
| Esquema `hr` legacy vs nuevo | Alta | Migracion Fase 0b a UUID unificado; deprecar `hr."Payroll"` legacy |
| Complejidad modulo RRHH | Alta | Portar modulos probados de Beraka; no reimplementar desde cero |

---

## 11. Preguntas Pendientes Antes de Implementar

### Resueltas

| # | Pregunta | Respuesta |
|---|---|---|
| — | Infraestructura BD | Supabase hibrido |
| — | PIN al facturar | Solo cajero de sesion |
| — | .NET | `net10.0-windows` |
| — | Repos web | `FlexoCable-backend` + `FlexoCable-adminweb` |
| — | Excel | Bidireccional (import + export) |
| — | Planilla RRHH | Quincenal + mensual + semanal; honorarios 10%; aguinaldo/vacaciones/liquidaciones fase 1 |
| — | Modelo planilla | Periodo + Corrida (Beraka); export Excel/PDF via `payroll-exports.service.ts` |
| — | IDs en BD | **UUID unificado** en todo el dominio (Beraka); sin INTEGER especial para WPF |

### Aun por confirmar (no bloquean Fase 0)

1. ~~**Modo offline:**~~ **Resuelto:** MVP robusto con cache + cola local (Fase 1).
2. Habra pagos mixtos en una misma factura (efectivo + tarjeta)?
3. Se requiere apertura de turno antes de vender o solo corte al final del dia?
4. La factura en contingencia puede imprimirse inmediatamente o solo despues de recibir sello MH?
5. El catalogo definitivo de 500+ productos: ¿archivo Excel existente? ¿columnas exactas?
6. Las notas de credito seran devolucion parcial, total o ambas?
7. Quien tendra permiso para ajustes manuales de inventario (rol web)?
8. El certificado DTE: PC de caja, Supabase Storage o solo backend?
9. El sistema web sera publico en internet o solo VPN/red privada?
10. Se mantiene PascalCase en tablas o se migra a snake_case antes de crecer?
11. `FlexoCable-backend` y `FlexoCable-adminweb` seran repos separados o un monorepo con `apps/api` y `apps/admin`?
12. Package manager para Node: npm, pnpm o bun?
13. JWT admin sera access token simple, access + refresh token, o sesion server-side con cookie httpOnly?
14. shadcn se instalara con componentes copiados al repo o se usara solo Radix UI directamente?

---

## 12. Auditoria QA — Brechas que Aun Faltan

Esta seccion resume lo que el plan v2.0 cubria parcialmente o no cubria. **No se debe iniciar desarrollo de Fase 3+ sin cerrar los items marcados como bloqueantes.**

### 12.1 Bloqueantes de arquitectura

| ID | Brecha | Por que importa | Accion en plan |
|---|---|---|---|
| QA-01 | **Caida de internet ≠ solo contingencia DTE** | Con Supabase remoto, sin red la caja no puede leer catalogo, validar PIN ni guardar ventas | Seccion 13 |
| QA-02 | **Sin seed de productos ni empleados** | Fases 1–3 no son probables | Fase 0: seed dev + import Excel |
| QA-03 | **Contradiccion estados orden** (corregida en v2.1) | Migraciones fallarian o UI mostraria estados invalidos | Fase 3 alineada a SQL |
| QA-04 | **Sin tabla `CashSessions`/`Payments`** | Corte y pagos mixtos no persisten | Fase 0 migracion |
| QA-18 | **Esquema planilla simplificado** | No cumple requerimiento RRHH FlexoCable | Fase 0b + seccion 17 |
| QA-19 | **Sin tablas bancos/documentos/ISR** | No hay expediente ni lote transferencias | Fase 0b |
| QA-05 | **Sin `.sln` ni tests** | Build CI y regresion imposibles | Fase 0: crear solucion + proyecto tests |
| QA-06 | **Sin `DteRetryBackgroundService`** | Contingencia DTE no se reintenta sola | Fase 4 |
| QA-07 | **Sin `IConnectivityService`** | UI no puede avisar "sin conexion" al cajero | Fase 1 |

### 12.2 Brechas operativas (no bloquean MVP pero afectan produccion)

| ID | Brecha | Riesgo |
|---|---|---|
| QA-08 | Sin estrategia de actualizacion de app WPF (ClickOnce/MSIX) | Parches manuales en sucursal |
| QA-09 | Sin sincronizacion de hora (NTP) documentada en instalacion | DTE rechazado por fecha |
| QA-10 | Sin escenario multi-PC (dos cajas simultaneas) | Concurrencia stock y correlativos DTE |
| QA-11 | Sin manejo de corte de energia (UPS mencionado en README, no en plan) | Ventas a medias, BD inconsistente |
| QA-12 | Sin escaner de codigos de barras | UX lenta en catalogo grande |
| QA-13 | Sin politica de cambio de precio con orden abierta | Total incorrecto si admin cambia precio en web |
| QA-14 | Sin ViewModels/MVVM en fases | Deuda tecnica en bindings |
| QA-15 | Sin pruebas E2E automatizadas | Regresion en flujos tactiles |
| QA-16 | `MainWindow.xaml` huerfano | Confusion en mantenimiento |
| QA-17 | README R-VTA-06 (PIN tecnico) contradice decision de cajero | Documentacion desalineada |

### 12.3 Matriz de modos de falla (QA)

| Escenario | Comportamiento esperado hoy (sin implementar) | Comportamiento objetivo |
|---|---|---|
| Sin internet, Supabase inalcanzable | App arranca (validacion BD comentada) pero servicios fallarian | Ver seccion 13 — modo degradado |
| Internet OK, MH caido | — | Venta `COMPLETADA`, DTE `CONTINGENCIA`, ticket imprimible segun politica |
| Impresora falla post-venta | — | Venta y DTE persistidos; reimpresion desde historial |
| Stock insuficiente en cierre | — | Rechazar con mensaje claro; no crear DTE |
| Dos cajeros, mismo producto ultima unidad | — | Uno gana (transaccion); otro recibe error de stock |
| Excel con filas invalidas | — | Import parcial rechazado con reporte de errores por fila |
| Planilla Excel legacy del cliente | — | Migracion unica via import; luego solo sistema |
| Excel planilla distinto a totales BD | — | Un motor de calculo; export lee BD ya calculada |

### 12.4 Estrategia de pruebas (faltaba en plan)

| Tipo | Alcance | Cuando |
|---|---|---|
| Unitarias | Servicios: PIN, inventario, totales, redondeo IVA | Cada fase de servicio |
| Integracion | EF + PostgreSQL/Supabase staging, transacciones stock | Fase 2–4 |
| Planilla | Redondeo, ISR brackets, tope ISSS, honorarios 10%, quincenal vs mensual | Fase 10 |
| DTE | Ambiente MH `00`, casos 01/03/05, rechazo, contingencia | Fase 4 |
| E2E manual | Dia completo caja: PIN → venta → DTE → ticket → corte | Fase 10 |
| Excel | Import 500 filas, filas con error, export reportes | Fase 9–10 |
| Offline | Simular desconexion Supabase durante venta | Fase 1 + 13 |

---

## 13. Resiliencia sin Internet (Recomendacion QA)

**Problema:** la contingencia DTE solo cubre fallas del Ministerio de Hacienda. Si Supabase no responde, la caja queda completamente inoperativa aunque la LAN local funcione.

**Recomendacion por fases:**

### MVP (Fase 1–5) — "Degradado controlado"

1. **`IConnectivityService`:** ping periodico a Supabase; banner visible en shell si hay falla.
2. **Cache local de solo lectura:** SQLite o archivo JSON con ultimo snapshot de catalogo (codigo, descripcion, stock, precio, unidad). Actualizar al conectar.
3. **Cola de operaciones pendientes (`system.PendingOperations` o archivo local cifrado):** si falla `SaveChanges`, encolar venta/orden con timestamp; reintentar al reconectar con idempotencia por `ClientRequestId`.
4. **Politica de venta sin BD:** **bloquear facturacion y DTE** si no hay conexion; permitir solo crear borradores locales que se sincronicen al volver red (opcional, Fase 3).
5. **Reintentos Npgsql:** Polly con backoff en servicios criticos.
6. **UPS obligatorio** en manual de instalacion (README ya lo menciona).

### Produccion (Fase 10+) — "Caja autonoma"

1. **PostgreSQL local en PC de caja** como primaria operativa.
2. **Job de sincronizacion** bidireccional con Supabase (pglogical, Debezium, worker Node o servicio local programado).
3. **Supabase** como hub para admin web, reportes y backup central.
4. **Conflictos:** last-write-wins solo en catalogo admin; ventas siempre append-only con IDs globales.

**Decision pendiente del equipo:** confirmar si MVP acepta "no vender sin Supabase" (mas simple) o exige cola local (mas trabajo en Fase 1).

---

## 14. Integracion Excel (Bidireccional)

Alcance confirmado: importar catalogo y entradas de inventario; exportar reportes y planilla.

### 14.1 Importacion (backend)

| Tipo | Plantilla Excel | Endpoint | Validaciones |
|---|---|---|---|
| Catalogo productos | `plantilla_catalogo.xlsx` | `POST /api/import/products` | Codigo unico, familia existente, unidad valida, precio >= 0 |
| Entradas inventario | `plantilla_entradas.xlsx` | `POST /api/import/inventory-entries` | Proveedor, cantidad > 0, producto activo, motivo |
| Empleados (opcional) | `plantilla_empleados.xlsx` | `POST /api/import/employees` | DUI unico, puesto valido |

**Libreria recomendada:** ExcelJS o SheetJS/xlsx en `FlexoCable-backend` (no COM Excel en servidor; no requiere Office instalado).

**Flujo:**

1. Admin sube archivo en `FlexoCable-adminweb` hacia `FlexoCable-backend` → `/api/v1/importaciones`.
2. Backend valida fila a fila; devuelve preview con errores antes de confirmar.
3. Confirmacion ejecuta transaccion; registra en `system.AuditLog`.
4. Movimientos de entrada generan `InventoryMovements` inmutables.

### 14.2 Exportacion (backend)

| Reporte | Formato | Fase |
|---|---|---|
| Inventario actual | `.xlsx` | Fase 9 |
| Movimientos por rango | `.xlsx` | Fase 9 |
| Ventas por fecha/cajero | `.xlsx` | Fase 10 |
| Planilla quincenal/mensual/semanal | `.xlsx` + PDF comprobante | Fase 10 — portar `payroll-exports.service.ts` |
| Planilla honorarios | `.xlsx` hoja separada + PDF retencion | Fase 10 |
| Lote transferencias bancarias | `.xlsx` agrupado por banco | Fase 10 |
| Ficha empleado | `.pdf` | Fase 10 |
| Aguinaldo | `.xlsx` + PDF | Fase 10 |
| Liquidacion / finiquito | `.pdf` | Fase 10 |
| DTEs emitidos/contingencia | `.xlsx` | Fase 10 |

### 14.3 Extensibilidad futura

- API versionada (`/api/v1/import/...`) para integraciones Power Query / Office Scripts.
- Webhooks opcionales post-import para notificar WPF que refresque cache de catalogo.
- No acoplar logica de negocio a formato Excel; mapear columnas en servicios del modulo `imports`.

### 14.4 Fase adicional en roadmap

**Fase 9b — Import/Export Excel (Media-Alta):** implementar plantillas, endpoints y UI de importacion en adminweb; usar para carga inicial de 500+ productos.

### 14.5 Exportacion planilla (referencia Beraka — v2.3)

La exportacion Excel/PDF de planilla **no se rediseña desde cero**. Se porta la logica probada de:

`beraka-core-api/src/modules/payroll-runs/payroll-exports.service.ts`

**Entregables de ese modulo (a replicar en FlexoCable-backend):**

| Funcion / salida | Formato | Contenido |
|---|---|---|
| `generatePayrollExcel(runId)` | `.xlsx` multi-hoja | Resumen corrida, detalle por empleado, transferencias bancarias agrupadas, honorarios |
| Boleta individual | `.pdf` | Comprobante de pago con devengos, deducciones y neto |
| Lote PDF | `.pdf` | Multiples comprobantes por corrida |
| Retencion honorarios | `.pdf` / hoja Excel | Prestadores con retencion 10% |

**Regla:** el Excel y el PDF leen los mismos totales ya persistidos en `PayrollDetails` tras `payroll.calculator` — no recalculan en el exportador salvo validacion de consistencia.

**Librerias:** ExcelJS + PDFKit (mismas que Beraka).

> La especificacion completa de BD, seeds, modulos y sub-fases RRHH esta en la **seccion 17** (final del documento).

---

## 15. Plan Inmediato de Ejecucion

Orden recomendado para empezar sin bloquearse:

1. Confirmar modo offline MVP (seccion 13): bloqueo total vs cola local.
2. Aplicar schema Prisma v3.0 desde `FlexoCable-backend` (`npm run db:push` en desarrollo; `prisma migrate` cuando se congele migracion).
3. Documentar SQL raw complementario para indices parciales, triggers, RLS y seeds que Prisma no exprese.
4. Alinear modelos EF Core WPF contra Prisma v3.0 antes de servicios.
5. Crear `.sln` y proyecto de tests.
6. Ejecutar schema en PostgreSQL local y Supabase staging.
7. Activar validacion de conexion al iniciar WPF + `IConnectivityService`.
8. Implementar `PinService` y sesion real.
9. Implementar `InventoryService` con transacciones.
10. Implementar `OrderService` y estados alineados al schema Prisma.
11. Conectar vistas WPF existentes a servicios.
12. Implementar DTE tipo `01` en ambiente test.
13. Agregar DTE tipo `03` y `05` + reintentos background.
14. Implementar impresion y corte.
15. Crear/continuar `FlexoCable-backend` (Node.js 22 + Express 5 + Prisma 6) y `FlexoCable-adminweb` (Next.js 15 + React 19).
16. Implementar import/export Excel bidireccional.
17. **Fase 9b:** Maestro de proveedores, ordenes de compra y recepcion con costeo promedio ponderado.
18. **Fase 10d:** Libros de IVA (ventas CF, ventas CCF, compras) desde DTEs y OCs; exportacion Excel.
19. **Fase 10a:** planilla ordinaria (portar calculo + `payroll-exports.service.ts`).
20. **Fase 10b:** aguinaldo y vacaciones.
21. **Fase 10c:** liquidaciones (+ ISR declaraciones si aplica).
22. **Fase 11:** Dashboard BI (ventas, inventario, compras, RRHH).
23. Reportes, backups, resiliencia offline produccion y auditoria.

---

## 16. Conclusion

El plan v2.3 incorpora auditoria QA, estrategia offline (MVP robusto confirmado), integracion Excel bidireccional, Gitmoji estandarizado, stack administrativo Node/Next y **especificacion completa del modulo RRHH/planilla** alineada a `beraka-core-api` (seccion 17). La refactorizacion del esquema `hr` en **Fase 0b** es prerequisito para el control de planilla quincenal, honorarios, aguinaldo, vacaciones y liquidaciones en `FlexoCable-adminweb`.

**Listo para iniciar Fase 0** (saneamiento + Fase 0b RRHH) y continuar a Fase 1 con cache + cola local.

Veredicto QA: el plan es solido en modulos y orden. Las secciones 12–14 y **17** cierran brechas de desconexion total, pruebas, Excel, importacion y planilla. Quedan preguntas menores (pagos mixtos, contingencia imprimible) que no bloquean el inicio. La politica de IDs queda resuelta: **UUID unificado en PostgreSQL**; las diferencias entre WPF y admin web se resuelven en codigo de cada stack, no en el esquema.

---

## 17. Modulo RRHH y Planilla — Especificacion (v2.3)

> **Estado:** Planeacion confirmada por el equipo. **No incluye implementacion de codigo** en esta revision; define refactorizacion de BD, alcance funcional y referencia para `FlexoCable-backend` + `FlexoCable-adminweb`.
>
> **Referencia funcional:** `C:\Users\progr\Documents\BerakaAtaco\CODIGO\beraka-core-api` — modulos `employees`, `payroll-runs`, `payroll-periods`, `banks`, `employee-documents`, `aguinaldo`, `employee-terminations`, `leave-requests`.

### 17.1 Objetivo

Dotar a FlexoCable de un **sistema de control de planilla** en la WebApp administrativa, equivalente en capacidades al modulo empleados/planilla de `beraka-core-api`, sin afectar la operacion WPF de caja (PIN, ventas, sesiones de cajero).

**Entregables de negocio:**

- Expediente completo del empleado (datos, bancos, documentos, historial salarial).
- Periodos y corridas de planilla (quincenal como operacion principal; tambien mensual y semanal).
- Calculo automatico legal SV + honorarios con retencion 10%.
- Aguinaldo, vacaciones y liquidaciones en primera fase administrativa.
- Ficha empleado PDF, comprobantes de pago PDF y Excel de planilla (via `payroll-exports.service.ts`).

### 17.2 Decisiones confirmadas

| Tema | Decision |
|---|---|
| Frecuencia planilla | **Quincenal** (principal) + **mensual** + **semanal** |
| Honorarios | Si, con **retencion ISR 10%** |
| Aguinaldo | Si, primera fase |
| Vacaciones | Si, primera fase (saldos + solicitudes + impacto en planilla) |
| Liquidaciones | Si, primera fase (`EmployeeTerminations`) |
| Modelo datos planilla | **Periodo + Corrida** (Beraka); reemplazar `hr."Payroll"` legacy |
| Contratos | Todos los tipos Beraka: `TIEMPO_PARCIAL`, `PLAZO_FIJO`, `HONORARIOS`, `PASANTE` |
| Campos empleado | Incluir **todos** los de Beraka aunque algunos queden opcionales en UI |
| Documentos | Mismos tipos seed que Beraka; modulo CRUD administrable |
| Bancos | Catalogo **editable** (CRUD admin) + seed inicial de bancos SV |
| Export Excel planilla | Portar `payroll-exports.service.ts` |
| Implementacion codigo | **Fuera de alcance inmediato** — solo planeacion en este documento |

### 17.3 Estrategia de identificadores (IDs)

**Regla unica:** la base de datos tiene **una sola estructura**, alineada a la referencia Beraka. **No** se mantienen tablas o tipos distintos segun si consume WPF o el admin web.

| Regla | Detalle |
|---|---|
| Tipo de PK/FK | `UUID` con `DEFAULT gen_random_uuid()` en tablas de negocio (`hr`, `sales`, `public`, `dte`, `system`) |
| Convencion SQL | Esquemas con tablas PascalCase entre comillas (`hr."Employees"`, etc.) — igual filosofia que hoy, pero IDs en UUID |
| Prisma (backend Node) | `String @id @default(uuid()) @db.Uuid` — igual que Beraka |
| EF Core (WPF) | Propiedades `Guid` en modelos; Npgsql mapea `uuid` nativamente |
| Migracion Fase 0b | `Squema.sql` v2.0.0, migraciones `0001` y `0002`, y modelos EF Core WPF usan **UUID** en PKs/FKs |

**Lo que NO se hace:**

- No dejar `SERIAL` en empleados “por WPF” mientras el admin usa UUID.
- No duplicar entidades (una tabla para caja, otra para web).

**Lo que SI se hace en codigo:**

| Capa | Responsabilidad |
|---|---|
| `FlexoCable-backend` (Node) | CRUD completo RRHH, planilla, inventario admin, `system.WebUsers`, reportes |
| `FlexoCable-adminweb` (Next) | UI que consume **solo** la API Node (`/api/v1/...`) |
| `FlexoCableSV.PuntoVenta` (WPF) | Lectura/escritura **operativa**: ordenes, DTE, caja, PIN, stock en venta. **No** implementa login web, planilla ni CRUD de empleados |

### 17.3.1 `system.WebUsers` — solo administracion web (Node)

`WebUsers` es la tabla de **usuarios del panel administrativo** (login email/password + JWT en `FlexoCable-backend`). **No pertenece al proyecto WPF en C#** y no se implementara alli.

| Sistema | Autenticacion | Tabla / campo |
|---|---|---|
| WPF caja | PIN 4 digitos del empleado | `hr.Employees.PinHash` |
| Admin web | Usuario + password JWT | `system.WebUsers` via API Node |

El modelo `WebUser.cs` **no existe en el proyecto WPF**; `system.WebUsers` lo consume solo `FlexoCable-backend` (Prisma).

### 17.4 Ampliacion de `hr."Employees"`

Ademas de campos actuales (`FirstName`, `LastName`, `Dui`, `Nit`, `IsssNumber`, `Nup`, `PositionId`, `HireDate`, `BaseSalary`, `ContractType`, `Afp`, contacto, emergencia, `PinHash`, `CanSell`, `CanCashier`), **agregar** (nombres SQL sugeridos en PascalCase):

| Grupo | Campos nuevos | Notas |
|---|---|---|
| Personales | `BirthDate`, `Gender`, `Nationality`, `PassportNumber`, `DependentsDescription` | Enums como VARCHAR con CHECK o tipo ENUM PG |
| Ubicacion | `DepartmentSv` | Departamento de El Salvador (enum SV) |
| Organizacion | `DepartmentId`, `DirectSupervisorId` | FK a `Departments` y self-FK empleado |
| Contrato | `ContractEndDate`, `SalaryType`, `DefaultBonus`, `DefaultViaticos` | `SalaryType`: MENSUAL, QUINCENAL, SEMANAL |
| Prevision | `AfpInstitution`, `AfpEnrollmentDate`, `IsssEnrolled`, `IsssEnrollmentDate` | AFP: CONFIA, CRECER |
| Pago | `PaymentChannel` | DEPOSITO_BANCARIO, EFECTIVO, CHEQUE |
| Prueba laboral | `OnProbation`, `ProbationEndDate`, `ProbationCompletedAt` | Art. 30 Codigo de Trabajo SV |
| Baja | `TerminationReason`, `TerminationNotes` | Enum `TerminationReason` Beraka |
| Asistencia (futuro) | `PinUpdatedAt`, `AttendanceEnabled` | PIN ya existe; asistencia opcional post-MVP |
| Estado civil / estudio | Normalizar `MaritalStatus`, `AcademicLevel` a enums Beraka | Reemplazar VARCHAR libre |

**Constraint `ContractType`:** ampliar CHECK de `('PLANILLA','HONORARIOS')` a `('TIEMPO_PARCIAL','PLAZO_FIJO','HONORARIOS','PASANTE')`. Migrar valor legacy `PLANILLA` → `PLAZO_FIJO` si aplica.

**WPF (caja):** solo lee/escribe campos operativos del empleado (`PinHash`, `CanSell`, `CanCashier`, `IsActive`, nombre para UI). Expediente, bancos, documentos y planilla se administran **exclusivamente** en `FlexoCable-backend` + `FlexoCable-adminweb`.

### 17.5 Mapeo de tablas: Beraka → FlexoCable (`hr`)

Convencion FlexoCable: esquema `hr`, tablas PascalCase entre comillas, columnas PascalCase.

| Beraka (Prisma) | FlexoCable (SQL propuesto) | Accion |
|---|---|---|
| `Department` | `hr."Departments"` | **Ampliar:** `ParentId`, `Description` |
| `Position` | `hr."Positions"` | **Ampliar:** `Description`; `DepartmentId` opcional si Beraka no lo exige en Position |
| `Employee` | `hr."Employees"` | **Alter** segun 17.4 |
| `Bank` | `hr."Banks"` | **Crear** |
| `EmployeeBankAccount` | `hr."EmployeeBankAccounts"` | **Crear** |
| `RequiredDocumentType` | `hr."RequiredDocumentTypes"` | **Crear** |
| `EmployeeDocument` | `hr."EmployeeDocuments"` | **Crear** |
| `SalaryHistory` | `hr."SalaryHistory"` | **Crear** |
| `HealthConditionRecord` | `hr."HealthConditionRecords"` | **Crear** (opcional UI fase 1; incluir en BD) |
| `PayrollPeriod` | `hr."PayrollPeriods"` | **Crear** — reemplaza concepto mes/anio |
| `PayrollRun` | `hr."PayrollRuns"` | **Crear** |
| `PayrollDetail` | `hr."PayrollDetails"` | **Reemplazar** tabla legacy (mismo nombre, estructura nueva) |
| `PayrollEarningLine` | `hr."PayrollEarningLines"` | **Crear** |
| `PayrollDeductionLine` | `hr."PayrollDeductionLines"` | **Crear** |
| `IsrBracket` | `hr."IsrBrackets"` | **Crear** |
| `Holiday` | `hr."Holidays"` | **Crear** |
| `AguinaldoRun` / `AguinaldoDetail` | `hr."AguinaldoRuns"` / `hr."AguinaldoDetails"` | **Crear** |
| `EmployeeTermination` | `hr."EmployeeTerminations"` | **Crear** |
| `LeaveType` / `LeaveRequest` / `VacationBalance` | `hr."LeaveTypes"` / `hr."LeaveRequests"` / `hr."VacationBalances"` | **Crear** |
| `IsrDeclaration` | `hr."IsrDeclarations"` | **Crear** (reportes MH; puede ser sub-entrega 10c) |
| `Payroll` (legacy) | `hr."Payroll"` | **Deprecar** — renombrar a `hr."PayrollLegacy"` o eliminar tras migracion vacia |

**Flujo de estados corrida (`PayrollRuns.Status`):** `EN_REVISION` → `APROBADA` → `PAGADA` | `ANULADA`.

**Flujo periodo:** `IsClosed` + `ClosedAt` + `ClosedBy` (FK `system.WebUsers`).

### 17.6 Checklist migracion SQL (Fase 0b)

Ejecutar en orden; cada paso idempotente donde sea posible:

- [ ] Backup staging antes de migrar.
- [x] **Migrar todas las PKs/FKs a UUID** (empleados, ordenes, sesiones caja, etc.) — alinear con Beraka; Fase 0b esquema base cerrada en repo.
- [ ] Renombrar o archivar `hr."Payroll"` y `hr."PayrollDetails"` legacy si existen datos.
- [ ] Crear tablas nuevas segun 17.5 (ver scripts de referencia en `beraka-core-api/prisma/schema/payroll.prisma`, `hr.prisma`, `employees.prisma`, `leaves.prisma`).
- [ ] `ALTER TABLE hr."Employees"` con columnas 17.4 y nuevo CHECK `ContractType`.
- [ ] Crear indices: `(PayrollRunId, EmployeeId)` UNIQUE en detalle; indices por `Status`, `PeriodId`, `EmployeeId`.
- [ ] Seed bancos SV (17.7).
- [ ] Seed tipos documento (17.7).
- [ ] Seed `IsrBrackets` por anio vigente (mensual y quincenal).
- [ ] Seed feriados nacionales por anio.
- [x] Actualizar modelos EF Core: `Employee` ampliado; eliminados `Payroll`/`PayrollDetail` legacy del proyecto WPF.
- [x] Validar que FKs operativas WPF (`sales.Orders.EmployeeId`, etc.) usan **UUID** y EF Core mapea `Guid`.
- [ ] `dotnet build` + smoke test conexion BD.

### 17.7 Seeds iniciales (referencia Beraka)

**Bancos** (`seedBanks` en `beraka-core-api/prisma/seed.ts`) — 16 entradas incluyendo BAC, Agricola, Cuscatlan, Davivienda, Promerica, Industrial, Hipotecario, Azul, Citibank, Procredit, cooperativas y `Efectivo / No aplica`. El adminweb debe permitir **alta, edicion y baja logica** via modulo `banks/` (como Beraka `banks.controller.ts` — catalogo editable, no solo lectura).

**Tipos de documento requerido** (`seedDocumentTypes`):

| Nombre | Obligatorio | Vencimiento |
|---|---|---|
| DUI | Si | No |
| NIT | Si | No |
| ISSS | Si | No |
| AFP | Si | No |
| Antecedentes Penales | Si | Si |
| Certificado de Salud | Si | Si |
| Manipulacion de Alimentos | Si | Si |
| Licencia de Conducir | No | Si |

Modulo `required-document-types/` editable desde adminweb.

### 17.8 Motor de calculo (reglas legales SV)

Portar desde Beraka (`payroll.constants.ts`, `payroll.calculator.ts`, `payroll.builder.ts`):

| Concepto | Regla |
|---|---|
| AFP empleado | 7.25% sobre **salario ordinario** del periodo |
| AFP patronal | 7.75% sobre salario ordinario |
| ISSS empleado | 3% sobre salario ordinario, tope **$1,000/mes** prorrateado |
| ISSS patronal | 7.5% sobre misma base ISSS |
| ISR | Tabla progresiva `IsrBrackets` por anio y tipo periodo |
| Horas extra diurnas / feriadas | ×2.0 del valor hora ordinaria |
| Horas extra nocturnas | ×2.5 |
| Honorarios | Retencion **10%** ISR; sin AFP/ISSS |
| Dias base | MENSUAL=30, QUINCENAL=15, SEMANAL=7 (ajustable por calendario del periodo) |
| Bonos / viaticos | Incluidos en bruto; excluidos de base ISR segun politica Beraka |

**Correccion critica vs SQL actual:** eliminar la suposicion de que ISSS/AFP aplican sobre `TotalIncome` con bonos y extras.

### 17.9 Planeacion backend (`FlexoCable-backend`) — sin implementar

Modulos a crear (estructura espejo Beraka, rutas bajo `/api/v1/`):

```text
modules/
  employees/                  # CRUD + getEmployeeFicha + ficha PDF
  employee-bank-accounts/
  employee-documents/
  banks/                      # CRUD catalogo editable
  required-document-types/
  salary-history/
  payroll-periods/
  payroll-runs/               # generate, update detail, approve, pay, delete
    payroll.calculator.ts     # portado
    payroll.builder.ts
    payroll.exports.service.ts  # Excel + PDF — funcion principal exporte
  aguinaldo/
  employee-terminations/
  leave-requests/
  vacation-balances/
  holidays/
  isr-declarations/           # opcional sub-fase
```

**Roles sugeridos:** `ADMIN` y `ACCOUNTANT` con acceso planilla; `OWNER` lectura reportes; permisos granulares futuros tipo Beraka (`module` + `action`).

### 17.10 Planeacion adminweb (`FlexoCable-adminweb`) — sin implementar

| Ruta | Funcionalidad |
|---|---|
| `/empleados` | Listado, alta, edicion, asignacion PIN |
| `/empleados/[id]/ficha` | Vista expediente + descarga PDF |
| `/empleados/[id]/bancos` | Cuentas bancarias del empleado |
| `/empleados/[id]/documentos` | Expediente documental |
| `/rrhh/bancos` | Catalogo editable de bancos |
| `/rrhh/tipos-documento` | Tipos de documento requerido |
| `/rrhh/feriados` | Calendario feriados |
| `/planilla/periodos` | CRUD periodos; cerrar periodo |
| `/planilla/corridas` | Generar, revisar lineas, aprobar, marcar pagada |
| `/planilla/corridas/[id]/export` | Descarga Excel + PDF lote |
| `/planilla/aguinaldo` | Corrida anual |
| `/planilla/liquidaciones` | Finiquitos |
| `/planilla/vacaciones` | Saldos y solicitudes |

### 17.11 Sub-fases sugeridas dentro de Fase 10

| Sub-fase | Alcance | Dependencia |
|---|---|---|
| **10a — Planilla ordinaria** | Periodos, corridas, calculo, Excel/PDF, ficha empleado | Fase 0b + Fase 8 scaffold |
| **10b — Aguinaldo y vacaciones** | Corridas aguinaldo; leave requests; impacto en planilla | 10a |
| **10c — Liquidaciones e ISR** | `EmployeeTerminations`; declaraciones ISR opcionales | 10a |

Todas deben tener **tablas creadas en Fase 0b** aunque la UI se entregue incrementalmente.

### 17.12 Alcance WPF vs administracion web

| Funcionalidad | Donde vive | WPF |
|---|---|---|
| Login PIN caja | WPF + `hr.Employees` | Si |
| Ventas, DTE, corte, impresion | WPF | Si |
| Consulta stock en caja | WPF (solo lectura) | Si |
| CRUD empleados, PINs, expediente | `FlexoCable-backend` + adminweb | No — solo consume PIN ya hasheado |
| Planilla, aguinaldo, liquidaciones | `FlexoCable-backend` | No |
| Inventario admin (entradas, ajustes) | `FlexoCable-backend` | No |
| Login admin (`WebUsers`) | `FlexoCable-backend` JWT | No — **no hay admin web en C#** |
| IDs en BD | UUID en todas las tablas | EF Core usa `Guid`; misma BD que Node |
