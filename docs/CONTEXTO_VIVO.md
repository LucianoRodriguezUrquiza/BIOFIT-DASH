# BIOFIT DASH — CONTEXTO VIVO / RELEVO ENTRE CONVERSACIONES

> **Leer primero al iniciar un chat nuevo.** Este documento es el punto de entrada operativo y se actualiza con cada cambio **confirmado** del proyecto. No sustituye al código fuente ni a las pruebas físicas.

> **ADVERTENCIA: ESTE ARCHIVO EN LA RAMA `feat/configurable-bilateral-cooldown-400-900ms` CONTIENE TRABAJO EXPERIMENTAL.** El `main` estable sigue siendo el PR #28 con 900 ms; todavía no se ha fusionado ningún cooldown configurable.

## Experimento activo — cooldown BILATERAL LEGACY desde Configuración (2026-11-23)

- **Objetivo:** permitir `900 / 800 / 700 / 600 / 500 / 400 ms` desde Configuración → Cooldown, con botones animados, un solo valor seleccionado, botón de restablecer, indicador `CD: NNN ms` durante los niveles y persistencia entre cierres de APK. **Predeterminado: 900 ms; los demás, experimentales.**
- **Estado inicial:** PR #28 estable, 900 ms fijo en `BiofitBilateralJumpDetector.PairCooldownSeconds`; detector usa `pairTime = Max(tizq,tder)` sobre reloj compartido y rechaza cuando intervalo entre pares aceptados es menor que el cooldown. El puente `BiofitGeomeTryLegacyBridge` recibe `BilateralJumpCandidate`; UI `BiofitGeometryUX` ya contiene botón animado y almacenamiento `PlayerPrefs` para skins.
- **Código examinado:** `BiofitBilateralJumpDetector.cs`, `BiofitClockAligner.cs`, `BiofitBLEMonitor.cs`, `BiofitGeomeTryLegacyBridge.cs`, `BiofitThreeModes.cs`, `BiofitGeometryUX.cs`, `BiofitSettingsButtonFeedback.cs`, `BiofitEventCapture.cs`, proyectos host y workflow CI.
- **Archivos funcionales modificados:** `Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs`, `BiofitBLEMonitor.cs`, `Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs`. **Nuevo:** `Assets/BIOFIT/Scripts/BiofitCooldownConfiguration.cs` y su `.meta`.
- **Decisión de arquitectura:** la propiedad `PairCooldownMilliseconds` del detector es la autoridad; `BiofitCooldownConfiguration` carga, valida y guarda una sola preferencia mediante `PlayerPrefs`. La UI lee y modifica el detector **a través de** `BiofitBLEMonitor`; no hay segundo temporizador, reconexión ni nueva escena. Ante un cambio de 900 a 400 ms durante un bloqueo, **no se acorta** el bloqueo iniciado; aumentar el valor sí puede extenderlo. El siguiente par aceptado adopta el valor elegido.
- **Parámetros:** cooldown seleccionable 900/800/700/600/500/400 ms; 900 ms por defecto. Sin cambios en umbral 0,12 g, ventana 180 ms, refractario por tobillo 320 ms, firmware, BLE, fases experimentales, colisiones, física o música.
- **Diagnósticos:** `COOLDOWN_MS=NNN` en inicio de captura, cambios de selección y decisiones bilaterales; `COOLDOWN_MS_FINAL` en resumen; se mantiene el formato CSV existente para evitar romper análisis/replay.
- **Pruebas añadidas:** `tests/CooldownRegression` (fronteras de seis valores, cambios durante bloqueo, duplicados/rebotes, un solo tobillo, datos inválidos, persistencia simulada/restablecer); incorporada a `.github/workflows/latency-regression.yml`. Compatibilidad de los harness `ReceiverRegression` y `DashUiRegression` preparada con shim PlayerPrefs. **Resultado verificado:** GitHub Actions [run 45276413840](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45276413840) finalizó con las 5 tareas en verde; `tests/CooldownRegression` compiló y pasó **251 comprobaciones**. Son pruebas host/estáticas, no Unity/Android.
- **Pruebas todavía pendientes:** Unity Editor, APK Android, seis ajustes físicos, visualización en diferentes resoluciones, persistencia real Android y diagnóstico de duplicados de un mismo salto a 400 ms.
- **Regresión/riesgo conocido:** con un cooldown corto el detector puede aceptar un segundo par UP generado por el **mismo salto físico** pasados 400 ms. El detector temporal no distingue por sí solo una nueva acción humana; no se garantiza fiabilidad física sin ensayos.
- **Rama y PR:** `feat/configurable-bilateral-cooldown-400-900ms` (base `main` documental `8d4ec590...`). **[PR #29 en borrador](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/29). No fusionar a `main` sin aprobación explícita.**
- **Incidencias y correcciones de CI:** (1) la verificación antigua de bytes idénticos del monitor/detector bloqueaba un cambio legítimo: se reemplazó por controles específicos de UUID, relojes, física y parámetros; (2) el test 400-100 ms coincidía con el refractario de tobillo 320 ms: se cambió el caso temporal aislado a 400-50 ms; (3) el contrafactual histórico 750 ms modificaba la constante ya no operativa: ahora altera solo el default de una compilación temporal, manteniendo 750 ms **fuera** del selector; (4) se añadió la nueva clase al harness de RawRegression. Todas las regresiones cerraron en verde.
- **Commits principales:** detector `d18d286`, UI `363e597`, persistencia `b305cb6`, test `3d85b67`, pruebas de compatibilidad y correcciones posteriores; estado comprobado mediante Actions en `7c8d89de`.
- **Documento completo y procedimiento experimental:** [COOLDOWN_BILATERAL_CONFIGURABLE.md](COOLDOWN_BILATERAL_CONFIGURABLE.md).
- **Próximo paso:** consultar CI, inspeccionar el diff, probar en un **clon independiente**; conservar intacto `C:\Users\Capo\Documents\BIOFIT-DASH-NUEVOS-NIVELES`.


>
> **Última actualización verificada:** 2026-11-23.
> **Repositorio:** https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY
> **Rama de referencia:** `main`.
> **Estado fuente:** [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md) · [Tres modos y niveles](TRES_MODOS_60S_BILATERAL.md) · [Recursos](ASSET_SOURCES.md).
> **Historial extenso (no vigente):** [CONTEXTO_MAESTRO_HISTORICO.md](CONTEXTO_MAESTRO_HISTORICO.md).

## REGLA PRIORITARIA E INNEGOCIABLE — CONTROL BLE

**La prioridad absoluta de BIOFIT GAME es conservar operativo y fiable el detector BLE BILATERAL LEGACY.** Si un cambio visual, de niveles, interfaz o rendimiento compromete la detección o la ejecución de movimientos, se debe **restaurar el funcionamiento de BILATERAL LEGACY antes que conservar dicho cambio**.

**Línea de base actual (no modificar sin ensayo controlado):** umbral UP 0,12 g, ventana bilateral 180 ms, cooldown entre pares aceptados **900 ms**, y lógica de firmware/recepción sin modificaciones.

**Idea futura, NO aprobada ni implementada:** evaluar bajar el cooldown de **900 ms a 600 ms**. Solo mediante rama experimental, copia de seguridad, comparación A/B con ambos tobillos, medición de falsos positivos, pares reconocidos, saltos efectivos y estabilidad en Unity/Android. No confundir cooldown con latencia BLE. Si empeora fiabilidad o estabilidad, regresar a 900 ms. **La versión estable mantiene 900 ms hasta que las mediciones justifiquen el cambio.**

## 1. Resumen del proyecto (conocido y confirmado)

BIOFIT GAME es una plataforma de exergaming. Su primer juego, BIOFIT DASH, se desarrolla con **Unity 6.3 LTS 6000.3.28f1** para Android. Dos trackers Waveshare ESP32-S3-LCD-1.47B con IMU QMI8658, montados en los tobillos, envían eventos por BLE. Unity utiliza **BILATERAL LEGACY** para activar una acción del personaje.

Modos activos:
- **Cube:** salto original de GeomeTry.
- **Ball:** inversión de gravedad desde una superficie.
- **Spider:** teletransporte a la superficie opuesta.
- Ship y Wave no forman parte de los cinco niveles nuevos.

Hay cinco mapas JSON en `Assets/BIOFIT/Resources/BiofitThreeModes/` (duración nominal aproximada de 60 s):
1. `BackOnTrack.json` — Cube.
2. `JumpyNightmare.json` — Ball.
3. `Polargeist.json` — Spider.
4. `StereoMadness.json` — Cube → Ball → Spider → Cube.
5. `WaveVibe.json` — Cube → Ball → Spider → Cube → Ball.

El control vigente mantiene **umbral UP 0,12 g, ventana bilateral 180 ms y cooldown 900 ms**. Los 900 ms son un intervalo refractario, **no** una medición de latencia BLE. La física del Cube y estos parámetros no deben cambiarse sin una prueba A/B explícita.

## 2. Evidencia y límites de lo que está validado

- **Probado en Unity Editor (comunicado por el usuario, 2026-11-23):** el proyecto de cinco niveles abre, compila y funciona en Play.
- **Probado en Android con ambos sensores físicos (comunicado por el usuario, 2026-11-23):** APK generada e instalada; los movimientos corporales controlan satisfactoriamente el juego con BLE BILATERAL LEGACY. El usuario decidió adoptar esta versión como referencia estable y fusionarla en `main`.
- **No medido cuantitativamente:** tasa de falsos positivos/negativos, latencia real, FPS, prueba formal prolongada de transiciones, colisiones y respawn. Que funcione bien en una prueba no demuestra todas estas métricas.
- **Pruebas automatizadas:** verificaciones estáticas y host en GitHub Actions; **no** sustituyen un ensayo Unity/Android real.
- **Firmware:** repositorio independiente: https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware. La versión realmente instalada en los ESP32 no está verificada por esta documentación.
- **Prohibido asumir como hecho:** que el detector entrega 900 ms de latencia; que los 60 FPS estén medidos; que una verificación CI en verde demuestre que el gameplay físico es fiable.

## 3. Recursos e instalaciones locales

- Proyecto Unity de trabajo en Windows: `C:\Users\Capo\Documents\BIOFIT-DASH-PRINCIPAL`.
- Biblioteca local de referencia: `C:\Users\Capo\Documents\BIOFIT-RECURSOS-GD`.
- Se catalogaron **194.750 registros** en el inventario local y se generó el catálogo HTML con buscador; el usuario confirmó que abre.
- **PR #26 fusionado a main (2026-11-23):** scripts PowerShell e inventarios ligeros bajo `tools/assets/` y `docs/assets/`. Las bibliotecas completas y las vistas HTML permanecen locales.
- Gráficos de Geometry Dash: consulta y pruebas locales; **no subir los recursos originales de terceros al repositorio público**. Registrar procedencia/licencia antes de redistribuir una APK.
- La interfaz local puede depender de `LevelSelect.png`, `Skins.png`, `Back.png`, `Pause.png` en `Assets/BIOFIT/Resources/BiofitUi/`; esos PNG están excluidos de GitHub.
- El clon `BIOFIT-DASH-PRINCIPAL` tenía modificaciones locales de Unity al cierre de la última sesión. **No ejecutar `git reset --hard`, `git clean -fd` ni sobrescribir sus assets sin antes revisar y respaldar.** Este documento no verifica que el estado local siga siendo el mismo.

## 4. Archivos clave para retomar trabajo

| Función | Ubicación |
| --- | --- |
| Estado técnico canónico | `docs/CONTEXTO_ACTUAL.md` |
| Puente BLE al personaje | `Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs` |
| Motor y arte provisional de tres modos | `Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs` |
| Monitor BLE | `Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs` |
| Mapas vigentes | `Assets/BIOFIT/Resources/BiofitThreeModes/*.json` |
| Catálogo y licencias | `docs/ASSET_SOURCES.md`, `docs/assets/README.md` |
| Descarga y clasificación local | `tools/assets/Preparar_Biblioteca_BIOFIT.ps1`, `tools/assets/Organizar_Catalogo_BIOFIT.ps1` |

## 5. Estado de la etapa actual

**Versión estable de referencia:** cinco niveles más exigentes `Nivel 1` a `Nivel 5`, seleccionables de izquierda a derecha, con Cube, Ball, Spider y secuencias de portales. **PR #28 fusionado en `main` el 2026-11-23**, commit de integración [`017eb093a670703591479228307817db51c8197b`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/commit/017eb093a670703591479228307817db51c8197b).

**Prueba funcional confirmada por el usuario:** ejecutó la versión en Unity Editor, construyó la APK e informó que en Android los movimientos físicos funcionan bien con los ESP32. Esta es la línea de base que **no se debe romper**. No se dispone de informe cuantitativo de falsas activaciones, latencia ni FPS.

**Restricciones:** mantener código de BLE BILATERAL LEGACY, umbral 0,12 g, ventana 180 ms, cooldown 900 ms, física original Cube y firmware intactos. La posibilidad de 600 ms es únicamente hipótesis futura para test A/B, no una configuración estable.

**Arte pendiente:** los iconos de Ball y Spider y los gráficos de sus portales **siguen siendo provisionales**; NO se importaron los sprites originales desde la biblioteca de Windows. El repositorio contiene soporte para importar PNG individuales en `Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/`, pero no incluye esos PNG. La biblioteca `BIOFIT-RECURSOS-GD` de unos 5,90 GB (~195.691 archivos) es local; el usuario contempla compartirla por Google Drive para seleccionar recursos. Comprobar licencia antes de publicar.

**Acción siguiente:** seleccionar y preparar arte (Ball, Spider y portales), idealmente desde un subconjunto accesible de Google Drive, en **rama separada**, verificar en Editor y Android, mantener `main` funcional. Nunca ejecutar reset o limpieza destructiva en `BIOFIT-DASH-PRINCIPAL` ni sobrescribir `BIOFIT-DASH-NUEVOS-NIVELES` sin verificar diferencias y respaldos. La APK compilada y los recursos locales excluidos no se guardan automáticamente en GitHub.

## 6. Registro cronológico resumido

| Fecha | Cambio verificado | Estado / evidencia |
| --- | --- | --- |
| 2026-11-22/23 | Base de Tres Modos y BLE BILATERAL LEGACY | Consolidación inicial; posteriormente se validaron los cinco niveles rediseñados |
| 2026-11-23 | Biblioteca de recursos local inventariada y catálogo HTML generado | El usuario confirmó catálogo funcional; recuento comunicado: 194.750 registros |
| 2026-11-23 | Scripts y documentación de recursos integrados | PR #26 fusionado, commit `17c92fcbdeb0c7d65b5610fcf649e089d7c9aced` |
| 2026-11-23 | Cinco niveles más exigentes y selector español: Android con sensores físicos | El usuario confirmó que la APK y los movimientos corporales funcionan; PR #28 fusionado, commit `017eb093a670703591479228307817db51c8197b` |

**Regla:** agregar una entrada SOLO cuando un cambio esté implementado o un ensayo se haya realizado. Distinguir `propuesto`, `en desarrollo`, `CI aprobado`, `probado en Editor` y `probado en Android con sensores`. Añadir enlaces a PR/commit o evidencia de prueba. Para mantener legibilidad, resumir aquí los últimos 10–15 hitos y dejar el historial profundo en Git.

## 7. Contrato de actualización después de cada paso

Cuando se complete una tarea de BIOFIT DASH:
1. Verificar qué cambió realmente y en qué rama/commit. Si algo solo fue sugerido, no declararlo terminado.
2. Actualizar **este archivo en el mismo PR**: fecha, estado, cambios, pruebas ejecutadas/pendientes y próxima tarea.
3. Si cambian arquitectura, parámetros BLE, mapas o restricciones, actualizar también `docs/CONTEXTO_ACTUAL.md` para que ambos documentos coincidan.
4. No borrar decisiones antiguas sin dejar un enlace al historial; conservar trazabilidad de regresiones y rollbacks.
5. Si la tarea es solo local (por ejemplo catálogo generado en Windows), escribir `confirmado por el usuario`; GitHub no puede verificar por sí solo el contenido de su disco.
6. Revisar Git y archivos locales excluidos antes de actualizar el proyecto principal Windows.
7. No fusionar cambios funcionales sin evidencias de pruebas acordes; evitar presentar CI como validación física.

## 8. Cómo iniciar un nuevo chat de ChatGPT

Compartir este enlace:

https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/blob/main/docs/CONTEXTO_VIVO.md

Y escribir:

> Continuemos BIOFIT DASH. Leé primero `docs/CONTEXTO_VIVO.md` y luego `docs/CONTEXTO_ACTUAL.md` del repositorio. Confirmá el último commit de `main` y separá cambios implementados, pruebas realizadas y tareas pendientes. Respetá BILATERAL LEGACY y la física del Cube. En esta conversación te indicaré la siguiente tarea. Al terminar una modificación, actualizá el contexto vivo con el resultado real.

**Importante:** abrir un nuevo chat no garantiza que ChatGPT lea este archivo automáticamente. El usuario debe enlazarlo o solicitar su lectura; si aún hay un PR sin fusionar, utilizar el enlace de esa rama hasta que llegue a `main`.

## 9. Cinco niveles progresivos — versión estable validada funcionalmente

- **Origen:** [PR #28 fusionado](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/28), desde `feat/five-level-progression-1500ms-v1` a `main` el 2026-11-23. Commit de integración: `017eb093a670703591479228307817db51c8197b`.
- **Contenido:** cinco mapas de ~60 segundos rotulados `Nivel 1` a `Nivel 5`, música original, progresión Cube/Ball/Spider y modos mixtos, obstáculos >=15 unidades entre centros, selector de izquierda a derecha y respuesta visual de botones.
- **Resultado comunicado por usuario:** Unity Editor funciona; APK Android compilada e instalada; entradas de salto/movimiento físico con ambos ESP32 funcionan correctamente; versión aceptada como estable hasta próximo cambio.
- **CI:** [GitHub Actions](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45253847730) completó pruebas estáticas/host. El CI no ejecuta una APK ni sustituye las pruebas físicas.
- **Arte aún provisional:** Ball, Spider y portales usan sprites fallback programáticos porque los gráficos de `BIOFIT-RECURSOS-GD` todavía no fueron seleccionados/importados. Conservar esa integración como próximo trabajo separado.
- **No afirmar sin datos:** FPS de 60 medidos, latencia exacta, validación de todos los eventos, cero falsos positivos o fiabilidad estadística.
- **Restricciones intocadas:** física Cube original, detector BLE BILATERAL LEGACY, firmware, umbral 0,12 g, ventana 180 ms, cooldown 900 ms, sincronización y UUID.
- **Detalles:** [NIVELES_PROGRESIVOS_1500MS.md](NIVELES_PROGRESIVOS_1500MS.md).
