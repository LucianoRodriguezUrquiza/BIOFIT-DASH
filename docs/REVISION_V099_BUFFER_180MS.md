# BIOFIT DASH v0.9.9 — prueba experimental de 180 ms (2026-11-18)

## Evidencia de v0.9.8 utilizada

Sesión Android `55235516`, archivos originales `BIOFIT_v098_20261118_204732_55235516.csv` y `_RAW.csv`, inspeccionados localmente (no subidos al repositorio). Ambos footers: SHA256 **correcto**. CSV principal: 4037 filas, 197 eventos IMU, 0 descartes de grabación; RAW: 4520 muestras (2265 izquierda, 2255 derecha), `gap_seq=0` en todas las filas. Dos `FRAME_CLAMP` registrados.

Marcas en Unity (segundos): SYNC001=91.944, SYNC002=129.690, SYNC003=151.381, FINAL=195.739. El último bloque no tuvo SYNC004; los eventos relevantes terminan aproximadamente en t=165,2 s.

| Bloque | Comandos `JUMP_COMMAND=BILATERAL Accepted` | `PairCooldown` | Usuario |
|---|---:|---:|---|
| A (SYNC001–SYNC002) | 11 | 9 | Una respuesta durante descenso |
| B (SYNC002–SYNC003) | 10 | 10 | Perfecto |
| C (SYNC003–final) | 10 | 10 | 3 de 10 elevaciones sin salto percibido |
| Total | **31** | **29** | 30 elevaciones relatadas |

**Resultados críticos:** 31/31 comandos recibidos por el juego fueron `Accepted` con `altitude=0.000`; **cero `JUMP_COMMAND Buffered` y cero `JUMP_BUFFER_Executed`/Expired/Cancelled**; por lo tanto **el buffer de 120 ms NO se activó** en esta sesión y no explica la percepción de mejora. Los 29 candidatos bilaterales fueron rechazados antes de llegar al juego debido a cooldown de 900 ms. Puede haber impulsos de bajada, duplicados de un gesto humano, falta de emparejamiento o movimientos nuevos: sin marcadores físicos individualizados no se puede inferir asignación 1:1. En especial, 10 comandos del bloque C **no contradicen** 3 saltos humanos perdidos perceptivamente si otros comandos fueron emitidos en instantes no previstos. Medianas de intervalos de comandos aceptados: bloque A ~1.404 s, B ~1.269 s, C ~0.970 s.

## Cambios v0.9.9 (solicitados por usuario)

- Nuevo **PR en borrador**, base `feat/short-jump-buffer-v098` (PR #14). v0.9.8 permanece intacta como referencia.
- Ventana `BiofitJumpBuffer.WindowSeconds`: **0.120 → 0.180 segundos**. Un solo slot; no extender ni encolar; caducidad por reloj monotónico de 180 ms. Solo entrada bilateral aceptada **en descenso**, dentro de últimos 180 ms de vuelo; al aterrizar ejecuta una vez si sigue vigente.
- Etiqueta GUI `v0.9.9`; `CONTROL_VERSION=0.9.9 ... jumpBufferMs=180`; indicador `PAR_BUFFERED_180MS`; nuevos archivos `BIOFIT_v099_...` manteniendo compatibilidad de sesiones antiguas.
- Pruebas host ampliadas: entrada con ~120–160 ms hasta aterrizar ahora admisible, un solo salto en aterrizaje; no apilamiento, caducidad 180 ms, pausa y reinicio. Firmware, detector, cooldown900ms, 0.12g, 180ms entre tobillos, física del cubo, BLE y exportación/RAW **sin cambios**.

## Limitaciones de interpretación y próximos pasos

**Aumentar el buffer NO corrige los 29 `PairCooldown`, ni garantiza recuperar tres elevaciones del bloque C.** Dado que el detector acepta un nuevo salto a partir de 900ms y el cubo normalmente aterriza a ~750–800ms, el buffer de 180 ms seguirá teniendo uso escaso a 30–120 FPS. El incremento aumenta en 60ms la vida útil de una entrada ya válida en casos excepcionales y puede aumentar saltos tardíos de comandos falsos; no reduce latencia física de BLE/IMU. El usuario podrá evaluar sensaciones en Android, pero la comparación de eficacia del nuevo buffer exige encontrar/inducir casos reales `Buffered` (sin forzar riesgos físicos ni cambiar física).

**No fusionar hasta Unity Editor y Android APK físico.** Mantener v0.9.8 como alternativa operativa. Si persisten pérdidas rápidas, el problema a investigar es la protección de 900 ms / clasificación de fase, no aumentar indefinidamente el buffer. Evaluar bajar cooldown solo con un ensayo discriminante que etiquete verdaderos movimientos nuevos vs impulsos de descenso.

## Prueba corta de aceptación

Con Unity cerrado: backup `Assets/Packages/ProjectSettings`, GitHub Desktop Fetch origin, pasar a `feat/jump-buffer-180ms-v099` conservando cambios locales y deteniéndose ante conflictos. Abrir Unity 6000.3.28f1, comprobar GUI `v0.9.9`, Console; compilar APK Android conservando Package Name y firma, instalar encima sin desinstalar, verificar `SESIONES`. BLE 2/2, alineado, `EXP:NO`, `UP:0.12g`, `PAR:180ms`, `SIN COLISION:SI`, reiniciar. INICIAR, RAW ON, QUIETO5s, DASH, SYNC001; hacer 10–15 movimientos bilaterales naturales, separar subbloques con SYNC y observar en qué movimiento hubo fallo o doble. SYNC final, QUIETO5s, RAW OFF, FINALIZAR, ARCHIVO, verificar pares de CSV y SHA, aportar observaciones. **No afirmar éxito si se registran 0 eventos buffered:** solo prueba estabilidad sin identificar mejora atribuible al parámetro.

