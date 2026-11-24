# BIOFIT Stereo/Ship experimental project setup.
# Runs pinned GeomeTry preparation and safely reuses user's existing 4 UI PNGs
# from a sibling project, without downloading or redistributing third-party art.
[CmdletBinding()]
param([string]$PreviousProject)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$prepare = Join-Path $PSScriptRoot 'prepare_geometry_original.ps1'
if (!(Test-Path -LiteralPath $prepare)) { throw "Missing prepare_geometry_original.ps1" }
& $prepare
if ($LASTEXITCODE -ne 0) { throw "GeomeTry preparation failed" }
$target = Join-Path $root 'Assets\BIOFIT\Resources\BiofitUi'
$names = @('LevelSelect.png', 'Skins.png', 'Back.png', 'Pause.png')
$missing = @($names | Where-Object { !(Test-Path -LiteralPath (Join-Path $target $_)) })
if ($missing.Count -eq 0) {
    Write-Host "All 4 UI images already installed. No copying needed." -ForegroundColor Green
    return
}
$parent = Split-Path -Parent $root
$candidates = @()
if ($PreviousProject) { $candidates += $PreviousProject }
$candidates += @(
    (Join-Path $parent 'BIOFIT-DASH-JOYSTICK-TEST'),
    (Join-Path $parent 'BIOFIT-DASH-NUEVOS-NIVELES'),
    (Join-Path $parent 'BIOFIT-DASH-COOLDOWN-EXPERIMENTAL')
)
$found = $null
foreach ($candidate in $candidates) {
    if (!(Test-Path -LiteralPath $candidate -PathType Container)) { continue }
    $source = Join-Path $candidate 'Assets\BIOFIT\Resources\BiofitUi'
    if ((@($missing | Where-Object {
        Test-Path -LiteralPath (Join-Path $source $_) -PathType Leaf
    })).Count -eq $missing.Count) { $found = $source; break }
}
if ($null -eq $found) {
    Write-Warning "Could not find all UI PNGs in sibling copies. The 4 assets remain optional and can be installed with install_biofit_ui.ps1 and the private ZIP."
    return
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach ($name in $missing) {
    Copy-Item -LiteralPath (Join-Path $found $name) -Destination (Join-Path $target $name) -ErrorAction Stop
    Write-Host "Reused local UI art: $name" -ForegroundColor Green
}
Write-Host "Stereo/Ship project prepared. Art copied only from user's own local project." -ForegroundColor Green
