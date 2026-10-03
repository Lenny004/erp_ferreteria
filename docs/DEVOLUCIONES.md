# Devoluciones POS — Fase 3: implementada

La aplicación busca ventas COMPLETADA, calcula devoluciones parciales o totales y las confirma con autorización por PIN. La orden original no se modifica: conserva COMPLETADA en `sales."Orders"` y sus detalles.

## Decisiones de Botti

| Tema | Decisión aplicada |
|---|---|
| Estado de la orden | Nunca cambia por una devolución. |
| Estado de devolución | `COMPLETADA` o `ANULADA`; la anulación queda sin flujo en esta fase. |
| Reintegros | `EFECTIVO`, `TARJETA`, `TRANSFERENCIA` y `NINGUNO`. No se ofrecen saldos a favor ni instrumentos equivalentes. |
| Inventario | `RestockQuantity = quantity`, la misma unidad que descontó `OrderService`; no se multiplica por `UnitsPerPackage`. |
| Costo | `OrderDetails.UnitCost` original, provisional y **a verificar con contador / normativa MH**. |
| Efectivo | `sales."CashMovements"`, ligado a `ReturnId`, con monto positivo y tipo `DEVOLUCION_EFECTIVO`. |
| Caja | `IdxCashSessionOpenByRegister` impide dos sesiones abiertas en una misma caja. |
| Fiscal | No se emite documento en este flujo; estado y tratamiento quedan a verificar con contador / normativa MH. |

El DDL de las tres tablas y del índice proviene literalmente del backend, PR #11, squash `8fdac39`, archivo `docs/pos/2_pos_devoluciones_squema.sql`. `Ferreteria.PuntoVenta/Squema.sql` es la referencia parcial que aplica el fixture; la fuente de verdad del esquema son las migraciones de Prisma del backend (ver `docs/pos/POS_SQUEMA_REFERENCIA.md`).

## Flujo transaccional

1. La UI genera una sola `ClientRequestId` por intento y solicita el PIN en un `PasswordBox` modal.
2. `PinAuthService` valida bcrypt; `IPinAttemptService` limita intentos y nunca se persiste el PIN.
3. El servicio exige ejecutor activo con `CanCashier` o puesto de historial completo. El PIN debe pertenecer a un empleado activo cuyo puesto esté en `SalesHistory:FullHistoryPositionNames`; si el request declara otro autorizador, se rechaza.
4. Se abre `Serializable`, se bloquea la orden con `FOR UPDATE` y recién después se leen las devoluciones `COMPLETADA` existentes.
5. Se valida vendido menos ya devuelto, el método de reintegro, cantidades positivas, montos y referencias. Para efectivo se bloquea la sesión `ABIERTA` de la caja configurada.
6. `EfReturnWriter` inserta `Returns` y `ReturnDetails`, bloquea cada producto, actualiza stock, crea el kardex, crea el movimiento de efectivo cuando aplica y agrega dos eventos de auditoría.
7. Un único `SaveChanges` y el `commit` del servicio confirman todo. Cualquier error provoca rollback de devolución, stock, kardex, caja y auditoría.
8. Una repetición consulta primero `ClientRequestId`. Si dos solicitudes compiten y PostgreSQL devuelve `23505` sobre `UqReturnsClientRequest`, se relee en un ámbito nuevo y se devuelve la fila existente.

## Kardex y cálculo

Cada línea reingresada crea `ENTRADA_DEVOLUCION` con cantidad positiva, stock anterior/posterior, orden original, ejecutor, costo original y costo total. `Restock = false` conserva `RestockQuantity = 0` y no crea movimiento.

La cantidad usa exactamente la unidad de `OrderDetails.quantity` que actualmente descuenta la venta; `UnitsPerPackage` se conserva como referencia y no altera el incremento. El costo de entrada es provisional: se toma `OrderDetails.UnitCost` y queda **a verificar con contador / normativa MH**. El prorrateo de IVA y su redondeo también quedan sujetos a esa verificación.

## Efectivo y corte

Solo `EFECTIVO` crea un `CashMovement` con `ReturnId`, `CashSessionId`, ejecutor, autorizador, `ClientRequestId` propio y razón de hasta 300 caracteres. `TARJETA`, `TRANSFERENCIA` y `NINGUNO` no crean movimientos de caja; `NINGUNO` exige monto cero.

El resumen y cierre de caja suman `DEVOLUCION_EFECTIVO` desde `sales."CashMovements"`. El efectivo esperado es fondo inicial + pagos en efectivo − devoluciones en efectivo. La consulta SQL de control vigente está en `docs/propuestas/POS_CORTE_CAJA.md`.

## Pendientes y riesgos

## Endurecimientos QA de esta fase

1. Un PIN correcto de un empleado que no tiene puesto de historial completo se trata igual que un PIN incorrecto: se muestra el mensaje genérico y se registra `PIN_FAIL` en el lockout persistente.
2. `ClientRequestId` es idempotente solo cuando coinciden orden, empleado, líneas, cantidades, reingreso, método y monto. Una repetición con otro contenido se rechaza, incluso al resolver la carrera de `UqReturnsClientRequest` (`23505`).
3. Los reintentos cubren `40001` y `40P01`, con contextos/transacciones nuevos. Los productos a reingresar se bloquean en una sola consulta parametrizada, ordenada por `ProductId`, para evitar deadlocks entre devoluciones concurrentes.
4. El `UpdatedAt` del producto y del movimiento usa el mismo instante UTC inyectado que la operación; no se consulta `DateTime.UtcNow` dentro del flujo.
5. La razón del kardex es `Devolución POS (costo a verificar)` y se trunca de forma segura al límite de 300 caracteres. El costo se toma del detalle original y queda a verificar con contador/MH.
6. Un reintegro `EFECTIVO` de monto cero se rechaza antes de abrir transacción o escribir la BD, con instrucciones para elegir `NINGUNO` o corregir las cantidades.

- Nota de crédito 05 / DTE e integración de `DteService.EmitCreditNoteAsync`: hoy ese método cancela la orden y restaura todo el inventario, por lo que debe adaptarse antes de conectarlo a este flujo. Todo el tratamiento fiscal es a verificar con contador / normativa MH.
- La impresión física del comprobante interno queda pendiente de probar en hardware; la vista previa ya usa `ReturnReceiptComposer` y el ancho configurado.
- La anulación de devoluciones, con estado `ANULADA`, todavía no tiene flujo.
- El costo y la unidad de reingreso podrían cambiar por criterio contable o una futura corrección del proceso de ventas.
- El plazo comercial, IVA, soporte del reintegro y documento fiscal aplicable requieren validación externa.

El POS no modifica ventas netas del panel ni la API, y no toca `Services/Dte/DteService.cs` en esta fase.
