# Rol PostgreSQL de mínimo privilegio del POS

`docs/pos/pos_app_rol_minimo.sql` es una propuesta idempotente para aplicación controlada por el DBA. No se ejecuta al iniciar la aplicación, no modifica `Squema.sql` y no contiene contraseñas.

## Matriz de privilegios

| Tabla | Privilegios de `pos_app` | Justificación y uso en POS |
|---|---|---|
| `public."MeasurementTypes"`, `public."Families"`, `public."Subfamilies"`, `public."SaleUnits"`, `public."ProductSaleUnits"`, `public."VolumeDiscounts"` | SELECT | Catálogo y cálculo de venta; lecturas en `ProductCatalogService.cs:150`, `OrderService.cs:657` y `InventoryService.cs:342`. |
| `public."Products"` | SELECT, INSERT, UPDATE | Alta y mantenimiento de catálogo, cambios de stock y lecturas; `ProductCatalogService.cs:80`, `InventoryService.cs:148`, `OrderService.cs:685`, `ReturnPersistenceContracts.cs:218`. |
| `public."InventoryMovements"` | SELECT, INSERT | El POS agrega movimientos en `InventoryService.cs:148`, `OrderService.cs:394`, `OrderService.cs:689`, `DteService.cs:640` y `ReturnPersistenceContracts.cs:217`; no actualiza filas existentes. |
| `public."StockAlerts"` | SELECT, INSERT, UPDATE | Alertas de stock; `InventoryService.cs:383` y `InventoryService.cs:398`. |
| `purchasing."Suppliers"` | SELECT, INSERT, UPDATE | CRUD de proveedores; `SupplierService.cs:48`, `SupplierService.cs:91`. |
| `public."Customers"` | SELECT, INSERT, UPDATE | CRUD y uso en ventas/DTE; `CustomerService.cs:47`, `OrderService.cs:271`, `DteService.cs:554`. |
| `sales."Orders"`, `sales."OrderDetails"`, `sales."CashSessions"` | SELECT, INSERT, UPDATE | Ventas, órdenes, apertura/cierre y estados; `OrderService.cs:111`, `CashSessionService.cs:225`, `CashSessionService.cs:340`. |
| `sales."Payments"` | SELECT, INSERT | El pago se agrega al registrar la venta en `OrderService.cs:392`; `CashSessionService.cs:405` solo lee. No hay UPDATE. |
| `sales."Returns"`, `sales."ReturnDetails"` | SELECT, INSERT | La devolución y sus detalles se insertan en `ReturnPersistenceContracts.cs:222` y `ReturnPersistenceContracts.cs:242`; lecturas en `ReturnService.cs:219` y `ReturnContracts.cs:399`. No hay UPDATE. |
| `sales."CashMovements"` | SELECT, INSERT | Los reintegros se insertan en `ReturnPersistenceContracts.cs:246`; lectura en `ICashMovementReader.cs:25`. No hay UPDATE. |
| `dte."DteConfig"`, `dte."DteIssued"`, `dte."DteContingency"` | SELECT, INSERT, UPDATE | Emisión, transmisión y contingencia; `DteService.cs:183`, `DteService.cs:484`, `DteService.cs:497`. |
| `hr."Departments"`, `hr."Positions"` | SELECT | Referencias de empleados y autorización; `AuthorizationGuard.cs:90`, `EmployeeService.cs:100`. No hay escritura POS en estas tablas. |
| `hr."Employees"` | SELECT, INSERT, UPDATE | Identidad, permisos y PIN; `AuthorizationGuard.cs:90`, `EmployeeService.cs:74`, `EmployeeService.cs:149`. |
| `system."Settings"` | SELECT, INSERT, UPDATE | Configuración operativa; `ConfigService.cs`. |
| `system."Printers"` | SELECT, INSERT, UPDATE | Configuración de impresión; `PrinterConfigService.cs:52`, `PrinterConfigService.cs:111`. |
| `system."AuditLog"` | SELECT, INSERT | Auditoría append-only y lockout; `AuditService.cs:73`, `AuditService.cs:97`, `PinAttemptService.cs:94`, `CashSessionService.cs:264`. No hay UPDATE. |

No se concede `DELETE` a las tablas POS. Las claves son UUID y no requieren privilegios sobre secuencias. El script concede únicamente `EXECUTE` sobre `pg_advisory_xact_lock(bigint)`, necesario para serialización de PIN y aperturas.

La decisión fiscal/legal de DTE y devoluciones queda **a verificar con contador/MH**.

## Verificación del rol

`DatabaseRoleIntegrationTests.PosAppRole_ExecutesCompletePosFlowAndPersistsPinAudit` prueba el flujo de caja con conexión `pos_app`. `DatabaseRoleIntegrationTests.PosAppRole_AllowsPosReadAndRejectsWebUsersAndDdl` verifica además que el rol no accede a `system."WebUsers"`, no crea DDL y no puede actualizar `system."AuditLog"`, `hr."Positions"` ni `hr."Departments"`.
