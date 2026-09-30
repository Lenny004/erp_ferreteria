# Devoluciones POS — Fase 2

Esta fase permite buscar ventas `COMPLETADA`, consultar sus líneas originales y calcular una devolución total o parcial sin escribir en la base de datos. La confirmación permanece deshabilitada porque todavía no existen `sales."Returns"`, `sales."ReturnDetails"` ni `sales."CashMovements"`.

## Decisiones del dueño

1. La orden conserva `COMPLETADA` tanto en una devolución parcial como total; el estado devuelta se derivará de los registros futuros.
2. Los estados de devolución serán `COMPLETADA` y `ANULADA` en mayúsculas, aunque la columna se llama `status`.
3. El movimiento de caja será `DEVOLUCION_EFECTIVO`; también quedan previstos `RETIRO` e `INGRESO`.
4. El reingreso usará `OrderDetails.UnitCost` y la misma cantidad que descontó la venta. Esta valorización queda **a verificar con contador**.
5. No se ofrecen vales ni crédito. La UI solo muestra `EFECTIVO`, `TARJETA`, `TRANSFERENCIA` y `NINGUNO`.

## Qué está implementado

- Contratos inmutables, catálogo configurable, autorización server-side y lector temporal fail-secure.
- Cálculo con precios, descuentos e IVA de la venta original; las devoluciones sucesivas cierran por remanente para evitar excedentes o centavos colgantes.
- Política fiscal intercambiable. No emite DTE ni nota de crédito.
- Comprobante interno puro de 32 o 48 columnas, sin identificadores fiscales inventados.
- Vista de cuatro pasos para búsqueda, líneas, motivo/reintegro y revisión; los botones de cantidad son táctiles y la confirmación está bloqueada.

## Pendiente de la migración

La lectura autoritativa de cantidades debe consultar `sales."ReturnDetails"`, y el lector de caja debe sumar `sales."CashMovements"` por sesión. Hasta entonces, ambos adaptadores devuelven cero/vacío y la solicitud de confirmación lanza una excepción controlada. No se modificó `Squema.sql`, `Models` ni se agregaron `DbSet`.

El método existente `DteService.EmitCreditNoteAsync` no se usa en esta fase: hoy cancela la orden y restaura todo el inventario, en conflicto con la decisión de conservar `COMPLETADA`. Se adaptará cuando exista el flujo de nota de crédito.

## A verificar con contador / normativa MH

| Tema | Punto abierto |
|---|---|
| CCF parcial | Si corresponde nota de crédito 05 y su contenido. |
| Factura 01 | Invalidación, otro documento y plazo aplicable. |
| Plazos | Plazo legal/comercial para aceptar, invalidar o emitir. |
| IVA | Prorrateo, redondeo y período fiscal de la devolución. |
| Reintegro en efectivo | Si está permitido y si exige comprobantes o DUI del cliente. |
| DTE/DUI del cliente | Datos que deben conservarse o exigirse. |
| Leyenda | Texto definitivo del comprobante interno. |
| Costo del reingreso | Uso de `UnitCost` original frente a otra convención contable. |

La tasa de IVA usada por el código es 13% y queda **a verificar con contador / normativa MH**. La interpretación de `OrderDetails.Subtotal` como precio por cantidad y `DiscountAmount` separado también queda marcada para verificar si otros canales usan otra convención.

Si las devoluciones en efectivo superan el efectivo cobrado de la sesión, el cálculo conserva un efectivo esperado negativo. Con la convención existente `declarado - esperado`, declarar cero se clasifica como sobrante; el cierre debe exigir la observación configurada.
