# Propuesta de índices — historial de ventas del POS

Esta propuesta es documental: no modifica `Squema.sql`, el modelo EF ni una base real. Cualquier aplicación requiere revisión del dueño y una migración equivalente en Prisma (`ferreteria_backend/prisma/schema.prisma`), por lo que es una dependencia cruzada.

## Estado actual y propuesta

`IdxOrdersCreatedAt` ya existe en `Squema.sql` sobre `sales."Orders"("CreatedAt")`. Para el orden estable `CreatedAt DESC, id` se propone un índice distinto:

```sql
CREATE INDEX IF NOT EXISTS "IdxOrdersCreatedAtId"
    ON sales."Orders" ("CreatedAt" DESC, "id");

CREATE INDEX IF NOT EXISTS "IdxOrdersCustomer"
    ON sales."Orders" ("CustomerId");
```

`IdxOrdersCustomer` sí falta actualmente y ayuda a las consultas por cliente. `ControlNumber` ya está cubierto por el índice de la restricción `UNIQUE`; no se propone otro índice redundante.

Para reimpresiones, el esquema ya tiene `IdxAuditLogRecord` en `("TableName", "RecordId")`. El conteo filtra también por `action`, por lo que se puede medir este índice adicional:

```sql
CREATE INDEX IF NOT EXISTS "IdxAuditLogActionRecord"
    ON system."AuditLog" ("action", "RecordId");
```

Solo debe agregarse si `EXPLAIN (ANALYZE, BUFFERS)` demuestra que el índice existente no alcanza; no se asume que ambos deban coexistir.

## Correcciones de mapeo EF

Se hicieron explícitos en `FerreteriaDbContext` los nombres que deben coincidir con
`Squema.sql`: `public."InventoryMovements"."quantity"` y
`public."InventoryMovements"."reason"`, además de `public."StockAlerts"."id"`.
La corrección evita que EF consulte o inserte usando `Quantity`, `Reason` o `Id`
cuando la base creada por `Squema.sql` usa los nombres en minúscula. No cambia el
esquema ni requiere migración: corrige únicamente el mapeo del modelo. La divergencia
de `InventoryMovement.Quantity` era un bug preexistente de `desarrollo`; `Reason` ya
estaba alineada y se conserva explícita para proteger el contrato con el esquema.

## Consulta SQL de control del resumen

El total e IVA del resumen suman únicamente órdenes `COMPLETADA`, aunque el filtro de la pantalla incluya `PENDIENTE` o `CANCELADA`. Las subconsultas por orden evitan duplicar totales cuando hay varios DTE:

```sql
WITH filtered_orders AS (
    SELECT o."id", o."status", o."total", o."TaxAmount"
    FROM sales."Orders" o
    WHERE o."CreatedAt" >= @fromUtc
      AND o."CreatedAt" < @toUtc
), dte_by_order AS (
    SELECT d."OrderId",
           bool_or(d."MhStatus" = 'CONTINGENCIA') AS has_contingency
    FROM dte."DteIssued" d
    GROUP BY d."OrderId"
), reprints_by_order AS (
    SELECT a."RecordId", COUNT(*) AS reprints
    FROM system."AuditLog" a
    WHERE a."action" = 'REIMPRIMIR'
      AND a."TableName" = 'sales.Orders'
    GROUP BY a."RecordId"
)
SELECT COUNT(*) AS ventas,
       COALESCE(SUM("total") FILTER (WHERE "status" = 'COMPLETADA'), 0) AS total,
       COALESCE(SUM("TaxAmount") FILTER (WHERE "status" = 'COMPLETADA'), 0) AS iva,
       COUNT(*) FILTER (WHERE dte_by_order.has_contingency) AS contingencias,
       COALESCE(SUM(reprints_by_order.reprints), 0) AS reimpresiones
FROM filtered_orders
LEFT JOIN dte_by_order ON dte_by_order."OrderId" = filtered_orders."id"
LEFT JOIN reprints_by_order ON reprints_by_order."RecordId" = filtered_orders."id"::text;
```

## Fuente de reimpresiones

La fuente operativa del historial es `system."AuditLog"` con `action = 'REIMPRIMIR'`, `TableName = 'sales.Orders'` y `RecordId` igual al id de la orden. El nombre lógico del evento sigue siendo `REIMPRESION_TICKET`, pero no cabe en `VARCHAR(10)`; por eso el código persistido es `REIMPRIMIR`. El contador `DteIssued.Reprints` se mantiene y se incrementa atómicamente cuando existe DTE para conservar trazabilidad fiscal; no es la fuente principal porque el POS imprime mayormente comprobantes internos sin DTE.

## Dependencia cruzada: ampliar la acción de auditoría

Como propuesta posterior, coordinada con Prisma, se puede ampliar `system."AuditLog"."action"` a `VARCHAR(30)`:

```sql
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'system'
          AND table_name = 'AuditLog'
          AND column_name = 'action'
    ) THEN
        ALTER TABLE system."AuditLog"
            ALTER COLUMN "action" TYPE VARCHAR(30);
    END IF;
END $$;
```

La migración equivalente debe actualizar `ferreteria_backend/prisma/schema.prisma` a `@db.VarChar(30)`. Después de aplicar ambos cambios coordinados, podría persistirse `REIMPRESION_TICKET` directamente. Las acciones existentes `IMPRESION_TICKET`, `CONFIGURACION_IMPRESORA` e `IMPRESORA_PREDETERMINADA` del módulo de impresión también superan `VARCHAR(10)`; quedan documentadas como problema fuera de este alcance y no se modifican aquí.
## Regla de permisos

El servicio carga el empleado y su puesto. Un cajero (`CanCashier`) ve únicamente sus ventas del día local de El Salvador; un puesto incluido en `SalesHistory:FullHistoryPositionNames` ve todo el historial. La configuración distribuida por defecto incluye `Administrador`, el puesto sembrado en `Squema.sql`. Agregar otro puesto requiere incluir su nombre exacto en `SalesHistory:FullHistoryPositionNames` dentro de `Config/appsettings.json`. Es una **decisión pendiente del dueño**: no se interpreta `CanSell` como rol de encargado. El alcance se intersecta con las fechas solicitadas y también protege el detalle.

## Puntos a verificar con contador / normativa MH

- Texto y obligatoriedad de `REIMPRESIÓN`.
- Tasa y tratamiento del IVA.
- Tipos y nombres visibles de DTE.
- Plazos de conservación del historial y DTE.

## Rendimiento

El fixture de integración mide una consulta paginada después de calentamiento y exige menos de un segundo. El valor medido se reporta en el PR; no se inventa una cifra aquí.
