# POS — QA de seguridad, fase 2

Esta fase endurece la autorización de la fase 1 y cubre ventas/devoluciones concurrentes, idempotencia por intento, reloj del negocio, configuración de conexión y el rol de base de datos. Las operaciones sensibles reciben `IAuthorizationGuard` obligatorio y fallan cerradas; no existe un camino de compatibilidad que conceda permisos cuando falta el guard.

## Decisiones principales

- `ClientRequestId` identifica el intento lógico de venta o devolución. Una repetición idéntica devuelve el mismo resultado; cualquier diferencia de empleado, caja, líneas, cantidades, reingreso, método o monto se rechaza con un mensaje de nueva operación.
- Las ventas y devoluciones bloquean todos los productos ordenados por UUID con una sola consulta `ANY(...) ... ORDER BY ... FOR UPDATE`. Los errores PostgreSQL `40001` y `40P01` reintentan hasta dos veces en contextos y transacciones nuevas.
- El reloj de persistencia es `TimeProvider`; los textos de recibo y DTE convierten el instante UTC a `Negocio:ZonaHoraria`. El criterio de fecha/hora de emisión y el costo contable de devoluciones quedan a verificar con contador/MH.
- El PIN de caja usa únicamente eventos persistentes en `system."AuditLog"`. Un PIN válido de un empleado sin autorización para devoluciones se registra como fallo y muestra el mismo mensaje genérico que un PIN incorrecto.
- No se puede desactivar al último administrador activo ni cambiar su puesto a uno no administrativo; la comprobación está protegida con lock advisory transaccional.

## Riesgos y verificaciones externas

La fecha de emisión DTE, el tratamiento fiscal de devoluciones, la nota de crédito, el costo de reingreso y el redondeo deben validarse con contador y Ministerio de Hacienda antes de producción. El hardware de impresión y el certificado TLS de PostgreSQL también requieren una prueba de despliegue.

La fecha de contratación mostrada en `UsuariosView` es un dato laboral, no fiscal; puede continuar usando la fecha de la PC y queda fuera del criterio de hora del negocio.

