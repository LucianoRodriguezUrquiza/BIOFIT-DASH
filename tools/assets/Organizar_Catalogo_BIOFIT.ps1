# BIOFIT DASH - Clasificacion y catalogo visual LOCAL de la biblioteca ya descargada.
# Compatible con Windows PowerShell 5.1; no necesita instalar programas.
# No copia archivos de terceros a Unity, no actualiza los clones ni modifica GitHub.
[CmdletBinding()]
param(
    [string]$Biblioteca = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'BIOFIT-RECURSOS-GD'),
    [ValidateRange(24,500)][int]$PorPagina = 150
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$inventario = Join-Path $Biblioteca '03_CATALOGO\INVENTARIO_COMPLETO.csv'
$fuentes = Join-Path $Biblioteca '01_FUENTES'
$colecciones = Join-Path $Biblioteca '02_COLECCIONES'
$catalogo = Join-Path $Biblioteca '03_CATALOGO'
$visual = Join-Path $catalogo 'VISUAL'
if (-not (Test-Path -LiteralPath $inventario -PathType Leaf)) { throw "No existe el inventario: $inventario" }
if (-not (Test-Path -LiteralPath $fuentes -PathType Container)) { throw "No existe la carpeta de fuentes: $fuentes" }
foreach ($ruta in @($colecciones,$catalogo,$visual)) { if (-not (Test-Path -LiteralPath $ruta)) { New-Item -ItemType Directory -Path $ruta -Force | Out-Null } }
$categorias = @('Cube','Ball','Spider','Portales','Obstaculos','Fondos','Decoraciones','Interfaces','Efectos','Animaciones','Audio','Atlas_y_datos','Modelos_3D','Otros')
$imagenes = @('.png','.jpg','.jpeg','.webp','.gif','.bmp','.svg','.avif')
$audio = @('.mp3','.ogg','.wav','.flac','.m4a','.aac','.mid','.midi')
$modelos = @('.fbx','.obj','.gltf','.glb','.blend','.stl','.dae','.3ds')
$datos = @('.plist','.json','.xml','.yaml','.yml','.atlas','.tpsheet','.txt','.md','.cs','.meta','.py','.shader','.cginc','.hlsl','.psd','.aseprite','.tsx','.tmx')
$enc = [System.Text.UTF8Encoding]::new($false)
$script:conteos = @{}; $script:previstas = @{}; $script:paginas = @{}; $script:enPagina = @{}; $script:htmlWriters = @{}
$csvWriters = @{}; $fuenteConteos = @{}
foreach ($c in $categorias) {
    $script:conteos[$c] = 0; $script:previstas[$c] = 0
    $script:paginas[$c] = 0; $script:enPagina[$c] = 0
    $dir = Join-Path $visual $c
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    # Solo se borran paginas HTML de una ejecucion previa: nunca assets ni CSV de origen.
    Get-ChildItem -LiteralPath $dir -Filter 'pagina-*.html' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    $csvWriters[$c] = [System.IO.StreamWriter]::new((Join-Path $colecciones ($c + '.csv')), $false, $enc)
    $csvWriters[$c].WriteLine('"Fuente","RutaRelativa","CategoriaBiofit","Extension","Bytes","EstadoLegal"')
}
function CsvLine([string[]]$datosFila) {
    $salida = New-Object 'System.Collections.Generic.List[string]'
    foreach ($v in $datosFila) { $salida.Add('"' + ([string]$v).Replace('"','""') + '"') }
    return [string]::Join(',', $salida.ToArray())
}
function Categoria([string]$ruta,[string]$ext,[string]$sugerida) {
    $r = $ruta.ToLowerInvariant().Replace('\','/')
    $n = [System.IO.Path]::GetFileNameWithoutExtension($r)
    if ($ext -in $script:audio) { return 'Audio' }
    if ($n -match 'portal|gateway|teleport.gate' -or $r -match '/portals?/') { return 'Portales' }
    if ($n -match '(^|[^a-z])spider([^a-z]|$)|spider_[0-9]|player_spider' -or $r -match '/spiders?/') { return 'Spider' }
    if ($n -match '(^|[^a-z])ball([^a-z]|$)|ball_[0-9]|player_ball' -or $r -match '/balls?/' ) { return 'Ball' }
    if ($n -match '(^|[^a-z])cube([^a-z]|$)|cube_[0-9]|player_cube' -or $r -match '/cubes?/' ) { return 'Cube' }
    if ($n -match 'spike|saw|blade|obstacle|hazard|trap|thorn|danger' -or $r -match '/(spikes|obstacles|hazards|traps)/') { return 'Obstaculos' }
    if ($n -match 'background|parallax|backdrop|(^|[_-])bg([_-]|$)|skybox' -or $r -match '/(background|backgrounds|parallax)/') { return 'Fondos' }
    if ($n -match 'decor|scenery|ornament|foliage|terrain|pillar' -or $r -match '/(decorations|decoration|scenery)/') { return 'Decoraciones' }
    if ($n -match 'button|menu|panel|(^|[_-])ui([_-]|$)|hud|pause|settings|progress|checkbox|cursor|toolbar|window' -or $r -match '/(gui|ui|interface|interfaces|menus)/') { return 'Interfaces' }
    if ($n -match 'animation|anim_|_anim|frames?|sprite.?sheet|sequence|walkcycle' -or $r -match '/(animations|animation)/') { return 'Animaciones' }
    if ($n -match 'particle|glow|trail|flash|explosion|smoke|spark|burst|effect|_fx|fx_' -or $r -match '/(effects|particles|vfx)/') { return 'Efectos' }
    if ($ext -in $script:modelos) { return 'Modelos_3D' }
    if ($ext -in $script:datos -or $n -match 'atlas|sprite.?sheet|texture.?pack') { return 'Atlas_y_datos' }
    if ($sugerida -eq 'Personajes_o_iconos') { return 'Interfaces' }
    if ($sugerida -eq 'Animaciones_o_fx') { return 'Efectos' }
    return 'Otros'
}
function Html([string]$valor) { return [System.Net.WebUtility]::HtmlEncode($valor) }
function UrlSegmentada([string]$valor) {
    $partes = $valor.Replace('\','/').Split('/')
    $a = New-Object 'System.Collections.Generic.List[string]'
    foreach ($p in $partes) { $a.Add([System.Uri]::EscapeDataString($p)) }
    return [string]::Join('/', $a.ToArray())
}
function HtmlInicio([string]$cat,[int]$numero) {
    $dir = Join-Path $script:visual $cat
    $ruta = Join-Path $dir ('pagina-{0:D4}.html' -f $numero)
    $w = [System.IO.StreamWriter]::new($ruta, $false, $script:enc)
    $script:htmlWriters[$cat] = $w
    $w.WriteLine('<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>BIOFIT - '+(Html $cat)+'</title>')
    $w.WriteLine('<style>body{margin:0;background:#0d1424;color:#e9f1fd;font:15px Segoe UI,Arial,sans-serif}header{padding:20px;background:#12223c;position:sticky;top:0;z-index:2;border-bottom:1px solid #344f73}h1{margin:0 0 10px;font-size:23px}a{color:#9fc7ff}input{box-sizing:border-box;padding:10px;background:#0c172b;color:white;border:1px solid #4d6384;border-radius:7px;max-width:350px;width:100%}.grid{padding:18px;display:grid;grid-template-columns:repeat(auto-fill,minmax(175px,1fr));gap:13px}.item{min-width:0;background:#192943;border:1px solid #2a4464;border-radius:10px;overflow:hidden}.preview{background:#0c1727;height:160px;display:flex;align-items:center;justify-content:center}.preview img{max-width:100%;max-height:152px;object-fit:contain;image-rendering:auto}.info{padding:10px;overflow-wrap:anywhere;font-size:12px}.name{font-weight:700;margin-bottom:4px}.muted{color:#b2c6e0}footer{padding:24px}small{font-size:11px}</style></head><body>')
    $w.WriteLine('<header><h1>BIOFIT DASH - '+(Html $cat)+' / pagina '+$numero+'</h1><p><a href="../index.html">Indice general</a>' + $(if ($numero -gt 1) { ' &nbsp;|&nbsp; <a href="pagina-{0:D4}.html">Anterior</a>' -f ($numero-1) } else { '' }) + '</p><input id="busca" placeholder="Filtrar esta pagina por nombre..." oninput="let q=this.value.toLowerCase();document.querySelectorAll(&quot;.item&quot;).forEach(x=>x.hidden=!x.dataset.f.includes(q))"></header><main class="grid">')
}
function HtmlFin([string]$cat,[bool]$tieneSiguiente) {
    $w = $script:htmlWriters[$cat]
    if ($null -eq $w) { return }
    $w.WriteLine('</main><footer><a href="../index.html">Volver a categorias</a>')
    if ($tieneSiguiente) { $w.WriteLine(' &nbsp;|&nbsp; <a href="pagina-{0:D4}.html">Pagina siguiente</a>' -f ($script:paginas[$cat]+1)) }
    $w.WriteLine('<p class="muted">Catalogo local. Los recursos GD son referencia, no autorizados para redistribuir. Solo hay vistas previas, no se copian imagenes.</p></footer></body></html>')
    $w.Dispose()
    $script:htmlWriters.Remove($cat)
}
function JsonStr([string]$s) {
    $t = $s.Replace('\','\\').Replace('"','\"').Replace("`r",'\r').Replace("`n",'\n').Replace('<','\u003c')
    return '"' + $t + '"'
}
$searchPath = Join-Path $visual 'BUSCADOR.html'
$search = [System.IO.StreamWriter]::new($searchPath,$false,$enc)
$searchPrefix = @'
<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>BIOFIT - Buscador general</title>
<style>body{margin:0;background:#0c1424;color:#eaf2ff;font:15px Segoe UI,Arial,sans-serif}header{position:sticky;top:0;z-index:3;background:#15253e;padding:18px;border-bottom:1px solid #355475}h1{font-size:21px;margin:0 0 12px}input,select{background:#07152a;color:#fff;border:1px solid #567391;padding:10px;border-radius:6px;max-width:95%}input{min-width:240px;width:35%}.grid{padding:16px;display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px}.item{background:#182c46;border:1px solid #2c4a6d;border-radius:9px;overflow:hidden}.preview{height:158px;display:flex;align-items:center;justify-content:center;background:#0b192d}.preview img{max-width:100%;max-height:150px;object-fit:contain}.info{padding:10px;font-size:12px;overflow-wrap:anywhere}.nav{padding:15px}button{padding:10px;margin:4px;background:#315681;color:white;border:0;border-radius:5px;cursor:pointer}a{color:#9fc9ff}.muted{color:#bed2e9}</style></head><body>
<header><h1>BIOFIT DASH - Buscar en TODAS las imagenes</h1><div><input id="q" placeholder="Buscar: spider, portal, spike, background..." oninput="filtrar()"><select id="cat" onchange="filtrar()"><option value="">Todas las categorias</option></select><select id="src" onchange="filtrar()"><option value="">Todas las fuentes</option></select></div><p><a href="index.html">Ver categorias</a> | <span id="info">Cargando indice...</span></p></header><section id="tarjetas" class="grid"></section><div class="nav"><button onclick="pagina(-1)">Anterior</button><span id="pagina"></span><button onclick="pagina(1)">Siguiente</button></div>
<script>const DATA=[
'@
$search.WriteLine($searchPrefix)
$clasificadoPath = Join-Path $catalogo 'INVENTARIO_CLASIFICADO.csv'
$main = [System.IO.StreamWriter]::new($clasificadoPath, $false, $enc)
$main.WriteLine('"Fuente","RutaRelativa","CategoriaBiofit","Extension","Bytes","EstadoLegal"')
$total = 0
try {
    Write-Host 'Clasificando recursos; no se modificara Unity ni GitHub...' -ForegroundColor Cyan
    Import-Csv -LiteralPath $inventario -Encoding UTF8 | ForEach-Object {
        $fila = $_
        $f = [string]$fila.Fuente
        $r = [string]$fila.RutaRelativa
        $ext = ([string]$fila.Extension).ToLowerInvariant()
        if ([string]::IsNullOrWhiteSpace($f) -or [string]::IsNullOrWhiteSpace($r)) { return }
        if ($r -match '(^|[\\/])\.\.([\\/]|$)' -or $f -match '[\\/]') { throw "Ruta insegura en el CSV: $f / $r" }
        $cat = Categoria $r $ext ([string]$fila.CategoriaSugerida)
        $linea = CsvLine @($f,$r,$cat,$ext,[string]$fila.Bytes,[string]$fila.EstadoLegal)
        $main.WriteLine($linea)
        $csvWriters[$cat].WriteLine($linea)
        $script:conteos[$cat] += 1
        if (-not $fuenteConteos.ContainsKey($f)) { $fuenteConteos[$f] = 0 }
        $fuenteConteos[$f] += 1
        $total += 1
        if ($ext -in $imagenes) {
            if ($script:paginas[$cat] -eq 0) {
                $script:paginas[$cat] = 1
                HtmlInicio $cat $script:paginas[$cat]
                $script:enPagina[$cat] = 0
            } elseif ($script:enPagina[$cat] -ge $PorPagina) {
                HtmlFin $cat $true
                $script:paginas[$cat] += 1
                HtmlInicio $cat $script:paginas[$cat]
                $script:enPagina[$cat] = 0
            }
            $href = '../../../01_FUENTES/' + (UrlSegmentada $f) + '/' + (UrlSegmentada $r)
            $archivo = [IO.Path]::GetFileName($r.Replace('\','/'))
            $w = $script:htmlWriters[$cat]
            $w.WriteLine('<article class="item" data-f="'+(Html ($archivo+' '+$r).ToLowerInvariant())+'"><a class="preview" href="'+(Html $href)+'" target="_blank" rel="noopener"><img loading="lazy" src="'+(Html $href)+'" alt="'+(Html $archivo)+'"></a><div class="info"><div class="name">'+(Html $archivo)+'</div><div class="muted">'+(Html $f)+'</div><small>'+(Html $r)+'</small></div></article>')
            $searchHref = '../../01_FUENTES/' + (UrlSegmentada $f) + '/' + (UrlSegmentada $r)
            $search.WriteLine('[' + (JsonStr $cat) + ',' + (JsonStr $f) + ',' + (JsonStr $r) + ',' + (JsonStr $searchHref) + '],')
            $script:previstas[$cat] += 1
            $script:enPagina[$cat] += 1
        }
        if (($total % 20000) -eq 0) { Write-Host ("Clasificados: $total") }
    }
} finally {
    $main.Dispose()
    foreach ($c in $categorias) { $csvWriters[$c].Dispose() }
    foreach ($c in $categorias) { if ($script:htmlWriters.ContainsKey($c)) { HtmlFin $c $false } }
    # Se cierra el buscador mas abajo, despues de escribir el final de DATA.
}
$searchSuffix = @'
];
let filtrados=[], p=0; const limite=100;
function filtrar(){const q=document.getElementById('q').value.toLocaleLowerCase();const c=document.getElementById('cat').value;const s=document.getElementById('src').value;filtrados=[];for(let i=0;i<DATA.length;i++){const d=DATA[i];if(c&&d[0]!==c)continue;if(s&&d[1]!==s)continue;if(q&&!((d[2]+' '+d[0]+' '+d[1]).toLocaleLowerCase().includes(q)))continue;filtrados.push(i)}p=0;dibujar()}
function pagina(x){const max=Math.max(0,Math.ceil(filtrados.length/limite)-1);p=Math.min(max,Math.max(0,p+x));dibujar();window.scrollTo(0,0)}
function dibujar(){const grid=document.getElementById('tarjetas');grid.replaceChildren();const start=p*limite;for(let z=start;z<Math.min(start+limite,filtrados.length);z++){const d=DATA[filtrados[z]];const article=document.createElement('article');article.className='item';const a=document.createElement('a');a.className='preview';a.href=d[3];a.target='_blank';a.rel='noopener';const img=document.createElement('img');img.src=d[3];img.loading='lazy';img.alt=d[2];a.appendChild(img);const inf=document.createElement('div');inf.className='info';const nm=document.createElement('b');nm.textContent=d[2].split(/[\\/]/).pop();const meta=document.createElement('p');meta.className='muted';meta.textContent=d[0]+' | '+d[1];const path=document.createElement('small');path.textContent=d[2];inf.append(nm,meta,path);article.append(a,inf);grid.appendChild(article)}document.getElementById('info').textContent=filtrados.length.toLocaleString('es')+' imagenes encontradas';document.getElementById('pagina').textContent='Pagina '+(p+1)+' / '+Math.max(1,Math.ceil(filtrados.length/limite))}
for(const cat of [...new Set(DATA.map(x=>x[0]))].sort()){let o=new Option(cat,cat);document.getElementById('cat').add(o)}
for(const src of [...new Set(DATA.map(x=>x[1]))].sort()){let o=new Option(src,src);document.getElementById('src').add(o)}
filtrar();
</script></body></html>
'@
$search.WriteLine($searchSuffix)
$search.Dispose()
$resumen = foreach ($c in $categorias) {
    [PSCustomObject]@{ Categoria = $c; Archivos = $script:conteos[$c]; ImagenesPrevisualizables = $script:previstas[$c]; PaginasHTML = $script:paginas[$c] }
}
$resumen | Export-Csv -LiteralPath (Join-Path $catalogo 'RESUMEN_CATEGORIAS.csv') -NoTypeInformation -Encoding UTF8
$indice = Join-Path $visual 'index.html'
$wi = [System.IO.StreamWriter]::new($indice,$false,$enc)
try {
    $wi.WriteLine('<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>BIOFIT - Biblioteca grafica</title><style>body{background:#0d1424;color:#e8f1fe;font:16px Segoe UI,Arial,sans-serif;max-width:1100px;margin:30px auto;padding:0 20px}h1{color:#f2f8ff}p{color:#b4c8e3}table{width:100%;border-collapse:collapse;background:#172944}th,td{text-align:left;padding:11px;border-bottom:1px solid #344e6e}a{color:#9fcbff}small{color:#c1d4ee}</style></head><body><h1>BIOFIT DASH | Biblioteca visual local</h1>')
    $wi.WriteLine('<p>Archivos clasificados: '+$total+'. La clasificacion es automatica y aproximada. No modifica los archivos originales ni el proyecto Unity.</p>')
    $wi.WriteLine('<p><a href="BUSCADOR.html">BUSCAR POR NOMBRE EN TODAS LAS FUENTES</a></p>')
    $wi.WriteLine('<table><thead><tr><th>Categoria</th><th>Archivos</th><th>Imagenes</th><th>Paginas</th></tr></thead><tbody>')
    foreach ($c in $categorias) {
        $link = $(if ($script:paginas[$c] -gt 0) { '<a href="'+$c+'/pagina-0001.html">'+$c+'</a>' } else { Html $c })
        $wi.WriteLine('<tr><td>'+$link+'</td><td>'+$script:conteos[$c]+'</td><td>'+$script:previstas[$c]+'</td><td>'+$script:paginas[$c]+'</td></tr>')
    }
    $wi.WriteLine('</tbody></table><h2>Origen</h2><ul>')
    foreach ($f in ($fuenteConteos.Keys | Sort-Object)) { $wi.WriteLine('<li>'+(Html $f)+': '+$fuenteConteos[$f]+' archivos</li>') }
    $wi.WriteLine('</ul><p><small>Los CSV por categoria se encuentran en 02_COLECCIONES; el inventario completo clasificado en 03_CATALOGO. Recursos de Geometry Dash: solo referencia local. Kenney: verificar licencia antes de distribuir.</small></p></body></html>')
} finally { $wi.Dispose() }
Write-Host ''
Write-Host "FINALIZADO: $total archivos organizados virtualmente." -ForegroundColor Green
Write-Host ('Catalogo visual: ' + $indice)
Write-Host ('CSV clasificado: ' + $clasificadoPath)
Write-Host 'Las fuentes originales y el proyecto Unity siguen intactos.'
Start-Process -FilePath $indice
