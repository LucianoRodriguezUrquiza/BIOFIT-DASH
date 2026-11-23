# BIOFIT DASH - importar SOLO PNG individuales seleccionados en la biblioteca local.
# No modifica firmware, fisicas, BLE ni submodulos. Los archivos copiados
# van a una carpeta ignorada por Git. Derechos de terceros: revisar por archivo.
param(
    [string]$Biblioteca = "$env:USERPROFILE\Documents\BIOFIT-RECURSOS-GD",
    [switch]$Aplicar,
    [switch]$SoloPrototipoPrivado,
    [switch]$Reemplazar
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$sourceRoot = Join-Path $Biblioteca "01_FUENTES"
$selectionDir = Join-Path $Biblioteca "04_SELECCION"
$manifest = Join-Path $selectionDir "ARTE_NIVELES_BIOFIT.csv"
$destFolder = Join-Path $project "Assets\BIOFIT\Resources\BiofitThreeModes\Artwork"
$targets = @("IconBall", "IconSpider", "PortalCube", "PortalBall",
    "PortalSpider", "DecoPulse", "DecoCrystal", "DecoStar")

if (!(Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "No se encontro la biblioteca original: $sourceRoot"
}
New-Item -ItemType Directory -Path $selectionDir -Force | Out-Null
if (!(Test-Path -LiteralPath $manifest -PathType Leaf)) {
    $rows = foreach ($name in $targets) {
        [PSCustomObject]@{
            Destino = "$name.png"
            RutaRelativa = ""
            Licencia = ""
            Aprobado = "NO"
        }
    }
    $rows | Export-Csv -LiteralPath $manifest -NoTypeInformation -Encoding UTF8
    Write-Host "CREADA PLANTILLA: $manifest" -ForegroundColor Cyan
    Write-Host "Abri el catalogo HTML y copia la ruta relativa a 01_FUENTES de cada PNG."
    Write-Host "Licencia: CC0 para recursos con licencia comprobada, o REFERENCIA_PRIVADA."
    Write-Host "Aprobado: SI. Volve a ejecutar con -Aplicar despues de completar el CSV."
    exit 0
}
if (!$Aplicar) {
    Write-Host "SELECCION LOCAL: $manifest" -ForegroundColor Cyan
    Import-Csv -LiteralPath $manifest | Format-Table -AutoSize
    Write-Host "Primero revisa cada PNG en el catalogo visual."
    Write-Host "Luego: powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\assets\Importar_Arte_Niveles_Local.ps1 -Aplicar"
    exit 0
}

$rows = @(Import-Csv -LiteralPath $manifest)
$approved = @($rows | Where-Object { $_.Aprobado -eq "SI" -and
    ![string]::IsNullOrWhiteSpace($_.RutaRelativa) })
if ($approved.Count -eq 0) { throw "No hay archivos aprobados en el manifiesto." }
New-Item -ItemType Directory -Path $destFolder -Force | Out-Null
$sourcePrefix = [IO.Path]::GetFullPath($sourceRoot)
$sourcePrefix = $sourcePrefix.TrimEnd([char[]]@('\','/')) + [IO.Path]::DirectorySeparatorChar
foreach ($item in $approved) {
    if ($targets -notcontains [IO.Path]::GetFileNameWithoutExtension($item.Destino) -or
        [IO.Path]::GetExtension($item.Destino).ToLowerInvariant() -ne ".png") {
        throw "Nombre destino invalido: $($item.Destino)"
    }
    if ($item.Licencia -ne "CC0" -and
        !($SoloPrototipoPrivado -and $item.Licencia -eq "REFERENCIA_PRIVADA")) {
        throw "Licencia sin comprobar para $($item.Destino). Usar CC0 verificada, o -SoloPrototipoPrivado para REFERENCIA_PRIVADA."
    }
    $source = [IO.Path]::GetFullPath((Join-Path $sourceRoot $item.RutaRelativa))
    if (!$source.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta fuera de la biblioteca: $source"
    }
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Archivo inexistente: $source"
    }
    $stream = [IO.File]::OpenRead($source)
    try {
        $signature = New-Object byte[] 8
        if ($stream.Read($signature, 0, 8) -ne 8 -or
            [BitConverter]::ToString($signature) -ne "89-50-4E-47-0D-0A-1A-0A") {
            throw "No es un PNG autentico: $source"
        }
    }
    finally { $stream.Dispose() }
    $destination = Join-Path $destFolder $item.Destino
    if (Test-Path -LiteralPath $destination) {
        $old = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        $new = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ($old -eq $new) {
            Write-Host "YA EXISTE IGUAL: $($item.Destino)" -ForegroundColor Green
            continue
        }
        if (!$Reemplazar) { throw "Destino distinto; usar -Reemplazar SOLO tras respaldarlo: $destination" }
    }
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
        throw "Fallo verificacion de copia: $destination"
    }
    Write-Host "IMPORTADO LOCAL: $($item.Destino) desde $source" -ForegroundColor Green
}
Write-Host "Abrir Unity: los PNG individuales se importan automaticamente como Sprite por BiofitArtworkImporter."
Write-Warning "No redistribuir sprites de Geometry Dash / RobTop ni usarlos en APK publica sin licencia. Los archivos originales del catalogo no fueron modificados."
