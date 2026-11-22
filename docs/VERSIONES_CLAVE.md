# BIOFIT DASH — Registro de tres versiones clave

**Fecha de organización:** 22 de noviembre de 2026.  
**Repositorio:** https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY  
**Objetivo:** conservar dos puntos de referencia históricos y una versión principal del videojuego, sin confundir funcionalidades experimentales con las validadas.

## 1. v0.9.10 — referencia bilateral tradicional

- **Rama:** `feat/phase-gated-jump-v0910`
- **Commit preciso:** `88b825b46f3f0a19233cee41799f86aab10a8764`
- **PR histórico:** [#16](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/16) (cerrado sin merge)
- **Tipo:** referencia histórica elegida por el usuario; NO afirmar que todo su contenido fue validado físicamente.
- **Uso:** comparar la respuesta y los controles del BIOFIT DASH original, antes de la nueva vía `LIFT_ONSET` / `CICLO EXP` de v0.9.11.
- **Integrado:** dos trackers tobillo BLE, emparejamiento `UP_IMPULSE`, umbral bilateral 0,12 g, ventana de pares 180 ms, protección 900 ms entre pares en `BLE: BILATERAL`, registros/diagnósticos y exportación.
- **También integrado:** `BiofitJumpBuffer` de una sola posición y 180 ms; `BLE: FASE EXP` opcional para intentar pares a 600 ms si ambos eventos tienen evidencia de fase. El modo normal no exige esta evidencia.
- **Restricción interpretativa:** el buffer ya aparece desde v0.9.8 (120 ms) y v0.9.9 (180 ms). En consecuencia, **v0.9.10 no es una versión sin buffering**. El usuario recuerda como satisfactoria su modalidad bilateral normal. El proyecto no debe reescribir esa historia.
- **Fuente original:** [`REVISION_V0910_FASES_BUFFER_180MS.md`](../docs/REVISION_V0910_FASES_BUFFER_180MS.md).

## 2. v0.9.11 — ciclo EXP experimental

- **Rama:** `feat/reliable-consecutive-jumps-v0911`
- **Commit preciso:** `5616fc4571193d4a2d076550a909089344144384`
- **PR histórico:** [#17](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/17) (cerrado sin merge).
- **Tipo:** experimento conservado para auditoría, NO producto final.
- **Integrado:** vía adicional `LIFT_ONSET` (evento de inicio de elevación) compatible con firmware v2.2.9 candidato; `CICLO EXP` independiente del modo BILATERAL LEGACY; pruebas de replays, caducidad del buffer y ciclos consecutivos.
- **Propósito:** estudiar candidatos tempranos de salto y recuperación entre ciclos sin falsos positivos durante descenso.
- **Resultado técnico:** la propia documentación califica esta versión como no validada físicamente y recoge la ineficacia de la vía de fase rápida v0.9.10 en las sesiones examinadas. No inferir precisión biomecánica ni latencia real de los datos sin etiquetas humanas.
- **Contención:** `BILATERAL LEGACY` permanece disponible; no convertir `CICLO EXP` en control predeterminado.
- **Fuente:** [`REVISION_V0911_CICLO.md`](REVISION_V0911_CICLO.md).

## 3. BIOFIT DASH GeomeTry — UI + 60 FPS objetivo

- **Rama de desarrollo conservada:** `feat/geometry-ui-skins-60fps-v1`.
- **Código de referencia probado en Unity:** `866f360c3e9d743c076da30e146bfe2c52d52860`.
- **Destino canónico:** `main` tras la organización del repositorio.
- **Origen:** la versión v0.9.11 aporta la infraestructura BLE general, pero el control del cubo GeomeTry **usa únicamente la ruta BILATERAL LEGACY**, no CICLO EXP.
- **Integrado:** GeomeTry original como submódulo Git y copia de trabajo generada por script; cinco mapas y física originales; BLE bilateral con el salto del cubo y la pantalla táctil; UI con selector, efectos de selección, 70 skins persistidas, volver, pausa, configuración de trackers, registro/archivos y un visor de FPS diferenciado Editor/Android.
- **Renderizado:** URP 2D original; `Application.targetFrameRate = 60` como **solicitud**, no medición demostrada.
- **Pruebas reportadas:** menús, niveles y últimas mejoras de interfaz funcionando en el Editor del usuario; APK compilada. **Pendiente de confirmar:** 60 FPS en teléfono, BLE con ambos tobillos en APK, autonomía y comportamiento de la aplicación instalada.
- **Recursos externos al Git:** el paquete `BIOFIT_GeomeTry_UI_Assets_HD_v2.zip` con cuatro PNG del usuario **no está integrado** en el repositorio; conservar junto a los respaldos.
- **Fuentes:** [`GEOMETRY_UI_SKINS_ANDROID_V1.md`](GEOMETRY_UI_SKINS_ANDROID_V1.md), [PR #21](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/21).

## Qué se conserva, qué se cierra

Las tres versiones elegidas quedan accesibles mediante **commits permanentes** y las respectivas ramas de trabajo. La evolución intermedia sigue siendo parte del historial Git, aunque se cierren PR antiguos o posteriormente se elimine alguna rama. Esto significa que **limpiar ramas no borra automáticamente su código histórico**.

La rama `docs/contexto-maestro` tiene documentación importante: no debe eliminarse como si fuera una versión obsoleta. `main` es la rama predeterminada y no una cuarta versión funcional.

La rama `feat/geometry-original-legacy-bridge` representa una **etapa intermedia**, antecedente directo de la interfaz actual. No es una de las tres versiones clave. Eliminar su referencia remota es una operación de organización independiente; **no** sobrescribir ni eliminar la copia local del usuario y no hacerlo antes de verificar que la versión final apunta a `main`.

## Protocolo para futuras iteraciones

1. Conservar sin modificaciones las referencias de v0.9.10, v0.9.11 y el snapshot GeomeTry final.
2. Desarrollar una nueva característica en una rama nueva desde `main`, evitando experimentar directamente sobre la referencia estable.
3. Verificar código, escenas y recursos en Unity antes de una APK.
4. Medir rendimiento y BLE en Android físico; no equiparar CI verde con 60 FPS o identificación correcta de gestos.
5. Si cambia el comportamiento de salto, documentar explícitamente qué modo BLE se utilizó, firmware instalado, parámetros y datos registrados.
