# Último avance experimental — Stereo Madness editado por el usuario (26/11/2026)

El usuario abrió Level Editor V6.4 y publicó el primer delta de Stereo Madness: [registro y verificación](../tools/level_editor/changes/INDEX.md), commit `9f1dba739338759ef27f6dfc5b2ab5e169e3586e`. Geometría objetivo: 2998 registros (69 ediciones, 86 altas, 103 bajas), cero monedas. Revisar cuatro grupos de sólidos superpuestos. El delta remoto y la salida del publicador coinciden. No asumir aplicación a Active local ni validación completa de juego. Los otros cuatro mapas conservan V5 y main sigue V6.3. Para próximos trabajos, recuperar este delta antes de reconstruir Stereo Madness experimental. Sincronizar rama local por fast-forward después de actualizaciones documentales remotas.

---

# Relevo actual — V6.4 experimental (niveles-mejorados)

V6.3 sigue siendo la versión estable de main. El último avance experimental es V6.4 Level Editor 1.0 y corrección visual inicial del Cube. Leer [guía de V6.4](LEVEL_EDITOR_V64.md), [validación](../tools/level_editor/VALIDACION_V64.json), [índice de deltas](../tools/level_editor/changes/INDEX.md). El usuario ya abrió el editor, editó Stereo Madness y publicó el primer delta. Quedan pendientes las pruebas completas de Unity, APK y BLE y verificar Active local; no cambiar main. Para continuar, verificar HEAD de ambas ramas y el estado local. El editor solo existe en Unity Editor; los mapas editados se aplican mediante datos JSON con runtime mínimo. Firmware, BLE BILATERAL LEGACY, parámetros, cooldown, física y submódulo no modificados. RCA obligatorio: registrar nuevos deltas y pruebas; no publicar recursos originales. Después de instalación, Unity > BIOFIT > Level Editor > Abrir editor. La siguiente tarea la define el usuario.

---

# BIOFIT DASH — relevo de desarrollo V6.3
La versión V6.3 fue aprobada como estable de trabajo por el usuario el 24/11/2026. Leer primero [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md), [ACEPTACION_V63.md](ACEPTACION_V63.md), [ASSET_SOURCES.md](ASSET_SOURCES.md), [referencias de diseño](referencias/V63/README.md) y [validación](../tools/campaign_v63/VALIDACION_V63.json). Verificar el SHA remoto de main antes de afirmar que está consolidada. El menú V6.1 es runtime; la escena antigua en Edit Mode es normal. Los mapas 2–5 conservan V5 y Stereo Madness tiene 3015 registros. DIPL, ICC, RFD, BRO y FLM obligatorios. Usar siempre el mismo proyecto local, preservar BLE BILATERAL LEGACY, firmware y cooldown, no modificar exclusiones. Contexto histórico: [V5](history/CONTEXTO_ACTUAL_V5.md), [relevo V5](history/CONTEXTO_VIVO_V5.md). La siguiente tarea la define el usuario.
