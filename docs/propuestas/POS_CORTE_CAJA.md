# POS: apertura y corte de caja

Este documento describe el contrato operativo implementado en la aplicación WPF. El reporte es interno y no es un documento fiscal. Los requisitos fiscales definitivos quedan **a verificar con contador / normativa MH**.

## Regla de una sesión por caja

Solo puede existir una sesión ABIERTA por `CashRegisterCode`, aunque los cajeros sean distintos. El servicio aplica esta regla con una transacción `Serializable`, y el esquema la respalda con el índice único parcial `IdxCashSessionOpenByRegister`. La violación `23505` se traduce a un mensaje operativo claro.

## Efectivo de devoluciones

`sales."CashMovements"` registra egresos que no caben como pagos negativos. Un movimiento `DEVOLUCION_EFECTIVO` tiene `ReturnId`, monto positivo, sesión, ejecutor, autorizador, clave de idempotencia y razón de hasta 300 caracteres. La devolución está ligada a la sesión en la que sale el efectivo, no a la orden original.

El cierre suma pagos `EFECTIVO` de órdenes COMPLETADA y resta el total de movimientos `DEVOLUCION_EFECTIVO` de la misma sesión. El valor se usa tanto en `GetSummaryAsync` como en `CloseAsync`, por lo que `ClosingExpectedAmount` conserva la resta.

## Consulta SQL de control vigente

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
), cash_refunds AS (
    SELECT COALESCE(SUM(m."amount"), 0) AS amount
    FROM sales."CashMovements" m
    WHERE m."CashSessionId" = @sessionId
      AND m."MovementType" = 'DEVOLUCION_EFECTIVO'
)
SELECT
    s."OpeningAmount",
    COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0) AS cash_sales,
    COALESCE((SELECT SUM("total") FROM completed), 0) AS total_sold,
    (SELECT amount FROM cash_refunds) AS cash_refunds,
    s."OpeningAmount"
        + COALESCE((SELECT amount FROM payment_totals WHERE "method" = 'EFECTIVO'), 0)
        - (SELECT amount FROM cash_refunds) AS expected_cash
FROM sales."CashSessions" s
WHERE s."id" = @sessionId;
~~~

`@sessionId` debe enviarse como parámetro UUID; nunca se concatenan valores.

## Permisos y auditoría

El servicio vuelve a comprobar `IsActive` y autorización en apertura, resumen y cierre. El dueño necesita `CanCashier`; los puestos de `SalesHistory:FullHistoryPositionNames` pueden consultar o cerrar sesiones ajenas. Las transiciones se auditan en `system."AuditLog"`.

## A verificar con contador / normativa MH

- Tratamiento del IVA, redondeo y requisitos de un corte diario.
- Conservación legal de reportes y DTE.
- Tratamiento permitido del reintegro en efectivo y comprobantes requeridos.
