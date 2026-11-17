# v0.9.5 — una captura integral, RAW opcional, sin cambiar el detector

Base: `audit/lift-phase-v094`, commit `52d61c0c2ca9aac6a3266f4f6181f04be8cfe005`. Firmware compatible y sin modificar: v2.2.7 `9cb5b7ac06315b0b657a33b8e608aca335eccb9e`. Esta revisión agrega adquisición diagnóstica, no implementa el clasificador propuesto en la [auditoría de fase](AUDITORIA_FASE_ELEVACION.md).

## Cambios implementados

| Archivo | Cambio / motivo |
| --- | --- |
| `BiofitRawLink.cs` | Suscripción RAW y comando CONFIG existente `STREAM,1`; apagado con `STREAM,0`; exige notificación `OK,STREAM,n`, además de escritura GATT. Transiciones serializadas, cancelación de operaciones y limpieza ante activación parcial. |
| `BiofitRawCapture.cs` | Copia de 20 bytes en callback hacia cola independiente de 2.048 paquetes. Un worker interpreta y registra seis ejes, secuencia, sensor ms, recepción monotónica, huecos, duplicados/desorden, espera de worker y diferencia entre intervalos de recepción/sensor. RAW nunca se entrega al detector de juego. |
| `BiofitSessionFile.cs` | Cada archivo tiene worker y cola propia de 8.192 filas. Escritura progresiva UTF-8 en `.partial`, cierre/flush, SHA256 y conteos; renombrado a `.csv` solo después de verificar. Error de disco o saturación impide presentar ese archivo como completo. |
| `BiofitEventCapture.cs` | En captura de DASH, todas las filas van al writer: 6.000 queda como límite de vista previa en memoria, no del archivo. Corrige whitelist de `MODO_EXP_UMBRAL_ON/OFF`. Umbral por pie ahora se registra como CONFIG, con su ID y valor. |
| `BiofitBLEMonitor.cs` | Integra ambos sensores, cierre asíncrono, recuperación/exportación de pareja de archivos, marcas de sincronización y telemetría de impacto RAW. Un fallo RAW no ocupa la cola EVENT/STATUS/TRANSPORT. |
| `BiofitDashGame.cs` | Identificación v0.9.5, botones RAW OFF/ON y SYNC numerado, contadores RAW izquierdo/derecho y estado real de cierre. Conserva EXP A/B/A y controles previos. |
| `tests/RawRegression`, pruebas Receiver/UI y workflow | Regresiones de transporte simulado, almacenamiento, cierre asíncrono, marcas/configuración y fallos. |

**Invariantes:** no se cambian `BiofitSingleFootDiagnostic`, `BiofitBilateralJumpDetector`, `BiofitDashCore`, filtros, ventanas, umbrales iniciales **0,25 / 0,25 / 0,12g y 180ms**, escenas locales, NVS, calibración ni UUID/protocolo. Se registra el estado EXP al INICIAR y cada transición ON/OFF. El modo guiado antiguo de Diagnóstico BLE conserva su comportamiento de portapapeles acotado; la sesión integral se realiza desde **BIOFIT DASH**.

## Ciclo y fallos

- RAW empieza apagado. Con ambos sensores conectados y captura activa, RAW ON suscribe cada característica existente `...0005` y ordena `STREAM,1` por CONFIG `...0004`. No usa `SAVE`, `CAL` ni cambios de parámetros. Esperar ON y que crezcan **ambos** contadores.
- La conexión inicial envía y espera `STREAM,0` para limpiar un posible estado residual de una ejecución anterior. Si falla esa extensión, el control EVENT sigue funcionando y RAW se informa no disponible.
- RAW OFF, FINALIZAR, pausa de aplicación y destrucción solicitan apagado. Si un sensor se desconecta, se desactiva RAW local de ambos, se invalidan callbacks viejos y se intenta enviar STREAM,0 a los dos. La captura de eventos continúa ante desconexión y la marca queda registrada.
- **Un enlace cortado no permite garantizar un comando remoto.** v2.2.7 no pone `streamRaw=false` en `onDisconnect`; no se cambió firmware. Sin conexión no notifica RAW, pero su flag puede seguir activo. En una conexión posterior se vuelve a ordenar STREAM,0. No se afirma apagado confirmado cuando faltó ACK. La aplicación no agrega reconexión automática nueva; una interrupción debe quedar identificada en la sesión.
- Cada operación tiene timeout mediante CancellationToken. El cierre puede tardar varios segundos si el enlace falla: la UI muestra “Cerrando”; no utiliza `Wait`/`Result` en producción. No comenzar otra captura ni exportar hasta terminar. Pausar Android o matar el proceso puede impedir completar continuaciones: `.partial` preserva lo ya escrito, **no se etiqueta como sesión finalizada**.
- El callback RAW solo valida tamaño/ID esperado, copia bytes y encola; parseo, texto y disco ocurren fuera de Unity. Hay colas separadas para RAW, archivo RAW y archivo principal. Ante saturación se cuenta la pérdida y se desactiva RAW al observarla. No se promete latencia física idéntica sin medir radio/Android; compartir CPU, GC y enlace BLE aún puede tener impacto.

## Archivos e integridad

`FINALIZAR` cierra dos archivos privados relacionados por el mismo ID:

1. `BIOFIT_v095_<fecha>_<id>.csv`: EV, APP, CONFIG, MARCA, DIAG; incluye resumen y calidad final RAW.
2. `RawCaptures/BIOFIT_v095_<fecha>_<id>_RAW.csv`: solo RAW, si se activó.

`3. ARCHIVO` publica ambos en Descargas mediante el exportador MediaStore ya existente. Cada uno conserva su propio footer SHA256 y conteo. El nombre RAW es distinto; pulsaciones repetidas pueden generar copias adicionales de Android, sin sobrescribir el original privado. Si exporta el principal y falla RAW, el mensaje distingue ese resultado; se puede reintentar. Si RAW quedó parcial, se advierte y no se exporta como válido. No se usa COPIAR para RAW.

Tras reiniciar la aplicación, ARCHIVO busca el último principal válido (incluye v0.8/v0.9 anteriores) y su compañero RAW si existe. Los RAW se guardan en subdirectorio para no desplazar al principal en esa búsqueda. Los registros anteriores no se borran. Actualizar APK con misma firma e identificador de paquete, **sin desinstalar ni borrar datos**.

Durante la captura no se acumula toda la sesión en memoria: preview acotado y colas limitadas. El hash final y el exportador actual sí leen el archivo completo, después de cerrar la captura; no se anuncia soporte ilimitado para sesiones de muchas horas. El protocolo siguiente genera pocos MB y dura aproximadamente 8–12 minutos. En falta de espacio se informa error, conservando parciales y originales. Solo una vista previa completa de ≤6.000 filas permite COPIAR como último recurso del principal; nunca se ofrece una vista truncada como exportación completa.

## Qué significa cada reloj

RAW conserva sensor ms u32 y secuencia u16 originales, manejando rollover dentro del segmento. Cada ON inicia `RAW_SEG_n`, sin imputar como pérdida los paquetes del intervalo OFF. Los huecos de secuencia reflejan notificaciones faltantes; **no equivalen a muestras perdidas de la IMU interna**. Duplicados/desorden e inválidos se cuentan y no se usan como muestra nueva.

Columnas RAW: `tipo;etapa;callback_monotonic_s;tracker_id;secuencia;sensor_ms;ax_g;ay_g;az_g;gx_dps;gy_dps;gz_dps;gap_seq;delta_rx_menos_sensor_ms;raw_worker_queue_ms`. XYZ son ejes físicos del chip, no anatómicos. No se estima altura/velocidad ni fase como dato medido.

- `delta_rx_menos_sensor_ms = Δrecepción − Δsensor`: identifica variación de entrega/ráfagas. Puede ser negativo; no es latencia BLE absoluta.
- `raw_worker_queue_ms`: tiempo desde callback administrado a procesamiento RAW; no es espera en la cola del juego.
- DIAG `RAW_CLOCK_ANCHOR`: pares sensor/recepción de STATUS a 10Hz desde que se crea captura RAW, para estimar alineación offline por tracker. No se cambia el estimador usado por el juego.
- DIAG `RAW_ACTIVE`: por segundo, conteos por pie, pérdidas, cola/escritura RAW, máximo de tiempo en callback RAW, media/máximo de cola EVENT, cantidad de eventos y máximo intervalo entre Update. Este último es intervalo del monitor, **no FPS de render**.
- TRANSPORT existente informa máximos acumulados desde arranque (`qMax`, `edadMax`, `notifyMax`, etc.). Comparar aumentos y tramos ON/OFF; no tratarlos como máximos exclusivos de cada bloque. Las columnas de EV conservan tiempos y cola existentes.

`SYNC 001`, `SYNC 002`… aparece en pantalla al pulsar SYNC y crea MARCA con reloj Unity más DIAG con reloj monotónico. Filmar la pulsación/cambio visible y repetir al inicio/final de bloques permite ajustar offset y deriva del vídeo. El timestamp de UI **no es el instante del fotón**: reportar al menos incertidumbre de un frame, más latencia de UI/cámara. Los dos ESP32 tampoco comparten reloj físico.

## 20Hz: decisión para esta sesión

**Conservar 20Hz y no reflashear ahora.** Para discriminar lóbulos separados ~0,7s se obtienen aproximadamente 14 instantáneas entre ellos. Con vídeo de pies/pantalla y EV es suficiente para investigar la fase probable y el efecto de umbral. No garantiza capturar máximos ni permite reproducir las dos muestras consecutivas del detector de ~200Hz; el filtrado de 22Hz tampoco convierte el flujo de 20Hz en una adquisición libre de aliasing.

Presupuesto incremental de los dos sensores (sin overhead de radio, retransmisiones ni STATUS/EVENT):

| Tasa por sensor | Notificaciones RAW/s totales | Datos de valor/s | ATT aproximado/s (20+3 bytes) |
| --- | ---: | ---: | ---: |
| 20Hz actual | 40 | 800B/s | 920B/s |
| 100Hz hipotético | 200 | 4.000B/s | 4.600B/s |
| 200Hz hipotético | 400 | 8.000B/s | 9.200B/s |

Es un cálculo de volumen, **no garantía de capacidad/latencia**. Hay dos conexiones, overhead de capas inferiores, scheduling Android y llamadas notify. Aumentar tasa exigiría cambiar firmware y probar prioridades/buffering; muestrear `liveSnapshot` más rápido no garantiza recuperar todas las muestras originales. No hace falta para esta sesión de hipótesis y sensibilidad. Sí será necesario un registro de cadencia IMU y estado interno sincronizados para validar/reproducir un clasificador fino, idealmente por lotes o registro local con secuencia, pérdidas y presupuesto medido, antes de promover un algoritmo nuevo.

Fuentes primarias: [ATT, tamaño de notificación limitado por MTU−3](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core-54/out/en/host/attribute-protocol--att-.html); [Unity 6.3, continuaciones y contexto principal](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/optimization/async-await-support/async-awaitable-continuations). Las firmas Subscribe/Unsubscribe/Write con CancellationToken se contrastaron con el código de UniBLE v0.1.1 usado por el proyecto. Estas fuentes no certifican el teléfono BIOFIT.

## Una compilación y una sesión

### Preparar una sola APK

1. Preservar los cambios locales de escenas/configuración del usuario antes de cambiar de rama. Usar `feat/raw-session-v095`; no fusionar PR ni descartar archivos locales.
2. En Unity 6.3 comprobar compilación C# y Play: panel v0.9.5, RAW OFF, EXP NO, 0,25/0,25/0,12g, 180ms; botones RAW y SYNC visibles en horizontal. En Play sin sensores, RAW debe indicar que faltan conexiones, sin bloquear el juego. Los tests host no prueban importación de Unity/IL2CPP.
3. Compilar **una APK** con identidad/firma existentes, escenas habituales y plugin UniBLE actual; instalar como actualización. Firmware v2.2.7 sin cambios. No se solicita APK intermedia ni se ha simulado su compilación.
4. Preparar otra cámara, preferentemente60fps, que vea pies y pantalla; mantenerla fija y grabar toda la sesión. Colocación habitual, ambos sensores2/2, CAL OK, reloj alineado, SIN COLISIÓN SI.

### Captura única integrada

Pulsar INICIAR una sola vez. Cada ciclo: subir, mantener arriba ~1s, bajar controladamente y apoyar ~1s. En cada bloque la mitad de ascensos son lentos y la mitad normales. Numerar intentos en voz alta; anotar temprano/tardío/omitido/doble. No usar golpes deliberados ni saltos altos. Pulsar SYNC antes de cada bloque y al final; registrar en voz alta bloque/umbral. Las comparaciones A/B/A son exploratorias y no aleatorizadas.

| Orden | Modo / umbral | RAW | Contenido |
| --- | --- | --- | --- |
| 1 | SOLO DER, EXP NO, 0,25g | OFF | QUIETO10s; **2 ciclos** (1 lento/1 normal), referencia de transporte sin RAW |
| 2 — A | SOLO DER 0,25g | ON | Esperar ACK y crecimiento de ambos contadores; QUIETO10s; ELEVACION **6 ciclos** (3 lentos/3 normales) |
| 3 — B | EXP SI, DER0,15g (dos pulsaciones −) | ON | **6 ciclos** con misma pauta; IZQ sigue0,25g |
| 4 — A' | EXP NO, confirmar DER0,25g | ON | **6 ciclos**, misma pauta |
| 5 | SOLO IZQ0,25g | ON | **6 ciclos**, control contralateral |
| 6 | BILATERAL/DASH, UP0,12g/180ms | ON | **6 ciclos**, 3 lentos/3 normales |
| 7 | BILATERAL, UP0,10g temporal | ON | **6 ciclos**, mismo patrón; restaurar0,12g al finalizar |
| 8 | SOLO DER0,25g, EXP NO | OFF | **2 ciclos** equivalentes al bloque1; QUIETO10s y SYNC final |

**Total40 ciclos**: se combinan A/B/A, pausa elevada y descenso en cada intento, sin repetir una segunda sesión de fase. Los cuatro ciclos RAW OFF son controles limitados de sobrecarga, no prueba estadística de equivalencia. Comparar eventos/colas/ráfagas dentro de actividad equivalente; no comparar solo quietud OFF contra movimiento ON.

Si falta ACK, uno de los contadores RAW no crece, se desconecta un sensor o aparece ERROR de almacenamiento, no continuar acumulando intentos como si el bloque fuera válido: marcarlo y finalizar conservando lo obtenido. No reinstalar ni borrar datos. Una sesión incompleta se informa como tal.

Al terminar: FINALIZAR; esperar “CSV guardado SHA256 OK” y estado RAW OFF, revisar errores/conteos; ARCHIVO. Conservar **los dos CSV completos** de igual ID y el vídeo original. No convertir a PDF/Docs ni usar portapapeles para RAW. Para verificar recuperación dentro de la misma ronda, después de exportar cerrar/abrir la aplicación y pulsar ARCHIVO **sin INICIAR**; comprobar que exporta la misma pareja (pueden aparecer copias con sufijo Android).

### Análisis posterior y criterio

Validar hash/filas de ambos archivos; localizar SYNC y CONFIG/EXP, alinear vídeo/Unity/monotónico y sensor por tracker con incertidumbre. Etiquetar inicio de ascenso, pausa y descenso de cada intento. Evaluar primer UP frente al corte y posible crecimiento posterior RAW; un pico no observado a20Hz no demuestra su ausencia. Medir temprano/tardío/omitido/doble por bloque y falsos en quietud. Contrastar RAW ON/OFF en pérdidas y colas EVENT, máximos de notificación y distribución de intervalos, sin llamar latencia física al tiempo de cola.

Resultado útil: decidir si el primer pulso débil se asocia con ascenso y el posterior con frenado, y si EXP adelanta respuesta a costa de falsos/dobles. No elegir nuevos defaults ni desplegar clasificador por este único ensayo. Si hace falta precisión de10ms, el siguiente hito es instrumentación a mayor frecuencia, no una reinterpretación de RAW20Hz.

## Verificación offline

Comandos: `dotnet run --project tests/{LatencyRegression,ReceiverRegression,AndroidDownloadsRegression,DashUiRegression,PhaseAudit,RawRegression}` (ejecutar cada proyecto); `python3 tests/check_android_export.py`; `python3 tests/PhaseAudit/replay_firmware.py ../firmware-audit-v227`; `git diff --check`.

Las pruebas RAW ejercitan ejes con signo, rollover, huecos, inválidos, duplicados, ACK ausente, timeout y fallo GATT, rollback, toggles, desconexión/pausa, más de6.000 filas, configuración/EXP/SYNC, compañero RAW, recuperación tras reinicio, errores IO y saturación. Stress sintético:24.000 RAW y12.000 APP más120 EV; el código de gameplay no recibe RAW. Se compila C# host y C++ host; **no Unity, APK, ESP32 ni radio Android real**. El workflow también conserva compilación Java heredada, que no constituye una APK ni el exportador activo.

Los fallos iniciales de regresión Receiver/UI exigían guardado síncrono: se actualizaron para esperar `CaptureCompletion` y comprobar el resultado real. No se retiraron las comprobaciones de archivo, SHA ni error de almacenamiento. Estado final y enlace de CI en el PR.

Resultado local: **273 comprobaciones C# aprobadas** (Latency58+9+31, Receiver36, AndroidDownloads16, UI68, Phase22, RAW33), contrato Android estático PASS y replay C++ de fase PASS. El ensayo RAW verificó24.002 filas válidas (24.000 de carga más2 del segundo segmento), cero rechazos de su writer y cero pérdida de su cola; también preservó las12.000 APP y120 EV. Es carga host sintética, no caudal BLE medido. CI remoto y commits se enlazan en el PR.
