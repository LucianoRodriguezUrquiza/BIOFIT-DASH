# BIOFIT DASH — CONTEXTO ACTUAL (rama principal)

**Referencia funcional:** Cube / Ball / Spider; cinco mapas propios de ~60 s; dos trackers tobillo ESP32-S3 QMI8658; Android Unity 6000.3.28f1; entrada BLE **BILATERAL LEGACY**. Fecha de consolidación: 22-23/11/2026.

> Este es el documento canónico de la versión principal; la auditoría histórica detallada queda en el historial Git. No utilizar MOV HOLD ni CICLO EXP como control principal.

## 1. Arquitectura y elección de control

- Dos dispositivos Waveshare ESP32-S3-LCD-1.47B, con IMU QMI8658, en tobillos, transmiten eventos de movimiento por BLE a Android.
- Receptor general: `Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs`. Emparejamiento de impulsos: `BiofitBilateralJumpDetector.cs`, alineación de relojes: `BiofitClockAligner.cs`.
- En GeomeTry el puente `Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs` escucha `BilateralJumpCandidate`. Cuando dos impulsos UP válidos llegan en una ventana de hasta 180 ms y pasan el cooldown de 900 ms, emite **una acción de juego**.
- Umbral UP de Unity: 0,12 g; ventana bilateral 180 ms; cooldown 900 ms; el rearmado por tobillo tiene su propio refractario. Estos valores NO se modificaron con Tres Modos. 900 ms es tiempo de bloqueo de pares, NO una latencia de la radio BLE.
- En Cube se ejecuta el `Cube.onClick()` original. En Ball, `BiofitModeMotor.TryPulse()` invierte la gravedad desde una superficie; en Spider teletransporta a la superficie contraria y agrega línea/flash breve. Mismo evento BLE, distinta acción según modo.
- Movimiento horizontal original `DefaultSpeed=6.5`, `Time.timeScale=1.5` => ~9,75 unidades de mundo por segundo real; física y `jumpForce=13.5` del Cube original NO cambiados. Cada nivel termina en marcador x=575 (~60,1s nominales sin morir).
- En Android se solicita un objetivo de 60 FPS. No confundir FPS solicitados con FPS medidos.

## 2. Los cinco niveles vigentes

| Orden de interfaz | JSON nuevo | Modo de juego | Portales automáticos |
|---|---|---|---|
| 1 | BackOnTrack.json | Cube | ninguno |
| 2 | JumpyNightmare.json | Ball | ninguno |
| 3 | Polargeist.json | Spider | ninguno |
| 4 | StereoMadness.json | Cube → Ball → Spider → Cube | x180, x380, x515 |
| 5 | WaveVibe.json | Cube → Ball → Spider → Cube → Ball | x110, x276, x404, x515 |

Los cinco JSON están en `Assets/BIOFIT/Resources/BiofitThreeModes/`; el archivo `ground` x=575 es marcador de fin. En Cube se requiere un único salto por pincho, sin encadenar saltos subsegundo. En Ball y Spider, los niveles contienen suelo/techo y amenazas alternadas. Las distancias nominales entre pinchos son al menos 38u (~3,9s), 52u (~5,3s), 46u (~4,7s), 45u (~4,6s) y 38u (~3,9s) respectivamente.

No se afirma que el cálculo de la distancia entre pinchos certifique cada ventana de entrada, choque o cambio de portal. La prueba real de jugabilidad sigue siendo necesaria.

## 3. Estructura del proyecto y los recursos gráficos

- Repositorio Git principal: `BIOFIT-GAME-UNITY`, rama `main`.
- Fuente tercera: submódulo Git `ThirdParty/GeomeTryOriginal` apuntando al commit **d922caddb4d21ecb791a266143e279bdb969d42c** de `Christ0u/GeomeTry`.
- `tools/prepare_geometry_original.ps1` copia sus Assets a `Assets/GeomeTryOriginal/Assets/` y aplica compatibilidad Unity 6: arregla tres archivos PNG codificados originalmente como WEBP y referencias a la cámara. El submódulo permanece sin modificar. Esa copia generada es ignorada por Git y debe recrearse en un clon nuevo.
- Recursos heredados: `Assets/GeomeTryOriginal/Assets/Resources/Sprites/`, `Resources/Prefabs/`, `Resources/UI/`, `Resources/Songs/`. GeomeTry aporta Cube, Ship, Wave, suelo, techo, pinchos, menús y música. Ship y Wave no son modos activos de los cinco niveles nuevos.
- UI BIOFIT: `Assets/BIOFIT/Resources/BiofitUi/` necesita `LevelSelect.png`, `Skins.png`, `Back.png`, `Pause.png`. Esta carpeta se excluye de Git por ser un pack gráfico externo/local de derechos de redistribución no aclarados. **Una descarga nueva en otra máquina NO lo incluye.** El preparador ejecuta `tools/restore_local_biofit_ui.ps1` para copiar los archivos automáticamente desde un proyecto anterior en la misma carpeta padre, si existe. Si no existen, informa cómo aportarlos.
- Ball, Spider y portales usan sprites propios generados en tiempo de ejecución por `BiofitThreeModeArt`. Se pueden sustituir por PNG importados como Sprite en `Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/`, nombrados `IconBall.png`, `IconSpider.png`, `PortalBall.png`, `PortalSpider.png`, `PortalCube.png`, siempre que se tengan derechos de uso. Los PNG originales de Geometry Dash no se incluyen.
- UI principal: `Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs`; adaptador de modos: `Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs`.
- Registración CSV, exportación Android y diagnósticos siguen disponibles. Firmware **no incluido** en este repositorio: `LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware` es otro proyecto y no se modifica por estos cambios.

## 4. Preparar un clon nuevo y compilar

**NO ejecutar prepare_geometry_original.ps1 sobre un proyecto ya preparado:** verifica y rechaza esa condición; no es un instalador incremental.

```powershell
cd "C:\Users\Capo\Documents"
git clone --recurse-submodules https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-DASH-PRINCIPAL
cd ".\BIOFIT-DASH-PRINCIPAL"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
```

Con Unity 6.3 LTS 6000.3.28f1, abrir **esa carpeta** en Unity Hub. Ejecutar:
1. `BIOFIT > GeomeTry UX > 1. Configurar modulo tactil Android`.
2. `BIOFIT > GeomeTry UX > 2. Verificar UX y recursos`.
3. `BIOFIT > GeomeTry ORIGINAL > 8. Verificar niveles 3 MODOS (60 s)`.
4. `BIOFIT > GeomeTry ORIGINAL > 4. Activar URP original para probar en Play`; abrir menú opción 2 y probar niveles con mouse/tecla Espacio.
5. Para Android: activar Android en Build Profiles y seleccionar `BIOFIT > GeomeTry ORIGINAL > 9. Compilar BIOFIT 3 MODOS (BLE LEGACY)`.

APK separada: package `com.biofitgame.threemodeslab`, nombre `BIOFIT TRES MODOS`; resultado en `Builds/`. El menú 3 pertenece a la compilación original de GeomeTry, no a la versión principal Tres Modos.

## 5. Validación y límites actuales

- Usuario confirmó funcionamiento de menús y mecánicas de Tres Modos **en Unity Editor**. No extrapolar a APK/Android BLE: quedan por validar sensores reales, sincronización, repetición tras muerte, fallas de teletransporte, techo/colliders y 60 FPS.
- SPIKE LAB anterior se probó físicamente con los dos trackers y un CSV. Detectó la funcionalidad general de LEGACY y permitió analizar rechazo por cooldown; NO se concluyó latencia BLE absoluta. El escenario SPIKE LAB ya no forma parte de la compilación principal.
- Prueba estática de escenarios: `python tests/check_three_modes_60s.py`. Sintaxis C# y pruebas de módulos vía GitHub Actions; no equivalen a ejecutar Unity ni medir saltos físicos.
- Optimización de latencia BLE pendiente. No subir la prioridad de GATT ni modificar cooldown/umbrales/firmware sin una prueba A/B controlada.
- Limpiar ramas remotas no elimina ni mejora los archivos de Unity. Las carpetas de trabajo existentes en Windows no deben borrarse por mantenimiento Git.

## 6. Restricciones del proyecto

**No modificar sin prueba explícita:** motor de salto Cube original, velocidad, gravedad, fuerza de salto, firma de eventos, parámetros de BILATERAL LEGACY, firmware ESP32/QMI8658, NVS, UUID BLE, configuración Android de captura y exportación. No activar MOV HOLD, FASE EXP o CICLO EXP en el juego.

Prioridad: pulir y validar físicamente la experiencia Cube/Ball/Spider de los cinco niveles; después, medir y optimizar latencia de forma independiente. Mantener la rama principal `main` como única referencia pública de trabajo.
