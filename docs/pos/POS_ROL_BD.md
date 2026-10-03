# Rol PostgreSQL de mínimo privilegio del POS

`docs/pos/pos_app_rol_minimo.sql` es un script aditivo e idempotente para revisión y aplicación controlada por el operador. No se ejecuta al iniciar la aplicación, no modifica `Squema.sql` y no contiene ninguna contraseña. En una sesión administrativa, el operador puede fijarla con `\password pos_app`; debe usar una contraseña propia de producción y rotarla según la política de la organización.

El rol queda sin superusuario, `CREATEDB`, `CREATEROLE`, replicación, bypass de RLS y sin `CREATE` en los esquemas. El script revoca `DELETE` y no otorga privilegios sobre `system."WebUsers"`, `fiscal` ni tablas de planilla. La conexión de producción debe usar `SSL Mode=Require` (y validar el certificado según la infraestructura).

## Matriz tabla × operación × servicio

| Tabla | Operaciones | Servicios POS que la usan |
|---|---|---|
| `public.MeasurementTypes`, `public.Families`, `public.Subfamilies`, `public.SaleUnits`, `public.ProductSaleUnits`, `public.VolumeDiscounts` | SELECT/INSERT/UPDATE | `ProductCatalogService`, `OrderService`, `InventoryService` |
| `public.Products` | SELECT/INSERT/UPDATE | `ProductCatalogService`, `OrderService`, `InventoryService`, `ReturnService`, `DteService` |
| `public.InventoryMovements` | SELECT/INSERT/UPDATE | `InventoryService`, `OrderService`, `ReturnService`, `DteService` |
| `public.StockAlerts` | SELECT/INSERT/UPDATE | `InventoryService` |
| `purchasing.Suppliers` | SELECT/INSERT/UPDATE | `SupplierService`, `ProductCatalogService` |
| `public.Customers` | SELECT/INSERT/UPDATE | `CustomerService`, `OrderService`, `ReturnService` |
| `sales.Orders`, `sales.OrderDetails`, `sales.Payments` | SELECT/INSERT/UPDATE | `OrderService`, `SalesHistoryService`, `ReturnService`, `ReportService`, `DteService` |
| `sales.CashSessions`, `sales.CashMovements` | SELECT/INSERT/UPDATE | `CashSessionService`, `OrderService`, `ReturnService`, `ReportService` |
| `sales.Returns`, `sales.ReturnDetails` | SELECT/INSERT/UPDATE | `ReturnService`, `SalesHistoryService`, `ReportService` |
| `dte.DteConfig`, `dte.DteIssued`, `dte.DteContingency` | SELECT/INSERT/UPDATE | `DteService`, `DteJsonBuilder`, historial DTE |
| `hr.Employees`, `hr.Positions`, `hr.Departments` | SELECT/INSERT/UPDATE | `EmployeeService`, `PinAuthService`, `AuthorizationGuard`, `SalesHistoryService` |
| `system.Settings` | SELECT/INSERT/UPDATE | `ConfigService`, configuración del POS |
| `system.Printers` | SELECT/INSERT/UPDATE | `PrinterConfigService`, impresión |
| `system.AuditLog` | SELECT/INSERT/UPDATE | `AuditService`, `PinAttemptService`, `CashSessionService`, `ReturnService` |

El código no ejecuta borrados de negocio en estos flujos; por eso no se concede `DELETE`. Tampoco usa secuencias de estas tablas: las claves son UUID. Las funciones necesarias para el lock transaccional de PostgreSQL conservan el `EXECUTE` público predeterminado; si la instalación lo revoca, debe concederse únicamente la función requerida por el DBA.

## Flujo de aceptación del rol

La prueba `DatabaseRoleIntegrationTests.PosAppRole_ExecutesCompletePosFlowAndPersistsPinAudit` abre una caja, registra una venta de contado, registra una devolución sin reintegro, cierra la caja y ejecuta el lockout persistente (`PIN_FAIL` hasta bloquear, seguido de `PIN_OK`) usando una conexión autenticada como `pos_app`. El script concede explícitamente solo `pg_advisory_xact_lock(bigint)` además de las tablas POS requeridas; no concede `GRANT ALL`.

## Cadena de conexión y rotación

La prioridad de configuración es la de `Host.CreateDefaultBuilder`: variable de entorno `ConnectionStrings__FerreteriaDB` y, en desarrollo, User Secrets (`ConnectionStrings:FerreteriaDB`). `Config/appsettings.json` conserva solo valores no sensibles y no debe recibir contraseñas. Si falta usuario o contraseña, el POS muestra un mensaje operativo y termina ordenadamente.

La credencial que estuvo versionada debe considerarse expuesta y ROTARSE en todos los lugares donde se haya reutilizado. No se reproduce aquí. DPAPI para almacenamiento local de cajas queda como siguiente paso si la política de despliegue requiere guardar un secreto local; esta fase no agrega paquetes ni persiste credenciales nuevas.
