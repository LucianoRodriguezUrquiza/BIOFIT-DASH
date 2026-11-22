# BIOFIT DASH / BILATERAL LEGACY - only the local imported WaveVibe map.
# Use AFTER prepare_geometry_original.ps1, with Unity CLOSED.
# Submodule, original level assets, other maps, firmware and physics remain unchanged.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$module = Join-Path $root "ThirdParty\GeomeTryOriginal"
$relative = "Assets\Resources\maps\BASE\WaveVibe.json"
$source = Join-Path $module $relative
$destination = Join-Path $root "Assets\GeomeTryOriginal\$relative"
$training = Join-Path $PSScriptRoot "geometry_training\WaveVibe.json"
$expectedCommit = "d922caddb4d21ecb791a266143e279bdb969d42c"

if (-not (Test-Path -LiteralPath $module -PathType Container)) {
    throw "Falta el submodulo original: $module"
}
$actualCommit = (& git -C $module rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $expectedCommit) {
    throw "Submodulo inesperado: $actualCommit. Esperado: $expectedCommit"
}
& git -C $module diff --quiet -- "Assets/Resources/maps/BASE/WaveVibe.json"
if ($LASTEXITCODE -ne 0) {
    throw "El WaveVibe del submodulo tiene modificaciones locales. No se toca."
}
foreach ($file in @($source, $destination, $training, "$source.meta", "$destination.meta")) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Falta archivo esperado: $file. Ejecuta primero prepare_geometry_original.ps1."
    }
}
function Same-Bytes([string]$a, [string]$b) {
    return (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash -eq
           (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash
}
if (-not (Same-Bytes "$source.meta" "$destination.meta")) {
    throw "La metadata/GUID de WaveVibe no coincide con el original. No se toca."
}

# Validate before writing. Keep the same id/name/difficulty/music and simple floor.
$level = Get-Content -LiteralPath $training -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedSpikes = @(25, 65, 105, 145, 185, 225, 265)
$actualSpikes = @($level.map | Where-Object { $_.type -eq "spike" })
$endMarkers = @($level.map | Where-Object { $_.type -eq "ground" })
$others = @($level.map | Where-Object { $_.type -ne "spike" -and $_.type -ne "ground" })
if ($level.id -ne 4 -or $level.name -ne "Wave Vibe" -or
    $level.difficulty -ne 1 -or $level.music -ne "CantLetGo" -or
    $actualSpikes.Count -ne $expectedSpikes.Count -or
    $endMarkers.Count -ne 1 -or $others.Count -ne 0 -or
    $endMarkers[0].x -ne 285 -or $endMarkers[0].y -ne -2) {
    throw "Mapa de entrenamiento inesperado. No se modifica ningun archivo."
}
for ($i = 0; $i -lt $expectedSpikes.Count; $i++) {
    if ($actualSpikes[$i].x -ne $expectedSpikes[$i] -or $actualSpikes[$i].y -ne 0) {
        throw "Posicion de pincho inesperada. No se modifica el mapa."
    }
}

if (Same-Bytes $destination $training) {
    Write-Host "OK: WaveVibe de entrenamiento ya instalado, sin cambios."
    exit 0
}
if (-not (Same-Bytes $destination $source)) {
    throw "WaveVibe importado tiene modificaciones locales desconocidas: $destination. No se sobrescribe."
}

# Only replace the copied JSON: never edit the pinned submodule or any .meta file.
Copy-Item -LiteralPath $training -Destination $destination -Force -ErrorAction Stop
if (-not (Same-Bytes $destination $training)) {
    throw "Fallo la verificacion SHA256 del mapa instalado."
}
Write-Host "OK: WaveVibe de entrenamiento, 7 pinchos x=25..265 separados 40 unidades."
Write-Host "OK: suelo plano del juego; meta/GUID, musica y fisica originales."
Write-Host "OK: longitud ~30 s nominales (6.5 unidades/s x timeScale 1.5)."
Write-Host "OK: submodulo y restantes cuatro mapas intactos."
