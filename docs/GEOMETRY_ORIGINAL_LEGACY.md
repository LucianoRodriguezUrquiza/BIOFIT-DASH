# BIOFIT GAME — GeomeTry ORIGINAL + BILATERAL LEGACY

## Identidad y alcance

- Base BIOFIT Unity v0.9.11: `5616fc4571193d4a2d076550a909089344144384`.
- GeomeTry original: https://github.com/Christ0u/GeomeTry , commit `d922caddb4d21ecb791a266143e279bdb969d42c`.
- Rama nueva independiente: `feat/geometry-original-legacy-bridge`. No depende de PR18 ni PR19.
- **Sin reescritura de los mapas, escenas, prefabs, física ni controladores originales.**
- No se altera firmware ESP32, NVS, UUID, umbrales, calibración, receptor ni los CSV históricos.
- Reutiliza el monitor BLE automático del proyecto BIOFIT, con el selector forzado exclusivamente a LEGACY.

## Por qué es un submódulo Git, no mapas regenerados

`Assets/GeomeTryOriginal` es un **submódulo Git de todo el repositorio original**.
Los 513 archivos originales y sus GUID se recuperan directamente y conservan sus
bytes y jerarquía: `Assets/GeomeTryOriginal/Assets/Scenes`,
`Resources/maps/BASE`, `Resources/Prefabs`, imágenes, scripts, shaders, etc.
No se usan los JSON comprimidos del PR19.

**Importante:** GitHub Desktop y ZIP de una rama con submódulos pueden dejar
el directorio vacío. No abras Unity antes de traer la fuente real:

```powershell
git submodule update --init --recursive
git submodule status
```

Si no se recuperan los submódulos, **no hay juego importable**. El repositorio
original es de Unity 6000.0.27f1 y se abre dentro de BIOFIT Unity 6000.3.28f1:
podría haber migraciones; todavía no se verificaron en Editor.

## Exactitud de la jugabilidad

El original sigue ejecutando `Character`, `Cube`, `Ship`, `Wave`,
`Game`, `GameManager`, `CharacterPortal`, `JumpOrb`, `JumpPad`
y los prefabs/animaciones/escenas originales. `Character.Start` mantiene
`Time.timeScale = 1.5`; `Cube` mantiene `jumpForce = 13.5`, velocidad
`6.5` y `gravityScale = 4`. `ProjectSettings/Physics2DSettings.asset` y
`TimeManager.asset` eran idénticos entre BIOFIT y GeomeTry al comparar.
Se importan las etiquetas y capas originales para los prefabs.

El único puente de entrada está en
`Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs`:

```
UP_IMPULSE -> BiofitBLEMonitor (filtros/tiempos originales)
           -> BiofitBilateralJumpDetector LEGACY (180 ms, 900 ms)
           -> BilateralJumpCandidate
           -> Cube.onClick() (pulso único)
           -> Cube.Jump() ORIGINAL -> Rigidbody2D ORIGINAL
```

El puente usa el campo protegido `keyPressed` por reflexión sobre el
`Cube` **solo durante un único onClick()**. No mantiene pulsación, ni cola,
ni anticipación de aterrizaje, ni cambia los saltos físicos. Las órdenes
en aire pueden no hacer nada. No se promete que cada gesto produzca salto:
el detector legacy mantiene su cooldown y su ambigüedad biomecánica.
`Ship` y `Wave` siguen con el input clásico de toque mantenido de
GeomeTry; no se añade control BLE de vuelo por ahora.

Para que coexistan el input clásico de GeomeTry y el Input System de BIOFIT,
en esta rama únicamente se cambia `activeInputHandler: 1` a `2 (Both)`.
No se reemplazan ni se reconfiguran trackers.

## Interfaz y sesiones

En `Main Menu` y `Level Selection` hay una caja BIOFIT con:
- conexiones 2/2 automáticas, estado de reloj y recuento de órdenes;
- registro INICIAR/FINALIZAR, RAW opcional, SYNC, exportación Downloads/SAF.

No se superpone ningún HUD de BIOFIT sobre `Level`: juego, cámaras,
pistas, colisiones, música, nave, wave, orbes y portales son de GeomeTry.

La conexión BLE permanece en un objeto persistente mientras se cambia entre
escenas originales. Esta arquitectura todavía necesita prueba Android real.
La señal BLE no se interpreta como un salto humano confirmado.

## Construcción local de APK — sin sobrescribir tu instalación

1. **Copia/respaldá toda tu carpeta local** antes de cambiar de rama;
   especialmente tus escenas/configuraciones locales y datos de sesiones.
   Nunca uses discard/reset/clean para resolver conflictos.
2. Trabajá sobre la copia usando rama `feat/geometry-original-legacy-bridge`
   y ejecutá `git submodule update --init --recursive` (con internet).
3. Unity Hub: abrí la **copia** con Editor **6000.3.28f1** y espera a que importe
   el submódulo completo; se resolverá URP 17.3.0. Comprobá la Console.
4. Menú **BIOFIT > GeomeTry ORIGINAL > 1. Verificar fuente y mapas**.
   Verifica cuatro JSON con hashes de blob Git, escenas y prefabs.
5. Menú **2. Abrir Main Menu original**. Play: probá pantalla táctil,
   selección, nivel, salto, muerte, nave, wave y orbes. Confirmá que no hay
   errores de referencias, Input/URP ni escenas.
6. File > Build Profiles > Android > Switch Platform. Comprobá permiso
   Bluetooth, Application Identifier, firma y versionCode. El build usa los
   valores locales existentes: no los modifica.
7. Menú **3. Compilar APK Android original + BLE LEGACY**. Si Unity lo completa,
   el archivo aparece en `Builds/BIOFIT_GeomeTry_ORIGINAL_LEGACY_*.apk`.
   El comando selecciona las diez escenas originales **sin sobrescribir
   las listas locales de Build Profiles**, y restaura el ajuste gráfico
   temporal tras compilar.
8. No desinstales tu APK anterior por defecto: Android requiere mismo package
   ID, firma y versionCode admisible para actualizar sin borrar los datos.
   Para evitar cualquier riesgo, probá con un paquete diferente **solo en
   una copia del proyecto**; eso crea otra aplicación, sin compartir archivos
   privados ni firma de la existente.

## Verificaciones y limitaciones

- El código y el submódulo están propuestos en GitHub para evaluar.
- No se ha ejecutado aquí Unity Editor/Play ni compilación Android:
  **no hay una APK verificada ni debe afirmarse lo contrario**.
- El submódulo guarda la geometría original, no una aproximación.
- Algunos recursos musicales/gráficos del repositorio tercero pueden tener
  licencias diferentes de la MIT del código. Antes de redistribuir una APK
  públicamente, comprobá sus permisos de uso; el submódulo no otorga derechos.
- Cualquier error de importación, renderer URP o entrada en el teléfono deberá
  resolverse sin modificar mapas ni parámetros físicos originales.
- Este cambio no demuestra equivalencia bit a bit de físicas entre Unity
  6000.0.27f1 original y 6000.3.28f1 BIOFIT: se conservan código, prefabs
  y parámetros; falta la prueba en Editor/Android.
