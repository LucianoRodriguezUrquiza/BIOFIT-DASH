# BIOFIT DASH v0.7 — UI horizontal y diagnóstico unilateral

## Estado
- Rama: `fix/ui-unilateral-v07`; basada en `fix/ble-latency-consolidated` (Astra/v0.6).
- PR de revisión: https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/2
- **Firmware NO modificado**: mantener ambos trackers en v2.2.6. No borrar NVS.
- **No fusionar con main** antes de completar la prueba visual y física.
- Pruebas de GitHub Actions: `dotnet run --project tests/LatencyRegression`, `tests/ReceiverRegression` y `tests/DashUiRegression`. Estas pruebas no compilan una APK real.

## Cambios funcionales
1. BIOFIT DASH toma el **ancho horizontal disponible** sin columnas laterales desperdiciadas.
2. Los ajustes se presentan en **panel lateral a la derecha**, que no invade la pista y puede ocultarse. La pista se amplía al ocultarlo.
3. El encabezado separa puntuación, estado BLE, reloj, pausa, reinicio y control de práctica.
4. Los controles tienen etiquetas nuevas y compactas: `SIN COLISION`, `MOSTRAR/OCULTAR PANEL`, `1. INICIAR`, `2. FINALIZAR`, `3. COPIAR`.
5. Umbral unilateral inicial: **0,35 g** (antes 0,45 g). Sigue ajustable en pasos de 0,05 g, con mínimo 0,25 g.
6. Nuevo diagnóstico **por tobillo** en el panel: `UP` (eventos frescos observados en modo unilateral de ese pie), `bajo` (no alcanzó umbral de Unity), `anti` (antirrebote de 320 ms), `salto` (saltos ejecutados) y `ocupado` (orden aceptada pero el juego no permitió saltar, normalmente por estar en el aire). Se contabilizan por separado IZQ y DER.
7. El CSV anota las causas de descartes unilaterales en registros APP. El transporte BLE, el detector bilateral, el firmware y su calibración no cambiaron.

**Límites de interpretación:** estos contadores no conocen el número real de elevaciones humanas; un movimiento físico puede emitir cero, uno o varios UP_IMPULSE. Un descarte por umbral de Unity no equivale a una falla del firmware. El juego sigue siendo un prototipo experimental.

## Actualización en Windows sin perder trabajos locales
1. Cerrar Unity.
2. En GitHub Desktop elegir `BIOFIT-GAME-UNITY`. Si hay archivos en `Changes`, preservarlos mediante `Branch > Stash All Changes` antes de cambiar de rama. No descartar ni hacer merge.
3. Presionar `Fetch origin`.
4. `Current Branch` > `origin/fix/ui-unilateral-v07` > `Checkout` o crear rama local si lo solicita.
5. Confirmar `Current Branch: fix/ui-unilateral-v07`; no fusionar PR #1 ni PR #2.
6. Abrir el proyecto en Unity 6000.3.28f1. Revisar `Console` sin errores rojos.
7. Ejecutar `BIOFIT > Verificar BIOFIT DASH (sin APK)` y `BIOFIT > Verificar Input Engine (sin APK)`.
8. Confirmar `File > Build Profiles > Android` con `DiagnosticoBLE` y `BiofitDash` habilitadas y `SampleScene` deshabilitada. `Export Project` y `Build App Bundle` desactivados.
9. Compilar **una sola APK**: `BIOFIT_GAME_DASH_BLE_v0_7_UI_SOLO.apk`. No se necesita reflashear las ESP32.
10. Instalarla. Si Android muestra error de firma, preservar la APK anterior y consultar antes de desinstalar.

## Una sola sesión para comprobar la UI y los falsos negativos
1. Encender ambos trackers v2.2.6. Abrir APK y esperar `2/2` conectados y `Reloj: IMU OK`.
2. Abrir BIOFIT DASH, activar `SIN COLISION: SI`, y comprobar que el panel lateral derecho **no tape el cubo, obstáculos ni botones**. Sacar una captura de pantalla horizontal.
3. Probar `OCULTAR PANEL`: la pista debe ocupar más ancho. Restablecer con `MOSTRAR PANEL`.
4. Pulsar `1. INICIAR` una vez, quedar quieto 10 s y presionar `ELEVACION`.
5. Desde BLE OFF presionar el selector dos veces hasta `BLE: SOLO IZQ`. Dejar `SOLO UP = 0,35 g`.
6. Hacer **10 elevaciones izquierdas identificables**, separadas 2 s, contando por separado cuántas produjeron salto visible. Observar los contadores `IZQ UP/bajo/anti/salto/ocupado`.
7. Cambiar el selector BLE una vez hasta `BLE: SOLO DER`; realizar **10 elevaciones derechas** equivalentes. Observar los contadores DER.
8. Pulsar `DASH`; desde SOLO DER presionar selector dos veces hasta `BLE: BILATERAL`. Confirmar `UP=0,12 g` y `PAR=180 ms`. Elevar ambos tobillos simultáneamente 10 veces, separados 2 s.
9. Pulsar `QUIETO`, esperar 10 s para detectar falsos positivos de reposo.
10. Pulsar `2. FINALIZAR`, luego `3. COPIAR`, pegar el registro completo en una nota y guardarlo.
11. Capturar pantalla del panel técnico y adjuntar el CSV. Relatar cuántos **intentos físicos** fueron exitosos en cada modo.

**Si un intento falla**, no modificar umbrales durante esta sesión: usaremos `bajo`, `anti`, `ocupado` y las magnitudes UP del CSV para identificar la causa. Si una elevación no genera ningún UP, será necesario estudiar el detector del firmware por separado en una revisión posterior; no recalibrar automáticamente.
