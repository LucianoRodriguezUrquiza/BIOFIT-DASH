# Instala las cuatro imagenes entregadas por el usuario sin copiar PNG manualmente.
# Requiere el paquete BIOFIT_UI_ASSETS.zip descargado localmente.
# NO publica los recursos en GitHub (posibles licencias de terceros).
[CmdletBinding()]
param(
    [string]$ArchivePath,
    [string]$ProjectRoot
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}
if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'Assets\BIOFIT\Resources') -PathType Container)) {
    throw "No se reconoce un proyecto BIOFIT DASH en: $ProjectRoot"
}
if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $homeDir = if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }
    $candidates = @(
        (Join-Path $homeDir 'Downloads\BIOFIT_UI_ASSETS.zip'),
        (Join-Path $homeDir 'Descargas\BIOFIT_UI_ASSETS.zip'),
        (Join-Path $ProjectRoot 'BIOFIT_UI_ASSETS.zip'),
        (Join-Path (Get-Location).Path 'BIOFIT_UI_ASSETS.zip')
    )
    $ArchivePath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($ArchivePath) -or -not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
    throw "No encontre BIOFIT_UI_ASSETS.zip en Descargas ni en el proyecto. Descarga el ZIP de la conversacion a Descargas, o pasa -ArchivePath con la ruta exacta."
}

$expected = [ordered]@{
    'Back.png'        = '37e64b538a7b9f98c2ddf26f6c8474ed16c9ad8116e9cf2eb3d841b1ceae1037'
    'LevelSelect.png' = 'f7e4295c30329990bc9511aef0b08adbd72b0b981efebca787e2b338641ac3b7'
    'Pause.png'       = '8b34f47fbf61834f85991357fce814e6a5fdc04e3f8040dafc95cbbfac76a0d5'
    'Skins.png'       = '3e9a3675f53e7b02303ced1a0b65aa1bb8ffcb0935fd3aae46597026b32ba7b5'
}
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('BIOFIT_UI_' + [guid]::NewGuid().ToString('N'))
$destination = Join-Path $ProjectRoot 'Assets\BIOFIT\Resources\BiofitUi'
$staging = [ordered]@{}
$changes = New-Object 'System.Collections.Generic.List[string]'
try {
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $temp -Force
    foreach ($entry in $expected.GetEnumerator()) {
        $name = [string]$entry.Key
        $source = Join-Path (Join-Path $temp 'BiofitUi') $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            $source = Join-Path $temp $name
        }
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Falta el archivo requerido en el ZIP: $name"
        }
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -ne $entry.Value) {
            throw "El archivo $name no coincide con el paquete original entregado. No se instalara."
        }
        $staging[$name] = $source
    }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupRoot = Join-Path $ProjectRoot ('BiofitUi_Asset_Backup_' + $timestamp)
    foreach ($entry in $staging.GetEnumerator()) {
        $target = Join-Path $destination $entry.Key
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            $oldHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($oldHash -eq $expected[$entry.Key]) {
                Write-Host "YA INSTALADO: $($entry.Key)" -ForegroundColor DarkGreen
                continue
            }
            New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
            Copy-Item -LiteralPath $target -Destination (Join-Path $backupRoot $entry.Key) -Force
            Write-Host "RESPALDO: $($entry.Key) => $backupRoot" -ForegroundColor Yellow
        }
        Copy-Item -LiteralPath $entry.Value -Destination $target -Force
        $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected[$entry.Key]) {
            throw "Error al verificar la instalacion del archivo $($entry.Key)"
        }
        $changes.Add([string]$entry.Key)
        Write-Host "INSTALADO: $($entry.Key)" -ForegroundColor Green
    }
    Write-Host ""
    Write-Host "BIOFIT UI LISTO. Archivos nuevos o actualizados: $($changes.Count) / 4" -ForegroundColor Green
    Write-Host "Destino: $destination"
    Write-Host "Unity importara los PNG al abrir el proyecto o regresar a la ventana del Editor."
}
finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}