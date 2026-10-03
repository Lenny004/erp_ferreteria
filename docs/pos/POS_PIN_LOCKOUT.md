# PIN: unicidad, lockout persistente y desbloqueo administrativo

## Unicidad

`EmployeeService.CreateAsync` y `SetPinAsync` verifican los hashes bcrypt dentro de una transacción y bajo el advisory lock de PIN. La auditoría `PIN_CHANGE` no contiene el PIN ni su hash. El login recorre los candidatos activos y rechaza coincidencias duplicadas heredadas.

## Lockout por terminal

El flujo de login no identifica al empleado antes de verificar el PIN; por eso un fallo no se atribuye a una persona. `PinAttemptService` usa `system."AuditLog"` append-only, con:

- `TableName = "pos.PinAttempts"`.
- `RecordId = "Caja:<código configurado>"`.
- `Action = "PIN_FAIL"`, `"PIN_OK"` o `"PIN_UNLOCK"`.
- `NewData` únicamente con el código de terminal; nunca contiene PIN, hash ni identidad inferida.

`PIN_OK` se registra para auditoría, pero no elimina los fallos de la terminal. `PinLockoutPolicy` ignora ese evento para el cálculo, conserva los fallos dentro de una ventana deslizante y aplica una duración progresiva a cada nuevo bloqueo que ocurra en esa ventana. Al salir los fallos de la ventana, el estado vuelve a cero. Si no se puede leer o escribir la auditoría, el flujo falla cerrado con `PinLockoutUnavailableException`.

El estado es por terminal (`RecordId = Caja:<código>`), no por empleado objetivo. El PIN se verifica antes de conocer qué empleado lo introdujo; por lo tanto, un fallo no tiene un objetivo confiable que atribuir. Un `PIN_OK` no reinicia la racha: solo `PIN_UNLOCK` la reinicia de forma administrativa.

Mientras existe un bloqueo activo, la UI no intenta validar otro PIN y `PinAttemptService` rechaza sin persistir cualquier `PIN_FAIL` concurrente. Esos intentos no se cuentan y no extienden el vencimiento. Cada nuevo bloqueo se calcula con progresión dentro de la ventana, pero nunca supera `MaxLockoutMinutes`; cuando los fallos salen de la ventana deslizante la progresión vuelve a comenzar.

## Desbloqueo administrativo

`IPinUnlockService.UnlockTerminalAsync` exige `PosPermission.AdministrarUsuarios` mediante `IAuthorizationGuard`, valida el código con `CashRegisterInputRules.ValidateCashRegisterCode` y persiste `PIN_UNLOCK` en una transacción con el mismo advisory lock del lockout. La fila usa la misma `TableName`, `RecordId = Caja:<código>`, `UserId` del administrador autenticado y `NewData` con el terminal y, si se ingresó, el motivo. No se agrega DDL: `pos_app` ya tiene `SELECT, INSERT` sobre `system."AuditLog"`.

La sección **Desbloquear terminal** de `UsuariosView` permite usar la caja configurada por defecto, indicar un motivo opcional y mostrar errores de autorización o validación en español. La operación puede ejecutarse desde otra terminal con sesión administrativa.

El riesgo residual es que un empleado puede provocar bloqueos de hasta `MaxLockoutMinutes` por terminal. El evento queda auditado por terminal y un administrador autorizado puede liberar la terminal sin borrar la evidencia histórica.

## Parámetros

La sección `PinLockout` de `Config/appsettings.json` tiene valores por defecto documentados:

| Parámetro | Predeterminado | Significado |
|---|---:|---|
| `MaxAttempts` | 5 | Fallos recientes que activan un bloqueo. |
| `WindowMinutes` | 15 | Ventana deslizante de conteo. |
| `InitialLockoutMinutes` | 2 | Primer bloqueo. |
| `ProgressiveMultiplier` | 2 | Multiplicador del bloqueo sucesivo. |
| `MaxLockoutMinutes` | 30 | Tope de duración. |

La progresión y la ventana son reglas puras en `PinLockoutPolicy`; `TimeProvider` permite probarlas sin depender del reloj del equipo. No se agregó DDL: los eventos existentes de `system."AuditLog"` son suficientes.

## Pruebas

Las pruebas unitarias cubren cuatro fallos, el umbral, expiración conservando la ventana, éxito de otro empleado, progresión con tope, fallos continuos durante horas, intentos durante bloqueo activo, desbloqueo administrativo y salida de ventana. `PinUnlockIntegrationTests` cubre administrador autorizado, cajero, suplantación e inactividad. `DatabaseRoleIntegrationTests.PosAppRole_ExecutesCompletePosFlowAndPersistsPinAudit` comprueba también el desbloqueo usando `pos_app`. Cada caso usa un código de caja único.
