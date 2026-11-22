# MOV HOLD V2 — CONTROL CONTINUO Y RECUPERACIÓN

Esta rama **feat/geometry-continuous-motion-jump-v2**, [PR #23 en borrador](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/23), corrige defectos reproducidos de MOV HOLD v1: heartbeat STATUS descartado, inicio que exige solapamiento MOV, pérdida de disponibilidad y listeners acumulados. Integra el botón virtual en `Character.Update`, recupera enlaces BLE y registra la cadena completa a través de muertes/respawns. No agrega un selector ni modifica la física, firmware o ramas anteriores. Los pares UP legacy quedan como diagnóstico; la intención sostenida controla el juego.

**Guía vigente:** [auditoría V2, resultados y preparación de una sola APK](docs/GEOMETRY_CONTINUOUS_MOTION_V2.md). App **BIOFIT MOV HOLD V2**, paquete `com.biofitgame.geometrymotionv2`, instalación independiente. Menú de build: **BIOFIT > GeomeTry ORIGINAL > 3. Compilar APK MOV HOLD V2 EXP + BLE LEGACY**. Los tests host pasan; **Unity nativo, APK y Android físico siguen pendientes**. V1 falló en la prueba del usuario; sus documentos se conservan solo como historial. Ver la guía V2 antes de usar las instrucciones generales de versiones anteriores que siguen debajo.

---

# BIOFIT DASH — versiones principales

BIOFIT GAME es una plataforma de exergaming para Unity y Android con sensores inerciales Waveshare ESP32-S3 (QMI8658), BLE y control del personaje mediante movimientos corporales.

## Las 3 versiones de referencia

| Versión | Estado | Qué contiene | Abrir |
|---|---|---|---|
| **BIOFIT DASH v0.9.10** | Referencia histórica del control bilateral | Juego BIOFIT DASH original; `BLE: BILATERAL` tradicional. **Incluye también** un buffer de salto de 180 ms y el modo opcional `FASE EXP`; no corresponde describirla como anterior a todo buffering. | [Código congelado](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/tree/88b825b46f3f0a19233cee41799f86aab10a8764) |
| **BIOFIT DASH v0.9.11** | Experimental, **no definitiva** | Laboratorio de saltos consecutivos, `CICLO EXP`, modo de inicio de elevación y pruebas de buffer, conservando el modo BILATERAL LEGACY por separado. | [Código congelado](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/tree/5616fc4571193d4a2d076550a909089344144384) |
| **BIOFIT DASH GeomeTry — UI + objetivo 60 FPS** | **Versión principal de referencia (22/11/2026)** | Cinco niveles GeomeTry originales, menú arcade, selector, 70 skins, pausa/volver, ajustes BLE, saltos `BILATERAL LEGACY` y objetivo Android de 60 FPS. | [Rama principal `main`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/tree/main) |

**Precisión sobre las pruebas:** la versión GeomeTry fue probada visual y funcionalmente por el usuario en Unity y se informó la generación de una APK. Los **60 FPS sostenidos**, el comportamiento de la APK en un teléfono y la comunicación con ambos tobillos **no están certificados** por esas pruebas. `Application.targetFrameRate = 60` expresa un objetivo, no un resultado medido.

**El sistema BILATERAL LEGACY es la mecánica seleccionada para el juego principal.** Los experimentos de fase o CICLO EXP no deben sustituirla por defecto.

[Ver la comparación técnica y las instrucciones de cada versión](docs/VERSIONES_CLAVE.md).

## Código de las versiones históricas

- [v0.9.10 — `feat/phase-gated-jump-v0910`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/tree/feat/phase-gated-jump-v0910), SHA `88b825b46f3f0a19233cee41799f86aab10a8764`.
- [v0.9.11 — `feat/reliable-consecutive-jumps-v0911`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/tree/feat/reliable-consecutive-jumps-v0911), SHA `5616fc4571193d4a2d076550a909089344144384`.
- [BIOFIT DASH GeomeTry UI — `feat/geometry-ui-skins-60fps-v1`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/tree/feat/geometry-ui-skins-60fps-v1), snapshot funcional de código `866f360c3e9d743c076da30e146bfe2c52d52860`. `main` apunta a esta versión con la documentación actualizada.

Los commits históricos se conservan, aunque se cierren PR o se eliminen ramas intermedias.

## Preparación de la versión principal

La copia funcional del usuario está en `BIOFIT-GeomeTry-UI-V1` (Unity **6000.3.28f1**). El repositorio contiene el submódulo original GeomeTry en `ThirdParty/GeomeTryOriginal`. Para crear una copia nueva, clonar con `--recurse-submodules` y, **con Unity cerrado**, ejecutar `tools/prepare_geometry_original.ps1` **solo si aún no se preparó**.

Los cuatro recursos gráficos personalizados aportados por el usuario (**LevelSelect HD, Back, Pause, Skins**) se distribuyeron separadamente. No están subidos al repositorio y son necesarios para reproducir la apariencia exacta. Se importan con `tools/install_geometry_ui_assets.ps1` usando el ZIP `BIOFIT_GeomeTry_UI_Assets_HD_v2.zip` entregado en la conversación.

En Unity: `BIOFIT > GeomeTry UX > 1. Configurar modulo tactil Android`, después `2. Verificar UX y recursos`; activar Android en Build Profiles y utilizar `BIOFIT > GeomeTry ORIGINAL > 3. Compilar APK Android original + BLE LEGACY`.

**Protección de datos:** no eliminar copias locales, escenas, ajustes `ProjectSettings`, archivos de sesiones Android ni firmware. No utilizar `git reset --hard` ni `git clean` sobre el proyecto de trabajo.

## Documentación

- [Historial de las tres versiones clave](docs/VERSIONES_CLAVE.md).
- [Características, interfaz y limitaciones GeomeTry](docs/GEOMETRY_UI_SKINS_ANDROID_V1.md).
- [Informe técnico del buffer v0.9.10](docs/REVISION_V0910_FASES_BUFFER_180MS.md).
- [Investigación experimental de ciclos v0.9.11](docs/REVISION_V0911_CICLO.md).
- [Criterios del proyecto](docs/CRITERIOS_DE_TRABAJO_BIOFIT_GAME.md).

El repositorio conserva el historial Git completo. Las ramas documentales y `main` no cuentan como versiones adicionales del videojuego.
