# BIOFIT DASH — cinco niveles progresivos, selector español y arte local (candidata)

> Estado: **candidata sin validación en Unity Editor ni en Android con los dos ESP32**. Este PR no altera el firmware, los parámetros BILATERAL LEGACY ni la física del Cube.

## Cambios y criterio físico

- Nombres visibles y campo JSON: **Nivel 1**, **Nivel 2**, **Nivel 3**, **Nivel 4**, **Nivel 5**, en ese orden **de izquierda a derecha**. Los nombres de archivo internos siguen siendo `BackOnTrack`, `JumpyNightmare`, `Polargeist`, `StereoMadness` y `WaveVibe` por compatibilidad con los scripts y el cargador; no se muestran como títulos de juego.
- Músicas **sin cambios**: StereoMadness, BackOnTrack, Polargeist, DryOut, CantLetGo (en el mismo orden).
- Longitud: marcador a x=575, duración nominal ~60 segundos, velocidad ~9,75 u/s reales.
- Cooldown BLE: 900 ms, umbral 0,12g, ventana bilateral 180 ms, periodo mismo tobillo 320 ms **sin cambios**.
- Condición de diseño: **al menos 15 unidades entre centros de pinchos consecutivos**, equivalentes a **1,538 segundos** a 9,75 u/s. La ventana temporal es estimada y no garantiza la jugabilidad.
- Salto Cube: 2,32 u de altura teórica; 459 ms de vuelo; 4,47 u de alcance; despegue nominal de ensayo `x_pincho - 3 u`. El detector **no** bufferiza saltos recibidos en el aire.

| Nivel | Modo | Pinchos | Separación mínima | Intervalo mínimo nominal | Portales |
| --- | --- | ---: | ---: | ---: | --- |
| Nivel 1 | Cube | 27 | 16 u | 1,64 s | ninguno |
| Nivel 2 | Ball | 25 | 18 u | 1,85 s | ninguno |
| Nivel 3 | Spider | 27 | 16 u | 1,64 s | ninguno |
| Nivel 4 | Cube → Ball → Spider → Cube | 20 | 17 u | 1,74 s | x=185, 385, 515 |
| Nivel 5 | Cube → Ball → Spider → Cube → Ball | 21 | 15 u | 1,54 s | x=117, 275, 405, 515 |

**Nota:** la dificultad aumenta también por alternancia de suelo/techo y cambios de modo, no únicamente por la cantidad o separación de pinchos. En niveles Ball/Spider los pinchos alternan suelo (y0) y techo (y7); el techo continuo está a y8. Cada activación aceptada debe tener suficiente margen para completar el cambio.

## Diseño visual y rendimiento

- En el selector se generan **cinco tarjetas reales** con textos en español `NIVEL 1..5`, sus modos y el icono correspondiente. Se evita reutilizar imágenes de tarjetas que pudieran llevar nombres anteriores impresos.
- El primer nivel ocupa la primera tarjeta a la izquierda, sin reordenar las coordenadas originales de los cinco espacios.
- Se aprovecha el espacio seguro de 1600×900, escalado a 16:9, heredado de `BiofitGeometryUX`.
- Los botones visibles y zonas táctiles de menú reciben animación de pulsación (escala; resaltado en hotspots).
- Se agregan símbolos originales sin colisiones (`biofitDecoPulse`, `biofitDecoCrystal`, `biofitDecoStar`) a los cinco mapas. No se alteran el piso ni el collider del personaje.
- Los iconos y portales usan primero un **PNG individual seleccionado localmente** y, si no existe, un sprite original generado en código. Los portales muestran un destello breve al cambiar de modo; se conservan los efectos de Ball y Spider ya implementados.
- Evitar texturas gigantes y sprites repetidos sin necesidad. Los fallback se almacenan en caché. Los 60 FPS son un objetivo, no una medición.

## Biblioteca de recursos: instalación local por elección explícita

El catálogo completo de 194.750 entradas reside **únicamente en Windows**:
`C:\Users\Capo\Documents\BIOFIT-RECURSOS-GD\03_CATALOGO\VISUAL\index.html`

No es accesible directamente desde el GitHub remoto. **No se ha afirmado que se haya elegido o integrado un sprite original concreto** de ese catálogo en el PR. El procedimiento optativo es:

1. Abrir el catálogo HTML y elegir archivos **PNG individuales** para Ball, Spider y cada portal. Una hoja de sprites o atlas con varios iconos requiere recorte adicional; copiarlo como PNG individual no basta.
2. Desde el nuevo clon del juego, correr:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\assets\Importar_Arte_Niveles_Local.ps1`
3. El script crea una plantilla:
   `BIOFIT-RECURSOS-GD\04_SELECCION\ARTE_NIVELES_BIOFIT.csv`.
4. Completar para cada destino `RutaRelativa` respecto a `01_FUENTES`, la licencia comprobada y `Aprobado=SI`.
5. Para CC0 comprobada, ejecutar con `-Aplicar`. Para archivos sin licencia de distribución y solo durante una prueba **privada**, usar `-Aplicar -SoloPrototipoPrivado` con `Licencia=REFERENCIA_PRIVADA`. **No distribuir esos archivos en GitHub o una APK publicada** sin permiso.
6. Se importan en `Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/`, carpeta ignorada por Git. El script verifica PNG real, rutas y SHA256; no modifica la biblioteca original.
7. Unity importa automáticamente esos PNG individuales como Sprite y los usa de forma preferente.

| Destino local | Función |
| --- | --- |
| `IconBall.png` | Modelo visual Ball |
| `IconSpider.png` | Modelo visual Spider |
| `PortalCube.png` | Portal hacia Cube |
| `PortalBall.png` | Portal hacia Ball |
| `PortalSpider.png` | Portal hacia Spider |
| `DecoPulse.png`, `DecoCrystal.png`, `DecoStar.png` | Decoración sin colisión |

El arte protegido de Geometry Dash puede ser útil para estudio y prototipado personal; **la procedencia no constituye una licencia de redistribución**. Para publicación preferir Kenney CC0 verificado o dibujos propios.

## Validación realizada y pendiente

Las pruebas automatizadas verifican estructura JSON, nombres, duraciones nominales, secuencia de portales, separación >=15 u, alternancia de superficies, recursos permitidos y un **sobre geométrico balístico simplificado** para Cube. Los cálculos **no ejecutan Unity**.

Antes de fusionar con el juego estable:

1. Ejecutar `python tests/check_three_modes_60s.py`.
2. En Unity 6000.3.28f1, ejecutar `BIOFIT > GeomeTry ORIGINAL > 8. Verificar niveles 3 MODOS (60 s)`.
3. Iniciar desde Main Menu, entrar al selector y verificar que se ven exactamente las cinco tarjetas desde `NIVEL 1` a `NIVEL 5`, de izquierda a derecha; probar respuesta al pulsar y volver.
4. Probar cinco mapas en Editor: dobles saltos, alternancia suelo/techo, cambios de portales, colisiones, muerte/respawn y retorno.
5. En Android 16:9 y con ambos sensores, medir al menos 20 pares de saltos a separaciones 15–18 u. Registrar `BLE_LEGACY_CUBE ... JUMP_APPLIED/NO_JUMP`, eventos por cooldown y FPS medidos.
6. Verificar los PNG seleccionados, importador, efectos y consumo de memoria en el celular. Mantener respaldo y no reemplazar el clon principal sin pruebas físicas.

**No se ha ejecutado una simulación Unity/Box2D real ni se ha medido una APK con BLE.** El modelo balístico continuo no evalúa exactamente los colliders, la carga del frame o la latencia real de eventos.
