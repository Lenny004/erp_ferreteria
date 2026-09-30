# POS: apertura y corte de caja

Este documento describe el contrato operativo implementado en la aplicación WPF. El reporte es interno y no es un documento fiscal. Los requisitos fiscales definitivos quedan **a verificar con contador / normativa MH**.

## Regla de una sesión por caja

Solo puede existir una sesión ABIERTA por CashRegisterCode, aunque los cajeros sean distintos. El servicio aplica esta regla con una transacción Serializable, mensajes diferenciados por empleado y traducción controlada de la violación 23505. La migración de Botti en ferreteria_backend, rama bd-devoluciones, agregará IdxCashSessionOpenByRegister como respaldo; todavía no está en desarrollo.

## sales.CashSessions

El cierre suma pagos EFECTIVO de órdenes COMPLETADA, resta devoluciones en efectivo leídas por ICashMovementReader y conserva cash_refunds = 0 mientras la migración no exista. Las ventas PENDIENTE y CANCELADA no forman parte del efectivo esperado.

## sales."CashMovements" pendiente

La definición real de Botti usa las columnas "amount" y "reason" en minúscula, MovementType, CashSessionId, EmployeeId, AuthorizedByEmployeeId, ClientRequestId y ReturnId. Un movimiento DEVOLUCION_EFECTIVO debe tener ReturnId y no la orden; la CHECK exige la equivalencia DEVOLUCION_EFECTIVO ⇔ ReturnId IS NOT NULL. amount debe ser mayor que cero, hay un único DEVOLUCION_EFECTIVO por devolución y reason admite hasta 300 caracteres.

La migración está en ferreteria_backend, rama bd-devoluciones, y todavía no está aplicada en desarrollo. El POS usa PendingMigrationCashMovementReader, que devuelve cero hasta que exista el esquema.

## Consulta SQL de control

La consulta actual no lee tablas nuevas y deja cash_refunds explícitamente en cero:

~~~sql
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
)
SELECT
    s."OpeningAmount",
    COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0) AS cash_sales,
    COALESCE((SELECT SUM("total") FROM completed), 0) AS total_sold,
    CAST(0 AS numeric) AS cash_refunds,
    s."OpeningAmount" + COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0) AS expected_cash
FROM sales."CashSessions" s
WHERE s."id" = @sessionId;
~~~

~~~sql
-- Tras la migración de ferreteria_backend, sustituir el cero por:
-- COALESCE((SELECT SUM(m."amount")
--           FROM sales."CashMovements" m
--           WHERE m."CashSessionId" = s."id"
--             AND m."MovementType" = 'DEVOLUCION_EFECTIVO'), 0) AS cash_refunds
~~~

@sessionId debe enviarse como NpgsqlParameter UUID; nunca se concatenan valores.

## Permisos y auditoría

El servidor vuelve a comprobar IsActive y autorización en apertura, resumen y cierre. El dueño necesita CanCashier; los puestos de SalesHistory:FullHistoryPositionNames pueden consultar o cerrar sesiones ajenas. Las transiciones se auditan en system."AuditLog".

## A verificar con contador / normativa MH

- Tratamiento del IVA, redondeo y requisitos de un corte diario.
- Conservación legal de reportes y DTE.
- Tratamiento permitido del reintegro en efectivo y comprobantes requeridos.
