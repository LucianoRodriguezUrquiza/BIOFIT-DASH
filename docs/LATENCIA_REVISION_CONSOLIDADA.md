# BIOFIT: revisión consolidada de latencia (2026-11-15)

Bases: Unity `fde68049f81b5e81d4d584ed7e1278b868c39718`; firmware `9bb4183db5d2bfd76bc0d9aeefd203d0543b315d`. Rama coordinada: `fix/ble-latency-consolidated` en ambos repositorios.

## Diagnóstico y alcance

**No está demostrada la causa exacta de los 1–2 segundos. No se declara resuelta la latencia física.** Los ejemplos suministrados demuestran compresión temporal anterior al callback C#; los CSV completos no estaban adjuntos. Se inspeccionó UniBLE v0.1.1 (`dba3f296`) y `BLECharacteristic.cpp` de Arduino-ESP32 3.3.0 y 3.3.5. En esas implementaciones, `notify()` usa notificación sin confirmación, no una espera fija de dos segundos. No se justifica migrar de stack ni modificar el plugin todavía.

| Hallazgo | Evidencia y ubicación | Gravedad / estado |
|---|---|---|
| Firmware entrega EVENT y STATUS desde el mismo bucle que LCD, USB y NVS | `.ino`, `loop()`; STATUS programado a 50 ms, observado ~2 s | Alta: acoplamiento confirmado; bloqueo responsable concreto **no confirmado** |
| Eventos antiguos podían activar el juego | `BiofitBLEMonitor.ReadEvent`: aceptaba estimaciones hasta 5 s sin filtro antes de ambos callbacks | Alta, confirmado y corregido |
| Reloj IMU se reiniciaba cada ~71,6 min | `.ino`: `s.us/1000`; STATUS y EVENT no compartían el rango completo de `millis()` | Alta para sesiones largas, confirmado y corregido; no explica necesariamente ensayo corto |
| Intervalo unilateral de 1,20 s | `BiofitDashGame.OnSingleCandidate` | Media: suprime respuestas consecutivas; no retrasa paquetes; reducido a 0,32 s |
| Secuencia recibida sólo se registraba | `BiofitBLEMonitor.ReadEvent` | Media: añadidos huecos, duplicados y descarte conservador de desorden del mismo tracker |
| Timestamps C# posteriores al handler Java | UniBLE Android `handleCharacteristicChanged`: `mainHandler.post` | Incertidumbre: el retraso Java previo al callback no está medido separadamente |
| Servicio GATT sin reserva explícita para extensiones | `.ino`, `startBLE()` | Reserva ampliada a 32 handles para seis características y CCCD |

## Cambios y compatibilidad

- Firmware 2.2.6: IMU prioridad 5 → cola FreeRTOS → tarea TX prioridad 2 en core 1 → EVENT prioritario; STATUS 10 Hz; TRANSPORT 1 Hz. LCD/USB/NVS quedan en `loop()` prioridad ordinaria. No se cambió el detector, filtros, ejes ni claves NVS.
- USB CDC usa timeout TX cero y Plotter sólo escribe cuando hay espacio para una línea. Las tres curvas y comandos permanecen. No se exige recalibrar; se mantiene la calibración automática de arranque ya existente.
- EVENT sigue siendo V2 y 16 bytes. Bit 7 de flags indica la extensión: bytes 14–15 little endian contienen espera en cola firmware en ms, saturada a 65535. Versiones anteriores siguen siendo legibles, pero no tienen esta métrica.
- STATUS conserva V2, 20 bytes y offsets. Se usa `millis()` real. En Unity se desenrolla su wrap de 49,7 días, se ignoran anclas duplicadas y se invalidan al reiniciar/desconectar.
- Nueva característica opcional `f9710000-6d68-4b7d-a742-6fae7b5f0007`, 20 bytes. Su ausencia no impide recibir V2. No cambia la dependencia UniBLE.
- Receptor → cola acotada a 1024 → `ReadEvent` → filtro de secuencia/edad → emparejamiento → callbacks de juego. Se rechaza si espera Unity + espera firmware >250 ms, o demora relativa estimada + espera Unity >250 ms. Sin reloj válido no se habilitan acciones. El umbral es de protección, **no una meta de latencia**.
- Todos los eventos leídos, incluidos antiguos/duplicados, conservan su fila EV. DIAG explica los rechazos y registra TRANSPORT; el CSV mantiene 15 columnas. El límite de 1500 filas sigue contabilizado. Una pérdida por límite de cola app se contabiliza aparte y no puede conservar su paquete.
- El emparejamiento conserva 0,12 g, 180 ms y cooldown bilateral de 0,90 s. Sigue siendo un par experimental, no confirmación biomecánica. Se mantiene aceptación de orden inverso entre tobillos; desorden/duplicación de secuencia del mismo tobillo no activa el juego.

TRANSPORT: byte 0 versión 1; byte 1 ID; u16 en 2 secuencia; u32 en 4 millis; u16 en 8 máximo profundidad cola, 10 eventos perdidos por cola firmware, 12 máximo edad cola ms, 14 máximo duración `notify` ms, 16 máximo intervalo de tarea TX ms, 18 errores GATT. Todos los enteros little endian; máximos/contadores acumulados desde arranque, saturados a 65535. `SUCCESS_NOTIFY` no confirma recepción Android. Huecos de secuencia tampoco identifican por sí solos pérdidas de radio.

## Verificación

Ejecutado localmente con .NET 8.0.428: 58 comprobaciones DASH, 9 InputEngine, 31 de reloj/edad/secuencia/CSV y 12 de integración del receptor: **110 comprobaciones sintéticas**. El receptor se compiló con sustitutos de API Unity/UniBLE exclusivamente para probar su lógica; no equivalen al motor, al plugin Android ni a una APK. Casos: ráfaga real 782→12 ms, desfases entre placas, par inverso 11 ms, reset/wrap, anclas insuficientes/caducadas/duplicadas, colas, antigüedad, duplicación y conservación CSV.

Ejecutado: `python tests/check_contract.py` en firmware comprueba equivalencia de ambos roles, conservación literal de funciones de calibración/NVS/clasificación (excepto corrección del timestamp), aislamiento TX y formato EVENT. Esto **no compila el firmware**.

Comandos repetibles en Unity:

```sh
dotnet run --project tests/LatencyRegression
dotnet run --project tests/ReceiverRegression
python tools/analyze_latency.py registro.txt
```

Se añadieron workflows GitHub Actions para estas pruebas y para compilar ambos sketch con Arduino-ESP32 3.3.5 y GFX Library for Arduino. El intento local de instalar el core quedó bloqueado por la descarga de índices desde Arduino CLI. Consultar el estado real del workflow antes de usar el firmware. Unity Editor/Android y hardware no están disponibles en este entorno.

## Una sola compilación Android y una sesión posterior

1. En GitHub Desktop, seleccionar **BIOFIT-GAME-UNITY → Fetch origin → Current branch → origin/fix/ble-latency-consolidated**. Hacer **Pull origin** si aparece. Repetir en **BIOFIT-GAME-Firmware**. Estos cambios no están en `main`.
2. Confirmar que Actions del firmware terminó correctamente. Si falla, no flashear: resolver ese error primero en la misma revisión.
3. Abrir cada `.ino` en Arduino IDE con las opciones habituales de su Waveshare. Compilar/subir izquierdo a su placa y derecho a la suya. Mantener **Erase All Flash desactivado** y la misma partición usada previamente; no usar `AXIS,RESET`, `DEFAULT` ni borrar NVS. Actualizar ambas porque comparten transporte y timestamps.
4. Abrir Unity 6000.3.28f1. Esperar a terminar importación. Ejecutar **BIOFIT → Verificar BIOFIT DASH (sin APK)** y **BIOFIT → Verificar Input Engine (sin APK)**. Si hay errores rojos, detenerse antes del build.
5. Abrir **BIOFIT → Crear o abrir BIOFIT DASH**. Compilar una sola APK Android de esta rama e instalarla. No hace falta generar APK por cada umbral.
6. Encender las dos placas en la orientación habitual y mantener pies apoyados durante el arranque. Abrir la APK; esperar **2/2 conectados** y **reloj: IMU estimado**. No mover para diagnosticar latencia mientras faltan anclas.
7. Pulsar **PRACTICA SIN COLISION: NO** una vez hasta que diga **SI**. Pulsar **1. INICIAR REGISTRO** una sola vez. Permanecer quieto 10 s.
8. Pulsar **ELEVACION**. Desde **BLE: OFF**, pulsar dos veces hasta **BLE: SOLO IZQ (DIAGNOSTICO)**. Pulsar **SOLO UP -** dos veces: verificar **0,35 g**. Hacer 5 elevaciones izquierdas cómodas, una cada 2 s. Observar si el cubo responde al inicio o al terminar.
9. Pulsar el selector BLE una vez: **SOLO DER**. Hacer 5 elevaciones derechas, una cada 2 s.
10. Pulsar selector BLE dos veces (OFF → BILATERAL). Mantener **UP 0,12 g / Par 180 ms**. Hacer 5 movimientos bilaterales del tipo ya ensayado, separados por 2 s. No interpretar automáticamente los pares como saltos reales.
11. Pulsar **CAMINAR** y caminar en el lugar 10 s; pulsar **CORRER** y repetir el gesto de carrera en el lugar ya utilizado durante 10 s. Pulsar **QUIETO** y permanecer 10 s.
12. Pulsar **2. FINALIZAR** y después **3. COPIAR REGISTRO**. Pegar el contenido completo, incluido resumen y cabecera CSV, en un archivo de texto. No iniciar una segunda grabación.
13. Para ver métricas por placa, usar **DIAGNOSTICO BLE**: qMax, drop, edadMax, notifyMax, cicloMax, err, rxHz, rxGapMax y statusGap. No hace falta otro ensayo para leerlas.

Conservar el CSV, versiones exactas del core/GFX, modelo Android y resultado de los 5 movimientos por modo. Si se dispone de otro teléfono, filmar simultáneamente tobillos y pantalla a 60/120 fps durante esas mismas elevaciones: contar fotogramas entre inicio visible y respuesta. Es una medición aproximada independiente de los relojes BLE; no hay medida absoluta movimiento-pantalla en el CSV.

## Incertidumbres y decisión siguiente

- `edadMax/cicloMax` altos: investigar scheduling/firmware. `notifyMax` alto: stack/control de congestión. Ambos bajos con `rxGapMax` alto y ráfagas: investigar conexión BLE y handler Java/Android. `espera_cola_unity_ms` alto: main thread de Unity.
- El reloj STATUS contiene una demora común desconocida. Un retraso constante compartido por STATUS y EVENT puede quedar oculto; el filtro no garantiza eliminar todo evento físicamente antiguo. Se registra explícitamente esta limitación; medir RTT/PING o vídeo si persiste.
- Pendientes: compilación Unity/Android real, aceptación física, negociación efectiva de intervalo BLE, medición de tiempo hasta callback Java, p50/p95 movimiento-pantalla y validación biomecánica. No se implementó reconexión GATT automática; al desconectar se invalidan anclas y pendientes, pero debe reiniciarse la app para una nueva conexión.
- `tools/analyze_latency.py` calcula p50/p95/máximo de espera Unity y demora **relativa** desde el CSV. No mezcla esos valores con latencia absoluta ni inventa percentiles físicos.
