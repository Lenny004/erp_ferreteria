# PIN: unicidad y lockout persistente

## Unicidad

`EmployeeService.CreateAsync` y `SetPinAsync` abren una transacción y toman `pg_advisory_xact_lock(735928559)`. Luego cargan todos los `PinHash` no nulos de empleados activos e inactivos y verifican el PIN propuesto con bcrypt. Si alguno coincide, se devuelve `ValidationException("Ese PIN no está disponible. Elija otro.")` sin identificar al dueño. Cambiar el PIN por el mismo PIN del propio empleado está permitido porque la comparación excluye su propio registro; el resultado sigue siendo único.

La auditoría `PIN_CHANGE` se agrega al mismo `FerreteriaDbContext` antes de `SaveChangesAsync` y no contiene el PIN ni el hash. El lock también cubre altas concurrentes para que solo una transacción pueda ganar un PIN.

El login no elige arbitrariamente entre datos heredados duplicados: `PinAuthService` verifica todos los candidatos activos ordenados por Id y rechaza si hay más de una coincidencia, registrando solo un warning sin PIN ni Ids.

El costo es aproximadamente `N × tiempo de bcrypt` por alta o cambio, donde `N` es la cantidad de hashes. Con el costo actual puede ser del orden de cientos de milisegundos por hash; debe medirse con el volumen real antes de aumentar el factor bcrypt.

## Lockout persistente

`system.AuditLog` alcanza para esta fase: es append-only, tiene índices por `(TableName, RecordId)` y `CreatedAt`, y sus columnas permiten la representación requerida. No se cambió el esquema ni se agregó DDL.

Cada evento usa:

- `TableName = "pos.PinAttempts"`.
- `RecordId = "Caja:<código configurado>"`.
- `Action = "PIN_FAIL"` o `"PIN_OK"`, ambos dentro de `VARCHAR(10)`.
- `UserId = null` y `NewData` solo con el terminal; nunca contiene el PIN.

La consulta del estado filtra `TableName`, `RecordId` y esas dos acciones, ordena por `CreatedAt DESC` y toma los últimos 100 eventos. La función pura `PinLockoutPolicy.Evaluate` reordena los eventos, reinicia con `PIN_OK`, bloquea al quinto fallo por dos minutos y vuelve a cero al vencer. El reloj se inyecta mediante `TimeProvider`.

`PinAttemptService` persiste tanto el login por PIN como el PIN del autorizador de devoluciones. Si PostgreSQL no responde al consultar o guardar, lanza `PinLockoutUnavailableException`; la UI muestra un mensaje seguro y no permite continuar. La implementación en memoria solo queda como adaptador de compatibilidad para los tests legacy que construyen el servicio sin infraestructura; la instancia de producción siempre usa el constructor registrado por DI.

Pruebas puras relevantes: `PinLockoutPolicyTests.Evaluate_CuatroFallos_NoBloquea`, `Evaluate_QuintoFallo_BloqueaDosMinutos`, `Evaluate_BloqueoVencido_ReiniciaRacha` y `Evaluate_PinCorrecto_ReiniciaRacha`.

La persistencia entre instancias y el vencimiento con `TimeProvider` se cubren en `PinLockoutPersistenceIntegrationTests.LockoutPersistente_SobreviveNuevaInstancia_YVenceAlAvanzarReloj`.

La cobertura de BD para repetición, concurrencia y duplicados heredados está en `PinUniquenessIntegrationTests`.
