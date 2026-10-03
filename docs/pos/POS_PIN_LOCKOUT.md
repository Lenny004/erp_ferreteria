# PIN: unicidad y lockout persistente

## Unicidad

`EmployeeService.CreateAsync` y `SetPinAsync` verifican los hashes bcrypt dentro de una transacción y bajo el advisory lock de PIN. La auditoría `PIN_CHANGE` no contiene el PIN ni su hash. El login recorre los candidatos activos y rechaza coincidencias duplicadas heredadas.

## Lockout por terminal

El flujo de login no identifica al empleado antes de verificar el PIN; por eso un fallo no se atribuye a una persona. `PinAttemptService` usa `system."AuditLog"` append-only, con:

- `TableName = "pos.PinAttempts"`.
- `RecordId = "Caja:<código configurado>"`.
- `Action = "PIN_FAIL"` o `"PIN_OK"`.
- `NewData` únicamente con el código de terminal; nunca contiene PIN, hash ni identidad inferida.

`PIN_OK` se registra para auditoría, pero no elimina los fallos de la terminal. `PinLockoutPolicy` ignora ese evento para el cálculo, conserva los fallos dentro de una ventana deslizante y aplica una duración progresiva a cada nuevo bloqueo que ocurra en esa ventana. Al salir los fallos de la ventana, el estado vuelve a cero. Si no se puede leer o escribir la auditoría, el flujo falla cerrado con `PinLockoutUnavailableException`.

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

Las pruebas unitarias cubren cuatro fallos, el umbral, expiración conservando la ventana, éxito de otro empleado, progresión con tope y salida de ventana. `PinLockoutPersistenceIntegrationTests` cubre persistencia entre instancias, vencimiento y el escenario `FallosTerminal_ExitoDeOtroEmpleado_NoReiniciaYBloquea`. Cada caso usa un código de caja único.
