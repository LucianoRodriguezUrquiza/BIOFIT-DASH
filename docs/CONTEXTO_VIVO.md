# BIOFIT DASH — CONTEXTO VIVO / RELEVO ENTRE CONVERSACIONES

> **Leer primero al iniciar un chat nuevo.** Este documento es el punto de entrada operativo y se actualiza con cada cambio **confirmado** del proyecto. No sustituye al código fuente ni a las pruebas físicas.
>
> **Última actualización verificada:** 2026-11-23.
> **Repositorio:** https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY
> **Rama de referencia:** `main`.
> **Estado fuente:** [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md) · [Tres modos y niveles](TRES_MODOS_60S_BILATERAL.md) · [Recursos](ASSET_SOURCES.md).
> **Historial extenso (no vigente):** [CONTEXTO_MAESTRO_HISTORICO.md](CONTEXTO_MAESTRO_HISTORICO.md).

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

- **Documentado como probado:** menús y mecánicas principales de Tres Modos en Unity Editor.
- **No validado aún para Tres Modos:** APK Android con los dos ESP32, fiabilidad de activaciones físicas, respawn, colliders/techo, transiciones y FPS reales.
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

**Objetivo activo:** mejorar progresivamente el aspecto y diseño de los cinco niveles existentes, comenzando por un nivel de referencia (Cube / BackOnTrack), sin romper la recepción BLE ni la jugabilidad.

**Lo que ya quedó completado:**
- Selección de Cube, Ball y Spider como modos activos y cinco niveles de ~60 s en `main`.
- Recolección, clasificación y catálogo visual local de recursos gráficos.
- Incorporación a GitHub de herramientas de descarga/catalogación y documentación (PR #26).

**Lo que sigue pendiente:**
1. Elegir un conjunto reducido de assets para el primer nivel; inspeccionar rutas, formatos, atlas/capas y procedencia.
2. Diseñar un **importador local y reversible** de recursos seleccionados sin copiar los 194.750 archivos a `Assets/`.
3. Conectar sprites, prefabs, efectos y UI al nivel de referencia; validar primero en Unity Editor.
4. Verificar la APK y el control físico con ambos trackers antes de afirmar estabilidad real.

**Próxima acción propuesta:** auditar el nivel BackOnTrack y definir la primera selección de sprites a incorporar, conservando una copia estable y sin modificar física, firmware o umbrales BLE.

## 6. Registro cronológico resumido

| Fecha | Cambio verificado | Estado / evidencia |
| --- | --- | --- |
| 2026-11-22/23 | Consolidación de Tres Modos, cinco niveles y BILATERAL LEGACY en `main` | Documentado en `docs/CONTEXTO_ACTUAL.md`; Editor probado, Android físico pendiente |
| 2026-11-23 | Biblioteca de recursos local inventariada y catálogo HTML generado | El usuario confirmó catálogo funcional; recuento comunicado: 194.750 registros |
| 2026-11-23 | Scripts y documentación de recursos integrados | PR #26 fusionado, commit `17c92fcbdeb0c7d65b5610fcf649e089d7c9aced` |
| 2026-11-23 | Se propone adoptar este archivo para relevos entre conversaciones | Cambio documental; no supone una nueva prueba funcional del juego |

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

## 9. Candidata de diseño de niveles más exigentes (NO validada físicamente)

- **Rama de trabajo:** `feat/five-level-progression-1500ms-v1`.
- Se rediseñaron los cinco mapas de ~60 segundos como `Nivel 1` a `Nivel 5`, con música original, progresión Cube / Ball / Spider / combinaciones, separación mínima >=15 u (>=1,538 s nominales entre pinchos) y validación estática ampliada.
- Selector reconstruido con tarjetas vivas y etiquetas españolas, de izquierda a derecha; botones con feedback táctil.
- Íconos y portales: fallbacks originales + carga preferente desde `Assets/BIOFIT/Resources/BiofitThreeModes/Artwork/` mediante importador local opt-in. El arte de la biblioteca de Windows no se incorporó en el repositorio público.
- **Estado:** pendiente verificar que Unity compila, que los cinco niveles son jugables en Editor y que las entradas BLE funcionan en Android real; no fusionar a main hasta confirmar esos puntos.
- **Guía de diseño e instalación:** [NIVELES_PROGRESIVOS_1500MS.md](NIVELES_PROGRESIVOS_1500MS.md).
- **Restricciones conservadas:** sin cambios a Cube original, BILATERAL LEGACY, firmware, tiempos de cooldown, sincronización ni UUIDs.
