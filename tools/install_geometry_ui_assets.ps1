# BIOFIT UI art import -- run with Unity CLOSED.
# Operates ONLY inside the new, dedicated GeomeTry UI clone.
param([string]$ZipPath = "$HOME\Downloads\BIOFIT_GeomeTry_UI_Assets.zip")
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if (!(Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
  throw "ZIP no encontrado: $ZipPath. Descargar desde el chat o pasar -ZipPath."
}
$expectedZip = "dcf32ae0cd3f8fa1d11668247bbec5a594635816283889c9248c52c9ab872ed2"
$actualZip = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualZip -ne $expectedZip) { throw "ZIP diferente al pack verificado." }
$dest = Join-Path $root "Assets\BIOFIT\Resources\BiofitUi"
$expected = @{
 "Skins.png" = "3e9a3675f53e7b02303ced1a0b65aa1bb8ffcb0935fd3aae46597026b32ba7b5"
 "LevelSelect.png" = "7186b226fad1ca49247d31aa65394a1d882effbe772c64ff02ab89f6040a4c8c"
 "Back.png" = "021a1ef6e58147d0293a89d09bc2170cc001ade5284549de690a54a8dd42f24d"
 "Pause.png" = "8b34f47fbf61834f85991357fce814e6a5fdc04e3f8040dafc95cbbfac76a0d5"
}
$temp = Join-Path $env:TEMP ("BiofitUiAssets_" + [Guid]::NewGuid().ToString("N"))
try {
  Expand-Archive -LiteralPath $ZipPath -DestinationPath $temp -Force
  $source = Join-Path $temp "Assets\BIOFIT\Resources\BiofitUi"
  foreach ($entry in $expected.GetEnumerator()) {
    $file = Join-Path $source $entry.Key
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Falta imagen: $($entry.Key)" }
    $sha = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sha -ne $entry.Value) { throw "SHA256 incorrecto: $($entry.Key)" }
  }
  New-Item -ItemType Directory -Path $dest -Force | Out-Null
  foreach ($entry in $expected.GetEnumerator()) {
    $from = Join-Path $source $entry.Key
    $to = Join-Path $dest $entry.Key
    if (Test-Path -LiteralPath $to -PathType Leaf) {
      $got = (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash.ToLowerInvariant()
      if ($got -ne $entry.Value) { throw "Archivo existente diferente, no se sobrescribe: $to" }
      Write-Host "OK (ya instalado): $($entry.Key)"
    } else {
      Copy-Item -LiteralPath $from -Destination $to -ErrorAction Stop
      Write-Host "OK (importado): $($entry.Key)"
    }
  }
  Write-Host "OK: 4 imagenes listas en $dest"
  Write-Host "El submodulo GeomeTry y los niveles permanecen intactos."
} finally {
  if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
