# BIOFIT GAME — cuatro recorridos con BILATERAL LEGACY

Base: Unity v0.9.11 `5616fc4571193d4a2d076550a909089344144384`, PR17.
Rama nueva: `feat/biofit-geometry-four-levels`. Firmware identificado:
v2.2.9 `274625f605ea92189abfb16275012f34ee14371f`, sin modificar.
El usuario confirmó que LEGACY respondió mejor físicamente que CICLO EXP.
Esta entrega usa exclusivamente LEGACY en el juego; conserva la investigación
anterior en su código/ramas, sin activarla ni requerirla.

## Qué está implementado

- Escena independiente `Assets/BIOFIT/Geometry/Scenes/BiofitGeometry.unity`.
- Menú de cuatro niveles, avance, cámara, cubo Rigidbody2D, obstáculos sólidos,
  pinchos, pads automáticos, orbes opcionales de un uso, muerte, pausa,
  reintento, final de nivel y regreso al menú.
- Entrada BLE legacy, ESPACIO, toque en pista y botón SALTAR. Ninguna mantiene
  una tecla virtual. Sin sensores se puede jugar manualmente.
- El mismo `BiofitBLEMonitor` vive durante selección, muerte y cambios de nivel.
  No se carga otra escena ni se recalibra al cambiar el recorrido.
- Registro principal/RAW, SYNC, archivo privado, historial, Downloads y SAF
  reutilizan las implementaciones anteriores. No se borran capturas.
- Menú Editor para abrir, validar con física Unity y construir UNA APK.
  La compilación usa solamente la escena nueva y no reemplaza Build Profiles,
  escenas existentes, identificador, firma ni versionCode.

## GeomeTry y alcance de la adaptación

Fuente fijada: `Christ0u/GeomeTry@d922caddb4d21ecb791a266143e279bdb969d42c`.
Se inspeccionaron Character, Cube, Game, Level, MapItem, Prefab, JumpOrb,
JumpPad, Portal, PauseManager, escena Level, prefabs Cube/Tile/Spike y
configuraciones físicas. Referencias y licencia en `ThirdParty/GeomeTry`.

**No es una importación fiel de los mapas comerciales ni un port de todos los
prefabs.** Los originales incluyen vuelo, plataformas elevadas y secuencias
de pulsaciones aéreas incompatibles con la cadencia legacy. Se conservaron
los cuatro JSON originales fuera de Assets, con hash y sin modificarlos.
`tools/adapt_geometry_maps.py` transforma intervalos consecutivos de cada mapa
en desafíos de cubo de dimensiones acotadas, conservando orden y motivos.
Es una simplificación sustancial y explícita del recorrido, no solo un cambio
de controles. Cada objeto original figura exactamente una vez en
`sections[].sourceIndices`; `adaptation` describe las conversiones.

| Catálogo interno | Fuente | Tramos | Objetos jugables | Duración teórica sin morir |
| --- | --- | ---: | ---: | ---: |
| 1 | StereoMadness.json | 53 | 53 | 66,4 s |
| 2 | BackOnTrack.json | 29 | 38 | 37,6 s |
| 3 | Polargeist.json | 39 | 55 | 49,6 s |
| 4 | JumpyNightmare.json | 13 | 20 | 18,4 s |

Los IDs de origen repetidos (Back on Track y Polargeist: 2) no controlan el
catálogo. Wave Vibe no se incluye. Portales de nave se convierten en tramos de
cubo; plataformas altas se reducen; cadenas densas se condensan; pads usan
impulso 13,5; orbes son opcionales y necesitan una orden nueva durante contacto.
No se necesita ninguna orden en vuelo para completar los recorridos adaptados.
Gráficos geométricos propios, sin música ni sprites comerciales. Licencia MIT
del código GeomeTry incluida en repositorio y pantalla CRÉDITOS. La declaración
MIT del repositorio no certifica derechos sobre material comercial externo;
ese material no se incorpora como assets a la aplicación.

## Entrada y tiempos

`UP_IMPULSE → filtros existentes → BiofitBilateralJumpDetector →
LegacyBilateralCandidateTimed(gapMs, pairedSeconds) → GeometryJumpCommands →
GeometryCube.TryJump → Rigidbody2D`.

La nueva notificación es adicional y solo legacy; no cambia
`BilateralJumpCandidate` ni sus consumidores previos. Su tiempo es el del
impulso más tardío del par alineado (no el último paquete recibido, ni gapMs).
El detector solo añade una propiedad de lectura; su algoritmo y constantes
se verifican contra los bytes de v0.9.11.

Cada orden tiene ID creciente, fuente, nivel e intento. Se consume una vez,
también si es rechazada. Se ejecuta inmediatamente si el cubo puede saltar;
en vuelo se rechaza salvo contacto con un orbe disponible. **No hay buffer de
aterrizaje en este controlador**, ni cola de saltos pendientes. El buffer de
BIOFIT DASH sigue intacto. Se conserva la frescura de 250 ms del receptor y
la tolerancia del reloj estimado, usando Stopwatch independiente de timeScale.
Pausa, muerte, reinicio, diagnóstico, menú y cambio de época invalidan entradas.
No se transportan órdenes a otro intento ni se repite el salto al mantener tecla.

Los CSV usan filas DIAG compatibles: GEOMETRY BLE_CANDIDATE, COMMAND_CREATED,
COMMAND_RESULT (Executed, ExecutedOrb, Airborne, Expired, Inactive...),
INPUT_CANCEL, AUTO_PAD, LEVEL_START, DEATH, PAUSE, RESUME y VICTORY.
Un pad automático se registra aparte: no cuenta como gesto humano ni orden BLE.
El conteo de órdenes tampoco mide exactitud biomecánica.

Física: velocidad horizontal 6,5; impulso 13,5; gravedad local 39,24 unidades/s²;
paso fijo 0,02 s; timeScale=1 (GeomeTry usaba 1,5). El Rigidbody recibe velocidad,
sin moverlo con transform.Translate. Gravedad local aplicada en FixedUpdate
para no modificar Physics2D global. Material sin fricción/rebote, detección
continua; giro visual separado del collider. Vuelo balístico ideal 0,688 s;
la integración discreta y colisiones pueden variar ese valor. Separación entre
exigencias ≥7,8 unidades/6,5 = 1,2 s. No se alteran 0,25/0,25/0,12 g,
180 ms, refractario 320 ms o cooldown 900 ms.

**Límite conservado:** pares legítimos cada 0,8 s no producen cinco órdenes;
en secuencia sintética limpia se aceptan tres de cinco. A 0,9/1,0/1,2 s se
aceptan cinco cuando el personaje está disponible. Un segundo impulso a 400 ms
se rechaza; a 1 s puede ser indistinguible de un movimiento nuevo. El adaptador
garantiza una ejecución por orden, no una orden por movimiento humano.

## Verificaciones reales y pendientes

Ejecutado en host Linux con .NET SDK 8.0.408:

```
dotnet run --project tests/GeometryControl
dotnet run --project tests/GeometryRuntimeSurface
dotnet run --project tests/LatencyRegression
dotnet run --project tests/ReceiverRegression
dotnet run --project tests/AndroidDownloadsRegression
dotnet run --project tests/DashUiRegression
dotnet run --project tests/LiftCycle
dotnet run --project tests/JumpControl
dotnet run --project tests/PhaseAudit
dotnet run --project tests/RawRegression
dotnet run --project tests/ArchiveRecovery096
python3 tools/adapt_geometry_maps.py --check
python3 tests/check_geometry_assets.py
python3 tests/JumpControl/compare_cooldowns.py
python3 tests/check_android_export.py
python3 tests/PhaseAudit/replay_firmware.py ../firmware-audit-v227
```

GeometryControl: **1.512 comprobaciones**, 24 recorridos completos sintéticos
con Euler/AABB conservador, dt 10/20 ms y marcas de salto ±60 ms. Incluye
cadencia real del detector, deduplicación, expiración, epochs, aire y origen
temporal. GeometryRuntimeSurface compila todos los nuevos scripts runtime y
receptor contra **shims**, no assemblies del Editor; además ejecuta 7 pruebas
de la UI real (paneles modales y captura privada íntegra). Contratos de assets,
GUID, originales y preservación: 167. ReceiverRegression: 52;
AndroidDownloads: 16 mocks; DashUI: 86; LiftCycle: 157; JumpControl: 258;
PhaseAudit: 22; RAW: 36; ArchiveRecovery: 79.016 comprobaciones, muchas por fila.
Export source, comparación de cooldown y replay firmware sintético: PASS.
Las pruebas de CICLO se mantienen solo como regresión del código histórico.

El primer ensayo nuevo mezcló señales de frenado con cadencia 0,8 s: mostró
la ambigüedad temporal legacy; se separó la prueba de cadencia limpia de la
prueba de lóbulos y se conservó un contraejemplo explícito a 1 s. No se cambió
el detector para ocultar esa limitación. Un script host inicialmente no halló
dotnet en PATH; se repitió con la ruta del SDK y pasó.

**NO ejecutado:** importación/compilación Unity, Play Mode, build APK, integración
Android real, radio BLE, latencia gesto-pantalla ni prueba física nueva. No hay
Unity Editor/Android Build Support disponibles en este entorno. No se generó
una APK ni se flashearon trackers. Un PR borrador con CI verde no equivale a
haber completado esas validaciones.

`GeometryPlayValidation` está preparado para ejecutar 24 recorridos con
Rigidbody2D, colliders y callbacks Unity reales en escenas físicas aisladas,
durante Play. Falla explícitamente si no despega, colisiona o no llega al final.
No sustituye un ensayo Android ni prueba sensores. La UI y la carga de escena
también necesitan inspección en Editor antes de compilar la candidata.

## Una preparación local y una APK

1. Cerrá Unity y copiá **la carpeta completa de tu proyecto local** a otra
   ubicación, con Assets, .meta, Packages, ProjectSettings y los nueve archivos
   locales. No descartes cambios ni uses reset/clean. Este respaldo de tu PC
   no está incluido en el trabajo remoto.
2. En GitHub Desktop, usá la **copia** del proyecto (File > Add local repository).
   Fetch origin; si no tiene cambios pendientes, elegí
   `feat/biofit-geometry-four-levels`. Si muestra cambios locales, conservá ambos
   estados y llevá esos cambios a la nueva rama mediante la opción de Desktop
   de conservarlos; no reemplaces archivos en conflicto automáticamente.
   La referencia v0.9.11 permanece en su rama original.
3. Unity Hub > Add project from disk > copia del proyecto > **6000.3.28f1**.
   Esperá la importación. BIOFIT > Geometry > **1. Abrir juego**.
4. Play. Comprobá menú, ESPACIO/toque, pausa y reintento. Ejecutá
   BIOFIT > Geometry > **2. Validar niveles y física (en Play)**; debe informar
   24 recorridos PASS en Console. Si falla, guardá el error y no compiles una
   candidata que el validador ya declaró fallida. No hace falta reinstalar nada.
5. Detené Play. File > Build Profiles > Android > Switch Platform. Conservá
   identificador y firma usados en tu APK instalada; revisá que versionCode
   no sea inferior al instalado. No uses el menú viejo de CICLO EXP.
6. BIOFIT > Geometry > **3. Compilar APK cuatro niveles LEGACY**. La salida es
   `Builds/BIOFIT_GEOMETRY_LEGACY_<fecha>.apk`, sin sobrescribir APK anteriores.
   Instalá como actualización con firma/paquete compatibles, **sin desinstalar**.

## Una sesión corta en el teléfono

1. Abrí la app con ambos trackers y su firmware/calibración actuales. Esperá
   **Sensores 2/2** y **Relojes listos**. Modo fijo **BILATERAL LEGACY**.
2. REGISTRO > **1. INICIAR** > **RAW: OFF** (esperar ON y muestras I/D).
   **SYNC VIDEO** si filmás; CERRAR.
3. Elegí **Jumpy Nightmare · BIOFIT**, el recorrido más corto, o Stereo Madness
   para comenzar. Realizá movimientos bilaterales separados al pasar por las
   marcas amarillas. El juego pide aproximadamente 1,2 s entre desafíos; los
   pads amarillos saltan automáticamente. No contarlos como comandos humanos.
4. Comprobá una muerte/reintento o pausa/continuación y regreso a NIVELES;
   los sensores deben seguir conectados. No es necesario repetir ocho bloques
   ni completar los cuatro recorridos físicamente en esta primera comprobación.
5. REGISTRO > **2. FINALIZAR**. Esperá cierre y verificación; **3. ARCHIVOS**.
   Conservá principal y RAW. Si Downloads falla, **GUARDAR CON SAF**. Históricos:
   SESIONES ANTERIORES > elegir archivo > EXPORTAR SELECCIÓN, sin grabar otro.
6. Evaluar por separado candidato BLE, orden, ejecución, pad, rechazo y movimiento
   humano observado. Si hay fallos, analizar esa captura y su tramo; no reajustar
   sensores ni iniciar otra batería extensa por defecto.

## Preservación

No se modifican main, ramas funcionales anteriores, PR18 cancelado, firmware,
NVS, calibración, UUID, protocolo, orientación, package manifest ni archivos
privados del teléfono. Las escenas/configuraciones locales del usuario no son
accesibles desde este entorno. El contexto maestro se actualiza por separado
en `docs/contexto-maestro`, conservando historial y decisiones anteriores.
