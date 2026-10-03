# POS — QA de seguridad, fase 3

Esta fase endurece la autorización de la fase 1 y cubre ventas/devoluciones concurrentes, idempotencia por intento, reloj del negocio, configuración de conexión y el rol de base de datos. Las operaciones sensibles reciben `IAuthorizationGuard` obligatorio y fallan cerradas; no existe un camino de compatibilidad que conceda permisos cuando falta el guard.

## Decisiones principales

- `ClientRequestId` identifica el intento lógico de venta o devolución. Una repetición idéntica devuelve el mismo resultado; cualquier diferencia de empleado, caja, líneas, cantidades, reingreso, método o monto se rechaza con un mensaje de nueva operación.
- Las ventas y devoluciones bloquean todos los productos ordenados por UUID con una sola consulta `ANY(...) ... ORDER BY ... FOR UPDATE`. Los errores PostgreSQL `40001` y `40P01` reintentan hasta dos veces en contextos y transacciones nuevas.
- El reloj de persistencia es `TimeProvider`; los textos de recibo y DTE convierten el instante UTC a `Negocio:ZonaHoraria`. El criterio de fecha/hora de emisión y el costo contable de devoluciones quedan a verificar con contador/MH.
- El PIN de caja usa únicamente eventos persistentes en `system."AuditLog"`. Un PIN válido de un empleado sin autorización para devoluciones se registra como fallo y muestra el mismo mensaje genérico que un PIN incorrecto.
- No se puede desactivar al último administrador activo ni cambiar su puesto a uno no administrativo; la comprobación está protegida con lock advisory transaccional.

## Matriz de autorización de operaciones sensibles

Las pruebas `SensitiveServiceAuthorizationIntegrationTests` usan el `AuthorizationGuard` real y una sesión cuyo empleado se recarga desde PostgreSQL. Para cada operación se comprueba permiso insuficiente sin cambios, autorización con persistencia, identificador actuante suplantado sin cambios y empleado de sesión desactivado después del login sin cambios. `PrinterConfigService` no recibe `userId` en su contrato; sus casos cubren permiso insuficiente, persistencia y recarga de empleado desactivado, y la suplantación queda como no aplicable.

| Servicio | Operaciones cubiertas | Permiso exigido |
|---|---|---|
| `EmployeeService` | `CreateAsync`, `UpdateAsync`, `SetPinAsync`, `DeactivateAsync` | `AdministrarUsuarios` |
| `ProductCatalogService` | `CreateProductAsync`, `UpdateProductAsync`, `DeactivateProductAsync`, `ReactivateProductAsync` | `AdministrarCatalogo` |
| `PrinterConfigService` | `SaveAsync`, `SetDefaultAsync` | `AdministrarConfiguracion` |
| `InventoryService` | `DecreaseStockAsync`, `RegisterEntryAsync`, `RegisterAdjustmentAsync` | `OperarInventario` |
| `SupplierService` | `CreateAsync`, `UpdateAsync`, `DeactivateAsync` | `OperarInventario` |
| `CustomerService` | `CreateAsync`, `UpdateAsync`, `DeactivateAsync` | `OperarCaja` |
| `OrderService` | `CreateCashSaleAsync`, `CreateConfectionOrderAsync`, `CompleteConfectionOrderAsync` | `OperarCaja` o `OperarInventario` según el flujo |

`CashSessionService` no implementa `IAuthorizationGuard`; valida empleado activo/permisos dentro de sus propios contratos. `ReturnService` tampoco usa ese guard: valida al ejecutor desde la BD y exige PIN autorizador, con lockout persistente.

## Concurrencia y reintentos

`Phase3ConcurrencyIntegrationTests` cubre dos ventas concurrentes en una sesión con productos `[A,B]` y `[B,A]`, idempotencia concurrente con el mismo `ClientRequestId`, venta concurrente con cierre y dos devoluciones de órdenes distintas con productos invertidos. `PostgresTransientRetryTests.SerializationRetry_ReusesSameClientRequestId` simula `40001` y verifica que el callback conserva la misma clave.

El cierre toma `FOR UPDATE` sobre la fila padre de la sesión; la venta toma `FOR SHARE` y después bloquea productos mediante `ANY(...) ORDER BY "id" FOR UPDATE`. El writer de devoluciones conserva el mismo orden definido por PostgreSQL. No se usa un `OrderBy` de GUID en C# para decidir bloqueos.

## Rol mínimo y lockout

`DatabaseRoleIntegrationTests.PosAppRole_ExecutesCompletePosFlowAndPersistsPinAudit` ejecuta apertura, venta, devolución, cierre y `PIN_FAIL`/lockout/`PIN_OK` autenticado como `pos_app`. La conexión usa `docs/pos/pos_app_rol_minimo.sql`, que otorga solo DML de las tablas POS y el `EXECUTE` de la función advisory concreta requerida. Los datos fiscales de devolución y emisión siguen marcados a verificar con contador/MH.

## Riesgos y verificaciones externas

La fecha de emisión DTE, el tratamiento fiscal de devoluciones, la nota de crédito, el costo de reingreso y el redondeo deben validarse con contador y Ministerio de Hacienda antes de producción. El hardware de impresión y el certificado TLS de PostgreSQL también requieren una prueba de despliegue.

La fecha de contratación mostrada en `UsuariosView` es un dato laboral, no fiscal; puede continuar usando la fecha de la PC y queda fuera del criterio de hora del negocio.
