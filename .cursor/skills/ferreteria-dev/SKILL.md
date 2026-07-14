---
name: ferreteria-dev
description: >-
  Aplica estándares Ferreteria al codificar, commitear, abrir PRs y revisar cambios
  en .NET 8, EF Core, PostgreSQL y DTE. Usar al escribir o revisar C#/XAML/SQL,
  al pedir commit, PR, pull request, gh, git diff, code review, o estándares del equipo.
---

# Ferreteria — Desarrollo unificado

**Fuente de verdad:** leer los documentos en `docs/`; no duplicar reglas aquí.

| Tarea | Documento |
|-------|-----------|
| C#, EF, servicios, calidad de código | [docs/FERRETERIA_C_CODING_STANDARDS_2026.md](../../../docs/FERRETERIA_C_CODING_STANDARDS_2026.md) |
| Git, commits, PRs, revisión | [docs/FERRETERIA_GIT_PR_2026.md](../../../docs/FERRETERIA_GIT_PR_2026.md) |
| Índice | [docs/README.md](../../../docs/README.md) |

## Flujo del agente

1. **Identificar la tarea** (implementar, commitear, PR, revisar).
2. **Abrir el documento correspondiente** y seguir sus secciones.
3. **Commits / PR:** ejecutar los comandos git indicados en FERRETERIA-DEV-STD-002; commit solo si el usuario lo pide explícitamente.
4. **Revisión:** combinar checklist de dominio (STD-002) con Pre-Commit y estilo C# (STD-001).
5. **Cambios de código:** cumplir nomenclatura, async, EF y guard clauses del STD-001; cambios mínimos al alcance pedido.

## Alcance

Repositorio Ferreteria: Punto de Venta WPF, APIs, `Squema.sql`, facturación DTE. Respuestas en **español** salvo petición contraria.
