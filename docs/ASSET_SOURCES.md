# BIOFIT DASH - Catálogo y procedencia de recursos

## Política de incorporación

Este repositorio **no aloja la biblioteca completa** de recursos de Geometry Dash.
La biblioteca maestra es local y se reconstruye ejecutando
`tools/assets/Preparar_Biblioteca_BIOFIT.ps1`.

- **Referencia de estudio:** se mantiene fuera de `Assets/` y fuera de Git.
- **Recursos distribuibles:** solo arte propio y recursos con licencia/documentación
  que permita la redistribución (por ejemplo, los paquetes Kenney CC0).
- **Comprobación individual:** antes de copiar un recurso, registrar nombre del pack,
  URL, licencia, autor, cambios y fecha de consulta.
- **Música y SFX:** nunca importar archivos de audio de terceros sin verificar derechos.
- **Compatibilidad:** mantener el submódulo GeomeTry y las referencias `.meta`
  existentes; no sustituir sprites existentes por lotes.

## Fuentes catalogadas

| Nombre | URL | Ref | Clasificación | Uso |
| --- | --- | --- | --- | --- |
| GD Assets | https://github.com/kenzieshane/Assets | `1.0` | Gráficos originales de RobTop; referencia, sin autorización general de redistribución | Iconos, portales, obstáculos, capas, efectos |
| GD Textures | https://github.com/Weebifying/gd-textures | Rama predeterminada al clonar | Gráficos originales de RobTop; referencia, sin autorización general de redistribución | Texturas, `.png`, `.plist`, versiones |
| Kenney assets (espejo comunitario) | https://github.com/shorepine/kenney | Rama predeterminada al clonar | Kenney CC0, comprobar licencia de cada pack y origen | Sprites, UI, iconos, modelos |
| Kenney oficial | https://kenney.nl/assets | Descargar por pack o catálogo oficial | CC0 en packs identificados como tal; verificar condiciones | Fuente primaria para packs y formatos originales |

El archivo local `BIOFIT-RECURSOS-GD/03_CATALOGO/FUENTES_COMMITS.csv` registra
los hashes de los commits efectivamente descargados.
`INVENTARIO_COMPLETO.csv` contiene el inventario de archivos. **No añadir los
assets originales de Geometry Dash al repositorio público.**

## Protocolo de integración de un recurso

1. Elegir la categoría y el archivo en el inventario local.
2. Comprobar su licencia en la fuente primaria, y registrar la procedencia.
3. Crear una copia bajo `Assets/BIOFIT/Art/ThirdParty/<Fuente>/<Paquete>/`
   (o `Assets/BIOFIT/Art/Original/` si el recurso es propio).
4. Permitir que Unity genere los `.meta` y ajustarlos cuando proceda.
5. Configurar Sprite (2D and UI), transparencia, Filter Mode, Pixels per Unit,
   compresión, atlas y referencias según el caso.
6. Verificar en Editor y en compilación Android; registrar impacto de tamaño
   de APK y rendimiento.
7. Incorporar solo recursos aprobados en una rama de trabajo y revisar el diff
   antes de fusionar a `main`.

## Registro de elementos integrados

| Archivo en `Assets` | Origen | Autor | Licencia | Modificaciones | Fecha de verificación |
| --- | --- | --- | --- | --- | --- |
| Pendiente | | | | | |
