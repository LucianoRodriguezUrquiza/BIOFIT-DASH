# BIOFIT UI art import -- run with Unity CLOSED.
# Operates ONLY inside the new, dedicated GeomeTry UI clone.
param([string]$ZipPath = "$HOME\Downloads\BIOFIT_GeomeTry_UI_Assets_HD_v2.zip")
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if (!(Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
  throw "ZIP no encontrado: $ZipPath. Descargar desde el chat o pasar -ZipPath."
}
$expectedZip = "3280ec87656f5d5d4a32bd85a8c35bdb41dbc38da643396b5a1e020c43467358"
$actualZip = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualZip -ne $expectedZip) { throw "ZIP diferente al pack verificado." }
$dest = Join-Path $root "Assets\BIOFIT\Resources\BiofitUi"
$expected = @{
 "Skins.png" = "3e9a3675f53e7b02303ced1a0b65aa1bb8ffcb0935fd3aae46597026b32ba7b5"
 "LevelSelect.png" = "f7e4295c30329990bc9511aef0b08adbd72b0b981efebca787e2b338641ac3b7"
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
      if ($got -ne $entry.Value) {
        # The only allowed replacement is the previous verified 527x234
        # low-resolution selector. User-created files are never overwritten.
        $oldSelectorHash = "7186b226fad1ca49247d31aa65394a1d882effbe772c64ff02ab89f6040a4c8c"
        if ($entry.Key -eq "LevelSelect.png" -and $got -eq $oldSelectorHash) {
          Copy-Item -LiteralPath $from -Destination $to -Force -ErrorAction Stop
          $after = (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash.ToLowerInvariant()
          if ($after -ne $entry.Value) { throw "Fallo al reemplazar selector HD: $to" }
          Write-Host "OK (selector 527px actualizado a 1672px): LevelSelect.png"
        } else {
          throw "Archivo local diferente; NO se sobrescribe: $to"
        }
      } else {
        Write-Host "OK (ya instalado): $($entry.Key)"
      }
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
