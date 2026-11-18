# BIOFIT DASH v0.9.10 (experimental) — fase bilateral + buffer único 180 ms

## Resumen ejecutivo
Base **`feat/jump-buffer-180ms-v099`**, PR #15. Nueva rama **`feat/phase-gated-jump-v0910`**, PR #16 DRAFT. **No fusionar ni considerar validado físicamente** hasta ensayos con ambos tobillos. Firmware de apoyo en [BIOFIT-GAME-Firmware PR #3](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware/pull/3), rama `feat/phase-evidence-v228`, basado en SHA congelado `9cb5b7ac06315b0b657a33b8e608aca335eccb9e`.

**Objetivo:** permitir un par de elevaciones bilaterales distinto antes del cooldown rígido de 900 ms, sin ejecutar artefactos de frenado de la bajada ni acumular saltos. Se protege la configuración probada: al abrir la APK el modo FASE está APAGADO; el modo normal `BLE: BILATERAL` conserva la lógica y protección de 900ms de v0.9.9.

## Qué hace realmente el firmware

Cada firmware (izq/dcha) ejecuta su captura IMU interna ~224 Hz y SU DETECTOR ORIGINAL `UP_IMPULSE` v2.2.7 **sin modificación de frecuencia, umbrales, NVS, UUID, secuencia, tamaño de paquete, RAW o transporte**.

Nuevo helper de solo cabecera `BiofitPhaseEvidence.h`, separado del detector original:
- Tras un candidato calificado, observa fase acelerométrica vertical negativa: `fUp <= -0.115g`.
- Requiere un mínimo de **340 ms desde el último UP calificado**.
- Busca un período continuo de por lo menos **80 ms** con `|fUp| <= 0.065g`, actividad `<= 0.075g`, giroscopio `<= 23°/s` y norma aceleración próxima a la gravedad (tolerancia `0.095g`).
- Solo al completar ese rearme, el SIGUIENTE evento `UP_IMPULSE` existente recibe **flag bit4=`0x10`** (representa **evidencia de ciclo independiente, NO contacto con suelo confirmado**).
- Un evento UP no calificado no reinicia el reloj de rearme, de modo que una oscilación del descenso no oculte el siguiente gesto legítimo.
- Calibración y corrección auto-rest tras dos segundos estables vuelven a habilitar el primer ciclo de manera conservadora.

El paquete `EVENT` conserva 16 bytes y protocolo V2. El byte `b[3]` ya lleva las flags (bit7 se utiliza para edad en cola firmware); el bit4 nuevo solo se marca en `UP_IMPULSE`. Firmware v2.2.7 anterior marca sin bit4, por lo que se mantiene compatibilidad de recibirlo en Unity. Página `TRANSPORT` 3 anuncia firmware entero `228` y LCD `v2.2.8 EXP`.

**Limitación física:** un tobillo aparentemente quieto no demuestra apoyo plantar; puede estar elevado e inmóvil. El criterio de aceleración negativa + estabilidad es una **heurística conservadora**, no reconstrucción de posición, ni ZUPT confirmado. Nunca confundir el Z interno QMI con Z corporal ni signo de aceleración con signo de velocidad.

## Qué hace Unity

Modo BLE nuevo **`BLE: FASE EXP`** (quinto estado del botón que antes solo recorría OFF / BILATERAL / SOLO IZQ / SOLO DER). Permanece **desactivado** por defecto. `BLE: BILATERAL` conserva exactamente la puerta tradicional de 900 ms aunque ambos firmwares sean 2.2.8.

`BiofitBilateralJumpDetector.ProcessImpulse` recibe dos parámetros opcionales adicionales `phaseEvidence=false`, `phaseFastEnabled=false` para compatibilidad con código y tests previos. Cada candidato pendiente de tobillo almacena el bit de evidencia de ese UP (no la evidencia del pie contrario).

- Cada par bilateral debe seguir aprobando `UP >= 0.12g`, restricción por tobillo `>= 320ms`, diferencia entre sensores `<=180ms`, relojes alineados, secuencia fresca y protección antigüedad de la puerta BLE.
- Si **los dos** UP tienen bit4 y `FASE EXP` está activo, el intervalo mínimo entre pares pasa de 900ms a **600ms** solo para ESE par.
- Si falta un solo bit4 o no está seleccionado el modo experimental, el par exige los **900ms** originales. Ningún positivo de aceleración aislado o de un solo pie puede saltar el límite.
- Contadores y diagnóstico: `BILATERAL_DECISION=... phaseHint=X phaseMode=X fastPhasePair=X`, `PHASE_FAST_ACCEPTED`, `PAR_FASE_RAPIDO_VALIDADO`, HUD `faseRap N`.
- Se mantiene **`BiofitJumpBuffer.WindowSeconds=0.180d`**. Solo **un** comando bilateral anticipado, entrada reconocida cuando el cubo está DESCENDIENDO y a <=180ms del suelo; expira por reloj REAL 180ms desde admisión; no encola, no prolonga ni acumula, se cancela en pausa/game-over/reset. Si llega demasiado temprano se rechaza (`Airborne`); no ejecuta luego una cadena. La física del cubo sigue igual (velocidad inicial 575, gravedad 1560).

**Ejemplo matemático hipotético:** par inicial t=0, cubo aterriza ~0.75 s, segundo movimiento real en t~0.62 s con ambos bits válidos. Filtro de 600ms lo deja pasar, el cubo sigue descendiendo con ~130ms pendientes y el único buffer lo ejecuta una vez al aterrizar. Antes el par en t~0.62 era bloqueado por 900ms. El tiempo exacto y detección en Android son pendientes de validación real.

**Compatibilidad:** Unity v0.9.10 sobre dos firmwares v2.2.7 funciona en modo BILATERAL y, si el usuario selecciona FASE EXP por error, cae al cooldown normal de 900ms porque faltan bits nuevos. App v0.9.9 puede leer el firmware v2.2.8 por BLE normal porque ignora ese bit; no habilita fast pairing.

## Hallazgo de archivos existentes y evidencia externa

Último ensayo v0.9.9 `BIOFIT_v099_20261118_212159_31fd08c1.csv` + RAW: 62 pares aceptados, 56 completos bloqueados por cooldown, **0 saltos por buffer**, ~20 Hz en la exportación RAW, no ~224 Hz de la IMU interna.

Se simuló *offline* el helper contra RAW descargados (estimando la vertical del vector de gravedad de reposo; aproximación 20 Hz con 1 muestra cada ~50 ms). Primero se detectó que reiniciar fase ante UP no calificado oculta todos los movimientos futuros: se corrigió. Con lógica corregida y parámetros conservadores, una aproximación encontró **15 pares aceptados existentes con ambos bits de calidad** y **1 candidato cooldown** doblemente calificado, pero esto **NO es un conteo fiable de gestos verdaderos ni una prueba de precisión**: la aplicación real trabaja a ~224Hz, el RAW submuestrea, las orientaciones cambian y los eventos de calidad solo se calculan con la nueva versión de firmware. No hay etiqueta de fase/contacto humano sincronizada.

Literatura orientadora (no valida automáticamente parámetros de BIOFIT):
- https://pmc.ncbi.nlm.nih.gov/articles/PMC4284057/ (detección de intervalos estacionarios + problemas de IMU de pie)
- https://pmc.ncbi.nlm.nih.gov/articles/PMC12158269/ (revisión de ZUPT, ventanas de aceleración y giroscopio)
- https://pmc.ncbi.nlm.nih.gov/articles/PMC8599134/ (identificación de fases con sensores inerciales, limitación de contacto físico).

## Controles de seguridad, pruebas y aceptación

- **Regresión C# `tests/JumpControl`**: 900ms heredado aunque lleguen flags, 600ms mínimo en fase, evidencia ausente en cualquier tobillo bloquea fast, después de 900ms fallback normal, caso nuevo real t+620ms → buffer 180ms → UNA ejecución al contacto del cubo, sin cola.
- **Regresiones existentes C#**: receiver, UI, RAW/CSV/SHA, recuperación, export Java, archivo/retención, compatibilidad de sensores originales.
- **Firmware:** pruebas C++ host sobre la cabecera real (sucesión positiva sin fase negativa, exigencia de quietud continua, rechazo giro, replay rollover) + contratos de Arduino/NVS y **compilación separada de sketches** por Arduino CLI (requiere revisar estado de PR #3).
- **No hay prueba de Unity Editor real, BLE Android ni APK firmada en este entorno.** Los jobs C# son de host y los de Arduino no hacen flashing. No prometer resultado humano hasta Android.

### Protocolo A/B sobre la misma instalación

1. Primero conservar v0.9.9 APK, archivos de sesiones y firmware .ino v2.2.7. No mergear.
2. Con Unity cerrado, cambiar GitHub Desktop a rama `feat/phase-gated-jump-v0910`; abrir en Unity 6000.3.28f1. Menú `BIOFIT > Compilar APK Android v0.9.10 FASE EXP` (previamente File > Build Profiles > Android activo + Android Build Support). Salida local del proyecto en `Builds/BIOFIT_v0910_FASE_EXP.apk`. Se reutilizan package ID y firma actuales, si son compatibles; **no desinstalar APK existente** para no perder sesiones.
3. Descargar firmware rama `feat/phase-evidence-v228` y compilar/flashear los dos ESP32 **solo cuando Arduino CI haya pasado**. Cada display debe mostrar `v2.2.8 EXP` y mantener calibración con pie apoyado. Si hay problemas, reflashear v2.2.7 desde commit congelado, NO borrar NVS.
4. Android `DIAGNOSTICO BLE`, ambos 2/2 conectados, CAL OK, reloj ALINEADO, `BLE: BILATERAL` inicialmente. `EXP:NO`, `UP:0.12g`, `PAR:180ms`, `SIN COLISION:SI`. Probar 5–10 elevaciones para confirmar que el modo heredado no cambió.
5. Activar `BLE: FASE EXP` desde botón BLE (cicla pasando por SOLO IZQ y SOLO DER). No cambiar umbrales. `INICIAR` → `RAW:ON` → QUIETO 5s → DASH → SYNC001.
6. Hacer 10 elevaciones separadas ~1s; descanso 5s y SYNC002; 10 elevaciones ~0.7–0.8s, descanso 5s y SYNC003; 10 elevaciones anticipadas según aterrizaje (sin forzar >2Hz), descanso 5s y SYNC004. No pulsar SALTAR, PAUSA, REINICIAR durante los bloques. Registrar manualmente correctos/omitidos/dobles y momentos tardíos, opcional vídeo de pies + pantalla.
7. QUIETO 5s → RAW OFF (esperar ambos) → FINALIZAR → SHA OK → ARCHIVO (dos CSV mismo ID `BIOFIT_v0910`; export/rescate según necesidad).
8. Comparar con v099 y A/B del mismo día: saltos por gesto confirmado, falsos durante quietud y descenso, pareo fast, buffers realmente ejecutados, tiempos de sensor, pérdidas de evento RAW y latencia visual por vídeo. **No aceptar la variante** por obtener igual número de comandos que de movimientos sin referencia de esos movimientos, ni por CI verde.
9. Si aparece un doble falso en descenso: volver a `BLE: BILATERAL` sin reflashear (cooldown900ms). Si la APK causa problema, reinstalar actualización de APK v099 conservando datos; ante pérdida de compatibilidad de firma, NO desinstalar para forzar downgrade.

### Limitaciones y criterios de parada
No existe garantía de 100% sin falsos positivos para una IMU de tobillo sin contacto plantar; 20Hz RAW no representa todos los microeventos de 224Hz. Riesgo principal: pie elevado quieto puede parecer apoyo. El algoritmo deliberadamente prioriza seguridad de no dobles sobre admitir cada salto a 0.5s. Si no demuestra mejora sobre v099 o produce falsas activaciones, **no integrar ni fusionar**, continuar juego con base estable.
