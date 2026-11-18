# BIOFIT DASH v0.9.6.1 — RAW y panel Android

**Base:** `fix/export-recovery-ui-v096` (PR #11). **Nuevo PR:** #12, borrador. **Alcance:** bugfix/UX; conservación total de algoritmo de saltos, umbrales, escritor SHA256, MediaStore, SAF, BLE UUID, firmware v2.2.7 y NVS.

## Hallazgo en captura del usuario

En screenshot Android 1536×691 px se observa `BLE: SOLO DER`, `Sensores: 2/2`, `Reloj: ALINEADO`, `CAL:OK` en ambos, `EXP: NO`, `RAW: OFF`, `EXPORTADO: CSV VERIFICADO`, `CSV guardado SHA256 OK` y pie `RAW OFF local; STREAM,0 no confirmado: A task was canceled. n=0`.

**No implica falla de encendido STREAM,1:** el estado `EXPORTADO` muestra captura anterior finalizada. Código original requiere `INICIAR` antes de activar RAW; el botón ignoraba las pulsaciones sin advertencia. `A task was canceled` informa falta de confirmación de un **STREAM,0 de apagado**, no necesariamente fallo de BLE por tiempo real. La v0.9.6 fue validada previamente con pareja principal/RAW continua de ~11min, por lo que el canal puede funcionar. Esto no prueba que funcione en toda conexión nueva.

**Otro problema real:** `CompleteCaptureAsync` y `OnApplicationPause` podían volver a transmitir STREAM,0 a ambos GATT aun después de cerrar y confirmar el apagado de RAW; se producían cancelaciones redundantes que ensuciaban la interfaz y retrasaban el cierre de sesiones.

## Cambios

- `BiofitBLEMonitor`: `ToggleRawCapture` explica por qué no puede activar RAW si no hay captura activa, si falta algún sensor o si todavía espera un comando BLE. Nuevo `RawButtonLabel` muestra `RAW: ESPERE` durante la transición para no aparentar ON antes de ACK.
- `BiofitBLEMonitor`: booleano interno `rawMayBeStreaming` se activa desde el primer intento de STREAM,1 y solo se limpia tras confirmación `STREAM,0` de ambos dispositivos. `SetRawCaptureAsync(false)` omite comandos redundantes cuando no existe posibilidad de streaming. Si una orden falla, mantiene la necesidad de reintentar el apagado. No se inventan ACK positivos.
- `BiofitDashLayout`: ancho lateral dinámico `min(500, max(410, 0.30*width))`; para la captura del usuario el panel pasa de ~412 a ~493 unidades virtuales, campo de juego un poco más estrecho.
- `BiofitDashGame`: versión visible `v0.9.6.1`, botones `INICIAR`, `FINALIZAR`, `ARCHIVO`, `RESCATE` (sin números), EXP y RAW contiguos y suficientemente anchos, texto grande para el requisito `RAW: PRESIONAR INICIAR PRIMERO`; estado RAW separado del exportador (sin mezclar log largo con SHA o `EXPORTADO`).

## Validación

Pruebas host aumentadas: `tests/DashUiRegression` verifica el panel en 1536×691 y toque RAW antes de iniciar; `tests/RawRegression` verifica que no se mande STREAM,0 sin intento previo, y que FINALIZAR no lo duplique si ambos STREAM,0 fueron reconocidos.

CI GitHub Actions obligatorio antes de instalar. **No se ha compilado APK en Unity ni probado en Android en esta rama.** No fusionar PR #12 ni descartar archivos locales del usuario.

## Prueba manual corta recomendada (sin nuevo experimento de tobillos)

1. Sin reinstalar (APK 0.9.6): asegurarse de que los dos sensores estén conectados; pulsar RAW antes de INICIAR para observar si el problema es únicamente la restricción de sesión. Para guardar RAW, pulsar INICIAR y recién luego RAW. Si eso funciona, el protocolo puede continuar con la v0.9.6 ya instalada y las mejoras visuales pueden esperar.
2. Si decide actualizar la interfaz: en GitHub Desktop preservar los 9 archivos locales, cambiar a `fix/raw-ux-v0961`, abrir Unity, Console/Play sin errores, compilar APK manteniendo package ID y firma, instalar encima de v0.9.6 sin borrar datos.
3. En APK parcheada: RAW antes de INICIAR debe avisar; INICIAR muestra GRABANDO; RAW pasa a ESPERE y luego ON con contadores izq/der creciendo; apagar confirma ambos STREAM,0; FINALIZAR guarda SHA; ARCHIVO exporta pareja y verifica. Una única prueba corta con RAW ON basta para validar que el bug de UX se resolvió.
4. Si RAW permanece OFF **durante** una captura iniciada, conservar captura de las líneas RAW Status y Readiness, incluyendo `STREAM,1`/GATT. No atribuir a UX ni tocar firmware/NVS sin analizar dicho error.