# Guía para Agentes de Cursor — Ferreteria.PuntoVenta

Este documento proporciona orientación a los agentes de Cursor que trabajan en el proyecto **Ferreteria.PuntoVenta**.

## Skills del proyecto

El repositorio incluye dos skills obligatorias que **todo agente debe seguir** al trabajar en el código:

### 1. Principios de calidad

**Ubicación:** `.cursor/skills/principios-calidad/SKILL.md`

**Descripción:** Estándar de 100 criterios de calidad organizados en 7 familias (arquitectura, validación, seguridad, transacciones, recuperación, verificación, telemetría).

**Cuándo invocar:**
- Al diseñar nuevas funcionalidades
- Al escribir código (servicios, ViewModels, entidades, lógica de negocio)
- Al revisar código existente
- Al auditar calidad o seguridad

**Aplicación en WPF/MVVM:**
- Views: SRP, separación presentación/lógica
- ViewModels: Responsabilidad única, bajo acoplamiento, determinismo
- Servicios: DIP, transacciones EF Core explícitas, manejo centralizado de excepciones
- Modelos: Inmutabilidad de PKs, validación, integridad referencial
- Seguridad: Zero Trust, validación entrada, hash bcrypt PINs, RBAC
- Recuperación: Rollback controlado, Circuit Breaker DTE MH, timeout, contingencia
- Telemetría: AuditService, logs estructurados sin datos sensibles

### 2. Documentación de código

**Ubicación:** `.cursor/skills/documentacion-codigo/SKILL.md`

**Descripción:** Estándar oficial de XML documentation comments de C# adaptado al contexto de Ferreteria.PuntoVenta.

**Cuándo invocar:**
- Al documentar clases, métodos, propiedades, ViewModels, servicios, entidades EF Core
- Al agregar comentarios inline en lógica compleja
- Al resolver advertencias CS1591 (miembros públicos sin documentación)

**Formato estándar:**
```csharp
/// <summary>
/// Descripción concisa de la responsabilidad.
/// </summary>
/// <param name="nombre">Descripción del parámetro.</param>
/// <returns>Qué devuelve y en qué casos.</returns>
/// <exception cref="Tipo">Cuándo se lanza.</exception>
/// <remarks>
/// Contexto adicional: transacciones, idempotencia, contingencia DTE.
/// </remarks>
```

**Idioma:** Español en todos los comentarios.

## Regla always-active

**Ubicación:** `.cursor/rules/reglas-proyecto.mdc`

Esta regla obliga a seguir ambas skills en todo cambio de código y a reportar en cada PR cómo se cumplieron.

### Checklist de PR (obligatorio)

Al crear o actualizar un PR, incluir en la descripción:

#### Cumplimiento de principios de calidad
- Familia 1 (Arquitectura): [describir]
- Familia 2 (Validación): [describir]
- Familia 3 (Seguridad): [describir si aplica]
- Familia 4 (Transacciones): [describir si aplica]
- Familia 5 (Recuperación): [describir si aplica]
- Familia 6 (Verificación): [describir]
- Familia 7 (Telemetría): [describir si aplica]

#### Documentación agregada
- Archivos documentados: [listar]
- XML comments: [resumen]
- CS1591 resueltas: [número]

## Contexto del proyecto

**Ferreteria.PuntoVenta** es una aplicación WPF en C# (.NET 10) con EF Core sobre PostgreSQL para punto de venta y confección de cables en El Salvador. Incluye:

- **Caja:** Facturación DTE (Ministerio de Hacienda), impresión tickets ESC/POS, gestión stock
- **Confección:** Órdenes de ensamble de cables custom
- **Autenticación:** PIN bcrypt (no contraseña tradicional)
- **Arquitectura:** MVVM (Views XAML + ViewModels + Services + Models + Data)
- **BD:** PostgreSQL con esquemas `public`, `sales`, `dte`, `hr`, `system`

**Referencia completa:** `README.md` en la raíz del repositorio.

## Recursos adicionales

- **Plan de desarrollo:** `docs/FERRETERIA_PLAN_FINALIZACION_APP.md`
- **Estándares Git/PR:** `docs/FERRETERIA_GIT_PR_2026.md`
- **Esquema BD:** `ferreteria_backend/prisma/schema.prisma` (fuente de verdad v3.0)

## Comandos útiles

```bash
# Build local
dotnet build

# Ejecutar app
dotnet run --project Ferreteria.PuntoVenta

# Generar documentación XML (automático al build si GenerateDocumentationFile está habilitado)
# Revisar advertencias CS1591 en output del build

# Conectar a PostgreSQL local (Docker)
# Puerto: 55432 (ver appsettings.json)
```

## Flujo de trabajo recomendado

1. **Leer** ambas skills antes de escribir código
2. **Diseñar** aplicando principios de calidad (familias 1-7)
3. **Escribir** código siguiendo SRP, DIP, validación Zero Trust
4. **Documentar** con XML comments (español, estándar Microsoft)
5. **Verificar** CS1591 resueltas, build sin errores
6. **Commitear** con mensaje al estilo del repo (ver FERRETERIA_GIT_PR_2026.md)
7. **PR** con checklist de cumplimiento de reglas

## Contacto

Para dudas sobre las skills o las reglas del proyecto, consultar con el equipo de desarrollo o revisar los archivos de skills directamente.

---

**Última actualización:** 2026-09-26
