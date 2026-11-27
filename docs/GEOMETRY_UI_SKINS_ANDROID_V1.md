# BIOFIT GeomeTry — UX, 70 skins, back/pause y 60 FPS

Esta rama deriva de feat/geometry-original-legacy-bridge y preserva el submodulo original completo en ThirdParty/GeomeTryOriginal, los JSON, escenas, movimiento, velocidad, salto, fisica y BLE BILATERAL LEGACY.

Cambios: nuevo controlador Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs; Main Menu conserva sus tres botones centrales, habilita Settings y Customisation (eventos originales desactivados) y oculta los controles inferiores y Credits. En Level Selection aparece selector visual de los cinco mapas con botones funcionales. Hay back en todas las escenas salvo Main Menu y pausa/reanudar/reiniciar/regresar en Level. La pausa restaura Time.timeScale y audio de antes, sin ajustar los parametros del cubo. Settings contiene estados BLE, registros, exportacion y FPS estimados. Customisation tiene 70 skins en una grilla de 10x7, persistidas en PlayerPrefs; solamente se sustituye el SpriteRenderer del cubo.

El panel IMGUI antiguo de BiofitGeomeTryLegacyBridge fue retirado sin cambiar el handler de BilateralJumpCandidate ni los umbrales/firmware del detector. Exposicion read-only de contadores para Settings.

Android solicita Application.targetFrameRate=60 y desactiva vSync. No cambia Time.fixedDeltaTime, velocidad, gravedad ni el 1.5x original de GeomeTry. Se necesita prueba real en dispositivo; no se garantizan 60 FPS.

## Archivos graficos proporcionados por el usuario

El pack entregado en el chat BIOFIT_GeomeTry_UI_Assets_HD_v2.zip contiene LevelSelect.png (reproduccion visual del selector indicado), Back.png (icono sin azul), Pause.png y Skins.png (70 imagenes exactamente del collage). El pack es necesario para reproducir las imagenes solicitadas, pero hay botones de reserva si no esta instalado. No se suben al repositorio remoto por tratarse de archivos graficos aportados por el usuario y para conservar el repositorio reutilizable.

## Instalacion sin tocar tu proyecto actual

1. Cerrar Unity; nunca resetear/descartar los cambios de tu copia existente.
2. Crear una copia separada desde PowerShell:
   cd "C:\Users\Capo\Documents"
   git clone --recurse-submodules --branch feat/geometry-ui-skins-60fps-v1 https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-GeomeTry-UI-V1
   cd ".\BIOFIT-GeomeTry-UI-V1"
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
3. Guardar el pack descargado desde el chat en Descargas y ejecutar en la nueva carpeta:
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\install_geometry_ui_assets.ps1"
   Si el archivo esta en otra ubicacion, agregar -ZipPath "RUTA".
4. Abrir BIOFIT-GeomeTry-UI-V1 con Unity 6000.3.28f1; comprobar consola, permisos, las diez escenas en Build Profiles, Player Settings con Input Manager (Old), y el EventSystem original adaptado a StandaloneInputModule (no modificar main ni firmware).
5. Probar todos los botones y niveles en Play, pausa/reinicio, retorno, skins, Settings/registro y guardar/reabrir.
6. Android > Switch Platform > BIOFIT > GeomeTry ORIGINAL > Compilar APK Android original + BLE LEGACY.
7. Probar primero tactil, luego dos WaveShare BLE y medir FPS reales en telefono.

## Limitaciones

Los scripts y tests host NO constituyen una prueba de compilacion en Unity Editor, ejecucion APK Android, autonomia ni 60 FPS reales. Las escenas originales Settings, Profile, Stats, Success y Customisation casi no contienen UI: el controlador aporta las pantallas solicitadas y un retorno seguro, no crea estadisticas o editor de niveles nuevos.


## Correcciones de la v2 (21 noviembre 2026)

- El Editor arranca con render Built-in en el proyecto base, pero GeomeTry requiere URP 2D. `BiofitGeometryUxPreflight.cs` configura el asset URP 2D original en esta carpeta dedicada para que los niveles se dibujen correctamente al usar Play. El build Android lo sigue estableciendo de modo temporal como antes.
- Antes se amplificaba el fondo 527x234; ahora se usa sin reescalado destructivo el archivo HD original 1672x941 del usuario. La escena respeta la proporción 16:9 incluso en Android 20:9.
- El selector tiene tarjeta enfocada que sube y late suavemente, flechas y puntos interactivos y texto de nivel actualizado, sin color amarillo superpuesto a la tarjeta.
- Configuracion, skins y pausa usan botones de estilo arcade con marcos blanco/negro, verdes intensos, letras blancas gruesas delineadas y logotipo HD. El boton volver mantiene su imagen original.
- Instalador del pack v2 protegido por SHA256: permite sustituir **solo** el selector viejo de SHA conocido, conservando las otras tres imagenes y cualquier cambio personalizado de usuario.
- Se reviso GD-Community-Hub/Assets solo como referencia. No se incorpora material grafico de Geometry Dash desde ese repositorio; el LICENSE de un repositorio espejo no garantiza derechos del contenido original.

### Actualizacion de la copia BIOFIT-GeomeTry-UI-V1 ya preparada

Con Unity cerrado ejecutar desde esa carpeta `git pull --ff-only origin feat/geometry-ui-skins-60fps-v1`. Guardar el ZIP HD v2 en Descargas. Instalar mediante `powershell -NoProfile -ExecutionPolicy Bypass -File ".\\tools\\install_geometry_ui_assets.ps1"`. No ejecutar prepare_geometry_original.ps1 otra vez: la copia ya existe. Abrir Unity y usar `BIOFIT > GeomeTry UX > 2. Verificar UX y recursos`.

**Estado:** las pruebas host verifican código y contratos; solo el Editor Unity en la computadora del usuario puede validar gráficos, navegación y build; se requiere el teléfono para FPS y BLE reales.


## Corrección de problemas observados en Unity (selección de skins / settings / reingreso)

- El atlas de 70 skins suministrado por el usuario mide 1280x887, y no utiliza un grid matemático exactamente uniforme. La pantalla Customisation renderiza **el archivo completo sin 70 recortes** y coloca 70 zonas táctiles invisibles; se muestra un contorno blanco solamente alrededor del elegido y se guarda en PlayerPrefs.
- Para mostrar la skin del cubo en el juego, Sprite.Create usa límites medidos en los espacios transparentes del collage. No cambia Rigidbody2D, ni Character, ni Cube, ni velocidad, ni BLE. La calidad de algunos sprites podría mejorar con archivos independientes originales, pero el selector ya no los corta.
- Se corrigieron la superposición del título de Configuración con el cuadro BLE, y el espacio entre título y atlas; botones verde lima usan texto azul marino oscuro, no blanco.
- Las esquinas usan el recurso original UI/Misc/Corner, con los cuatro RectTransform completamente dentro del Canvas y UV espejadas (sin rotaciones de contenedores fuera de pantalla).
- Tras salir del primer nivel y regresar al Main Menu, los listeners UnityEvent serializados podían referirse a un UIManager de una instancia de GameManager que fue destruida (el original vive con DontDestroyOnLoad). Se enlazan **los tres botones principales** a la nueva capa de navegación y se reemplaza el UnityEvent persistente. El método GameManager.LoadLevel original sigue intacto.
- Sigue pendiente la comprobación de reingreso y skins en Unity Editor, y Android/60fps/2 IMUs con hardware.


## Revisión de los ocho defectos observados en el video (22/11/2026)

Implementada en la misma rama de desarrollo, sin tocar las Waveshare, física ni scripts originales de GeomeTry.

1. Selector: BiofitUiArtwork.cs limpia una vez el fondo HD 1672x941, extrae las cinco tarjetas en texturas transparentes separadas y las dibuja independientemente. El enfoque sube de forma vertical, nunca crece hasta invadir tarjetas vecinas. Los cinco recortes conservan sus dimensiones reales y sombras originales.
2. Indicadores: al generar el fondo se eliminan la hilera de círculos incorporada a la imagen y el antiguo texto fijo de nivel. Unity dibuja solamente una serie de cinco puntos, con el índice seleccionado, y un rótulo dinámico.
3. Personalización: se muestra íntegro el atlas 1280x887, ancho 790 (antes 915) y margen inferior superior a 50 unidades UI en el diseño 1600x900. Los 70 hotspots, PlayerPrefs y recorte de sprite SOLO para el cubo durante el juego se mantienen. No se altera Rigidbody2D.
4. Logo: se compone desde el diseño HD del usuario en una nueva textura con alfa transparente, eliminando el azul del fondo; no hay una franja rectangular superpuesta.
5. Esquinas y volver: cuatro decoraciones extraídas de la referencia del usuario, sin fondo azul, pivotadas dentro del lienzo. La esquina superior izquierda usa la decoración derecha reflejada para evitar duplicar la flecha Atrás. El selector aprovecha la flecha original con una única zona táctil.
6. Pantallas responsivas: canvas interno fijo 1600x900, escalado uniformemente con Screen.safeArea. Conserva proporciones en 16:9, 20:9 o 4:3; los laterales externos reciben fondo azul fuera del nivel, sin cubrir la escena del videojuego.
7. Configuración BLE: cuatro tarjetas independientes para conexión WAVESHARE, eventos/pares, captura/archivos y rendimiento. Los botones conservan sus acciones y el texto azul oscuro sobre verde lima.
8. FPS: muestreo observado de cuadros en ventanas reales de 0,75 s usando Time.unscaledDeltaTime. La pantalla indica explícitamente Editor (NO representa Android) o Android (objetivo solicitado 60 FPS, no garantizados). Nunca se equipara la tasa del archivo de video a la del juego.

### Instalación y verificaciones

No descargar otro ZIP ni ejecutar otra vez la preparación original. Se reutiliza el paquete BIOFIT_GeomeTry_UI_Assets_HD_v2.zip ya instalado. Al actualizar desde Git se importa el nuevo archivo BiofitUiArtwork.cs. El preflight permite lectura CPU de LevelSelect.png para hacer el recorte y canal alfa en memoria; no se reemplaza la imagen fuente del usuario.

Cerrar Unity, ejecutar git status --short y luego, si Git lo permite sin conflictos, git pull --ff-only origin feat/geometry-ui-skins-60fps-v1 en la carpeta BIOFIT-GeomeTry-UI-V1. No usar reset, clean ni descartar datos locales. Abrir Unity 6000.3.28f1 y ejecutar BIOFIT > GeomeTry UX > 1. Configurar modulo tactil Android, seguido de 2. Verificar UX y recursos.

Pruebas en Unity: Game View 1280x720 y al menos una relación de aspecto más amplia, flechas de selección, cinco tarjetas sin superposición ni puntos duplicados, skin de última fila, logo y esquinas, paneles BLE, FPS claramente marcado como Editor, pausa, volver y reingreso a un nivel. Luego comprobar APK Android, FPS reales, dos Waveshare y BILATERAL LEGACY.

Las pruebas automatizadas son de sintaxis/contratos; sin Unity Editor o teléfono físico no se certifican renderizado, compilación APK, 60 FPS ni BLE. Mantener PR #21 en borrador sin fusionarlo.

## 27/11/2026 — Selector unificado de personajes (Cube / Ball / Spider)
- Usuario solicita estructura inspirada en sus tres capturas de Geometry Dash, sin candados, tienda, monedas, estrellas, diamantes ni categorías ajenas. Todos los skins disponibles desde el comienzo. Se reutiliza el atlas propio de 70 Cube, y los manifiestos y sprites locales V6.5 Ball/Spider (instalación previamente reportada: 119/69). No se publican ni se vuelven a importar recursos propietarios; no se cambian los PNG locales. Fondo gris recreado proceduralmente (aprox. RGB 149 a 66 según referencia; no es la textura original extraída), esquinas y flecha existentes, fila horizontal de tres categorías, preview, grilla 10x3, selección blanca y paginación interactiva. Flechas limitadas a primera/última página; página independiente y retorno a la skin elegida. Persistencia usa las tres claves previas, no se reinicia elección. Para Cube se comparten los recortes medidos del atlas entre UV de interfaz y Sprite.Create de juego, sin variar PPU/colliders; las celdas ya no muestran toda la imagen íntegra, posible revisión visual si algún contorno sobresale del recorte. No se altera arte original de los cubos. Fallback informativo si faltan recursos. No cambia física, control, mapas, BMD/GFA, BLE ni firmware. CI anterior de reconexión 5/5 (46037751060). Validación visual y APK pendientes. Sin ZIP, con Unity cerrado git pull --ff-only en feat/bmd-v231; no volver a preparar/importar.

## RCA 27/11/2026 — contraste del selector futurista de vehículos
Tras captura real aportada por el usuario, se elimina ArcadeCorners exclusivamente de Customisation. Se conservan el PNG local, su aspect-fill y brillo, el botón VOLVER original, los sprites y el layout de 30 skins por página. Placas gris oscuro casi opacas independientes detrás de título PERSONAJE, preview, texto de skin activa, cada categoría, cada skin y contador de página. No interceptan raycasts; se mantienen marco blanco de selección y botones existentes. Se eliminan únicamente los puntos circulares de paginación del selector de personajes (no las categorías ni indicadores de selección de nivel); navegación por flechas rectangulares gris oscuro con chevrón blanco y borde cian, deshabilitadas visual/funcionalmente en extremos, y contador numérico. No se usa backIcon para paginación. Sin cambios a física, mapas, deltas, firmware, BLE/GFA ni otros menús. No se requiere reinstalar arte: git pull --ff-only con Unity cerrado. Contratos y geometría revisados; prueba visual Unity y APK pendientes.

## RCA 27/11/2026 — sistema visual arcade común (Configuración y Personajes)
Usuario solicita eliminar apariencia plana, conservar estructura C y selector, usar esquinas redondeadas y fuente del juego; prioridad texto contenido. Se implementan máscaras redondeadas RGBA generadas en ejecución y sprites de nueve cortes, cache por radio y liberación al destruir UI. Doble marco oscuro/cian, base opaca azul petróleo, relieve superior e inferior en botones verde/cian, mismos componentes para los dos menús. Se conservan PNG locales, navegación, 30 skins por página, selección, BMD y lógica de registro/exportación. Etiquetas con padding 8 px horizontal/4 vertical adicionales, TMP autosize y ellipsis de respaldo, y ajuste uniforme de vértices reales BMFont (no solo avances), habilitado exclusivamente en las etiquetas de estos menús. Se reduce ligeramente arte de preview y celdas para respetar márgenes de marco; nombre de categoría queda dentro del mismo botón. Pruebas de geometría, estilo y límites de tinta actualizadas. No se distribuyen assets externos ni se requiere copiar nuevos PNG. No se alteran niveles, físicas, BLE, firmware ni main. Validación visual Unity y Android pendiente. Actualizar con Unity cerrado mediante git pull --ff-only en feat/bmd-v231.
