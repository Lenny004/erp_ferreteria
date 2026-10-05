> **Estado: implementada en `ferreteria_backend` PR #15**, mediante la migración `prisma/migrations/3_pos_vkpistoday_zona_horaria`.
# Propuesta: día de negocio del POS y `VKpisToday`

## Problema

La vista `sales."VKpisToday"` del esquema generado por el backend filtra actualmente comparando
`o."CreatedAt"::date` con la fecha actual de la sesión. En PostgreSQL, tanto la conversión
`timestamptz::date` como esa fecha usan la zona horaria de la sesión. El POS no fija esa sesión y la base de datos opera en UTC,
por lo que la fecha del corte no coincide con la fecha comercial de El Salvador.

Por ejemplo, una venta a las 23:30 del 29/09/2026 hora local es
`2026-09-30T05:30:00Z`: el filtro actual la cuenta como 30/09. Una venta a las 00:05 del
30/09/2026 es `2026-09-30T06:05:00Z` y también cae como 30/09 en UTC. El día comercial
debe separar ambas ventas.

## Cambio propuesto para `ferreteria_backend`

La migración Prisma debe reemplazar la vista usando las columnas reales de
`Ferreteria.PuntoVenta/Squema.sql`: `sales."Orders"."CreatedAt"`, `"total"` y `"status"`.
La forma recomendada es convertir el instante a la zona antes de extraer la fecha:

```sql
CREATE OR REPLACE VIEW sales."VKpisToday" AS
SELECT
    COUNT(*)                    AS "TotalOrders",
    COALESCE(SUM(o."total"), 0) AS "TotalAmount",
    COALESCE(AVG(o."total"), 0) AS "AvgTicket"
FROM sales."Orders" o
WHERE (o."CreatedAt" AT TIME ZONE 'America/El_Salvador')::date
        = (now() AT TIME ZONE 'America/El_Salvador')::date
  AND o."status" = 'COMPLETADA';
```

El backend ya introdujo `BUSINESS_TZ` en el PR #13. La migración debe tomar esa configuración
como fuente de la zona. Una vista PostgreSQL no recibe parámetros por consulta; si Prisma no
puede generar la definición con una variable, se debe renderizar el literal configurado durante
la migración. Para la instalación actual el literal es `'America/El_Salvador'`; cambiarlo
requiere una nueva migración cuando cambie la zona del negocio.

Otra alternativa es fijar `timezone` en el rol o en la base de datos. Centraliza el comportamiento
de la fecha actual y de las conversiones implícitas, pero afecta consultas no relacionadas, tareas
administrativas y otros clientes. La expresión explícita `AT TIME ZONE` hace visible el contrato
de la vista y evita depender de la zona de sesión, por lo que es la opción preferida.

## Alcance del POS

El POS no consulta `sales."VKpisToday"` y no se cambió esa vista en este repositorio. La aplicación
usa `Negocio:ZonaHoraria`, valida la zona al arrancar, convierte fechas locales a UTC y consulta
con rangos semiabiertos `[inicio, fin)`. Los reportes agrupan por día local mediante
`AT TIME ZONE` parametrizado; no agregan `Timezone=` a la cadena de conexión ni ejecutan `SET TIME ZONE`.

## Usos de “hoy” revisados

| Uso | Resultado | Motivo |
|---|---|---|
| `SalesHistoryFilter.CreateShortcutRange` (`Hoy`, `Ayer`, `Semana`, `Mes`) | Cambiado | Usa `BusinessCalendar`, lunes como inicio de semana y rangos locales semiabiertos. |
| `SalesHistoryFilter.CreateLocalDateRange` | Cambiado | Usa fechas locales inclusivas y fin exclusivo del día siguiente. |
| `HistorialFacturasView` | Cambiado | Inyecta `BusinessCalendar` para atajos y para abrir el diálogo de rango. |
| `SalesHistoryRangeDialog` | Cambiado | La fecha predeterminada proviene de `BusinessCalendar.Today()`. |
| `ReportService` ventas, compras y productos principales | Cambiado | Recibe `DateOnly`, convierte a UTC y filtra con `>= inicio` y `< fin`; ventas agrupa por fecha local. |
| `VKpisToday` | Propuesto en backend | El POS no la consulta; la corrección pertenece a Prisma/backend. |
| `CashRegisterReportComposer` y contratos visibles | Cambiado | Solo formatean instantes para pantalla/impresión con la zona configurada; el corte sigue siendo por sesión. |
| `ReturnReceiptComposer` (devoluciones, llegó con #6) | Cambiado | Solo formatea la fecha de la venta original con la zona configurada. |
| `ReturnService` (búsqueda y plazo de devolución) | Sin cambio | Usa ventanas móviles en días (`now - N días`), no un corte por día calendario. |
| DTE y servicios de impresión | Sin cambio | Quedan fuera de este PR porque sus fechas fiscales/impresas requieren una revisión específica. |
| `UsuariosView` (fecha de contratación) | Sin cambio | Es un valor de formulario, no un corte de día de negocio. |

## Riesgos y verificación operativa

- DTE e impresión todavía usan `DateTime.Now` o `ToLocalTime()` del equipo; pueden mostrar o
  emitir una fecha distinta si el reloj o la zona de Windows están mal configurados.
- Al corregir los límites, cifras históricas de reportes pueden cambiar de día, aunque no cambien
  los totales del rango completo.
- La zona configurada debe existir tanto en .NET como en el catálogo de PostgreSQL
  (`pg_timezone_names`).
- La configuración debe mantenerse alineada entre `Negocio:ZonaHoraria` del POS,
  `BUSINESS_TZ` del backend y la zona literal usada por la migración de la vista.

La prueba de integración `ReportServiceIntegrationTests` siembra ventas a las 23:30 del 14/06/2030 y a las 00:05 del
15/06/2030 hora local (`2030-06-15T05:30:00Z` y `2030-06-15T06:05:00Z`), una fecha alejada del reloj real y del
`Now` del fixture para no chocar con otras siembras, y comprueba la separación en reportes e historial. Otra prueba
evalúa en PostgreSQL, con la sesión en UTC, el corte actual (`::date`) frente a la expresión propuesta usando
`2026-09-30T05:30:00Z`: el corte actual da 30/09 y la expresión propuesta, 29/09.
