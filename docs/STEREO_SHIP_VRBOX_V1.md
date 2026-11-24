# BIOFIT DASH — VR BOX mantenido + modo Ship + Nivel 1 Stereo Madness (v1 EXPERIMENTAL)

**Fecha:** 24/11/2026. **Rama:** `feat/stereo-madness-ship-vrbox-held-v1`. **Procedencia:** se creó desde la rama experimental `feat/vr-joystick-input-diagnostics` del [PR #30](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/30), que incorpora los cambios **fusionados en `main` mediante PR #29** (cooldown configurable 900/800/700/600/500/400 ms). **No modificar `main` hasta validación humana de APK**. Las operaciones de desarrollo NO alteraron el firmware ESP32.

> **Precisión crítica:** el nuevo Nivel 1 es una **reconstrucción reproducible GUIADA POR EL VIDEO** y ocupa todo el recorrido de duración similar, **NO una copia verificada objeto por objeto de Stereo Madness original**. Se revisó el MP4 compartido (`Geometry Dash - Stereo Madness 100% (All Coins) (1).mp4`), 1280×720, 60 FPS, 95,52 segundos con interfaz y pantalla de victoria. En el repositorio NO están los 2.291 objetos originales ni sus texturas patentadas. El usuario debe saberlo antes de evaluar fidelidad.

## 1. Auditoría e invariantes

Código original importado por el submódulo Git `ThirdParty/GeomeTryOriginal` de `Christ0u/GeomeTry`, pin `d922caddb4d21ecb791a266143e279bdb969d42c`:

- `Character.cs`: velocidad horizontal 6,5 unidades/s y `Time.timeScale = 1.5` en la versión pinneada; por lo tanto velocidad efectiva ~9,75 unidades/s, y la acción de Cube durante una pulsación sostenida ocurre al detectar suelo en cada `Update`.
- `Cube.cs`: `onClick()` exige `keyPressed` y salta exclusivamente si `CheckGrounded()`; `jumpForce = 13.5`. **No se reescribió.**
- `Ship.cs` original: `gravityScale = 2.5`, `maxVelocityY = 10`, inclinación visual con `Mathf.Atan2(vy,7)` y límite ±40°. Se reutilizaron esos parámetros para el adaptador; **no afirmar equivalencia bit a bit con Geometry Dash de RobTop** ni con el código del submódulo, porque se conserva el mismo objeto Cube.
- `BiofitGeomeTryLegacyBridge.cs`: BLE BILATERAL LEGACY permanece con emparejado 180 ms y umbral UP 0,12g; un par aceptado impulsa el Cube o llama a `BiofitModeMotor.TryPulse()`. Sin cambios a archivo ni a UUID, clocks, sincronización, firmware o detección.
- `BiofitBilateralJumpDetector.cs`: **seis cooldowns** persistentes `900/800/700/600/500/400` ms, con predeterminado 900 y 400 elegido habitualmente por el usuario; sin modificación.

## 2. Implementación joystick VR BOX

**Perfil comprobado físicamente:** `CAMBIO DE MODO + C` → nombre HID `VR BOX`, documentado en [JOYSTICK - Modo Juego.md](../JOYSTICK%20-%20Modo%20Juego.md) de `main`. A/R1 = botones Unity 0+13; B/R2 = 0; C = 13; D = 1. El usuario solicitó que **todas** las teclas representen la **misma acción de salto**, no que se usen para pausa/volver/reiniciar en esta versión. Ningún código independiente se suma como un segundo salto.

`BiofitVrBoxInput` en `BiofitThreeModes.cs`:
- Confirma nombre `VR BOX` mediante `Input.GetJoystickNames()` (refresco 250ms) y escucha los tres KeyCodes generales 0/1/13. Si el mando está desconectado, no inyecta acciones.
- `AnyHeld` = OR de `Input.GetKey(KeyCode.JoystickButton0/1/13)`; A/R1 con 0+13 desencadena **un único estado lógico TRUE**, no dos acciones.
- `AnyDown` = OR de `GetKeyDown` de los tres códigos para Ball y Spider.

`BiofitVrBoxController`: se añade **solo al personaje real** por el `Bootstrap` cada vez que reaparece tras una muerte; si es Cube y se mantiene cualquiera de los cuatro botones, ejecuta `cube.onClick()` en cada Update, con `Character.keyPressed` temporal vía reflexión/restauración y física intacta. La función original `CheckGrounded()` determina cuándo vuelve a saltar al aterrizar. **No se crean temporizadores que generen saltos en el aire**.

En Ball/Spider se recibe el pulso `AnyDown`, pasando por el `TryPulse()` existente y su protección 120ms. La pulsación sostenida automática se ofrece específicamente al **Cube**, como solicitó el usuario; no se fuerza teletransporte/gravedad repetidos de otros modos.

En modo Mouse, la ruta original `Mouse0`/táctil permanece, pero **el problema de volumen/Atrás del firmware Android no se resolvió ni se intercepta**. Usar preferentemente `CAMBIO+C`.

## 3. Implementación Ship

Nuevo `BiofitMode.Ship` y `biofitShipPortal`, con arte procedural independiente `IconShip` / `PortalShip` sustituible por PNG de licencia válida desde `Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/`. El componente `Cube` se conserva en el mismo GameObject para cámara, progreso, muerte/respawn, BLE y escenas; se deshabilita **solo durante Ship**. El motor de movimiento comparte la posición y el Rigidbody2D original.

- `FixedUpdate` mantiene el movimiento horizontal de 6,5 unidades/s nominales de `BiofitModeMotor`; `body.gravityScale` es **−2,5** durante tecla presionada y **+2,5** al soltar. `vy` limitado ±10 y rotación ±40°.
- Control continuo: `Space`, clic/touch mantenido, cualquiera de los botones 0/1/13 mantenidos, o intervalo temporal disparado por un impulso BLE.
- **BLE no es una tecla permanentemente presionada:** `BiofitModeMotor.TryPulse()` para Ship agrega un empuje virtual temporal de **400 ms por par aceptado**, evitando que quede ascendiendo para siempre. El usuario podrá evaluar después una duración diferente. No confundir **400 ms de impulso de nave** con los **400 ms de cooldown configurable del detector bilateral**: son parámetros distintos.
- Transiciones con `biofitCubePortal` restauran Cube y gravitación normal, sin mutar el submódulo.
- En el Nivel 1 se construye techo de referencia únicamente en tramos Ship (`x≈247–381` y `726–786`), manteniendo intacto el techo de niveles 2–5.

**Requiere prueba física:** respuesta vertical, ausencia de vuelo involuntario, salida correcta del portal, reinicio tras muerte, límites del techo y compatibilidad con pulsación corporal. Lo disponible en GitHub CI es análisis estático de sintaxis/contratos, no una compilación APK de Unity.

## 4. Nivel 1: Stereo Madness, alcance real

Archivo activo **`Assets/BIOFIT/Resources/BiofitThreeModes/BackOnTrack.json`**, porque `BiofitGeometryUX.MapKeys[0] = "BackOnTrack"` y su `Level.Id=1`. **No cambiar el nombre de la ruta sin actualizar el loader.** Se mantuvieron intactos los otros cuatro JSON: JumpyNightmare, Polargeist, StereoMadness (en el proyecto era Nivel 4, nombre histórico, NO el oficial) y WaveVibe.

- Referencia primaria: video entregado por el usuario de 95,52 s, cuadros revisados en tramos de 3 y 5 s, que muestran suelo, elevaciones, pinchos, pasarelas, dos fases de nave, tres monedas y meta.
- Mapa generado: **825 unidades**, ~85–86 s al desplazamiento actual, **489 objetos**: 143 pinchos, 250 bloques, 71 plataformas pequeñas, 17 decoraciones, cuatro portales (2 Ship + 2 Cube), 3 monedas colectables y marcador final.
- Secuencia de modos en el mapa: **Cube (0–245), Ship (245–381), Cube (381–724), Ship (724–786), Cube (786–825)**. Los valores son **aproximados** mediante observación del video, no extraídos del GMD oficial.
- Música `StereoMadness` cargada de `Resources/Songs/Musics` tal como ya existía en el proyecto, sin distribuir audio nuevo.
- Las 3 monedas usan `biofitStereoCoin` como trigger físico con contador para ese intento y un recurso procedural del proyecto. Se muestra un HUD `MONEDAS N / 3`. Como el juego original reaparece tras morir sin reconstruir la escena, al detectar un personaje nuevo se restaura el contador a cero y se reactivan las monedas recogidas durante el intento anterior. **La ubicación y sus rutas no están certificadas frente al original.** Evitar presentar esto como 3 monedas idénticas.
- El JSON y su fuente están sincronizados mediante `node tools/assets/generate_stereo_video_map.js --check`. Para regenerar: `node tools/assets/generate_stereo_video_map.js`. Los IDs/posiciones/grupos son de una reconstrucción inferida del video.
- `BiofitThreeModesBuild.VerifyMaps()` ahora verifica la estructura nueva del Nivel 1 y aplica los invariantes históricos de 575 unidades y su dificultad a niveles 2–5. El nuevo mapa no tiene el espaciado clínico de 1,5 s entre pinchos del antiguo Nivel 1; **no fue validado en saltos corporales** y la dificultad real podría ser significativamente mayor.

**Límite de exactitud:** la copia de GDBrowser `https://gdbrowser.com/77236587` informa **2.291 objetos**. Para reproducir exactamente el original hace falta el *level object string*, los IDs/propiedades/color/capas/colisiones de todos ellos, sprites equivalentes y un ajuste de escala de ~30 píxeles GD por unidad. Un video, incluso completo, NO garantiza recuperar esos objetos ocultos y su editor.

## 5. Herramienta preparada para el origen de datos exactos

`tools/assets/fetch_stereo_reference.py` permite descargar mediante **POST de lectura** la copia pública `77236587` desde `https://www.boomlings.com/database/downloadGJLevel22.php`, siguiendo el protocolo documentado por [GD Docs](https://boomlings.dev/resources/server/level). Extrae el campo `4` de la respuesta, decodifica Base64URL + gzip/zlib y recopila pares de objeto `1` = ID, `2` = X, `3` = Y y `6` = rotación. Escribe `Builds/stereo_reference_objects_77236587.json` **fuera de Git** y reporta cuántos IDs requieren mapear. Si la API está caída o bloquea tráfico, permite `--source ruta.txt`.

```powershell
python .\tools\assets\fetch_stereo_reference.py
# o con datos de nivel proporcionados por el editor:
python .\tools\assets\fetch_stereo_reference.py --source .\nivel_original.txt
```

**NO sustituye automáticamente el Nivel 1** porque sería incorrecto ignorar cientos de objetos sin soportar. Esta herramienta no se ejecuta durante el build ni CI, así que no depende de una API externa. Su parser gzip/objeto sí tiene test offline.

**La bibliotecas de arte ya catalogadas** en `docs/ASSET_SOURCES.md` son `https://github.com/kenzieshane/Assets`, `https://github.com/Weebifying/gd-textures` (referencia RobTop, no redistribuir) y `https://github.com/shorepine/kenney`/Kenney oficial CC0 (uso y redistribución permitidos cuando se verifica cada pack). Se usó arte procedural y prefabs del submódulo existente en vez de añadir PNG propietarios al GitHub.

## 6. Validación y preparación del proyecto Windows

**CI aplicable**: `tests/check_stereo_ship_vrbox.py`, `tests/test_stereo_source_parser.py`, `tests/check_three_modes_60s.py` adaptado sin rebajar los tests de 2–5, `tests/check_joystick_diagnostic.py`, `tests/check_geometry_original.py`, `GeometryUxSyntax` (Roslyn), validación de PowerShell y resto de regresiones, compatibilidad Windows/Android-export, simulación de firmware.

**No son equivalentes** a Editor Unity ni APK Android ni prueba física. Pendientes:
- Abrir proyecto en Unity 6000.3.28f1 (Editor no disponible en CI) y verificar Console.
- Validar sprites/colliders de `tile`, `smallTile`, `spike`, `biofitShipPortal` y `biofitStereoCoin` tras preparación del submódulo.
- Compilar APK con comando del menú, instalar en Android y comprobar ritmo del Nivel 1 en Cube/Ship (salto mantenido, despegue, descenso, pausa, muerte y respawn).
- Verificar que no se rompan los otros 4 niveles, Ball/Spider, selección de skin, música ni cooldown 900/800/700/600/500/400 ms.
- Probar cada botón A, B, C, D, R1, R2 en modo CAMBIO+C; presionar 1 vez, mantener >2 s y combinar BLE.
- Cuantificar acciones repetidas, salto permanente al mantener, latch de Ship por BLE, desincronización música, colisiones y FPS Android.
- Verificar ubicación y capturabilidad de las 3 monedas y que no se salte por una pulsación al abrir/cerrar pausa.

**Instalación segura, sin sobrescribir la versión estable:**
```powershell
cd "C:\Users\Capo\Documents"
git clone --recurse-submodules --branch feat/stereo-madness-ship-vrbox-held-v1 https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git "BIOFIT-DASH-STEREO-SHIP-TEST"
cd "BIOFIT-DASH-STEREO-SHIP-TEST"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
```
Los cuatro PNG de `Assets/BIOFIT/Resources/BiofitUi` no se descargan desde GitHub. **La preparación habitual invoca automáticamente `tools/restore_local_biofit_ui.ps1`**, que ahora busca también el clon existente `BIOFIT-DASH-JOYSTICK-TEST`, `BIOFIT-DASH-NUEVOS-NIVELES` y `BIOFIT-DASH-COOLDOWN-EXPERIMENTAL`; copia los cuatro PNG únicamente si están completos, conservando el proyecto fuente y verificando SHA-256. Si no los encuentra, se puede usar `tools/install_biofit_ui.ps1` con el ZIP privado; no alterar `main`. El `Skins.png` podría versionarse por un push local separado autorizado, si se ejecutó efectivamente. **No afirmar que los gráficos están ya en este branch sin inspeccionarlo**.

Compilación en Editor:
`BIOFIT → GeomeTry ORIGINAL → 8. Verificar niveles 3 MODOS (60 s)` (nombre heredado; el Nivel 1 ahora supera 60 s), y luego `9. Compilar BIOFIT 3 MODOS (BLE LEGACY)`.
APK generada bajo `Builds` con el mismo identificador `com.biofitgame.threemodeslab`; guardar APK estable antes de instalar experimental.

## 7. Estado de entrega

**En rama nueva, pruebas estáticas e integración de host en ejecución / completadas según GitHub Actions.** Mantener PR **DRAFT**, no fusionar a `main` sin validación del usuario. No descalificar el proyecto estable que ya tenía validación física de cooldown 400ms; el cambio experimental tiene riesgos reales de geometría y avión.

**Trabajo futuro explícito para poder afirmar reproducción 1:1 de Stereo Madness:** obtener cadena original completa con la herramienta anterior, mapear ID→prefab para todos los objetos relevantes (2.291), normalizar coordenadas GD a Unity y los offsets/rotaciones/capas, reproducir el layout incluyendo rutas de monedas y portales, y comparar un video de APK con el MP4 de referencia. Hasta completar ese proceso, el nivel se llama **reconstrucción experimental**, no réplica exacta.
