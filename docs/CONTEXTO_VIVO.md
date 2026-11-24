# BIOFIT DASH — CONTEXTO VIVO / RELEVO ENTRE CONVERSACIONES

> **Leer primero al iniciar un chat nuevo.** Este documento es el punto de entrada operativo y se actualiza con cada cambio **confirmado** del proyecto. No sustituye al código fuente ni a las pruebas físicas.

## Joystick VR BOX — MODO JUEGO validado físicamente (2026-11-24)

**Fuente principal de continuidad:** [JOYSTICK - Modo Juego.md](../JOYSTICK%20-%20Modo%20Juego.md), archivo creado directamente en `main` con las seis capturas y observaciones del usuario. **Para ESTE mando, `MODO CAMBIO + C` es el modo de juego probado en Android**, aunque manuales de otros mandos sugirieran `CAMBIO + B`. No corregir este hallazgo por analogía con dispositivos externos.

- Unity Android detectó el dispositivo **`VR BOX`** en la pantalla del diagnóstico (implementada en rama experimental [PR #30](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/30), **no integrada aún a `main`**).
- Matriz física de botones capturada **en orden A, B, C, D, R1, R2:** **A y R1 → JoystickButton0 + JoystickButton13; B y R2 → JoystickButton0; C → JoystickButton13; D → JoystickButton1**. Los nombres equivalentes `Joystick1ButtonX` son alias del mando 1; no acciones independientes.
- En `CAMBIO + C`, el stick **no funcionó** según la prueba del usuario, aunque las capturas solo mostraban ejes en 0.00 en instantes aislados. En **modo Mouse (`CAMBIO + D`)**, el stick/puntero funciona pero ocurren comportamientos problemáticos de volumen y Atrás, incluso salida del juego; no adoptarlo como perfil principal.
- Hay **cuatro patrones lógicos**, no seis botones inequívocamente independientes. Cualquier futura asignación debe resolver **el patrón 0+13 antes de interpretar 0 y 13 por separado**, porque A/R1 puede provocar dos acciones si se mapea de forma ingenua. El diagnóstico muestra en varios botones 2 renglones numerados, **no demuestra por sí solo un doble comando real**.
- **Pendiente:** implementar mapeo de acciones al juego desde este patrón comprobado y validarlo físicamente en Android; estudiar stick con eventos/ejes completos. **Este commit solamente documenta información; no modifica lógica de joystick, BLE, cooldown 900/800/700/600/500/400 ms ni física**.

## Nueva base funcional aprobada — Cooldown configurable (2026-11-23)

**Decisión del usuario:** después de probar físicamente BIOFIT DASH en su teléfono Android con los dos trackers ESP32 en los tobillos, informó que el juego **funciona muy bien con 400 ms** y autorizó expresamente integrar la funcionalidad del PR #29 en `main` como **nueva versión principal**. Su valor **preferido por ahora es 400 ms**, pero deben **conservarse las seis opciones**: 900, 800, 700, 600, 500 y 400 ms. No bloquear el cooldown en 400 ni eliminar el selector.

**Diferenciar preferencia personal y default de aplicación:** una instalación nueva conserva el **valor predeterminado de 900 ms**; si el usuario selecciona 400 ms, `PlayerPrefs` guarda 400 y lo restaura al abrir. Esto preserva la referencia histórica y la capacidad de seguir experimentando. El cooldown es tiempo de bloqueo entre pares aceptados, **no** la latencia BLE del primer salto.

**Estado de verificación:** 400 ms **validado físicamente por declaración del usuario**, satisfactoriamente en la experiencia que probó, sin métricas numéricas publicadas. No equivale a una prueba estadística de sensibilidad/especificidad, a demostrar cero duplicados ni a validar individualmente los otros cuatro tiempos intermedios. Los seis valores y las fronteras superaron **251 pruebas deterministas de host**, y el commit del PR #29 `3a5956dcec304a6ae49db31291a01b4ad19d9783` superó **5/5 jobs** en [GitHub Actions #45276650454](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45276650454).

**Arquitectura aprobada:** `BiofitBilateralJumpDetector.PairCooldownMilliseconds` manda sobre el timer real; `BiofitCooldownConfiguration` persiste mediante `PlayerPrefs`; `BiofitBLEMonitor` expone/aplica la selección; `BiofitGeometryUX` muestra submenú independiente, resaltado de opción activa, restaurar a 900 y HUD `CD: NNN ms`. El CSV diagnóstico incorpora `COOLDOWN_MS` sin modificar la estructura existente. El bloqueo en curso no se acorta al seleccionar un valor menor; el nuevo valor gobierna pares posteriores.

**Parámetros que NO cambiaron:** BILATERAL LEGACY, umbral UP 0,12 g, ventana bilateral 180 ms, refractario individual 320 ms, UUID y clocks BLE, firmware y física de Cube/Ball/Spider, cinco niveles, portales y música.

**Archivos relevantes:** `Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs`, `BiofitCooldownConfiguration.cs`, `BiofitBLEMonitor.cs`, `Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs`, `tests/CooldownRegression/`, `docs/COOLDOWN_BILATERAL_CONFIGURABLE.md`. Los harness/CI se adaptaron para incluir la nueva clase. Ver detalles completos de la auditoría, correcciones y procedimiento en [COOLDOWN_BILATERAL_CONFIGURABLE.md](COOLDOWN_BILATERAL_CONFIGURABLE.md).

**Evidencia Git comprobada:** [PR #29 fusionado](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/29) a `main` el 2026-11-23, commit de integración [`e9356570d97b7ceba081a2bed540bf68ed1ff6dc`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/commit/e9356570d97b7ceba081a2bed540bf68ed1ff6dc). Rama de origen `feat/configurable-bilateral-cooldown-400-900ms`. `main` pasa a ser la referencia estable para continuar el menú Configuración.

**Pendiente para investigación futura:** sesiones comparativas instrumentadas, tasas de saltos aceptados/omitidos y falsos positivos, duplicados con el mismo gesto a 400 ms, FPS/latencia medida, pruebas individuales de 800/700/600/500 ms y persistencia/reinicio controlados si todavía no se documentaron con evidencia. No impedir avanzar el desarrollo visual y de Configuración mientras no haya una regresión observada.

**Próximo trabajo solicitado:** seguir mejorando el **menú Configuración** desde esta versión principal, sin alterar el control BLE. El proyecto de Windows validado `C:\Users\Capo\Documents\BIOFIT-DASH-NUEVOS-NIVELES` y el clon experimental deben conservarse, sin sobrescribir ni limpiar sus recursos locales.



>
> **Última actualización verificada:** 2026-11-24.
> **Repositorio:** https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY
> **Rama de referencia:** `main`.
> **Estado fuente:** [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md) · [Tres modos y niveles](TRES_MODOS_60S_BILATERAL.md) · [Recursos](ASSET_SOURCES.md).
> **Historial extenso (no vigente):** [CONTEXTO_MAESTRO_HISTORICO.md](CONTEXTO_MAESTRO_HISTORICO.md).

## REGLA PRIORITARIA E INNEGOCIABLE — CONTROL BLE

**La prioridad absoluta de BIOFIT GAME es conservar operativo y fiable el detector BLE BILATERAL LEGACY.** Si un cambio visual, de niveles, interfaz o rendimiento compromete la detección o la ejecución de movimientos, se debe **restaurar el funcionamiento de BILATERAL LEGACY antes que conservar dicho cambio**.

**Línea de base actual (no modificar sin ensayo controlado):** umbral UP 0,12 g, ventana bilateral 180 ms, refractario individual 320 ms, y **cooldown configurable de 900/800/700/600/500/400 ms**. El default es 900 ms; el usuario validó físicamente 400 ms y lo prefiere actualmente. No alterar lógica de firmware/recepción.

**Comparaciones futuras:** los seis valores ya se pueden seleccionar en la misma APK; 400 ms tiene evaluación física satisfactoria comunicada por el usuario. Pendiente: cuantificar A/B, falsos positivos, pares reconocidos y acciones ejecutadas, sin confundir cooldown con latencia BLE. Conservar la opción 900 ms y el botón de restablecimiento.

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

El control vigente mantiene **umbral UP 0,12 g, ventana bilateral 180 ms y cooldown seleccionable 900/800/700/600/500/400 ms** (predeterminado 900; preferencia actual del usuario: 400 ms). El cooldown no es una medición de latencia BLE. La física del Cube y los demás filtros no deben cambiarse incidentalmente.

## 2. Evidencia y límites de lo que está validado

- **Probado en Unity Editor (comunicado por el usuario, 2026-11-23):** el proyecto de cinco niveles abre, compila y funciona en Play.
- **Probado en Android con ambos sensores físicos (comunicado por el usuario, 2026-11-23):** APK generada e instalada; los movimientos corporales controlan satisfactoriamente el juego con BLE BILATERAL LEGACY. El usuario decidió adoptar esta versión como referencia estable y fusionarla en `main`.
- **No medido cuantitativamente:** tasa de falsos positivos/negativos, latencia real, FPS, prueba formal prolongada de transiciones, colisiones y respawn. Que funcione bien en una prueba no demuestra todas estas métricas.
- **Pruebas automatizadas:** verificaciones estáticas y host en GitHub Actions; **no** sustituyen un ensayo Unity/Android real.
- **Firmware:** repositorio independiente: https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware. La versión realmente instalada en los ESP32 no está verificada por esta documentación.
- **Prohibido asumir como hecho:** que el cooldown representa la latencia BLE; que los 60 FPS estén medidos; que CI en verde demuestre fiabilidad estadística o ausencia de duplicados.

## 3. Recursos e instalaciones locales

- **Última carpeta funcional de referencia indicada por el usuario:** `C:\Users\Capo\Documents\BIOFIT-DASH-NUEVOS-NIVELES`. Conserva también el clon experimental `BIOFIT-DASH-COOLDOWN-EXPERIMENTAL`. No tocar la antigua `BIOFIT-DASH-PRINCIPAL` sin inspeccionar su estado.
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

**Versión funcional base:** los cinco niveles del PR #28 permanecen, y **PR #29** agrega el cooldown seleccionable y el HUD, con aprobación del usuario para adoptarlo como nueva versión principal. La referencia histórica de niveles es el commit [`017eb093a670703591479228307817db51c8197b`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/commit/017eb093a670703591479228307817db51c8197b); la nueva versión principal de cooldown configurable está fusionada mediante [PR #29](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/29), merge `e9356570`.

**Prueba funcional confirmada por el usuario:** ejecutó la versión en Unity Editor, construyó la APK e informó que en Android los movimientos físicos funcionan bien con los ESP32. Esta es la línea de base que **no se debe romper**. No se dispone de informe cuantitativo de falsas activaciones, latencia ni FPS.

**Restricciones:** mantener BILATERAL LEGACY, umbral 0,12 g, ventana 180 ms, refractario individual 320 ms, **selector completo 900/800/700/600/500/400 ms**, física original Cube y firmware intactos. La preferencia del usuario por 400 ms no justifica eliminar alternativas ni cambiar el default de 900 ms.

**Arte pendiente:** los iconos de Ball y Spider y los gráficos de sus portales **siguen siendo provisionales**; NO se importaron los sprites originales desde la biblioteca de Windows. El repositorio contiene soporte para importar PNG individuales en `Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/`, pero no incluye esos PNG. La biblioteca `BIOFIT-RECURSOS-GD` de unos 5,90 GB (~195.691 archivos) es local; el usuario contempla compartirla por Google Drive para seleccionar recursos. Comprobar licencia antes de publicar.

**Acción siguiente:** seleccionar y preparar arte (Ball, Spider y portales), idealmente desde un subconjunto accesible de Google Drive, en **rama separada**, verificar en Editor y Android, mantener `main` funcional. Nunca ejecutar reset o limpieza destructiva en `BIOFIT-DASH-PRINCIPAL` ni sobrescribir `BIOFIT-DASH-NUEVOS-NIVELES` sin verificar diferencias y respaldos. La APK compilada y los recursos locales excluidos no se guardan automáticamente en GitHub.

## 6. Registro cronológico resumido

| Fecha | Cambio verificado | Estado / evidencia |
| --- | --- | --- |
| 2026-11-22/23 | Base de Tres Modos y BLE BILATERAL LEGACY | Consolidación inicial; posteriormente se validaron los cinco niveles rediseñados |
| 2026-11-23 | Biblioteca de recursos local inventariada y catálogo HTML generado | El usuario confirmó catálogo funcional; recuento comunicado: 194.750 registros |
| 2026-11-23 | Scripts y documentación de recursos integrados | PR #26 fusionado, commit `17c92fcbdeb0c7d65b5610fcf649e089d7c9aced` |
| 2026-11-23 | Cinco niveles más exigentes y selector español: Android con sensores físicos | El usuario confirmó que la APK y los movimientos corporales funcionan; PR #28 fusionado, commit `017eb093a670703591479228307817db51c8197b` |
| 2026-11-23 | Cooldown configurable con seis opciones, 400 ms preferido | Validación física satisfactoria de 400 ms declarada por usuario; PR #29 fusionado a `main` (`e9356570`); CI 251 checks, 5/5 jobs en verde. No hay todavía informe cuantitativo. |

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
- **Restricciones intocadas:** física Cube original, BLE BILATERAL LEGACY, firmware, umbral 0,12 g, ventana 180 ms, sincronización y UUID; el cooldown ahora es seleccionable 900/800/700/600/500/400 ms sin reemplazar el detector.
- **Detalles:** [NIVELES_PROGRESIVOS_1500MS.md](NIVELES_PROGRESIVOS_1500MS.md).
