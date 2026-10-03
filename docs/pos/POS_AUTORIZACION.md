# Autorización real del POS

## Diseño

La aplicación registra `IAuthorizationGuard` como singleton junto con la sesión local. Cada operación sensible lo invoca al comienzo del servicio. El guard rechaza una sesión ausente, un empleado inactivo o un `actingEmployeeId` distinto al autenticado y recarga el empleado con su puesto desde PostgreSQL. La decisión se ejecuta en `PosAuthorizationPolicy`, una política pura y testeable.

Los servicios protegidos reciben el guard como dependencia obligatoria; si la composición no lo proporciona, el constructor lanza `ArgumentNullException`. No existe un modo legacy que otorgue autorización por ausencia del guard. Los dobles de autorización están únicamente en el proyecto de tests y conceden o deniegan permisos explícitamente.

## Matriz de operaciones

| Operación | Permiso | Servicio | Cobertura automatizada actual |
|---|---|---|---|
| Crear empleado | `AdministrarUsuarios` | `EmployeeService.CreateAsync` | `SensitiveServiceAuthorizationIntegrationTests.EmployeeService_Create_SinAdministracion_Rechaza`; `PinUniquenessIntegrationTests.CreateAsync_PinRepetido_RechazaSinRevelarPropietario` |
| Editar empleado, incluido cambio de puesto | `AdministrarUsuarios` | `EmployeeService.UpdateAsync` | guard común; protección de último administrador en servicio |
| Cambiar PIN | `AdministrarUsuarios` | `EmployeeService.SetPinAsync` | `PinUniquenessIntegrationTests.SetPinAsync_PinRepetido_RechazaSinRevelarPropietario`; prueba concurrente del mismo PIN |
| Desactivar empleado | `AdministrarUsuarios` | `EmployeeService.DeactivateAsync` | guard común; protección transaccional de último administrador |
| Lecturas de empleados, departamentos y puestos | `AdministrarUsuarios` | `EmployeeService.Get*Async` | guard común y política pura |
| Crear, editar, activar y desactivar producto, precio y costo | `AdministrarCatalogo` | `ProductCatalogService` | `SensitiveServiceAuthorizationIntegrationTests.ProductCatalogService_Create_SinAdministracion_Rechaza` |
| Guardar y establecer impresora predeterminada | `AdministrarConfiguracion` | `PrinterConfigService` | `SensitiveServiceAuthorizationIntegrationTests.PrinterConfigService_Save_SinAdministracion_Rechaza` |
| Entradas, ajustes y salida directa de inventario | `OperarInventario` | `InventoryService` | `SensitiveServiceAuthorizationIntegrationTests.InventoryService_RegisterEntry_Cajero_Rechaza` |
| Altas, cambios y bajas de proveedores | `OperarInventario` | `SupplierService` | `SensitiveServiceAuthorizationIntegrationTests.SupplierService_Create_Cajero_Rechaza` |
| Altas, cambios y bajas de clientes | `OperarCaja` | `CustomerService` | `SensitiveServiceAuthorizationIntegrationTests.CustomerService_Create_Vendedor_Rechaza` |
| Venta de mostrador y facturación de confección | `OperarCaja` | `OrderService` | `SensitiveServiceAuthorizationIntegrationTests.OrderService_CreateCashSale_Vendedor_Rechaza` |
| Creación de orden de confección | `OperarInventario` | `OrderService` | política del servicio y guard común |

Las lecturas de catálogo, stock, proveedores y clientes también pasan por el guard del módulo operativo. No existe una ruta anónima. `CashSessionService`, `ReturnService` y `SalesHistoryService` conservan sus validaciones transaccionales y de empleado activo. `DteService.RestoreInventory` no fue modificado.

## Último administrador activo

`EmployeeService.UpdateAsync` y `DeactivateAsync` toman un advisory lock transaccional con nombre, cuentan administradores activos y rechazan dejar cero. Con dos administradores, la baja de uno sí puede completarse; con uno, se rechaza con un mensaje claro.

La validez legal o fiscal de empleados, clientes y permisos operativos queda a verificar con contador/MH cuando corresponda; esta capa aplica autorización técnica.
