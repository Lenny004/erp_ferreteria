# FlexoCable SV - Plan Integral de Finalizacion de la Aplicacion

> **Documento:** FLEXO-PLAN-001  
> **Version:** 1.0  
> **Fecha:** Junio 2026  
> **Objetivo:** Definir los modulos faltantes, dependencias, orden de desarrollo y criterios de finalizacion para completar FlexoCable SV.

---

## 1. Diagnostico Actual

La aplicacion WPF ya cuenta con estructura visual y navegacion principal:

- Inicio
- PIN
- Caja
- Facturacion
- Historial de facturas
- Consultar stock
- Corte de caja
- Devoluciones
- Impresoras
- Confeccion
- Ordenes de confeccion
- Ver codigos

Sin embargo, la mayor parte de estas vistas aun requiere integracion real con servicios, base de datos y reglas de negocio.

Hallazgos principales:

- `DTEService.cs` esta vacio.
- `InventarioService.cs` esta vacio.
- `ImpresionService.cs` esta vacio.
- `PinWindow` usa PINs hardcoded.
- `FlexoDbContext` y `Squema.sql` estan avanzados y pueden funcionar como base del dominio.
- Aun no existe el proyecto `FlexoCableSV.Web`.
- La validacion de conexion a base de datos esta comentada en `App.xaml.cs`.

Conclusion: el foco inmediato no debe ser crear mas pantallas, sino conectar las vistas existentes con datos reales, seguridad, servicios transaccionales, DTE, impresion y administracion web.

---

## 2. Modulos Faltantes

### 2.1 Base de Datos Operativa

**Descripcion:** Consolidar PostgreSQL como fuente unica de verdad para productos, empleados, ventas, DTE, inventario, configuracion y auditoria.

**Incluye:**

- Validar `Squema.sql` contra los modelos EF.
- Cargar catalogo inicial de productos.
- Cargar empleados, roles y permisos.
- Crear seed de tipos de medida, familias, subfamilias, proveedores y configuracion.
- Confirmar indices, triggers y constraints criticos.
- Crear datos minimos para pruebas de caja.

**Dependencias:** Ninguna.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- La app conecta a PostgreSQL sin errores.
- `FlexoDbContext` consulta productos, empleados y ordenes reales.
- Existe catalogo minimo funcional.
- Hay empleados con `can_cashier`, `can_sell` y `pin_hash`.
- No hay datos criticos hardcoded para operacion.

---

### 2.2 Seguridad, PIN y Sesion de Caja

**Descripcion:** Reemplazar PINs hardcoded por validacion real contra `hr.employees.pin_hash`.

**Incluye:**

- Crear `PinService`.
- Validar PIN con bcrypt.
- Filtrar empleados activos con permiso `can_cashier`.
- Guardar empleado autenticado en sesion de caja.
- Usar ese empleado para facturacion, corte, devoluciones y auditoria.
- Definir timeout de sesion.

**Dependencias:**

- Base de datos operativa.
- Tabla `hr.employees` poblada.

**Complejidad:** Media.

**Criterios de finalizacion:**

- `PinWindow` no contiene PINs hardcoded.
- Un cajero valido entra a Caja.
- Un PIN invalido es rechazado.
- El sistema sabe que empleado emitio cada venta.
- La sesion se cierra correctamente al salir o expirar.

---

### 2.3 Catalogo e Inventario Core

**Descripcion:** Servicio central para consultar stock, descontar inventario y registrar movimientos inmutables.

**Incluye:**

- Implementar `InventarioService`.
- Buscar productos por codigo, descripcion y familia.
- Validar unidades: `METRO`, `PIEZA`, `KIT`, `PESO`.
- Validar stock no negativo.
- Registrar movimientos en `InventoryMovements`.
- Generar o resolver alertas de stock bajo.
- Conectar `ConsultarStockView`.
- Conectar `VerCodigosView`.

**Dependencias:**

- Base de datos operativa.
- Seguridad si los ajustes requieren empleado/PIN.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- Caja y Confeccion consultan productos reales.
- El stock se descuenta al cerrar una venta.
- No se permite vender mas stock del disponible.
- Los movimientos quedan registrados.
- Las alertas de stock bajo aparecen correctamente.

---

### 2.4 Ordenes y Ventas

**Descripcion:** Hacer que el flujo de venta y confeccion cree ordenes reales en `sales.orders` y `sales.order_details`.

**Incluye:**

- Crear servicio de ordenes/ventas.
- Guardar borradores de confeccion.
- Crear ordenes desde Caja.
- Asociar tecnico/cajero.
- Calcular subtotales, IVA y total.
- Manejar estados: `BORRADOR`, `PENDIENTE_DTE`, `CERRADA`, `CONTINGENCIA`, `ANULADA`.
- Validar integridad entre orden, detalle, producto y stock.

**Dependencias:**

- Base de datos operativa.
- Inventario core.
- Seguridad/PIN.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- Una venta puede crearse desde UI.
- Los detalles se guardan en BD.
- Los totales son consistentes.
- Una orden cerrada descuenta inventario.
- El historial muestra ordenes reales.

---

### 2.5 Facturacion Electronica DTE

**Descripcion:** Implementar emision fiscal contra Ministerio de Hacienda, incluyendo contingencia.

**Incluye:**

- Implementar `DTEService`.
- Generar JSON DTE tipo `01`, `03` y `05`.
- Validar estructura DTE antes de enviar.
- Firmar con certificado `.p12`.
- Autenticarse contra API MH.
- Enviar DTE.
- Guardar sello de recepcion.
- Guardar errores.
- Crear cola de contingencia.
- Reintentar DTEs pendientes.

**Dependencias:**

- Ordenes y ventas.
- Base de datos DTE.
- Configuracion fiscal real.
- Seguridad de empleado/cajero.
- Inventario, porque la venta no debe cerrarse sin stock valido.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- Se genera DTE valido en ambiente de pruebas.
- El DTE se guarda en `dte.dte_issued`.
- Si MH falla, se guarda en `dte.dte_contingency`.
- El sistema puede reintentar contingencias.
- La venta queda trazable con su DTE.

---

### 2.6 Impresion y Tickets

**Descripcion:** Servicio para imprimir tickets termicos con datos de venta, DTE y QR.

**Incluye:**

- Implementar `ImpresionService`.
- Configurar impresoras desde `ImpresorasView`.
- Guardar impresora predeterminada en `system.printers` o `system.settings`.
- Imprimir prueba.
- Imprimir ticket de venta.
- Reimprimir desde historial.
- Incluir QR DTE.

**Dependencias:**

- DTE emitido o contingencia.
- Ordenes cerradas.
- Configuracion de impresora.

**Complejidad:** Media.

**Criterios de finalizacion:**

- Se puede seleccionar impresora.
- Se imprime ticket de prueba.
- Una venta cerrada imprime ticket.
- El ticket incluye total, productos, cajero, fecha y QR si aplica.
- El historial permite reimpresion.

---

### 2.7 Corte de Caja

**Descripcion:** Cierre financiero por turno o cajero.

**Incluye:**

- Ventas por metodo de pago.
- Total efectivo, tarjeta y transferencia.
- DTEs emitidos, fallidos y en contingencia.
- Diferencias entre monto declarado y calculado.
- Registro de cierre.
- Bloqueo o advertencia para ventas no cerradas.

**Dependencias:**

- Seguridad/PIN.
- Ordenes y ventas.
- DTE.
- Metodos de pago.

**Complejidad:** Media.

**Criterios de finalizacion:**

- Un cajero puede cerrar turno.
- El sistema calcula totales automaticamente.
- El corte queda registrado.
- Se puede consultar o imprimir corte.
- No se pierden ventas en contingencia.

---

### 2.8 Devoluciones y Nota de Credito

**Descripcion:** Flujo formal para anular o devolver ventas usando DTE-05.

**Incluye:**

- Buscar factura emitida.
- Seleccionar productos a devolver.
- Validar que no se devuelva mas de lo vendido.
- Generar DTE tipo `05`.
- Reingresar stock si corresponde.
- Registrar movimiento `ENTRADA_DEVOLUCION`.
- Auditar empleado que autorizo.

**Dependencias:**

- DTE operativo.
- Ventas cerradas.
- Inventario core.
- Seguridad/PIN.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- Se puede generar nota de credito desde una factura valida.
- El stock se restaura correctamente.
- La factura original queda relacionada con la nota.
- No se borra informacion historica.
- Se imprime comprobante de devolucion.

---

### 2.9 WebApp Administrativa

**Descripcion:** Proyecto Next.js para administracion, empleados, PINs, inventario, planilla y reportes.

**Estado actual:** No existe `FlexoCableSV.Web`.

**Incluye:**

- Crear proyecto Next.js.
- Configurar Prisma con PostgreSQL existente.
- Login con usuario y contrasena.
- Roles: Admin, Contador, Dueno.
- Dashboard.
- CRUD empleados.
- Asignacion/cambio de PIN.
- Gestion completa de inventario.
- Planilla.
- Reportes.

**Dependencias:**

- Base de datos estable.
- Modelo de empleados definido.
- Reglas de inventario cerradas.
- Reglas de planilla definidas.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- Admin puede iniciar sesion.
- Admin puede crear empleados y asignar PIN.
- Los PINs creados funcionan en WPF.
- Inventario web modifica datos reales.
- Dashboard muestra KPIs reales.
- Reportes exportan informacion util.

---

### 2.10 Planilla y Nomina

**Descripcion:** Calculo de pagos legales de empleados en El Salvador.

**Incluye:**

- Empleados, puestos y departamentos.
- Salario base.
- ISSS.
- AFP.
- ISR.
- Horas extra.
- Bonificaciones.
- Descuentos.
- Cierre mensual inmutable.
- Recibo PDF.

**Dependencias:**

- WebApp.
- Empleados.
- Parametros legales configurables.

**Complejidad:** Alta.

**Criterios de finalizacion:**

- Se puede generar planilla mensual.
- Los calculos son verificables.
- Una planilla cerrada no se modifica.
- Se genera recibo por empleado.
- Hay historial por mes/ano.

---

### 2.11 Reportes y Dashboard

**Descripcion:** Capa de analisis para administracion y dueno.

**Incluye:**

- Ventas por dia.
- Ventas por tecnico/cajero.
- Ticket promedio.
- Productos mas vendidos.
- Alertas de stock.
- Rotacion de inventario.
- DTEs enviados/fallidos.
- Exportacion a Excel.

**Dependencias:**

- Ventas reales.
- Inventario real.
- DTE real.
- WebApp.

**Complejidad:** Media.

**Criterios de finalizacion:**

- KPIs coinciden con datos de BD.
- Filtros por fecha funcionan.
- Exportacion funciona.
- Reportes principales cargan rapido.

---

### 2.12 Auditoria, Backup y Operacion

**Descripcion:** Preparar el sistema para uso real en sucursal.

**Incluye:**

- `system.audit_log`.
- Logs de errores.
- Backup automatico con `pg_dump`.
- Retencion de backups.
- Configuracion de ambientes: desarrollo, pruebas y produccion.
- Manejo seguro de certificados DTE.
- Documentacion de instalacion.
- Pruebas de restauracion.

**Dependencias:**

- BD estable.
- Flujos principales implementados.

**Complejidad:** Media.

**Criterios de finalizacion:**

- Cada accion critica queda auditada.
- Hay backup diario automatico.
- Se probo restaurar un backup.
- No hay secretos reales en el repositorio.
- Hay guia clara de instalacion.

---

## 3. Mapa de Dependencias

```text
Base de datos
  -> Seguridad/PIN
  -> Inventario core
  -> Ordenes y ventas
  -> DTE
  -> Impresion
  -> Corte de caja
  -> Devoluciones / Nota de credito
  -> WebApp administrativa
  -> Planilla
  -> Reportes
  -> Auditoria, backup y despliegue
```

| Modulo | Bloquea a |
|---|---|
| Base de datos | Todo |
| Seguridad/PIN | Caja, ventas, corte, devoluciones, auditoria |
| Inventario | Ventas, confeccion, devoluciones, reportes |
| Ordenes/Ventas | DTE, impresion, historial, corte |
| DTE | Facturacion real, notas de credito, tickets fiscales |
| WebApp | Gestion de empleados, PINs, inventario administrativo, planilla |
| Empleados | PIN, ventas por cajero, comisiones, planilla |
| Auditoria | Produccion segura |

---

## 4. Roadmap Recomendado

### Fase 0 - Estabilizacion Tecnica

**Prioridad:** Critica  
**Esfuerzo:** Medio  
**Objetivo:** Que la app arranque conectada a BD real.

**Tareas:**

- Validar `Squema.sql`.
- Ejecutar BD local.
- Activar validacion de conexion en `App.xaml.cs`.
- Verificar que EF Core mapea correctamente las tablas.
- Corregir diferencias entre README, `.csproj` y realidad tecnica.
- Confirmar version objetivo: README indica `.NET 8`, pero el proyecto usa `net10.0-windows`.

**Dependencias:** Ninguna.

**Criterios de cierre:**

- `dotnet build` exitoso.
- App abre solo si puede conectar a PostgreSQL.
- Consulta simple a productos y empleados funciona.
- Hay seed minimo.

---

### Fase 1 - Seguridad y Datos Base

**Prioridad:** Critica  
**Esfuerzo:** Medio  
**Objetivo:** Eliminar datos hardcoded y habilitar usuarios reales.

**Tareas:**

- Crear `PinService`.
- Validar bcrypt contra `hr.employees.pin_hash`.
- Reemplazar PINs hardcoded.
- Crear empleados seed.
- Mantener empleado autenticado en sesion.
- Registrar usuario/cajero en acciones criticas.

**Dependencias:** Fase 0.

**Criterios de cierre:**

- Solo empleados autorizados entran a Caja.
- El empleado autenticado queda disponible para facturar.
- PIN invalido no permite acceso.
- No quedan PINs hardcoded.

---

### Fase 2 - Inventario y Catalogo Funcional

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Que las vistas consulten datos reales.

**Tareas:**

- Implementar `InventarioService`.
- Conectar `ConsultarStockView`.
- Conectar `VerCodigosView`.
- Implementar busqueda por codigo/descripcion.
- Mostrar stock, unidad y estado visual.
- Validar decimales segun tipo de medida.
- Registrar movimientos.

**Dependencias:** Fase 0 y parcialmente Fase 1.

**Criterios de cierre:**

- Se pueden buscar productos reales.
- El stock mostrado viene de BD.
- El sistema distingue metros, piezas, kits y peso.
- Stock bajo se identifica correctamente.

---

### Fase 3 - Ordenes, Confeccion y Venta Interna

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Crear ordenes reales antes de facturar.

**Tareas:**

- Crear servicio de ordenes.
- Conectar `OrdenesConfeccionView`.
- Conectar `FacturacionView`.
- Guardar cabecera y detalle.
- Calcular totales.
- Validar stock antes de cerrar.
- Descontar inventario al cerrar venta.
- Conectar `HistorialVentasView` y `HistorialFacturasView` con BD.

**Dependencias:** Fase 1 y Fase 2.

**Criterios de cierre:**

- Se crea una orden real.
- Se agregan productos reales.
- Se calcula total correctamente.
- La venta descuenta inventario.
- El historial muestra ventas reales.

---

### Fase 4 - DTE MVP

**Prioridad:** Critica  
**Esfuerzo:** Alto  
**Objetivo:** Facturar electronicamente en ambiente de pruebas.

**Tareas:**

- Implementar `DTEService`.
- Leer configuracion desde `dte.dte_config`.
- Generar JSON DTE tipo `01`.
- Agregar tipo `03`.
- Firmar documento.
- Enviar a MH test.
- Guardar respuesta.
- Implementar contingencia basica.

**Dependencias:** Fase 3.

**Criterios de cierre:**

- Una venta genera DTE test valido.
- La respuesta de MH queda guardada.
- Si falla, la venta queda en contingencia.
- No se pierde la orden ni el detalle.

---

### Fase 5 - Impresion, Reimpresion y Caja Operativa

**Prioridad:** Alta  
**Esfuerzo:** Medio  
**Objetivo:** Completar operacion diaria de caja.

**Tareas:**

- Implementar `ImpresionService`.
- Conectar `ImpresorasView`.
- Imprimir ticket con QR.
- Reimprimir desde historial.
- Implementar `CorteCajaView`.
- Resumir ventas por metodo de pago.
- Registrar cierre de turno.

**Dependencias:** Fase 4.

**Criterios de cierre:**

- Se imprime ticket real.
- Se puede reimprimir.
- El corte de caja calcula totales reales.
- El cierre queda guardado.

---

### Fase 6 - Devoluciones y Nota de Credito

**Prioridad:** Alta  
**Esfuerzo:** Alto  
**Objetivo:** Manejar anulaciones/devoluciones correctamente.

**Tareas:**

- Conectar `DevolucionesView`.
- Buscar factura original.
- Validar productos devueltos.
- Generar DTE-05.
- Reingresar stock.
- Registrar auditoria.

**Dependencias:** Fase 4 y Fase 5.

**Criterios de cierre:**

- Se emite nota de credito.
- Se actualiza inventario correctamente.
- La factura original mantiene trazabilidad.
- La devolucion queda auditada.

---

### Fase 7 - WebApp Administrativa Base

**Prioridad:** Alta  
**Esfuerzo:** Alto  
**Objetivo:** Crear la WebApp que administra empleados, PINs e inventario.

**Tareas:**

- Crear `FlexoCableSV.Web`.
- Configurar Next.js, Prisma, Tailwind y autenticacion.
- Crear login.
- Crear roles.
- CRUD empleados.
- Asignar y cambiar PIN.
- CRUD basico de productos.
- Pantalla dashboard inicial.

**Dependencias:** Fase 0 y modelo de empleados estable.

**Criterios de cierre:**

- Admin inicia sesion.
- Admin crea empleados.
- Admin asigna PIN.
- Ese PIN funciona en la app WPF.
- WebApp lee/escribe en la misma BD.

---

### Fase 8 - Inventario Administrativo Web

**Prioridad:** Media-Alta  
**Esfuerzo:** Alto  
**Objetivo:** Centralizar entradas, ajustes y reconciliacion.

**Tareas:**

- Entradas de mercancia.
- Ajustes con motivo obligatorio.
- Reconciliacion fisica.
- Alertas de stock.
- Proveedores.
- Historial de movimientos.

**Dependencias:** Fase 7 e `InventarioService` estable.

**Criterios de cierre:**

- Se registran entradas desde web.
- Los ajustes quedan auditados.
- El stock coincide entre WebApp y WPF.
- Alertas funcionan.

---

### Fase 9 - Planilla y RRHH

**Prioridad:** Media  
**Esfuerzo:** Alto  
**Objetivo:** Automatizar nomina.

**Tareas:**

- Completar empleados, puestos y departamentos.
- Configurar parametros legales.
- Calcular ISSS, AFP e ISR.
- Registrar horas extra.
- Registrar bonos/descuentos.
- Cerrar planilla mensual.
- Generar recibos PDF.

**Dependencias:** Fase 7.

**Criterios de cierre:**

- Se calcula planilla mensual.
- Los calculos son verificables.
- Planilla cerrada queda inmutable.
- Se genera recibo PDF.

---

### Fase 10 - Reportes, Auditoria y Produccion

**Prioridad:** Media  
**Esfuerzo:** Medio  
**Objetivo:** Preparar el sistema para uso real.

**Tareas:**

- Dashboard final.
- Reportes exportables.
- Auditoria de acciones criticas.
- Backup automatico.
- Restauracion probada.
- Configuracion produccion.
- Documentacion de operacion.
- Pruebas end-to-end.

**Dependencias:** Todas las fases operativas.

**Criterios de cierre:**

- Reportes coinciden con BD.
- Hay backups automaticos.
- Se probo restaurar backup.
- DTE funciona en ambiente final.
- Caja puede operar un dia completo sin intervencion tecnica.

---

## 5. Orden Inmediato de Desarrollo

Este es el orden recomendado para iniciar implementacion sin bloquearse por dependencias futuras:

1. Arreglar y validar conexion real a PostgreSQL.
2. Cargar seed minimo: empleados, productos, familias y medidas.
3. Implementar `PinService`.
4. Reemplazar PINs hardcoded.
5. Implementar `InventarioService`.
6. Conectar `ConsultarStockView` y `VerCodigosView`.
7. Crear servicio de ordenes.
8. Conectar `FacturacionView`.
9. Descontar inventario al cerrar venta.
10. Implementar `DTEService` en ambiente de prueba.
11. Implementar impresion.
12. Implementar corte de caja.
13. Crear WebApp.
14. Mover gestion de empleados/PINs a WebApp.
15. Completar inventario web, planilla y reportes.

---

## 6. Prioridades Resumidas

| Prioridad | Modulos |
|---|---|
| Critica | BD, PIN real, inventario, ordenes, ventas, DTE |
| Alta | Impresion, corte de caja, devoluciones, WebApp base |
| Media | Inventario administrativo, planilla, reportes |
| Operativa | Auditoria, backups, documentacion, despliegue |

---

## 7. Recomendacion Arquitectonica Final

La ruta mas segura es convertir primero la WPF en una caja funcional real:

```text
BD + PIN + Inventario + Venta + DTE + Impresion
```

Luego se debe construir la WebApp administrativa sobre reglas de negocio ya estabilizadas:

```text
Empleados/PINs + Inventario administrativo + Planilla + Reportes
```

Esto minimiza dependencias bloqueantes, evita duplicar logica y reduce el riesgo de tener una WebApp administrando datos que la caja todavia no sabe procesar correctamente.
