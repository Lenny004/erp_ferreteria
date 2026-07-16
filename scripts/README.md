# Scripts — Ferreteria (erp_ferreteria)

Utilidades de línea de comandos para el despliegue de la app de caja
`Ferreteria.PuntoVenta` (WPF, `net10.0-windows`).

---

## `publish-caja.ps1`

Publica la app de caja lista para copiar a la PC de la sucursal.

### Uso rápido (PowerShell)

```powershell
# Desde la raíz del repo erp_ferreteria

# 1) Publicación framework-dependent (requiere runtime .NET 10 en la PC de caja)
./scripts/publish-caja.ps1

# 2) Publicación autocontenida (no requiere .NET instalado) en un único ejecutable
./scripts/publish-caja.ps1 -SelfContained -SingleFile
```

Salida por defecto: `./publish/caja`.

### Parámetros

| Parámetro | Tipo | Descripción |
|---|---|---|
| `-SelfContained` | switch | Publica autocontenido `win-x64` (`--self-contained true`). No requiere .NET instalado en la caja. |
| `-SingleFile` | switch | Empaqueta en un único ejecutable (`-p:PublishSingleFile=true`). Úsalo junto con `-SelfContained`. |
| `-OutputDir` | string | Carpeta de salida (por defecto `./publish/caja`). |
| `-Configuration` | string | Configuración de build (por defecto `Release`). |

### Qué hace

1. Verifica que exista `dotnet` (avisa cómo instalarlo si falta).
2. Verifica que exista el proyecto `Ferreteria.PuntoVenta/Ferreteria.PuntoVenta.csproj`.
3. Ejecuta `dotnet publish -c Release -r win-x64` (framework-dependent por defecto).
4. Confirma que `Config/appsettings.json` quedó junto al ejecutable **sin sobrescribir credenciales**.
5. Imprime los pasos de post-despliegue.

> **Nota:** `Config/appsettings.json` se copia por `CopyToOutputDirectory` del `.csproj`. El script
> **no** modifica ni sobrescribe ese archivo, para no borrar credenciales del MH ya cargadas en la
> PC de caja.

---

## Flujo de despliegue completo

1. **Publicar** con `publish-caja.ps1` (elige framework-dependent o autocontenido).
2. **Copiar** la carpeta `publish/caja` a la PC de la sucursal.
3. **Instalar el Firmador del MH** (`svfe-api-firmador`) como servicio de Windows en el puerto `8113`.
4. **Configurar** la sección `[Mh]` de `Config/appsettings.json` con los datos que entregue el
   Ministerio de Hacienda (NIT, credenciales de API, certificado `.p12`, códigos de establecimiento).
   Dejar los valores **vacíos** hasta tenerlos.
5. **Cargar** la ficha fiscal del emisor en la tabla `dte.DteConfig` de la base de datos.
6. **Configurar la impresora térmica** (USB o red 9100, ancho 80/58mm) y hacer impresión de prueba.
7. **Mantener** `Ambiente = 00` (pruebas) hasta certificar con el MH; luego cambiar a `01` (producción).

Guía técnica y operativa completa:
[`docs/FERRETERIA_FACTURACION_DTE_IMPRESION_2026.md`](../docs/FERRETERIA_FACTURACION_DTE_IMPRESION_2026.md).

---

## Requisitos

| Requisito | Versión |
|---|---|
| .NET SDK | 10.0+ (`net10.0-windows`) |
| PowerShell | 5.1+ o PowerShell 7+ |
| SO | Windows 10/11 (la app WPF es solo Windows) |
