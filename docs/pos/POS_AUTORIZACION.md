# Autorización real del POS

## Diseño

La aplicación registra `IAuthorizationGuard` como singleton junto con la sesión local. Cada operación sensible lo invoca al comienzo del servicio, antes de abrir una transacción o modificar datos. El guard toma el `Id` de `ICurrentSessionService`, rechaza una sesión ausente o un `actingEmployeeId` diferente, y recarga con `AsNoTracking()` el empleado y su `Position` desde PostgreSQL. La decisión se ejecuta en `PosAuthorizationPolicy`, una política pura y testeable.

Un empleado inactivo no recibe ningún permiso. Los puestos se comparan con `Trim()` y `OrdinalIgnoreCase`. La configuración es:

```json
"Autorizacion": {
  "PuestosAdministracion": [ "Administrador" ]
}
```

Si la lista está vacía, la política falla cerrada y ningún puesto recibe permisos administrativos. El nombre `Administrador` se conserva porque es el único puesto administrativo existente en el repositorio y coincide con `SalesHistory:FullHistoryPositionNames`.

## Matriz de operaciones

| Operación | Permiso | Aplicación | Cobertura automatizada |
|---|---|---|---|
| Crear, editar, cambiar PIN y desactivar empleado; listar RRHH | `AdministrarUsuarios` | `EmployeeService` | `SensitiveServiceAuthorizationIntegrationTests.EmployeeService_Create_SinAdministracion_Rechaza`, `PosAuthorizationPolicyTests` |
| Crear, editar, activar y desactivar producto, incluyendo precio y costo | `AdministrarCatalogo` | `ProductCatalogService` | `SensitiveServiceAuthorizationIntegrationTests.ProductCatalogService_Create_SinAdministracion_Rechaza`, `PosAuthorizationPolicyTests` |
| Guardar y establecer impresora predeterminada | `AdministrarConfiguracion` | `PrinterConfigService` | `SensitiveServiceAuthorizationIntegrationTests.PrinterConfigService_Save_SinAdministracion_Rechaza`, `PosAuthorizationPolicyTests` |
| Entradas, ajustes y salida directa de inventario | `OperarInventario` | `InventoryService` | `SensitiveServiceAuthorizationIntegrationTests.InventoryService_RegisterEntry_Cajero_Rechaza`, `PosAuthorizationPolicyTests` |
| Altas, cambios y bajas de proveedores | `OperarInventario` | `SupplierService` | `SensitiveServiceAuthorizationIntegrationTests.SupplierService_Create_Cajero_Rechaza`, `PosAuthorizationPolicyTests` |
| Altas, cambios y bajas de clientes | `OperarCaja` | `CustomerService` | `SensitiveServiceAuthorizationIntegrationTests.CustomerService_Create_Vendedor_Rechaza`, `PosAuthorizationPolicyTests` |
| Venta de mostrador y facturación de confección | `OperarCaja` | `OrderService` | `SensitiveServiceAuthorizationIntegrationTests.OrderService_CreateCashSale_Vendedor_Rechaza`, `PosAuthorizationPolicyTests` |
| Creación de orden de confección | `OperarInventario` | `OrderService` | Política pura; cobertura de servicio queda ligada al flujo de inventario |

Las lecturas del catálogo, stock, proveedores y clientes permanecen libres porque alimentan pantallas operativas. Las lecturas de empleados (`GetEmployeesAsync`, `GetByIdAsync`, departamentos y puestos) quedan protegidas por `AdministrarUsuarios` porque en este repositorio solo son usadas por `UsuariosView`.

`CashSessionService`, `ReturnService` y `SalesHistoryService` conservan sus validaciones transaccionales existentes, incluyendo la comprobación de empleado activo. `ConfigService` sigue siendo un marcador sin escritura de configuración de negocio; no se añadió una operación ficticia. `DteService.RestoreInventory` no fue modificado.

## Decisiones y riesgos

- La auditoría usa el identificador que devuelve el guard, nunca un `userId` no verificado. El PIN se audita dentro del mismo `DbContext` y transacción que el cambio de hash.
- No se implementó todavía una regla especial de auto-desactivación o de conservación de un administrador activo. Es un riesgo operativo: un administrador podría quitarse permisos o desactivarse y dejar el sistema sin otro administrador; la siguiente fase debe resolverlo con una operación transaccional y una prueba de concurrencia.
- Los servicios conservan parámetros opcionales de guard únicamente para los harnesses legacy enlazados del proyecto de tests; la composición de producción en `App.xaml.cs` siempre registra `IAuthorizationGuard`.
- La UI filtra `NavSections` con permisos efectivos. `ProductosView` permanece en solo lectura sin `AdministrarCatalogo`; `UsuariosView` e `ImpresorasView` muestran el mensaje seguro de `UnauthorizedOperationException` si se abren por una ruta no prevista.
- La validez legal o fiscal de los datos de empleados y clientes queda a verificar con contador/MH; esta capa solo aplica autorización técnica.
