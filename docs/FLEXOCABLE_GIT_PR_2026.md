# FlexoCable SV — Git, Pull Requests y Revisión

> **Documento:** FLEXO-DEV-STD-002  
> **Versión:** 1.0  
> **Fecha:** Mayo 2026  
> **Complementa:** [FLEXOCABLE_C_CODING_STANDARDS_2026.md](FLEXOCABLE_C_CODING_STANDARDS_2026.md)

---

## Índice

1. [Cuándo commitear](#cuándo-commitear)
2. [Protocolo git](#protocolo-git)
3. [Mensajes de commit](#mensajes-de-commit)
4. [Pull requests](#pull-requests)
5. [Revisión de código](#revisión-de-código)
6. [Checklist pre-commit (código)](#checklist-pre-commit-código)

---

## Cuándo commitear

Solo crear commits cuando el desarrollador (o el agente, con petición explícita) lo solicite. Si no está claro, preguntar antes.

---

## Protocolo git

| Regla | Detalle |
|-------|---------|
| Config | No modificar `git config` |
| Destructivos | No `push --force`, `reset --hard`, etc., salvo petición explícita |
| Hooks | No `--no-verify` salvo petición explícita |
| Amend | Solo si se pide, el commit es de la sesión actual y no está en remoto |
| Secretos | No commitear `.env`, credenciales ni cadenas de conexión reales |
| Interactivo | No usar flags `-i` |
| Push | No hacer `push` salvo petición explícita |

### Comandos antes de commitear

```bash
git status
git diff
git log -10 --oneline
```

Analizar todos los cambios que irán al commit. Mensaje en **español**, 1–2 oraciones, enfoque en el **por qué**.

### Crear el commit

```bash
git add <rutas relevantes>
git commit -m "<mensaje>"
git status
```

En PowerShell, usar `-m` con el mensaje en una sola línea si el heredoc de bash no aplica.

---

## Mensajes de commit

Patrón del historial de FlexoCable:

1. **Emoji** según tipo de cambio.
2. **Descripción** clara en español (puede ser una línea larga).
3. Contexto de negocio cuando aplique: DTE, inventario, `employee_id`, `Squema.sql`, etc.

| Emoji | Uso típico |
|-------|------------|
| 🎉 | Documentación inicial o hitos grandes |
| ✨ | Nueva funcionalidad o modelos/entidades |
| 🔧 | Mejoras, ajustes, SQL, configuración |
| ♻️ | Refactor sin cambio de comportamiento visible |
| 🐛 | Corrección de bug |
| 🔒 | Seguridad o permisos |
| 📝 | Solo documentación |

**Ejemplo:**

```
🔧 Se actualizó Squema.sql y los modelos EF para usar employee_id en el modal de PIN y evitar alertas de stock duplicadas.
```

Evitar mensajes genéricos (`fix`, `update`, `cambios varios`) sin contexto.

---

## Pull requests

Usar **`gh`** para operaciones en GitHub.

### Antes de crear la PR

```bash
git status
git diff
git branch -vv
git log <rama-base>..HEAD --oneline
git diff <rama-base>...HEAD
```

Incluir **todos** los commits de la rama. Rama base habitual: `main` (o la indicada por el equipo).

### Crear PR

1. Crear rama si hace falta.
2. `git push -u origin HEAD` solo si la rama no está en remoto.
3. `gh pr create` con título y cuerpo en español.

**Plantilla del cuerpo:**

```markdown
## Resumen
- <cambio principal 1>
- <cambio principal 2>

## Plan de prueba
- [ ] <caso en Punto de Venta / API / BD>
```

Mencionar impacto en **DTE**, **inventario**, **PIN/técnicos** o **Squema.sql** cuando aplique. Devolver la URL de la PR.

---

## Revisión de código

### Dominio FlexoCable

- [ ] **Correctitud**: ventas, stock (metro/pieza/kit/peso), redondeos y totales.
- [ ] **EF / datos**: relaciones, obligatorios, `employee_id` vs legacy, alineación con `Squema.sql`.
- [ ] **PostgreSQL**: índices, funciones, triggers; alertas de stock sin duplicados.
- [ ] **DTE / MH**: cola de contingencia, sellos; sin credenciales en logs.
- [ ] **UI WPF**: PIN al facturar (no al entrar); XAML coherente.
- [ ] **Seguridad**: sin secretos; validación en APIs.
- [ ] **Alcance**: cambios mínimos; sin refactors no pedidos.

### Estilo y calidad C#

Aplicar [FLEXOCABLE_C_CODING_STANDARDS_2026.md](FLEXOCABLE_C_CODING_STANDARDS_2026.md) (nomenclatura, async, EF, StyleCop/Roslynator).

### Formato del feedback

```markdown
### Crítico
- ...

### Sugerencia
- ...

### Opcional
- ...
```

Ser concreto: archivo, comportamiento esperado vs observado.

---

## Checklist pre-commit (código)

Antes de `git commit`, además del mensaje y el protocolo git, verificar el **Pre-Commit Checklist** en [FLEXOCABLE_C_CODING_STANDARDS_2026.md § Checklist de Calidad](FLEXOCABLE_C_CODING_STANDARDS_2026.md#checklist-de-calidad):

- Nomenclatura, formato, guard clauses, async.
- `dotnet build` sin warnings críticos relevantes.
- StyleCop / Roslynator según umbrales del estándar.

---

**Documentos del equipo**

| ID | Archivo | Contenido |
|----|---------|-----------|
| FLEXO-DEV-STD-001 | `FLEXOCABLE_C_CODING_STANDARDS_2026.md` | C#, EF, servicios, herramientas |
| FLEXO-DEV-STD-002 | `FLEXOCABLE_GIT_PR_2026.md` | Git, PR, revisión (este documento) |
