# BIOFIT DASH v0.9.8 — ventana corta de salto anticipado (sin cola)

**Estado:** propuesta de integración en PR borrador, pendiente de compilar en Unity y validar en Android. Base exacta: `feat/final-jump-control-v097` (PR #13), commit `63cce5f17c75294f29f73ce570d49beb1ddf9a96`.

## Motivación

En prueba Android v0.9.7 de 50 elevaciones bilaterales, el usuario observó pérdidas de comandos en ritmo rápido. El informe asociado contabilizó 40 saltos bilaterales aceptados, 35 pares rechazados por enfriamiento bilateral de 900 ms, cero rechazos por cubo en aire. Es **incorrecto concluir que la falta de jump buffering explica los 35 rechazos**: éstos suceden *antes* de `BiofitDashCore.TryJump`.

El usuario solicita una ventana anticipada de ~0,1–0,2 s pero **sin acumulación de saltos**, y quiere explorar en etapas futuras Ship y Wave controlados por movimientos corporales discretos de salto. No implementar aún lógica específica Ship/Wave ni cambiar firmware.

## Implementación mínima

- Nueva clase de producción `BiofitJumpBuffer` (con `.meta` versionado). Solo se habilita cuando `SubmitJump` recibe un **par bilateral ya aprobado por el detector** (dos trackers, alineación, edad, emparejamiento, 0,12 g y ventana180 ms).
- Si el cubo está en suelo, `TryJump` mantiene la ejecución inmediata de v097.
- Solo si el cubo **desciende** y la predicción física de contacto queda a **120 ms o menos**, se admite una **única entrada pendiente**.
- Esa entrada vence por **reloj monotónico real**, a los **120 ms** desde su recepción. Una entrada adicional durante la espera retorna `AlreadyBuffered`, no reemplaza, acumula ni prolonga la caducidad del comando anterior.
- Si el cubo aterriza antes de vencer y el juego sigue activo, se ejecuta una vez en `Update`; si hay pausa, game over, reinicio, cambio de escena o vence, se cancela.
- **Sin modificación de la física** del cubo, gravedad, altura, FPS clamp, detectores BLE, umbrales, UUID, ESP32, firmware v2.2.7, NVS, sesiones ni exportación.
- **Contabilización:** `JUMP_COMMAND result=Buffered` al admitir el único comando; `JUMP_BUFFER_Executed id=...` y `SALTO_BLE_REALIZADO` solo al ejecutarlo. `JUMP_BUFFER_Expired` / `Cancelled` dejan evidencia de intentos no ejecutados; `AlreadyBuffered` se rechaza sin una segunda ejecución. En el panel aparece `BUF` si hay un comando esperando.
- La regla solo se aplica a `source=BILATERAL`. Para diagnósticos unilaterales y botón/teclado, la semántica de v0.9.7 continúa inalterada.
- Identificador de archivos de nuevas sesiones `BIOFIT_v097_...` **sin cambios** (mantiene el formato y la lectura de histórico), aunque la etiqueta UI y `CONTROL_VERSION` muestran v0.9.8; no asumir que el prefijo del CSV es la única fuente de versión.

## Límites explícitos

- **El enfriamiento bilateral de 900 ms permanece intacto.** El cubo aterriza alrededor de 750–800 ms a 30–120 FPS. Por lo tanto, un segundo par detectado antes de 900 ms sigue filtrado y nunca llega al buffer. **No prometer que este cambio convierte cinco saltos físicos a 0,5s en cinco acciones.** Tampoco acelera la detección IMU/BLE, ni reduce mágicamente la demora subjetiva gesto→píxel de 100ms.
- Un par BLE de frenado de descenso **que supere los filtros previos** puede seguir considerándose un movimiento nuevo: 120ms de buffer no clasifica fases biomecánicas. La ventana corta evita acumular eventos, pero no certifica cero falsos.
- En niveles con colisiones, el buffer se consume al aterrizar **después** de `BiofitDashCore.Step`. Si la colisión se evalúa dentro del mismo frame en que cae, podría ser demasiado tarde para evitarla. El ensayo usa `SIN COLISION: SI`, y la integración de niveles requerirá revisar orden de resolución física/colisión antes de exigir *frame-perfect*.
- La predicción de aterrizaje se basa en la física integrada actual del cubo; cuando hay `FRAME_CLAMP`, el reloj real de caducidad prevalece sobre el reloj simulado del juego. La acción puede vencer sin ejecutarse si Unity deja de actualizar.
- Sin cola de múltiples saltos, sin ejecución diferida de una pulsación antigua y sin retocar `PairCooldownSeconds`.

## Validación automática

Prueba `tests/JumpControl` añadida al CI existente: salto a suelo inmediato; segundo salto admisible faltando <=75ms; segundo comando durante espera no se acumula ni reinicia expiración; se ejecuta una sola vez; nunca tras vencimiento de 120ms, pausa o reinicio; comandos tempranos y manuales no se almacenan; demostración explícita de que par posterior a >=900ms encuentra cubo ya en suelo en FPS normal. El test reutiliza `BiofitDashCore` real y el nuevo buffer puro (no inventa una física distinta).

**No confundir pruebas host/CI con Unity Editor/APK Android.**

## Protocolo físico corto (solo después de compilar v0.9.8)

1. Respaldar `Assets/Packages/ProjectSettings` y cambios locales; cambiar por GitHub Desktop a `feat/short-jump-buffer-v098` con Unity cerrado. Si aparece conflicto, no descartar cambios.
2. Unity 6000.3.28f1: abrir escena usada por v0.9.7, Console sin errores, Play muestra v0.9.8. Build Android APK conservando identificador y firma. Actualizar **sin desinstalar ni borrar datos**.
3. En Android conectar 2 sensores, `BLE: BILATERAL`, `EXP: NO`, `UP: 0,12 g`, `PAR: 180 ms`, `SIN COLISION: SI`. Reiniciar.
4. `INICIAR` → `RAW ON` con ambos contadores en aumento → `QUIETO` 5s → `DASH` → `SYNC 001`. Hacer 5–10 elevaciones bilaterales seguras, registrando velocidad aproximada y respuesta visual por gesto, sin forzar impacto ni equilibrio.
5. `QUIETO` 5s → `SYNC 002` → `FINALIZAR` → `ARCHIVO`. Verificar CSV principal y RAW en Descargas, ambos SHA256 OK, conservar originales.
6. Comparar `BILATERAL_DECISION`, `JUMP_COMMAND`, `JUMP_BUFFER_Executed/Expired/Cancelled`, `SALTO_BLE_REALIZADO` y `FRAME_CLAMP`. Si cero buffer en el experimento, **es esperable** con 900ms de cooldown; no concluir que falló.

## Arquitectura posterior: Ship / Wave por impulsos

Conservar entrada única (`BilateralJumpCandidate`) y estudiar *impulse-based controls* en rama separada:
- **Ship**: cada elevación válida añade un impulso discreto ascendente y la gravedad devuelve al descenso. Control de vuelo por ráfagas corporales, no exige una señal HOLD.
- **Wave**: cada elevación válida puede conmutar pendiente o generar un segmento corto ascendente, sujeto a límites de cadencia, evitando exigir 2–3Hz si los tobillos no entregan esa tasa.
- Testear jugabilidad primero con teclado y luego mapeo de sensores, sin incluirlo en PR de buffer.

