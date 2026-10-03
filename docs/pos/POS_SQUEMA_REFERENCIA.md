# `Squema.sql` como referencia parcial

**Fuente de verdad del esquema:** las migraciones de Prisma del backend (`ferreteria_backend`, carpeta `prisma/migrations`, rama `desarrollo`). `Ferreteria.PuntoVenta/Squema.sql` es solo una **referencia parcial**: el fixture de Testcontainers del POS lo aplica tal cual para crear una BD de prueba, y no se ejecuta contra ninguna BD real. Si hay diferencias, manda el backend.

Las tablas de la tienda web (`system."ShopCustomers"`, `system."ShopOrders"`, `system."ShopPayments"`, etc.) **no** están en `Squema.sql` y no se agregan aquí: inventarlas sin su DDL real sería un riesgo. Por eso, las sentencias de migración que solo tocan esas tablas, o que crean FK hacia ellas, se omiten y se documentan en el comentario del bloque correspondiente.

## Migraciones reflejadas

| Migración (backend) | Qué se copió a `Squema.sql` | Qué se omitió y por qué |
|---|---|---|
| `0_init`, `1_inventory_counts` | No revisadas en el PR de QA de seguridad | Pendiente comparar con `Squema.sql` |
| `2_pos_devoluciones` | Tablas `sales."Returns"`, `sales."ReturnDetails"`, `sales."CashMovements"` (vía `docs/pos/2_pos_devoluciones_squema.sql`, PR #11) | — |
| `3_pos_vkpistoday_zona_horaria` | Vista `VKpisToday` con corte de día en `America/El_Salvador` (PR #15) | — |
| `4_qa_seguridad` | `system."WebUsers"."TokenVersion"` | `ShopCustomers.TokenVersion` y `ShopPayments.ConfirmedByWebUserId`/`ConfirmedAt` con su índice y FK: tablas ausentes |
| `5_qa_seguimiento` | Nada | Solo altera `ShopPayments` (`CustomerReference`, `CustomerReferenceAt`): tabla ausente |
| `6_qa_cancelacion_tienda` | `PurchaseOrders."EmployeeId"` pasa a NULL-able; `PurchaseOrders."CreatedByWebUserId"` con FK a `WebUsers` e índice; `InventoryMovements."ShopOrderId"` | FK `FKInventoryMovementsShopOrder` hacia `system."ShopOrders"`: tabla ausente |
| `7_qa_movimientos_tienda_validacion` | Índice `IdxInvMovShopOrder` | `VALIDATE CONSTRAINT` de la FK omitida en la 6 |
| `8_qa_notas_recepcion` | `PurchaseOrders."ReceivedByWebUserId"` con FK a `WebUsers` e índice | `SET LOCAL lock_timeout`: solo afecta la ejecución de la migración |

## Impacto en el POS (EF Core)

- La caja no tiene entidad EF para `purchasing."PurchaseOrders"` y no lee `EmployeeId` de esa tabla: el cambio de nulabilidad no afecta al mapeo.
- `public."InventoryMovements"."ShopOrderId"` es NULL-able; la caja no la mapea ni la escribe, así que sus inserciones la dejan en NULL.
- `system."WebUsers"` no tiene entidad EF en el POS.

Al agregar una migración nueva en el backend, actualizar esta tabla y el encabezado de `Squema.sql`.
