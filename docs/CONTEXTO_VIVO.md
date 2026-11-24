# BIOFIT DASH — CONTEXTO VIVO / RELEVO ENTRE CONVERSACIONES

> **Leer primero al iniciar un chat nuevo.** Este documento es el punto de entrada operativo y se actualiza con cada cambio **confirmado** del proyecto. No sustituye al código fuente ni a las pruebas físicas.

## PRÓXIMA VERSIÓN EXPERIMENTAL — STEREO MADNESS + SHIP + VR BOX (2026-11-24)

**Rama de trabajo:** `feat/stereo-madness-ship-vrbox-held-v1`, nacida de `feat/vr-joystick-input-diagnostics` ([PR #30 en borrador](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/30)), que a su vez se basó en **`main` con el cooldown configurable PR #29 ya integrado**. **No está fusionada a `main`**. Guía técnica/evidencia/limitaciones: [STEREO_SHIP_VRBOX_V1.md](STEREO_SHIP_VRBOX_V1.md).

**Requisito 1, joystick:** los seis botones físicos A/B/C/D/R1/R2 del mando **`VR BOX` en `CAMBIO+C` se utilizan como LA MISMA acción** del personaje, sin asignar pausa/atrás. Dado el mapeo `A=R1=0+13`, `B=R2=0`, `C=13`, `D=1`, el software calcula **OR de 0/1/13** una vez por frame. Para **Cube**, si el botón sigue sostenido, el adaptador llama `cube.onClick()` cada frame; el `CheckGrounded()` original evita saltos en aire y permite repetir salto al aterrizar. En Ball/Spider solo pulso en `GetKeyDown`. No cambia el modo Mouse ni la entrada táctil. Nuevo controlador dentro de `BiofitThreeModes.cs`.

**Requisito 2, nave Ship:** se añadió `BiofitMode.Ship`, `biofitShipPortal`, sprite/portal procedural opcionalmente reemplazable por PNG con licencia y techo limitado a tramos de nave. Valores de referencia del `Ship.cs` original del submódulo: gravedad ±2,5, `|vy|<=10` e inclinación ±40°. La nave sube mientras se mantiene joystick/Mouse/Space; cada par BLE válido activa temporalmente **400 ms de empuje virtual**, independientes de los 6 posibles cooldowns (900–400 ms) del detector. Preserva objeto `Cube` para muerte, cámara, física, BLE y respawn, sin modificar firmware.

**Requisito 3, Nivel 1:** JSON real `Assets/BIOFIT/Resources/BiofitThreeModes/BackOnTrack.json` (clave histórica del loader, no confundir con `StereoMadness.json` de Nivel 4) reemplazado por **reconstrucción video-guiada** de ~85 s, 825u, **489 objetos**, fases Cube–Ship–Cube–Ship–Cube, 3 monedas, música `StereoMadness`. El video aportado tiene 95,52 s e incluye menú/finalización. El layout es una reconstrucción **APROXIMADA**, no el nivel exacto de RobTop: GDBrowser `77236587` declara 2.291 objetos; aún no existe correspondencia importada 1:1. Archivo fuente reproducible `tools/assets/generate_stereo_video_map.js`; herramienta separada `tools/assets/fetch_stereo_reference.py` puede consultar públicamente datos de objetos para auditoría, sin alterar automáticamente el nivel. No subir PNG originales RobTop al repositorio público.

**Calidad:** revisar GitHub Actions de la rama y los tests `check_stereo_ship_vrbox.py`, `test_stereo_source_parser.py`, `check_three_modes_60s.py` y fuente `GeometryUxSyntax`. **No hay Unity Editor ni APK Android ni jugabilidad física verificadas en esta nueva rama aún**. Probar colisiones/bloques, duración de audio, salto sostenido, portal Ship, empuje BLE, monedas, los otros cuatro niveles y los seis cooldowns antes de cualquier integración. Mantener PR borrador, `main` intacto.

## Joystick VR BOX — MODO JUEGO validado físicamente (2026-11-24)

**Fuente principal de continuidad:** [JOYSTICK - Modo Juego.md](../JOYSTICK%20-%20Modo%20Juego.md), archivo creado directamente en `main` con las seis capturas y observaciones del usuario. **Para ESTE mando, `MODO CAMBIO + C` es el modo de juego probado en Android**, aunque manuales de otros mandos sugirieran `CAMBIO + B`. No corregir este hallazgo por analogía con dispositivos externos.

- Unity Android detectó el dispositivo **`VR BOX`** en la pantalla del diagnóstico (implementada en rama experimental [PR #30](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/30), **no integrada aún a `main`**).
- Matriz física de botones capturada **en orden A, B, C, D, R1, R2:** **A y R1 → JoystickButton0 + JoystickButton13; B y R2 → JoystickButton0; C → JoystickButton13; D → JoystickButton1**. Los nombres equivalentes `Joystick1ButtonX` son alias del mando 1; no acciones independientes.
- En `CAMBIO + C`, el stick **no funcionó** según la prueba del usuario, aunque las capturas solo mostraban ejes en 0.00 en instantes aislados. En **modo Mouse (`CAMBIO + D`)**, el stick/puntero funciona pero ocurren comportamientos problemáticos de volumen y Atrás, incluso salida del juego; no adoptarlo como perfil principal.
- Hay **cuatro patrones lógicos**, no seis botones inequívocamente independientes. Cualquier futura asignación debe resolver **el patrón 0+13 antes de interpretar 0 y 13 por separado**, porque A/R1 puede provocar dos acciones si se mapea de forma ingenua. El diagnóstico muestra en varios botones 2 renglones numerados, **no demuestra por sí solo un doble comando real**.
- **Observación física adicional (2026-11-24):** al entrar a un nivel con **CAMBIO+C**, las teclas **NO hacen saltar al Cube**, aunque el diagnóstico `Configuración → JOYSTICK` sí registra los botones. Con **CAMBIO+D (Mouse)**, A/R1 sí lo hacen saltar al simular clic, pero ese perfil tiene efectos adversos de volumen/Atrás/salida del juego.
- **Causa técnica verificada en código:** `BiofitGeometryUX.RefreshJoystickDiagnostics` solo **muestra** eventos en el submenú experimental; no los aplica al gameplay. `BiofitThreeModes.cs` escucha manualmente `Space`/`Mouse0`, y `BiofitGeomeTryLegacyBridge.cs` ejecuta acciones por **BLE bilateral**. Por tanto **hay que programar un puente de gamepad → acción de personaje**, respetando prioridades del patrón 0+13 para evitar acciones dobles y conservando sensores/controles actuales. **La ausencia de salto en modo+C no demuestra un problema de Bluetooth ni es contradictoria con la detección correcta de botones en Unity**.
- **Pendiente:** implementar y validar físicamente el mapeo `VR BOX` → Cube/Ball/Spider en una rama experimental, incluyendo protección de dobles eventos, acceso a pausa/atrás, convivencia con touch y BLE, y pruebas sin joystick conectado; estudiar stick con eventos/ejes completos. **Esta actualización solo documenta información; no modifica lógica de joystick, BLE, cooldown 900/800/700/600/500/400 ms ni física**.

## VALIDACIÓN FÍSICA JOYSTICK — MODO CAMBIO + C (2026-11-24)

- **Fuente canónica ya archivada directamente en `main`:** [JOYSTICK - Modo Juego.md](../JOYSTICK%20-%20Modo%20Juego.md). El usuario probó en Android el joystick identificado como `VR BOX` y demostró **`MODO CAMBIO + C` como modo juego** (no `+B`, aunque manuales de modelos similares lo sugieran). La nueva pantalla de diagnóstico **ya fue validada físicamente** para lectura de botones; el remapeo aún no se implementó.
- **Resultados A, B, C, D, R1, R2 en ese orden:** `A=0+13`, `B=0`, `C=13`, `D=1`, `R1=0+13`, `R2=0`. Código de Unity `JoystickButtonX`; `Joystick1ButtonX` es alias específico del primer mando. A/R1 y B/R2 indistinguibles entre sí. La detección muestra algunos renglones duplicados, **sin evidenciar todavía doble comando real**.
- **Stick:** no utilizable en modo `+C` según usuario. En modo `+D` mueve puntero pero controles volumen/atrás son problemáticos y pueden cerrar la aplicación; **no adoptar modo mouse** como perfil principal. Evitar remapear las señales 0 y 13 independientemente sin prioridad: A/R1 puede producir ambas simultáneamente.
- La UI experimental se actualizó para decir **`MODO JUEGO COMPROBADO: CAMBIO + C`**; mantener rama y PR #30 en borrador hasta implementar y validar bindings. No tocar la base BLE ni las seis opciones 900/800/700/600/500/400 ms.

## Solicitud explícita de versionar Skins.png (2026-11-23)

- El usuario aclaró que usa estos gráficos solo en su proyecto y solicitó explícitamente **publicar Skins.png** en GitHub a pesar de la recomendación inicial de mantenerlo privado. El archivo original extraído de `BiofitUi.rar` mide **936729 bytes**, es PNG RGBA de **1280 × 887**, SHA-256 `3e9a3675f53e7b02303ced1a0b65aa1bb8ffcb0935fd3aae46597026b32ba7b5`.
- **Estado real:** `.gitignore` permite ahora versionar **solo** `Assets/BIOFIT/Resources/BiofitUi/Skins.png`, mientras que los otros tres PNG permanecen excluidos. Se agregó el parámetro opt-in `-PublicarSkins` a `tools/install_biofit_ui.ps1`; el script extrae automáticamente el ZIP validado, hace commit únicamente de Skins y ejecuta `git push` a la rama experimental, si el usuario tiene acceso GitHub desde su computadora.
- **LIMITACIÓN:** en esta sesión la herramienta conectada de GitHub no tiene acceso directo a los bytes binarios del PNG del contenedor, por lo que **NO está subido todavía**. No afirmar que se publicó hasta ver el blob en GitHub. El usuario solo necesitará descargar el ZIP a Descargas y ejecutar el comando de instalación con `-PublicarSkins` desde su clon. No promoverlo a `main` antes de las pruebas.
- Publicar el recurso en un repositorio público lo hace accesible a terceros. Aun cuando los modelos no coincidan exactamente con los originales, esto no determina por sí solo la ausencia de derechos de terceros.

## Recursos UI privados — instalación automática sin copiar cuatro PNG (2026-11-23)

- El usuario aportó `BiofitUi.rar` con `Back.png` (7.606 B), `LevelSelect.png` (2.433.590 B), `Pause.png` (158.591 B) y `Skins.png` (936.729 B). Se extrajeron y verificaron correctamente. `Skins.png` muestra iconos reconocibles de Geometry Dash, así que **no publicar imágenes en GitHub público sin verificar licencia**; `.gitignore` preserva la exclusión de `Assets/BIOFIT/Resources/BiofitUi/`.
- Se entregó al usuario un único archivo local `BIOFIT_UI_ASSETS.zip` en su conversación, con los cuatro PNG. La rama experimental del joystick incluye `tools/install_biofit_ui.ps1` para buscarlo en Descargas, extraerlo, validar SHA-256, respaldar archivos locales anteriores y copiar todo a `Assets/BIOFIT/Resources/BiofitUi/` mediante **un comando**. Ya no hay que copiar manualmente cuatro PNG al clonar.
- Guía técnica y hashes: [INSTALACION_AUTOMATICA_BIOFIT_UI.md](INSTALACION_AUTOMATICA_BIOFIT_UI.md). **No confundir:** el ZIP NO está alojado en el repositorio GitHub; el instalador sí. Unity/Windows aún deben probar la instalación real; CI únicamente valida sintaxis. Esta herramienta no altera la base funcional ni la configuración del joystick/BLE.

## En curso — joystick VR Bluetooth 3.0, diagnóstico Android (2026-11-23)

- **Rama experimental:** `feat/vr-joystick-input-diagnostics`, creada desde `main` verificado (`e1db0a5c`) después del merge de cooldown PR #29. No cambiar `main` ni eliminar ninguna de las seis opciones (900/800/700/600/500/400 ms).
- **Dispositivo proporcionado por el usuario:** DUAITEK/TodoMicro `JOYSTICK-VR`, Bluetooth clásico 3.0. Prueba fuera del juego: A/R1 clic, B larga/R2 Atrás, C volumen arriba, D volumen abajo, stick 360°. La equivalencia con `KeyCode` Unity **aún no se ha identificado**, y las teclas de volumen pueden ser del sistema.
- **Investigación:** el manual de hardware semejante VR-PARK Y1 documenta `@+B` para modo gamepad horizontal y `@+D` para mouse; no asegurar que idéntico protocolo HID aplique al hardware comercial exacto. Fuentes oficiales Android distinguen eventos de teclado/gamepad, axes y sistema.
- **Auditoría:** Unity `activeInputHandler: 0` usa Legacy Input Manager; `com.unity.inputsystem` está instalado pero no activado. Cube y Ball/Spider tienen rutas de touch/Space; un evento mouse de A podría funcionar sin crear pulsos extra. **Mantener BILATERAL LEGACY y las físicas**.
- **Implementado solamente como diagnóstico:** botón `JOYSTICK` en Configuración abre pantalla para mostrar nombres `Input.GetJoystickNames`, últimos `KeyCode`, nuevos ejes de solo diagnóstico `BIOFIT VR Stick X/Y` y ejes existentes `Horizontal/Vertical`, contador, limpiar y volver; registra eventos en Console. No altera el gameplay, ni incluye remapeo todavía. La pantalla nueva precisa **ensayo en APK Android real**; la CI no sustituye hardware.
- **Documentación y guía:** [JOYSTICK_VR_BLUETOOTH_DIAGNOSTICO.md](JOYSTICK_VR_BLUETOOTH_DIAGNOSTICO.md). Después de identificar códigos reales, diseñar mapeo por acción (confirmar/jugar/volver/pausa/reiniciar/navegación), preferencias persistentes y prevención de doble salto por clic emulado.
- **Estado:** código de diagnóstico en rama para pruebas. **No fusionado a `main`**; preserve la versión estable PR #29 para jugar físicamente. Próximo: compilar APK experimental, probar @+B y @+D, registrar teclas de A/B/C/D/R1/R2/stick, validar UI en 16:9 y luego implementar bindings fiables.

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
