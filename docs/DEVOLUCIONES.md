# Devoluciones POS — Fase 2

La fase 2 permite buscar ventas COMPLETADA, revisar líneas y preparar devoluciones totales o parciales. La confirmación solo se habilita cuando existan el lector autoritativo y el writer de persistencia de la migración. No se emiten DTE ni notas de crédito en esta fase.

## Decisiones del dueño

1. La orden siempre conserva COMPLETADA, incluso cuando se devuelve todo; nunca se escribe Orders.status.
2. Returns.status usa COMPLETADA y ANULADA. ANULADA queda reservado para un flujo futuro.
3. Los reintegros permitidos son EFECTIVO, TARJETA, TRANSFERENCIA y NINGUNO. No hay vales ni crédito a favor.
4. El reingreso usa el costo original OrderDetails.UnitCost y la cantidad en la misma unidad que descontó la venta. Es una decisión provisional, a verificar con contador.
5. La devolución de efectivo se liga a la devolución mediante ReturnId, nunca a la orden.

## Flujo transaccional de confirmación

El servicio valida primero la forma de la solicitud sin consultar la BD: identificadores, líneas no vacías, duplicados, cantidades positivas con hasta tres decimales, motivo, notas y whitelist de reintegros. Si Capabilities.CanConfirmReturns es falso, lanza ReturnsUnavailableException antes de abrir una transacción y no realiza escrituras.

Cuando el writer y el reader están disponibles:

1. Abre una transacción Serializable con reintentos acotados ante PostgreSQL 40001.
2. Autoriza al ejecutor y al autorizador: ambos deben estar activos y ser cajeros o pertenecer a FullHistoryPositionNames.
3. Bloquea la fila de sales."Orders" con SELECT parametrizado ... FOR UPDATE y comprueba que siga COMPLETADA y sea VENTA_CAJA u ORDEN_CONFECCION.
4. Consulta el ClientRequestId en IReturnWriter. Si existe, devuelve el resultado sin volver a escribir.
5. Lee las devoluciones existentes con IReturnedQuantityReader, usando el mismo DbContext y la transacción ya abierta.
6. Carga las líneas originales y valida en el servicio que cantidad solicitada <= vendida - ya devuelta. La BD todavía no valida ese exceso; Serializable más FOR UPDATE hacen que las solicitudes concurrentes se serialicen sobre la orden.
7. Para EFECTIVO exige y bloquea la sesión ABIERTA de Caja:Codigo. Para otros métodos conserva la sesión abierta si existe y, si no, usa CashSessionId null.
8. Calcula la política fiscal, arma ReturnPersistenceRecord, llama al writer y confirma. La orden no cambia de estado.

## Contratos de persistencia

ReturnHeaderRecord representa las columnas reales de sales."Returns": OrderId, CashSessionId, EmployeeId, AuthorizedByEmployeeId, ClientRequestId, ReturnType, status, FiscalStatus, CreditNoteDteId, ReasonCode, notes, subtotal, DiscountAmount, TaxAmount, total, RefundMethod, RefundAmount, CreatedAt y UpdatedAt. status siempre es COMPLETADA y CreditNoteDteId es null en esta fase.

ReturnDetailRecord representa sales."ReturnDetails": OrderDetailId, ProductId, quantity, UnitsPerPackage, UnitPrice, UnitCost, DiscountAmount, subtotal, TaxAmount, Restocked, RestockQuantity, InventoryMovementId y CreatedAt.

CashMovementRecord solo se prepara para EFECTIVO. Usa MovementType DEVOLUCION_EFECTIVO, amount igual a RefundAmount, reason truncado como máximo a 300 caracteres y ReturnId asignado por el writer. La CHECK de Botti exige ReturnId para ese movimiento y el índice parcial permite uno por devolución.

Cada línea Restocked produce una línea de kardex ENTRADA_DEVOLUCION. quantity no se multiplica por UnitsPerPackage porque replica la unidad que hoy descuenta OrderService; el costo es OrderDetails.UnitCost original. Es provisional, a verificar con contador.

IReturnWriter expone IsAvailable, FindByClientRequestIdAsync y PersistAsync. PersistAsync deberá insertar Returns, ReturnDetails, InventoryMovements, actualizar stock, insertar CashMovements y escribir AuditLog dentro de la transacción recibida. PendingMigrationReturnWriter permanece inerte hasta la migración.

## Cálculo

El crédito usa precios y descuentos originales. El neto original es la suma de subtotal de líneas menos descuentos de línea y descuento de encabezado. Para una devolución parcial, el IVA es Round2(sale.TaxAmount × netoDevuelto / netoOriginal); si el neto original no es positivo, es cero. Cuando se agota la orden, el IVA y el total cierran por remanente contra la venta original. Esto evita superar el IVA original y elimina centavos colgantes en tramos sucesivos. IVA, redondeo y la interpretación contable quedan a verificar con contador / normativa MH.

Las invariantes lanzan InvalidReturnException: total calculado > 0; créditos previos + actual no superan total, subtotal, descuento ni IVA originales; y RefundAmount es cero para NINGUNO o igual al total para los demás métodos. Restock=false deja RestockQuantity y RestockCost en cero.

## Opciones y binding

ReturnOptions inicia Motivos y MetodosReintegroPermitidos vacíos. ReturnOptions.CreateDefault() sirve para tests y fallback. App.xaml.cs registra el mismo ReturnOptions.ApplyDefaults como PostConfigure: aplica cuatro motivos solo si la lista está vacía, deduplica códigos sin distinguir mayúsculas, rechaza códigos fuera de 1–30 caracteres y filtra siempre los reintegros a EFECTIVO, TARJETA, TRANSFERENCIA y NINGUNO. Así el binder no agrega los defaults a la colección y VALE nunca llega a la UI ni a la validación.

La vista muestra checkbox Reingresa a inventario por línea, usa el catálogo filtrado, deshabilita Confirmar devolución si no hay capacidad y mantiene visible el motivo operativo. La leyenda y todo tratamiento fiscal o contable son provisionales, a verificar con contador / normativa MH.

## Pendiente de la migración

- Reader autoritativo sobre sales."ReturnDetails".
- CashMovementReader real sobre sales."CashMovements".
- Writer real e idempotencia por ClientRequestId.
- Habilitar la confirmación solo después de ambas fuentes autoritativas.
- Entidades EF y Squema.sql desde docs/pos/2_pos_devoluciones_squema.sql del backend cuando la migración llegue a desarrollo; esa ruta no se copia en esta fase.
- Test de integración real con Returns, ReturnDetails y CashMovements.
- Adaptar la nota de crédito de DteService: hoy cancela la orden y restaura todo el inventario, lo que choca con la decisión 1.

## Riesgos

- La BD actual no valida exceso de devolución; una implementación futura no debe omitir bloqueo, lectura dentro de la transacción ni revalidación.
- El reingreso puede requerir otra unidad o valorización contable.
- El tratamiento de NC 05, factura 01, IVA, plazos y efectivo puede cambiar por contador o normativa MH.
- La confirmación real debe mantener atomicidad entre devolución, kardex, stock, caja y auditoría.

## A verificar con contador / normativa MH

| Tema | Punto abierto |
|---|---|
| CCF 03 | Documento aplicable, incluyendo NC 05 para parciales. |
| Factura 01 | Invalidación, otro documento y plazo. |
| Plazos | Plazo legal o comercial de aceptación y emisión. |
| IVA | Prorrateo, redondeo y período fiscal. |
| Efectivo | Permiso, soporte y datos del cliente. |
| DTE/DUI | Datos que deben conservarse o exigirse. |
| Comprobante | Leyenda definitiva y carácter fiscal. |
| Kardex | Costo y unidad de reingreso. |

La implementación no modifica ventas netas del panel ni la API, no usa SQL contra las tablas nuevas y no cambia DteService.cs.
