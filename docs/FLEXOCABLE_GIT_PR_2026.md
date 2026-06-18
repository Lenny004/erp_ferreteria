# FlexoCable SV — Git, Pull Requests y Revisión

> **Documento:** FLEXO-DEV-STD-002  
> **Versión:** 1.1  
> **Fecha:** Junio 2026  
> **Complementa:** [FLEXOCABLE_C_CODING_STANDARDS_2026.md](FLEXOCABLE_C_CODING_STANDARDS_2026.md)

---

## Índice

1. [Cuándo commitear y hacer push](#cuándo-commitear-y-hacer-push)
2. [Ciclo diario FlexoCable (add → commit → push)](#ciclo-diario-flexocable-add--commit--push)
3. [Referencia rápida de comandos Git](#referencia-rápida-de-comandos-git)
4. [Protocolo git](#protocolo-git)
5. [Mensajes de commit](#mensajes-de-commit)
6. [Pull requests](#pull-requests)
7. [Revisión de código](#revisión-de-código)
8. [Checklist pre-commit (código)](#checklist-pre-commit-código)

---

## Cuándo commitear y hacer push

**Práctica del equipo:** al terminar una tarea lógica (feature, fix, doc, migración, etc.), crear **un commit** y **un push** al remoto. No acumular muchos cambios sin subir.

| Momento | Acción |
|---------|--------|
| Terminaste una tarea acotada | `git add` → `git commit` → `git push` |
| Cambio solo local / experimental | Puedes commitear en rama sin push hasta que esté listo |
| Trabajo con agente (Cursor) | Pedir explícitamente *"haz commit y push"* si quieres que el agente lo ejecute |
| No está claro si commitear | Preguntar antes |

**Repos del monorepo / workspace:**

| Carpeta | Uso |
|---------|-----|
| `FlexoCable/` | WPF Punto de Venta, docs, herramientas |
| `FlexoCable-backend/` | Prisma, seed, Docker PostgreSQL |

Cada carpeta puede ser su propio repositorio Git. Entra a la carpeta correcta antes de `git status`, `commit` o `push`.

---

## Ciclo diario FlexoCable (add → commit → push)

Flujo mínimo cada vez que completes algo y quieras dejarlo guardado en GitHub:

```powershell
# 1. Ir al repo correcto
cd "C:\Users\progr\Documents\FlexoCable Sistema\FlexoCable"
# o: cd "...\FlexoCable-backend"

# 2. Ver qué cambió
git status
git diff

# 3. Agregar archivos (solo lo relevante; evitar binarios y secretos)
git add .
# o: git add docs/FLEXOCABLE_PLAN_FINALIZACION_APP.md

# 4. Commit con mensaje claro (ver sección Mensajes de commit)
git commit -m "📝 Documentar flujo de confección y cliente Consumidor Final en el plan"

# 5. Subir al remoto
git push origin main
# Si es la primera vez en una rama nueva:
# git push -u origin nombre-de-rama
```

**Ramas (recomendado para features):**

```powershell
git switch -c feature/ordenes-confeccion
# ... trabajar, commit, push ...
git push -u origin feature/ordenes-confeccion
# Luego abrir PR hacia main (sección Pull requests)
```

**Antes de cada commit, revisar:**

- [ ] No hay `.env`, passwords ni `appsettings` con secretos reales
- [ ] `git status` muestra solo lo que quieres incluir
- [ ] El mensaje describe el **por qué**, no solo el qué

---

## Referencia rápida de comandos Git

### Configuración inicial (una sola vez por máquina)

```bash
git config --global user.name "Lenny004"
git config --global user.email "tu-email@ejemplo.com"
```

Comprobar:

```bash
git config --global --list
```

### Inicio de proyecto

```bash
git init                    # Inicializar repo en la carpeta actual
git clone URL               # Clonar repo existente
```

### Ciclo diario (add → commit → push)

```bash
git status                  # Ver qué cambió
git add archivo.cs          # Agregar un archivo al staging
git add .                   # Agregar todo al staging (revisar antes)
git commit -m "mensaje"     # Guardar checkpoint local
git push origin main        # Subir a GitHub (rama main)
```

### Historial

```bash
git log                     # Ver todos los commits
git log --oneline           # Ver commits en una línea
git diff                    # Ver cambios sin staging
git diff --staged           # Ver cambios ya en staging
```

### Deshacer cosas

```bash
git restore archivo.cs              # Descartar cambios locales del archivo
git restore --staged archivo.cs     # Sacar del staging sin borrar cambios
git revert HASH                     # Crear commit que deshace un commit anterior
git reset --soft HEAD~1             # Deshacer último commit; cambios quedan en staging
```

Usar `reset --hard` solo si sabes que perderás cambios locales. No usar en commits ya pusheados sin acuerdo del equipo.

### Ramas

```bash
git branch                          # Ver ramas
git branch nueva-feature            # Crear rama
git switch nueva-feature            # Cambiar a rama (Git 2.23+)
git checkout nueva-feature          # Alternativa clásica
git merge nueva-feature             # Fusionar rama en la actual
git branch -d nueva-feature         # Eliminar rama local (ya fusionada)
```

### Remoto (GitHub)

```bash
git remote add origin URL           # Conectar carpeta local con GitHub
git remote -v                       # Ver remotos configurados
git pull origin main                # Traer cambios del remoto y fusionar
git fetch origin                    # Traer cambios sin fusionar
```

Crear repo en GitHub desde la carpeta actual (requiere [GitHub CLI](https://cli.github.com/) — `gh`):

```bash
gh repo create nombre-repo --private --source=. --remote=origin --push
```

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
