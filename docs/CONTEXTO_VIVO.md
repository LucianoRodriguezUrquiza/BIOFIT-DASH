## Avance 26/11/2026 — segundo delta de Stereo Madness y primer delta de Back on Track

Verificados remotamente en `082ea260382f144a38d3f8c17285e5b8f0671615`. Stereo Madness: aplicar primero `BIOFIT_level_1_20261126_001459.json` y después `BIOFIT_level_1_20261126_004628.json` (base 8c7adbe0f89959dcf86b89e70c10ed62f084ebbb2407420a56a4ffb159798f0f, objetivo bd2d104cfc88b79751e9cde421f097d6f8413da80847a7ee38c7583379cbfc43; 2991 registros, 16 altas, 23 bajas). El usuario informó que solucionó el fallo de teletransporte del Spider en 86–88% aumentando ligeramente el espacio vertical libre. El motor exige distancia entre centros de al menos 1 unidad Unity (30 GD), además de apoyo/destino y espacio libre de colliders; no confundir con separación mínima entre caras, que depende del tamaño del personaje. Back on Track: `BIOFIT_level_2_20261126_114359.json` (base V6.3, objetivo 39e8beeb17c3a549a6eb452587da86b0655b9e81b0e650ffddc0e9c4cd151651; 1920 registros, 135 ediciones, 268 altas, 82 bajas). Integridad de índices y altas/bajas verificada; no se recalcularon aquí los hashes de los mapas objetivo ni se probó la APK. Usuario declara finalizada la edición de Back on Track; no equivale a certificación integral. Main V6.3 sin cambios.

Físicas de referencia verificadas en GeomeTry Character/Cube y BiofitV5Interaction: velocidad horizontal 6.5 u/s de simulación; salto normal 13.5 u/s vertical inicial; pad amarillo 20, rosa 12.5, gravedad celeste invierte gravedad (no asigna un impulso vertical fijo en el adaptador). Time.timeScale 1.5: velocidades respecto al tiempo real 9.75 horizontal, 20.25/30/18.75 vertical inicial. No modificar estas físicas ni ICC.

---

# Último avance experimental — Stereo Madness editado por el usuario (26/11/2026)

El usuario abrió Level Editor V6.4 y publicó el primer delta de Stereo Madness: [registro y verificación](../tools/level_editor/changes/INDEX.md), commit `9f1dba739338759ef27f6dfc5b2ab5e169e3586e`. Geometría objetivo: 2998 registros (69 ediciones, 86 altas, 103 bajas), cero monedas. Revisar cuatro grupos de sólidos superpuestos. El delta remoto y la salida del publicador coinciden. No asumir aplicación a Active local ni validación completa de juego. Los otros cuatro mapas conservan V5 y main sigue V6.3. Para próximos trabajos, recuperar este delta antes de reconstruir Stereo Madness experimental. Sincronizar rama local por fast-forward después de actualizaciones documentales remotas.

---

# Relevo actual — V6.4 experimental (niveles-mejorados)

V6.3 sigue siendo la versión estable de main. El último avance experimental es V6.4 Level Editor 1.0 y corrección visual inicial del Cube. Leer [guía de V6.4](LEVEL_EDITOR_V64.md), [validación](../tools/level_editor/VALIDACION_V64.json), [índice de deltas](../tools/level_editor/changes/INDEX.md). El usuario ya abrió el editor, editó Stereo Madness y publicó el primer delta. Quedan pendientes las pruebas completas de Unity, APK y BLE y verificar Active local; no cambiar main. Para continuar, verificar HEAD de ambas ramas y el estado local. El editor solo existe en Unity Editor; los mapas editados se aplican mediante datos JSON con runtime mínimo. Firmware, BLE BILATERAL LEGACY, parámetros, cooldown, física y submódulo no modificados. RCA obligatorio: registrar nuevos deltas y pruebas; no publicar recursos originales. Después de instalación, Unity > BIOFIT > Level Editor > Abrir editor. La siguiente tarea la define el usuario.

---

# BIOFIT DASH — relevo de desarrollo V6.3
La versión V6.3 fue aprobada como estable de trabajo por el usuario el 24/11/2026. Leer primero [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md), [ACEPTACION_V63.md](ACEPTACION_V63.md), [ASSET_SOURCES.md](ASSET_SOURCES.md), [referencias de diseño](referencias/V63/README.md) y [validación](../tools/campaign_v63/VALIDACION_V63.json). Verificar el SHA remoto de main antes de afirmar que está consolidada. El menú V6.1 es runtime; la escena antigua en Edit Mode es normal. Los mapas 2–5 conservan V5 y Stereo Madness tiene 3015 registros. DIPL, ICC, RFD, BRO y FLM obligatorios. Usar siempre el mismo proyecto local, preservar BLE BILATERAL LEGACY, firmware y cooldown, no modificar exclusiones. Contexto histórico: [V5](history/CONTEXTO_ACTUAL_V5.md), [relevo V5](history/CONTEXTO_VIVO_V5.md). La siguiente tarea la define el usuario.
