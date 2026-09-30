# POS: apertura y corte de caja

Este documento describe el contrato operativo implementado en la aplicación WPF. El reporte de corte es interno y **no es un documento fiscal**. Los requisitos fiscales definitivos quedan **a verificar con contador / normativa MH**.

## `sales.CashSessions` para panel y API

| Campo | Significado operativo |
|---|---|
| `id` | Identificador del turno. Se usa como FK en órdenes y pagos. |
| `EmployeeId` | Empleado dueño del turno que lo abrió. |
| `CashRegisterCode` | Código de la caja física, por ejemplo `CAJA-01`. |
| `OpenedAt` | Fecha y hora UTC de apertura. |
| `ClosedAt` | Fecha y hora UTC del cierre; es nulo mientras está `ABIERTA`. |
| `OpeningAmount` | Fondo inicial en efectivo, normalizado a dos decimales. |
| `ClosingDeclaredAmount` | Efectivo contado por el empleado al cerrar. |
| `ClosingExpectedAmount` | Fondo inicial más pagos `EFECTIVO` de órdenes `COMPLETADA`, menos devoluciones en efectivo; hoy las devoluciones consideradas son cero porque no existe movimiento persistible. |
| `Difference` | Efectivo declarado menos efectivo esperado. Positivo es sobrante y negativo es faltante. |
| `Status` | `ABIERTA`, `CERRADA` o `CANCELADA`. Solo `ABIERTA` acepta ventas. |
| `Notes` | Observación de apertura o cierre, sin secretos ni datos sensibles. |
| `CreatedAt` / `UpdatedAt` | Timestamps UTC de mantenimiento. |

La aplicación lee las órdenes y pagos por `CashSessionId` dentro de la transacción Serializable del cierre. Las ventas `PENDIENTE` y `CANCELADA` no forman parte del efectivo esperado ni del total vendido.

## Consulta SQL de control

Para validar un resumen contra PostgreSQL, sustituir `:session_id` por un parámetro UUID; no concatenar valores:

```sql
WITH session_orders AS (
    SELECT o."id", o."status", o."total", o."TaxAmount"
    FROM sales."Orders" o
    WHERE o."CashSessionId" = @sessionId
), completed AS (
    SELECT * FROM session_orders WHERE "status" = 'COMPLETADA'
), payment_totals AS (
    SELECT p."method", COALESCE(SUM(p."amount"), 0) AS amount
    FROM sales."Payments" p
    JOIN completed o ON o."id" = p."OrderId"
    WHERE p."CashSessionId" = @sessionId
    GROUP BY p."method"
), latest_dte AS (
    SELECT DISTINCT ON (d."OrderId") d."OrderId", d."MhStatus"
    FROM dte."DteIssued" d
    JOIN completed o ON o."id" = d."OrderId"
    ORDER BY d."OrderId", d."IssuedAt" DESC
)
SELECT
    s."OpeningAmount",
    COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0) AS cash_sales,
    COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'TARJETA'), 0) AS card_sales,
    COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'TRANSFERENCIA'), 0) AS transfer_sales,
    COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'OTRO'), 0) AS other_sales,
    COALESCE((SELECT SUM("total") FROM completed), 0) AS total_sold,
    COALESCE((SELECT SUM("TaxAmount") FROM completed), 0) AS tax_amount,
    (SELECT COUNT(*) FROM completed) AS completed_sales,
    (SELECT COUNT(*) FROM session_orders WHERE "status" = 'PENDIENTE') AS pending_sales,
    (SELECT COUNT(*) FROM session_orders WHERE "status" = 'CANCELADA') AS cancelled_sales,
    (SELECT COUNT(*) FROM latest_dte WHERE "MhStatus" IS NOT NULL) AS dte_count,
    (SELECT COUNT(*) FROM latest_dte WHERE "MhStatus" = 'CONTINGENCIA') AS contingency_dte_count,
    (SELECT COUNT(*) FROM completed c LEFT JOIN latest_dte d ON d."OrderId" = c."id" WHERE d."OrderId" IS NULL) AS sales_without_dte,
    CAST(0 AS numeric) AS cash_refunds,
    s."OpeningAmount" + COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0) AS expected_cash
FROM sales."CashSessions" s
WHERE s."id" = @sessionId;
```

`@sessionId` debe enviarse como `NpgsqlParameter` de tipo UUID. Las devoluciones en efectivo son cero
hasta que exista el movimiento persistible del módulo de devoluciones.

## Dependencias de esquema propuestas, no aplicadas

El esquema actual permite dos empleados con sesiones `ABIERTA` en la misma caja porque el índice existente también incluye `EmployeeId`. Se propone, como dependencia cruzada con `ferreteria_backend/prisma/schema.prisma` y su migración, el siguiente índice idempotente. Esta entrega no modifica `Squema.sql`, modelos ni Prisma:

```sql
CREATE UNIQUE INDEX IF NOT EXISTS "IdxCashSessionOpenByRegister"
    ON sales."CashSessions"("CashRegisterCode")
    WHERE "status" = 'ABIERTA';
```

También queda propuesto, sin aplicar, `sales."CashMovements"` para registrar `DEVOLUCION_EFECTIVO`, `RETIRO` e `INGRESO`. Debería incluir como mínimo `id`, `CashSessionId`, `MovementType`, `Amount`, `Reason`, `EmployeeId`, `CreatedAt` y una referencia opcional a la orden. Hasta que exista ese módulo, `ICashMovementReader` devuelve cero y el corte no inventa movimientos.

El POS lee las devoluciones mediante `ICashMovementReader` dentro de la transacción Serializable del resumen o cierre. La implementación actual es `PendingMigrationCashMovementReader`; cuando exista la migración sumará los movimientos `DEVOLUCION_EFECTIVO` de la sesión.

Si se necesita trazabilidad directa en la sesión, se proponen columnas `ClosedByEmployeeId`, `CancelledByEmployeeId` y `CancelledAt`. Actualmente quién cerró o canceló se conserva en `system."AuditLog"` mediante `UserId` y `NewData`; no se agregan columnas en esta entrega.

## Permisos

- El cajero ve y reimprime únicamente sus órdenes cuyo `CashSessionId` pertenece a una sesión `ABIERTA` de ese empleado. Una sesión cerrada deja de estar en su alcance.
- Si el cajero no tiene una sesión abierta, el alcance es vacío y la vista indica: “Abra caja para ver las ventas de su turno”.
- Los puestos listados en `SalesHistory:FullHistoryPositionNames` ven todo y pueden consultar/cerrar sesiones ajenas. La configuración actual contiene únicamente `Administrador`.
- En el esquema no existe el puesto `Supervisor`. Si el dueño crea ese puesto, basta agregar su nombre a `FullHistoryPositionNames`; es una decisión pendiente del dueño. No se agrega al seed ni se crea una segunda lista para caja.
- Toda operación de apertura, resumen, cierre y reimpresión vuelve a comprobar `IsActive` y autorización en el servidor. El dueño de una sesión necesita `CanCashier`; un puesto de acceso completo activo puede cerrar una sesión ajena.

## Códigos de auditoría de impresión

`system."AuditLog"."action"` es `VARCHAR(10)`. Los códigos persistidos se acortaron y el nombre largo se conserva en `NewData.Evento`:

| Antes | Después | Evento lógico en `NewData.Evento` |
|---|---|---|
| `IMPRESION_TICKET` | `IMPRIMIR` | `IMPRESION_TICKET` |
| `CONFIGURACION_IMPRESORA` | `CFG_IMPRES` | `CONFIGURACION_IMPRESORA` |
| `IMPRESORA_PREDETERMINADA` | `PREDET_IMP` | `IMPRESORA_PREDETERMINADA` |

La misma convención se aplica a todas las clases `*AuditActions`: los campos terminados en `Event` son nombres lógicos, los terminados en `TableName` son tablas y el resto son códigos persistidos.

## A verificar con contador / normativa MH

- Requisitos del corte Z, cierre o reporte diario que deban conservarse.
- Tratamiento del IVA del 13 % para cada tipo de operación.
- Criterio de redondeo contable; la aplicación usa `MidpointRounding.AwayFromZero` a dos decimales como decisión técnica provisional.
- Conservación, exportación y retención legal de reportes y DTE.
- Que este reporte interno de corte no es un documento fiscal ni sustituye un DTE, un corte Z o un reporte exigido por el MH.
