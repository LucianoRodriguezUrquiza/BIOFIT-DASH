# BIOFIT DASH: recuperar cuatro PNG de interfaz desde proyectos locales previos.
# No descarga ni redistribuye assets; nunca modifica proyectos fuente.
# Idempotente: si el destino ya está completo, no altera sus archivos.
param([string]$SourceDir = "")
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$target = Join-Path $root "Assets\BIOFIT\Resources\BiofitUi"
$names = @("LevelSelect.png", "Skins.png", "Back.png", "Pause.png")

function Complete-Assets([string]$folder) {
    if (!(Test-Path -LiteralPath $folder -PathType Container)) { return $false }
    foreach ($name in $names) {
        $file = Join-Path $folder $name
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) { return $false }
        if ((Get-Item -LiteralPath $file).Length -le 0) { return $false }
    }
    return $true
}

if (Complete-Assets $target) {
    Write-Host "OK: cuatro PNG BIOFIT ya estan presentes. No se modifican."
    exit 0
}
if (Test-Path -LiteralPath $target) {
    $existing = @(Get-ChildItem -LiteralPath $target -File -Filter "*.png" -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        throw "BiofitUi esta incompleto, pero contiene PNG existentes. No se sobrescriben: $target"
    }
}

$parent = [IO.Path]::GetFullPath((Join-Path $root ".."))
$candidates = @()
if ($SourceDir -ne "") { $candidates += [IO.Path]::GetFullPath($SourceDir) }
else {
    foreach ($project in @("BIOFIT-DASH-JOYSTICK-TEST", "BIOFIT-DASH-NUEVOS-NIVELES", "BIOFIT-DASH-COOLDOWN-EXPERIMENTAL", "BIOFIT-TRES-MODOS", "BIOFIT-SPIKE-LAB-LEGACY", "BIOFIT-GeomeTry-UI-V1")) {
        $candidate = Join-Path (Join-Path $parent $project) "Assets\BIOFIT\Resources\BiofitUi"
        if ([IO.Path]::GetFullPath($candidate).TrimEnd('\') -ne $target.TrimEnd('\')) {
            $candidates += $candidate
        }
    }
}
$source = $null
foreach ($candidate in $candidates) {
    if (Complete-Assets $candidate) { $source = $candidate; break }
}
if ($null -eq $source) {
    if ($SourceDir -ne "") { throw "La ruta indicada no contiene los cuatro PNG: $SourceDir" }
    Write-Warning "No se encontraron LevelSelect.png, Skins.png, Back.png y Pause.png en otros proyectos. Para reproducir la interfaz, copialos a: $target"
    exit 0
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in $names) {
    $a = Join-Path $source $name
    $b = Join-Path $target $name
    Copy-Item -LiteralPath $a -Destination $b -ErrorAction Stop
    $ha = (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash
    $hb = (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash
    if ($ha -ne $hb) { throw "Fallo SHA256 al copiar $name" }
}
Write-Host "OK: cuatro PNG restaurados desde $source"
Write-Host "OK: fuente intacta, destino verificado con SHA256; Unity generara los .meta."
