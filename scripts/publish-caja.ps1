<#
.SYNOPSIS
    Publica la app de caja Ferreteria.PuntoVenta para desplegarla en la PC de la sucursal.

.DESCRIPTION
    Compila y publica el proyecto WPF Ferreteria.PuntoVenta (net10.0-windows) hacia
    ./publish/caja. Por defecto genera una publicacion "framework-dependent" (requiere
    el runtime .NET instalado en la PC de caja). Con -SelfContained genera una version
    autocontenida win-x64 (no requiere .NET instalado) y con -SingleFile la empaqueta en
    un unico ejecutable.

    El archivo Config/appsettings.json se copia junto al ejecutable por CopyToOutputDirectory,
    por lo que este script NO lo sobrescribe si el destino ya tiene credenciales cargadas.

.PARAMETER SelfContained
    Publica autocontenido (win-x64, --self-contained true). Util cuando la PC de caja no
    tiene el runtime .NET instalado.

.PARAMETER SingleFile
    Empaqueta la salida en un unico archivo ejecutable (-p:PublishSingleFile=true).
    Recomendado junto con -SelfContained.

.PARAMETER OutputDir
    Carpeta de salida. Por defecto: ./publish/caja (relativa a la raiz del repo).

.PARAMETER Configuration
    Configuracion de compilacion. Por defecto: Release.

.EXAMPLE
    ./scripts/publish-caja.ps1
    Publicacion framework-dependent en ./publish/caja.

.EXAMPLE
    ./scripts/publish-caja.ps1 -SelfContained -SingleFile
    Publicacion autocontenida win-x64 en un unico ejecutable.

.NOTES
    Repo: erp_ferreteria · Proyecto: Ferreteria.PuntoVenta (WPF)
    Complementa: docs/FERRETERIA_FACTURACION_DTE_IMPRESION_2026.md
#>
[CmdletBinding()]
param(
    [switch] $SelfContained,
    [switch] $SingleFile,
    [string] $OutputDir,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# --- Rutas base (el script vive en <repo>/scripts) ---------------------------------
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'Ferreteria.PuntoVenta'
$Project    = Join-Path $ProjectDir 'Ferreteria.PuntoVenta.csproj'

if (-not $OutputDir) {
    $OutputDir = Join-Path $RepoRoot 'publish/caja'
}

Write-Host ''
Write-Host '=== Ferreteria - Publicacion de la app de caja ===' -ForegroundColor Cyan
Write-Host ''

# --- 1. Verificar que exista dotnet ------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host 'ERROR: no se encontro el comando "dotnet" en el PATH.' -ForegroundColor Red
    Write-Host 'Instale el .NET SDK 10.0+ desde https://dotnet.microsoft.com/download' -ForegroundColor Yellow
    Write-Host 'Luego reinicie la terminal y vuelva a ejecutar este script.' -ForegroundColor Yellow
    exit 1
}
Write-Host ("dotnet detectado: {0}" -f (dotnet --version)) -ForegroundColor Green

# --- 2. Verificar que exista el proyecto -------------------------------------------
if (-not (Test-Path $Project)) {
    Write-Host ("ERROR: no se encontro el proyecto en: {0}" -f $Project) -ForegroundColor Red
    exit 1
}

# --- 3. Construir argumentos de dotnet publish -------------------------------------
$publishArgs = @(
    'publish', $Project,
    '-c', $Configuration,
    '-r', 'win-x64',
    '-o', $OutputDir
)

if ($SelfContained) {
    $publishArgs += @('--self-contained', 'true')
    Write-Host 'Modo: AUTOCONTENIDO (win-x64, no requiere .NET instalado en la PC de caja)' -ForegroundColor Yellow
} else {
    $publishArgs += @('--self-contained', 'false')
    Write-Host 'Modo: FRAMEWORK-DEPENDENT (requiere runtime .NET 10 en la PC de caja)' -ForegroundColor Yellow
}

if ($SingleFile) {
    $publishArgs += '-p:PublishSingleFile=true'
    Write-Host 'Empaquetado: UNICO EJECUTABLE (PublishSingleFile)' -ForegroundColor Yellow
}

# --- 4. Publicar -------------------------------------------------------------------
Write-Host ''
Write-Host ("Publicando en: {0}" -f $OutputDir) -ForegroundColor Cyan
Write-Host ("Comando: dotnet {0}" -f ($publishArgs -join ' ')) -ForegroundColor DarkGray
Write-Host ''

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host 'ERROR: la publicacion fallo. Revise la salida de dotnet arriba.' -ForegroundColor Red
    exit $LASTEXITCODE
}

# --- 5. Verificar appsettings.json en la salida (sin sobrescribir credenciales) ----
$publishedConfig = Join-Path $OutputDir 'Config/appsettings.json'
if (Test-Path $publishedConfig) {
    Write-Host ''
    Write-Host ("appsettings.json presente en la publicacion: {0}" -f $publishedConfig) -ForegroundColor Green
    Write-Host 'NOTA: este script NO sobrescribe credenciales. Edite la seccion [Mh] en la PC de caja.' -ForegroundColor Yellow
} else {
    Write-Host ''
    Write-Host 'ADVERTENCIA: no se encontro Config/appsettings.json en la salida.' -ForegroundColor Yellow
    Write-Host 'Verifique el CopyToOutputDirectory del .csproj o copie el archivo manualmente.' -ForegroundColor Yellow
}

# --- 6. Instrucciones de post-despliegue -------------------------------------------
Write-Host ''
Write-Host '=== Publicacion completada ===' -ForegroundColor Green
Write-Host ("Carpeta de salida: {0}" -f $OutputDir) -ForegroundColor Cyan
Write-Host ''
Write-Host 'PASOS DE POST-DESPLIEGUE (en la PC de la sucursal):' -ForegroundColor Cyan
Write-Host '  1. Copie la carpeta de salida a la PC de caja.'
Write-Host '  2. Instale el FIRMADOR del MH (svfe-api-firmador) como servicio en puerto 8113 (Java).'
Write-Host '  3. Complete la seccion [Mh] de Config/appsettings.json con los datos del MH'
Write-Host '     (NIT, ApiUser/ApiPassword, CertPath/CertPasswordPri, CodEstable/CodPuntoVenta).'
Write-Host '     Deje los valores vacios hasta que el Ministerio de Hacienda los entregue.'
Write-Host '  4. Cargue la ficha fiscal del emisor en la tabla dte.DteConfig de la BD.'
Write-Host '  5. Configure la impresora termica (USB o red 9100, ancho 80/58mm) e imprima prueba.'
Write-Host '  6. Mantenga Ambiente=00 (pruebas) hasta certificar; luego cambie a 01 (produccion).'
Write-Host ''
Write-Host 'Guia completa: docs/FERRETERIA_FACTURACION_DTE_IMPRESION_2026.md' -ForegroundColor DarkGray
Write-Host ''
