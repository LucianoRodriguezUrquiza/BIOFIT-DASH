# BIOFIT DASH - Preparar biblioteca maestra de assets
# Ejecutar desde Windows PowerShell 5.1 o PowerShell 7 con Git instalado.
# No modifica ningun proyecto Unity ni realiza commit/push a GitHub.
# Fuentes de Geometry Dash: SOLO REFERENCIA, NO REDISTRIBUIR.
# Kenney: CC0 segun origen, verificar licencia del recurso antes de integrarlo.

[CmdletBinding()]
param(
    [ValidateSet('Completo','Referencias')]
    [string]$Modo = 'Completo',
    [string]$Destino = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'BIOFIT-RECURSOS-GD')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Carpeta([string]$Ruta) {
    if (-not (Test-Path -LiteralPath $Ruta)) {
        New-Item -ItemType Directory -Path $Ruta -Force | Out-Null
    }
}

function EjecutarGit([string[]]$Argumentos) {
    & git @Argumentos
    if ($LASTEXITCODE -ne 0) {
        throw ('Git devolvio error ' + $LASTEXITCODE + '. Argumentos: git ' + ($Argumentos -join ' '))
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'No se encontro Git. Instalalo desde https://git-scm.com/download/win y volve a ejecutar el script.'
}

$srcRoot = Join-Path $Destino '01_FUENTES'
$catalogo = Join-Path $Destino '03_CATALOGO'
$colecciones = Join-Path $Destino '02_COLECCIONES'
$paraUnity = Join-Path $Destino '04_LISTOS_PARA_UNITY'
foreach ($dir in @($Destino,$srcRoot,$catalogo,$colecciones,$paraUnity)) { Carpeta $dir }
foreach ($name in @('01_PERSONAJES','02_PORTALES','03_OBSTACULOS','04_FONDOS','05_ANIMACIONES','06_INTERFAZ','07_EFECTOS','08_AUDIO','09_OTROS')) {
    Carpeta (Join-Path $colecciones $name)
}

$sources = @(
    [PSCustomObject]@{
        Nombre = 'GDAssets_Kenzie_v1.0'
        URL = 'https://github.com/kenzieshane/Assets.git'
        Ref = '1.0'
        Estado = 'REFERENCIA_NO_REDISTRIBUIR'
    },
    [PSCustomObject]@{
        Nombre = 'GDTextures_Weebifying'
        URL = 'https://github.com/Weebifying/gd-textures.git'
        Ref = ''
        Estado = 'REFERENCIA_NO_REDISTRIBUIR'
    }
)
if ($Modo -eq 'Completo') {
    $sources += [PSCustomObject]@{
        Nombre = 'Kenney_CC0_comunidad'
        URL = 'https://github.com/shorepine/kenney.git'
        Ref = ''
        Estado = 'CC0_VERIFICAR_ORIGEN'
    }
}

$versiones = New-Object 'System.Collections.Generic.List[object]'
foreach ($src in $sources) {
    $ruta = Join-Path $srcRoot $src.Nombre
    Write-Host ''
    Write-Host ('==> Fuente: ' + $src.Nombre) -ForegroundColor Cyan
    if (Test-Path -LiteralPath $ruta) {
        if (-not (Test-Path -LiteralPath (Join-Path $ruta '.git'))) {
            throw ('La carpeta ya existe pero no contiene un clon Git valido: ' + $ruta + '. No se sobreescribio nada.')
        }
        Write-Host 'Ya existe: se conserva sin actualizar para no mezclar versiones.' -ForegroundColor Yellow
    } else {
        $argsGit = @('-c','core.longpaths=true','clone','--depth','1')
        if ($src.Ref -ne '') { $argsGit += @('--branch',$src.Ref) }
        $argsGit += @('--', $src.URL, $ruta)
        EjecutarGit $argsGit
    }
    $commit = (& git -C $ruta rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw ('No se pudo leer HEAD en ' + $ruta) }
    $versiones.Add([PSCustomObject]@{
        Fuente = $src.Nombre
        URL = $src.URL
        RefSolicitada = $(if ($src.Ref) { $src.Ref } else { 'default en el primer clon' })
        Commit = $commit
        Clasificacion = $src.Estado
        RutaLocal = $ruta
    })
    Write-Host ('Commit: ' + $commit.Substring(0,12))
}

$versiones | Export-Csv -LiteralPath (Join-Path $catalogo 'FUENTES_COMMITS.csv') -NoTypeInformation -Encoding UTF8

# Inventariar cada archivo (sin duplicar los objetos de Git internos).
# Este inventario puede abrirse en Excel y filtrar por categoria/extensiones.
$csvInventario = Join-Path $catalogo 'INVENTARIO_COMPLETO.csv'
if (Test-Path -LiteralPath $csvInventario) { Remove-Item -LiteralPath $csvInventario -Force }
$primerLote = $true
foreach ($src in $sources) {
    $ruta = Join-Path $srcRoot $src.Nombre
    Write-Host ('Indexando ' + $src.Nombre + '...') -ForegroundColor Cyan
    $registros = Get-ChildItem -LiteralPath $ruta -Recurse -File -ErrorAction Stop | Where-Object {
        $_.FullName -notmatch '[\\/]\.git[\\/]'
    } | ForEach-Object {
        $archivo = $_
        $nombre = $archivo.Name.ToLowerInvariant()
        $ext = $archivo.Extension.ToLowerInvariant()
        $categoria = 'Otros'
        if ($nombre -match 'portal') { $categoria = 'Portales' }
        elseif ($nombre -match 'spider|cube|ball|player_|icon') { $categoria = 'Personajes_o_iconos' }
        elseif ($nombre -match 'spike|obstacle|saw|blade|hazard') { $categoria = 'Obstaculos' }
        elseif ($nombre -match 'background|bg_|parallax') { $categoria = 'Fondos' }
        elseif ($nombre -match 'anim|dash|trail|particle') { $categoria = 'Animaciones_o_fx' }
        elseif ($nombre -match 'button|menu|panel|ui_|hud|pause|settings') { $categoria = 'Interfaz' }
        elseif ($ext -in @('.mp3','.ogg','.wav','.flac','.m4a')) { $categoria = 'Audio' }
        [PSCustomObject]@{
            Fuente = $src.Nombre
            RutaRelativa = $archivo.FullName.Substring($ruta.Length).TrimStart([char]'\',[char]'/')
            Extension = $ext
            CategoriaSugerida = $categoria
            Bytes = $archivo.Length
            EstadoLegal = $src.Estado
        }
    }
    if ($null -ne $registros) {
        if ($primerLote) {
            $registros | Export-Csv -LiteralPath $csvInventario -Encoding UTF8 -NoTypeInformation
            $primerLote = $false
        } else {
            $registros | Export-Csv -LiteralPath $csvInventario -Encoding UTF8 -NoTypeInformation -Append
        }
    }
}

$guia = @'
# BIOFIT DASH - Biblioteca maestra de recursos

Esta carpeta es EXTERNA al proyecto Unity y al repositorio Git principal.

## Carpetas
- 01_FUENTES: clonado de fuentes completas. No modificar el contenido original.
- 02_COLECCIONES: carpetas de trabajo para seleccionar recursos con fines de estudio.
- 03_CATALOGO: inventario CSV de todos los archivos y hashes de commit de Git.
- 04_LISTOS_PARA_UNITY: copiar AQUI solamente assets propios o con licencia valida.

## Derechos de uso
- GDAssets_Kenzie_v1.0: contienen assets originales de Geometry Dash, derechos de RobTop Games; solo referencia. Un LICENSE de codigo fuente no autoriza redistribuir los graficos de RobTop.
- GDTextures_Weebifying: recursos originales de Geometry Dash, derechos de RobTop Games; solo referencia.
- Kenney_CC0_comunidad: replica comunitaria de assets Kenney licenciados CC0. Verificar origen y el archivo de licencia de cada pack antes de incluirlo en APK/repo. La copia puede no contener todos los formatos del paquete oficial.

No subir las dos bibliotecas de Geometry Dash a GitHub ni copiarlas al proyecto Unity de distribucion. No incluir musica protegida.

## Integracion
1. Elegir un recurso en el inventario, abrir el archivo original y comprobar su licencia.
2. Copiar los archivos autorizados a 04_LISTOS_PARA_UNITY.
3. Copiar solo esos archivos al proyecto Unity bajo Assets/BIOFIT/Art/ThirdParty/.
4. Registrar fuente, licencia, autor, pack y modificaciones en docs/ASSET_SOURCES.md del repositorio de Unity.
5. Probar en Unity Editor y en APK antes de integrarlos a una rama estable.

## Reproduccion
Consultar FUENTES_COMMITS.csv: registra el commit exacto de cada fuente descargada.
La descarga solo sucede una vez: ejecutar nuevamente NO actualiza clones existentes ni pisa ningun archivo.
'@
Set-Content -LiteralPath (Join-Path $catalogo 'LEEME_BIBLIOTECA.md') -Value $guia -Encoding UTF8

Write-Host ''
Write-Host 'BIBLIOTECA PREPARADA.' -ForegroundColor Green
Write-Host ('Carpeta: ' + $Destino)
Write-Host ('Fuentes: ' + $sources.Count)
Write-Host ('Inventario: ' + $csvInventario)
Write-Host 'Unity y GitHub NO fueron modificados.' -ForegroundColor Green
