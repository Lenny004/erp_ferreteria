# Ferreteria — Facturación Electrónica DTE e Impresión Térmica

> **Documento:** FERRETERIA-OPS-DTE-001
> **Versión:** 1.0
> **Fecha:** Julio 2026
> **Ámbito:** App de caja `Ferreteria.PuntoVenta` (WPF, `net10.0-windows`)
> **País / autoridad fiscal:** El Salvador — Ministerio de Hacienda (MH)
> **Complementa:** [`README.md`](../README.md) · [`FERRETERIA_PLAN_FINALIZACION_APP.md`](FERRETERIA_PLAN_FINALIZACION_APP.md)

Documento técnico-operativo para **desplegar, configurar y operar** la facturación electrónica DTE
y la impresión térmica de tickets en la PC de caja de la sucursal de San Salvador.

> **Aviso importante:** todos los datos fiscales del emisor (NIT, NRC, credenciales de API,
> certificado `.p12`, etc.) son **placeholders vacíos** hasta que el Ministerio de Hacienda y el
> contador del cliente los entreguen. **No inventar valores.** Ver
> [§2 Requisitos previos del MH](#2-requisitos-previos-del-ministerio-de-hacienda).

---

## Índice

1. [Arquitectura del módulo DTE](#1-arquitectura-del-módulo-dte)
2. [Requisitos previos del Ministerio de Hacienda](#2-requisitos-previos-del-ministerio-de-hacienda)
3. [Firmador del MH (svfe-api-firmador) en la PC de caja](#3-firmador-del-mh-svfe-api-firmador-en-la-pc-de-caja)
4. [Ambiente de pruebas vs producción y certificación](#4-ambiente-de-pruebas-vs-producción-y-certificación)
5. [Impresión térmica del ticket](#5-impresión-térmica-del-ticket)
6. [Contingencia y reintentos](#6-contingencia-y-reintentos)
7. [Retención legal de DTE (10 años)](#7-retención-legal-de-dte-10-años)
8. [Solución de problemas](#8-solución-de-problemas)
9. [Checklist de puesta en marcha](#9-checklist-de-puesta-en-marcha)

---

## 1. Arquitectura del módulo DTE

La caja **nunca** habla directamente firmando el JSON: usa el **Firmador local del MH** (un servicio
Java que corre en la misma PC) para producir el documento firmado (JWS), y luego transmite ese
documento firmado a la **API de recepción del MH** usando un token de sesión.

### 1.1 Flujo normal (en línea)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  PC DE CAJA (sucursal San Salvador)                                            │
│                                                                                │
│   ┌───────────────────────┐        1. Construir JSON DTE                       │
│   │  Ferreteria.PuntoVenta │  ───────────────────────────────────────┐        │
│   │  (WPF · DTEService)    │                                          │        │
│   └───────────┬───────────┘                                          ▼        │
│               │  2. POST /firmardocumento/            ┌────────────────────┐   │
│               ├──────────────────────────────────────►  FIRMADOR MH LOCAL │   │
│               │     { nit, activo, passwordPri,       │  (Java .jar)       │   │
│               │       dteJson }                       │  http://localhost: │   │
│               │  ◄──────────────────────────────────  │  8113/firmar...    │   │
│               │     { status: OK, body: <JWS firma> } └────────────────────┘   │
│               │                                                                 │
│               │  3. POST /seguridad/auth  (user + pwd de la app MH)             │
│               │        ─────────────────────────────────►                      │
│               │        ◄──────── { token Bearer }                              │
│               │                                                                 │
│               │  4. POST /fesv/recepciondte  (Authorization: Bearer <token>)    │
│               │        { ambiente, idEnvio, documento: <JWS>, ... }             │
│               │        ─────────────────────────────────►                      │
│               │        ◄──────── { estado: PROCESADO, selloRecibido, ... }      │
│               ▼                                                                 │
│   ┌───────────────────────┐    5. Persistir en BD  dte.DteIssued               │
│   │  PostgreSQL (dte.*)    │       (MhStatus=PROCESADO, MhSello, JsonPayload)   │
│   └───────────────────────┘                                                     │
│               │                                                                 │
│               │  6. Imprimir ticket ESC/POS 80mm con QR de consulta pública    │
│               ▼                                                                 │
│         ┌─────────────┐                                                         │
│         │ Impresora   │                                                         │
│         │ térmica     │                                                         │
│         └─────────────┘                                                         │
└─────────────────────────────────────────┬──────────────────────────────────────┘
                                           │  (Internet)
                                           ▼
                          ┌─────────────────────────────────┐
                          │  API MINISTERIO DE HACIENDA      │
                          │  Pruebas:  apitest.dtes.mh.gob.sv │
                          │  Producción:  api.dtes.mh.gob.sv  │
                          └─────────────────────────────────┘
```

### 1.2 Flujo de contingencia (MH o Firmador no responden)

```
      ┌───────────────┐   falla firma / auth / recepción / red
      │ DTEService     │ ─────────────────────────────────────────┐
      └───────┬───────┘                                            ▼
              │                                    ┌──────────────────────────────┐
              │ marca MhStatus = CONTINGENCIA      │  dte.DteIssued                │
              │ (guarda JsonPayload sin sello)     │  MhStatus = CONTINGENCIA      │
              ▼                                    └──────────────┬───────────────┘
      ┌───────────────┐  crea/actualiza registro                 │
      │ Cola local     │◄─────────────────────────────────────────┘
      │ dte.DteContingency (AttemptCount, LastError, NextRetryAt)  │
      └───────┬───────┘
              │  worker de reintento (cada ~15 min, backoff)
              ▼
      ┌───────────────┐  reintenta pasos 2–4 del flujo normal
      │ Reenvío al MH │ ── éxito ──► MhStatus=PROCESADO + MhSello, ResolvedAt=now
      └───────┬───────┘ ── falla ──► AttemptCount++, NextRetryAt=now+backoff
              │
              └──► la venta ya está COMPLETADA; el ticket se puede imprimir marcado
                   "DTE EN CONTINGENCIA" y reimprimirse con sello al resolverse.
```

**Reglas alineadas al esquema** (ver `README.md` y modelos EF Core):

| Entidad | Campo | Valores | Nota |
|---|---|---|---|
| `sales.Orders` | `Status` | `PENDIENTE`, `COMPLETADA`, `CANCELADA` | La contingencia fiscal **no** es estado de la orden |
| `dte.DteIssued` | `MhStatus` | `PENDIENTE`, `PROCESADO`, `RECHAZADO`, `CONTINGENCIA` | Una orden `COMPLETADA` puede tener DTE en contingencia |
| `dte.DteContingency` | `AttemptCount`, `LastError`, `NextRetryAt`, `ResolvedAt` | — | Cola de reintentos por DTE |

### 1.3 Tipos de DTE soportados

| Código | Tipo | Cuándo se usa |
|---|---|---|
| `01` | Factura de Consumidor Final | Ventas a personas naturales (sin NRC) |
| `03` | Comprobante de Crédito Fiscal (CCF) | Ventas a empresas / contribuyentes (con NIT + NRC) |
| `05` | Nota de Crédito | Devoluciones o anulaciones de un DTE previo |

### 1.4 Componentes en la PC de caja

| Componente | Qué es | Puerto / ruta |
|---|---|---|
| `Ferreteria.PuntoVenta` | App WPF de caja (este repo) | Ejecutable local |
| Firmador MH (`svfe-api-firmador`) | Servicio Java del MH que firma el JSON con el `.p12` | `http://localhost:8113/firmardocumento/` |
| Certificado `.p12` | Certificado del emisor emitido por el MH | Ruta local segura (ver §3) |
| PostgreSQL | BD del sistema (esquema `dte`) | Local/servidor, puerto `55432` en dev |
| Impresora térmica | ESC/POS 80mm (USB o red 9100) | Local o IP LAN |

---

## 2. Requisitos previos del Ministerio de Hacienda

El cliente (a través de su contador y del portal del MH) **DEBE** entregar los siguientes datos
**antes** de emitir DTE reales. Sin ellos la caja no puede firmar ni transmitir.

> Mientras no se entreguen, los valores quedan **vacíos** en configuración. **No usar datos de
> ejemplo en producción.**

### 2.1 Dónde se coloca cada dato

Hay **dos lugares** de configuración:

- **A) Sección `Mh` de `Ferreteria.PuntoVenta/Config/appsettings.json`** — parámetros de conexión y
  del firmador (rutas, credenciales de aplicación, URLs, puerto del firmador). Este archivo se copia
  junto al ejecutable (`CopyToOutputDirectory`).
- **B) Tabla `dte.DteConfig` de la base de datos** — datos de identidad fiscal del emisor que
  aparecen impresos en el DTE (NIT, NRC, razón social, dirección, actividad económica, etc.).

| # | Dato que entrega el MH / contador | Dónde se coloca | Clave / columna | Ejemplo de formato | Obligatorio |
|---|---|---|---|---|---|
| 1 | NIT del emisor | BD `dte.DteConfig` | `EmisorNit` | `0614-XXXXXX-XXX-X` (14 díg. sin guiones en JSON) | ✅ |
| 2 | NRC del emisor | BD `dte.DteConfig` | `EmisorNrc` | `XXXXXX-X` | ✅ |
| 3 | Nombre / razón social | BD `dte.DteConfig` | `EmisorName` | `Ferreteria, S.A. de C.V.` | ✅ |
| 4 | Nombre comercial | BD `dte.DteConfig` | `EmisorTradeName` | `Ferreteria` | ⬜ (opcional) |
| 5 | Código de actividad económica | BD `dte.DteConfig` | `ActividadEconomica` | `47524` (CIIU MH) | ✅ |
| 6 | Descripción de actividad económica | `appsettings.json` `Mh:DescActividad` | `DescActividad` | `Venta de artículos de ferretería` | ✅ |
| 7 | Departamento | BD `dte.DteConfig` | `Department` | `06` (San Salvador, código MH) | ✅ |
| 8 | Municipio | BD `dte.DteConfig` | `Municipality` | `14` (San Salvador, código MH) | ✅ |
| 9 | Dirección / complemento | BD `dte.DteConfig` | `AddressLine` | `Col. X, Calle Y #Z` | ✅ |
| 10 | Teléfono | BD `dte.DteConfig` | `Phone` | `2222-2222` | ⬜ |
| 11 | Correo del emisor | BD `dte.DteConfig` | `Email` | `facturacion@ferreteria.com.sv` | ✅ |
| 12 | Usuario del API de transmisión (credencial de aplicación MH) | `appsettings.json` `Mh:ApiUser` | `ApiUser` | NIT o usuario asignado por el MH | ✅ |
| 13 | Contraseña del API de transmisión | `appsettings.json` `Mh:ApiPassword` | `ApiPassword` | *(secreto)* | ✅ |
| 14 | Certificado `.p12` del emisor | Archivo en disco + ruta en `appsettings.json` `Mh:CertPath` | `CertPath` | `C:\Ferreteria\dte\cert\0614XXXXXXXXX.crt` | ✅ |
| 15 | Contraseña privada del certificado (`passwordPri`) | `appsettings.json` `Mh:CertPasswordPri` | `CertPasswordPri` | *(secreto)* | ✅ |
| 16 | Código de establecimiento | `appsettings.json` `Mh:CodEstable` | `CodEstable` | `M001` / según MH | ✅ |
| 17 | Código de punto de venta | `appsettings.json` `Mh:CodPuntoVenta` | `CodPuntoVenta` | `P001` / según MH | ✅ |
| 18 | Ambiente (00 pruebas / 01 producción) | BD `dte.DteConfig` + `appsettings.json` `Mh:Ambiente` | `Ambiente` | `00` o `01` | ✅ |

> **Departamento/Municipio:** el MH usa **códigos numéricos** (catálogo CAT-012/013), no el nombre.
> Confirmar los códigos con el contador. San Salvador suele ser departamento `06`, municipio `14`.

### 2.2 Plantilla de la sección `Mh` en `appsettings.json`

> **No se debe editar el archivo de código en esta entrega.** Esta es la **plantilla de referencia**
> que el técnico de despliegue debe agregar manualmente en
> `Ferreteria.PuntoVenta/Config/appsettings.json` cuando MH entregue los datos. Dejar los strings
> **vacíos** hasta tenerlos.

```jsonc
{
  "ConnectionStrings": {
    "FerreteriaDB": "Host=...;Database=ferreteria;Username=...;Password=...;Port=5432;SearchPath=public,purchasing,sales,dte,hr,system"
  },
  "Mh": {
    "Ambiente": "00",
    "FirmadorUrl": "http://localhost:8113/firmardocumento/",
    "AuthUrlPruebas": "https://apitest.dtes.mh.gob.sv/seguridad/auth",
    "RecepcionUrlPruebas": "https://apitest.dtes.mh.gob.sv/fesv/recepciondte",
    "AuthUrlProduccion": "https://api.dtes.mh.gob.sv/seguridad/auth",
    "RecepcionUrlProduccion": "https://api.dtes.mh.gob.sv/fesv/recepciondte",
    "ConsultaPublicaUrl": "https://admin.factura.gob.sv/consultaPublica",
    "ApiUser": "",
    "ApiPassword": "",
    "CertPath": "",
    "CertPasswordPri": "",
    "DescActividad": "",
    "CodEstable": "",
    "CodPuntoVenta": "",
    "RetryIntervalMinutes": 15,
    "MaxRetries": 10
  }
}
```

> **Seguridad:** `appsettings.json` con credenciales reales **no** debe subirse a Git ni compartirse
> por correo. Tratarlo como archivo de secretos en la PC de caja (ver §3.4). El script
> `scripts/publish-caja.ps1` **no sobrescribe** este archivo si ya tiene credenciales.

### 2.3 Estado inicial (placeholders)

| Campo | Valor inicial | Se llena cuando… |
|---|---|---|
| `EmisorNit`, `EmisorNrc`, `EmisorName`, … (`dte.DteConfig`) | vacío / `IsActive=false` | El contador entrega la ficha fiscal |
| `Mh:ApiUser`, `Mh:ApiPassword` | `""` | El MH habilita la credencial de aplicación (transmisión) |
| `Mh:CertPath`, `Mh:CertPasswordPri` | `""` | El MH emite el certificado `.p12` y su clave privada |
| `Mh:CodEstable`, `Mh:CodPuntoVenta` | `""` | El MH registra el establecimiento y punto de venta |
| `Ambiente` | `00` (pruebas) | Se cambia a `01` solo tras certificar (ver §4) |

---

## 3. Firmador del MH (svfe-api-firmador) en la PC de caja

El **Firmador** es un microservicio oficial del MH (aplicación Java `.jar`) que recibe el JSON del
DTE y lo devuelve **firmado digitalmente (JWS)** usando el certificado `.p12` del emisor. Corre en la
**misma PC de caja** escuchando en `http://localhost:8113`.

### 3.1 Requisitos

| Requisito | Detalle |
|---|---|
| Java Runtime | JRE/JDK 11+ (según versión del firmador vigente del MH) |
| Puerto libre | `8113` en `localhost` |
| Certificado `.p12` | Provisto por el MH, colocado en la carpeta de certificados del firmador |
| `.crt` derivado (si aplica) | Algunas versiones usan `.crt` en `svfe-api-firmador/uploads` |

### 3.2 Instalación (resumen)

1. Instalar **Java** (verificar con `java -version`).
2. Descargar el paquete **`svfe-api-firmador`** desde el portal del MH (Factura Electrónica →
   descargas / firmador).
3. Descomprimir en una carpeta fija, p. ej. `C:\Ferreteria\firmador\`.
4. Copiar el certificado `.p12`/`.crt` del emisor a la carpeta de certificados del firmador
   (típicamente `.../uploads/`).
5. Arrancar manualmente para probar:

```powershell
cd C:\Ferreteria\firmador
java -jar svfe-api-firmador-<version>.jar
```

6. Verificar que responde:

```powershell
# El servicio debe estar escuchando en 8113
curl.exe http://localhost:8113/actuator/health   # o la ruta de health de la versión
```

### 3.3 Contrato del endpoint de firma

`POST http://localhost:8113/firmardocumento/`

```jsonc
{
  "nit": "0614XXXXXXXXX",         // NIT del emisor sin guiones
  "activo": true,
  "passwordPri": "<CertPasswordPri>",
  "dteJson": { /* JSON del DTE construido por la app */ }
}
```

Respuesta esperada:

```jsonc
{ "status": "OK", "body": "<documento firmado JWS>" }
```

El campo `body` (JWS) es lo que luego se envía al MH en `POST /fesv/recepciondte` como `documento`.

### 3.4 Ejecutar el Firmador como servicio de Windows (recomendado)

Para que el firmador arranque solo con la PC (sin abrir una consola manual), registrarlo como
**servicio de Windows**. Dos opciones habituales:

**Opción A — NSSM (Non-Sucking Service Manager):**

```powershell
# Instalar el servicio (rutas de ejemplo; ajustar versión del jar)
nssm install FerreteriaFirmadorMH "C:\Program Files\Java\jre\bin\java.exe" "-jar C:\Ferreteria\firmador\svfe-api-firmador.jar"
nssm set FerreteriaFirmadorMH AppDirectory "C:\Ferreteria\firmador"
nssm set FerreteriaFirmadorMH Start SERVICE_AUTO_START
nssm set FerreteriaFirmadorMH DisplayName "Ferreteria - Firmador MH (DTE)"
nssm start FerreteriaFirmadorMH
```

**Opción B — Tarea programada al iniciar sesión** (más simple, menos robusta): crear una tarea en el
Programador de tareas que ejecute el `java -jar ...` "Al iniciar el equipo".

> **Regla de oro:** la caja **no arranca a facturar** si el firmador no responde en `:8113`. La app
> debe detectar esto y avisar (o entrar en contingencia).

---

## 4. Ambiente de pruebas vs producción y certificación

### 4.1 URLs por ambiente

| Ambiente | Código | Auth | Recepción |
|---|---|---|---|
| **Pruebas** | `00` | `https://apitest.dtes.mh.gob.sv/seguridad/auth` | `https://apitest.dtes.mh.gob.sv/fesv/recepciondte` |
| **Producción** | `01` | `https://api.dtes.mh.gob.sv/seguridad/auth` | `https://api.dtes.mh.gob.sv/fesv/recepciondte` |

> **Nota:** versiones anteriores de este repo (`README.md`) mencionaban `apifacturatest.mh.gob.sv` /
> `apifactura.mh.gob.sv`. Las URLs vigentes del sistema de transmisión son las de esta tabla
> (`*.dtes.mh.gob.sv`). Confirmar siempre con la documentación oficial del MH al certificar.

### 4.2 Cómo cambiar de `00` a `01`

El ambiente se define en **dos lugares** y **ambos** deben coincidir:

1. **BD:** `UPDATE dte."DteConfig" SET "Ambiente"='01' WHERE "IsActive"=true;`
2. **`appsettings.json`:** `"Mh": { "Ambiente": "01", ... }` en la PC de caja.

La app usa `Ambiente` para elegir automáticamente entre las URLs de pruebas y producción de la §4.1.

> **No** cambiar a `01` hasta completar la certificación. En producción, cada DTE emitido es **real y
> fiscalmente vinculante**.

### 4.3 Checklist de certificación con el MH

- [ ] Ficha fiscal del emisor completa en `dte.DteConfig` (§2).
- [ ] Credencial de aplicación (transmisión) habilitada por el MH (`ApiUser`/`ApiPassword`).
- [ ] Certificado `.p12` instalado en el firmador y `passwordPri` cargado.
- [ ] Firmador corriendo como servicio en `:8113`.
- [ ] Emisión de prueba de **DTE-01** (Consumidor Final) con `selloRecibido` en ambiente `00`.
- [ ] Emisión de prueba de **DTE-03** (CCF) con receptor con NIT/NRC en ambiente `00`.
- [ ] Emisión de prueba de **DTE-05** (Nota de Crédito) referenciando un DTE previo.
- [ ] Prueba de **contingencia**: apagar red/firmador, verificar cola y reintento (§6).
- [ ] Impresión de ticket con **QR de consulta pública** y validación del QR (§5).
- [ ] Validación de los DTE de prueba en el portal del MH.
- [ ] Aprobación formal del MH para pasar a producción.
- [ ] Cambio de `Ambiente` a `01` en BD **y** `appsettings.json`.
- [ ] Primera factura real supervisada + confirmación del sello.

---

## 5. Impresión térmica del ticket

### 5.1 Configuración de la impresora

| Aspecto | Detalle |
|---|---|
| Estándar | ESC/POS (comandos de impresora térmica) |
| Ancho de papel | **80mm** (48 columnas aprox.) o **58mm** (32 columnas) |
| Conexión USB | Instalar driver del fabricante; la app imprime por la impresora predeterminada de Windows o por nombre |
| Conexión de red | **TCP puerto 9100** (RAW/JetDirect); configurar IP fija en la impresora |
| Configuración en la app | Vista **Impresoras** (`ImpresorasView`): elegir impresora, ancho 80/58mm, IP+puerto, impresión de prueba |

**Red (9100):** asignar IP fija a la impresora (p. ej. `192.168.1.50`), confirmar conectividad:

```powershell
Test-NetConnection 192.168.1.50 -Port 9100
```

### 5.2 Impresión de prueba

Desde **Impresoras → Impresión de prueba** se emite un ticket de muestra (sin valor fiscal) para
validar alineación, ancho de columnas y corte de papel. Hacerlo tras cada cambio de papel (80↔58mm)
o de impresora.

### 5.3 Contenido del ticket 2026 (DTE)

El ticket **no** reemplaza al JSON firmado; es la representación impresa. Debe incluir:

```
        FERRETERIA (Nombre comercial)
     Ferreteria, S.A. de C.V. (Razón social)
        NIT: 0614-XXXXXX-XXX-X   NRC: XXXXXX-X
     Actividad: Venta de artículos de ferretería
        Col. X, Calle Y #Z, San Salvador
              Tel: 2222-2222
------------------------------------------------
DOCUMENTO TRIBUTARIO ELECTRÓNICO
Factura de Consumidor Final (DTE-01)
Cód. Generación: 550E8400-E29B-41D4-A716-...
Número de Control: DTE-01-M001P001-00000000000001
Sello Recepción: 2024AB...   (o "EN CONTINGENCIA")
Ambiente: PRODUCCIÓN (01)
Fecha/Hora emisión: 2026-07-16 10:35:02
Cajero: (PIN de sesión)
------------------------------------------------
CANT  DESCRIPCIÓN            P.UNIT    TOTAL
2 pza Boquilla Acelerador     1.50      3.00
5.50m Cable Galv. Acero #3    2.00     11.00
------------------------------------------------
              Suma gravada:            14.00
              IVA (13%):                1.82
              TOTAL A PAGAR:  $        15.82
   Son: QUINCE 82/100 DÓLARES
------------------------------------------------
Forma de pago: Efectivo
------------------------------------------------
      [ QR de consulta pública del DTE ]
   Verifique su documento en:
   admin.factura.gob.sv/consultaPublica
------------------------------------------------
        ¡Gracias por su compra!
```

Elementos obligatorios del ticket:

- **Encabezado del emisor:** nombre comercial, razón social, NIT, NRC, actividad económica,
  dirección y teléfono (desde `dte.DteConfig`).
- **Tipo de DTE** y su nombre (01/03/05).
- **Código de generación** (UUID) y **Número de control** (`DTE-TT-EstablecimientoPuntoVenta-correlativo`).
- **Sello de recepción** (`MhSello`) o leyenda **"EN CONTINGENCIA"** si aún no hay sello.
- **Ambiente** (pruebas/producción) — en pruebas dejar visible que **no tiene valor fiscal**.
- **Detalle de líneas** (cantidad, descripción, precio unitario, total).
- **Totales:** suma gravada, **IVA 13%**, total a pagar, y **total en letras**.
- **QR de consulta pública** que apunta a la URL de verificación del MH con el código de generación.
- Forma de pago y fecha/hora de emisión.

> El **QR** se genera con `QRCoder` (ya referenciado en el `.csproj`) apuntando a la URL de consulta
> pública del MH con los parámetros del DTE (ambiente, código de generación, fecha). Para
> contingencia, se reimprime el ticket con el sello una vez el MH lo procese.

---

## 6. Contingencia y reintentos

La contingencia cubre el caso en que **el MH no responde** (caída del servicio, mantenimiento, sin
internet) o **el firmador local falla**. La venta **no se bloquea**: se completa y el DTE queda en
cola para reenvío.

### 6.1 Qué pasa cuando algo falla

| Falla | Comportamiento |
|---|---|
| Firmador local no responde (`:8113`) | La app avisa; si persiste, guarda el DTE con `MhStatus=CONTINGENCIA` (sin firma) y crea registro en `dte.DteContingency` |
| Auth MH falla (credenciales/servicio) | Reintento; si persiste → contingencia |
| Recepción MH no responde / timeout | `MhStatus=CONTINGENCIA`, se guarda `JsonPayload`, se encola |
| MH responde **RECHAZADO** | `MhStatus=RECHAZADO` + `MhResponse` con el motivo — **no** es contingencia; requiere corrección (no se reintenta ciego) |

### 6.2 Cola de reintento

- Registro por DTE en `dte.DteContingency`: `AttemptCount`, `LastError`, `NextRetryAt`, `ResolvedAt`.
- Worker reintenta cada **~15 min** (`Mh:RetryIntervalMinutes`), con **backoff** creciente hasta
  `Mh:MaxRetries`.
- Al éxito: `MhStatus=PROCESADO`, se guarda `MhSello`, se fija `ResolvedAt` y se puede **reimprimir**
  el ticket ya con sello.
- Mientras dure la contingencia, el ticket impreso lleva la leyenda **"DTE EN CONTINGENCIA"**.

### 6.3 Resolución

1. Restablecer internet / firmador / servicio del MH.
2. El worker reenvía automáticamente en el siguiente ciclo (o forzar desde **Historial de Facturas**).
3. Verificar que `MhStatus` pasa a `PROCESADO` y que aparece `MhSello`.
4. Reimprimir el ticket definitivo si el cliente lo requiere.
5. Si un DTE queda `RECHAZADO`, revisar `MhResponse`, corregir y **reemitir** (no reusar el mismo
   código de generación).

---

## 7. Retención legal de DTE (10 años)

El MH exige conservar los DTE (**JSON firmado + sello de recepción**) por **10 años**.

| Qué se conserva | Dónde | Retención |
|---|---|---|
| JSON del DTE (payload) | `dte.DteIssued.JsonPayload` | 10 años |
| Sello de recepción del MH | `dte.DteIssued.MhSello` | 10 años |
| Respuesta cruda del MH | `dte.DteIssued.MhResponse` | 10 años |
| Representación impresa (opcional PDF) | `dte.DteIssued.PdfUrl` | 10 años |

**Recomendaciones:**

- **Backup diario** de PostgreSQL (`pg_dump`) con retención larga; **no** depender solo de la PC de caja.
- Guardar copia de los DTE firmados (JSON) en almacenamiento adicional (servidor/nube del cliente).
- No borrar filas de `dte.*`: los movimientos y documentos fiscales son **inmutables** (una anulación
  se hace con **Nota de Crédito DTE-05**, no borrando).

---

## 8. Solución de problemas

| Síntoma / error | Causa probable | Acción |
|---|---|---|
| `401 Unauthorized` en `/seguridad/auth` | `ApiUser`/`ApiPassword` incorrectos o credencial no habilitada | Verificar credenciales en `appsettings.json`; confirmar habilitación con el MH |
| Auth OK pero `/fesv/recepciondte` da `401` | Token expirado o no enviado en `Authorization: Bearer` | Reautenticar antes de transmitir; revisar cabecera |
| Firmador responde `status != OK` | `passwordPri` incorrecta o `.p12` no cargado en el firmador | Revisar `CertPasswordPri` y que el `.p12` esté en la carpeta del firmador |
| `Connection refused` a `localhost:8113` | Firmador apagado / servicio detenido / puerto ocupado | Iniciar servicio `FerreteriaFirmadorMH`; verificar puerto con `netstat -ano \| findstr 8113` |
| MH devuelve `estado: RECHAZADO` | JSON inválido, catálogos (dpto/municipio/actividad) errados, correlativo repetido | Leer `MhResponse`, corregir dato, reemitir con **nuevo** código de generación |
| Sello nunca llega, DTE queda en `CONTINGENCIA` | Sin internet o MH caído | Esperar reintento (15 min) o forzar reenvío; verificar conectividad |
| Error de certificado / `.p12` expirado | Certificado vencido | Renovar `.p12` con el MH y reinstalar en el firmador |
| Ambiente equivocado (pruebas emite como producción) | `Ambiente` distinto entre BD y `appsettings.json` | Igualar `Ambiente` en `dte.DteConfig` **y** `appsettings.json` |
| Ticket sale cortado / mal alineado | Ancho 80/58mm mal configurado | Ajustar ancho en **Impresoras**; hacer impresión de prueba |
| Impresora de red no imprime | IP/puerto 9100 incorrectos o impresora apagada | `Test-NetConnection IP -Port 9100`; revisar IP fija |
| QR no valida en el portal | Ambiente pruebas, o parámetros del QR mal formados | En pruebas el QR apunta a datos de prueba; validar formato de URL de consulta pública |
| `Municipio/Departamento inválido` | Se guardó el nombre en vez del **código** MH | Usar códigos numéricos del catálogo MH en `Department`/`Municipality` |

---

## 9. Checklist de puesta en marcha

1. [ ] Instalar la app publicada (`scripts/publish-caja.ps1`) en la PC de caja.
2. [ ] Instalar **Java** y el **Firmador MH** como servicio en `:8113` (§3).
3. [ ] Colocar el certificado `.p12` en el firmador y cargar `passwordPri`.
4. [ ] Completar la sección `Mh` de `appsettings.json` (§2.2) **sin subirla a Git**.
5. [ ] Cargar la ficha fiscal del emisor en `dte.DteConfig` (§2.1).
6. [ ] Configurar la impresora térmica (USB/red 9100, ancho) y hacer impresión de prueba (§5).
7. [ ] Emitir DTE 01/03/05 de prueba en ambiente `00` y validar sellos (§4.3).
8. [ ] Probar contingencia y reintentos (§6).
9. [ ] Certificar con el MH y **cambiar a `01`** en BD y `appsettings.json` (§4.2).
10. [ ] Configurar **backup diario** de PostgreSQL (retención 10 años de DTE) (§7).

---

> **Recordatorio final:** hasta que el MH y el contador entreguen NIT, NRC, credenciales de API,
> certificado `.p12` y códigos de establecimiento/punto de venta, el sistema queda **configurado pero
> inactivo** para facturación real. Mantener `Ambiente=00` y placeholders vacíos hasta entonces.
