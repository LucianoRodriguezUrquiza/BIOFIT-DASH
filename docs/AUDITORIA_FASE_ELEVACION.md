# Auditoría de fase de elevación — 2026-11-17

## Alcance y resultado

Referencias inspeccionadas: contexto maestro en `docs/contexto-maestro`; Unity v0.9.4 `6feb3c0fcfb205e87d7e6c9d606a1147285a87dc`; firmware v2.2.7 `9cb5b7ac06315b0b657a33b8e608aca335eccb9e`; [informe A/B](ANALISIS_CSV_20261117_AB.md). En esta auditoría se revisó el informe publicado, **no se volvió a descargar ni verificar el CSV original privado**. Sus conteos y SHA se atribuyen al análisis anterior. Las pruebas nuevas usan valores transcritos explícitamente y trayectorias sintéticas.

**Conclusión:** UP es un candidato de aceleración positiva, no un detector inequívoco de subida. Se reproduce con el código real que un frenado de descenso genera UP con velocidad física todavía negativa. Es una limitación demostrada respecto al objetivo biomecánico, no evidencia de corrupción ni explicación confirmada de todos los fallos derechos. No se cambia producción, firmware, NVS, calibración, UUID ni valores normales **0,25/0,25/0,12 g y 180 ms**. Se incorporan pruebas y este diseño para evaluación offline.

## Evidencia y confianza

| Conclusión | Evidencia | Confianza / límite |
| --- | --- | --- |
| Primeros impulsos derechos débiles quedan fuera | Informe: 7/10 primeros UP <0,25g; reproducción C# de esas amplitudes: 3 aceptados y 7 rechazados | Alta para decisión por umbral; agrupación en gestos no etiquetada |
| Un impulso posterior puede activar el salto | 0,172g a 180,550s rechazado; 0,373g a 181,319s aceptado; diferencia 769ms | Alta para selección de eventos; no es latencia biomecánica ni BLE |
| Tres primeros pares bilaterales no califican | Derecho 0,111/0,114/0,100g <0,12g aunque el otro pie sea fuerte y próximo | Alta para descarte; no aumentar ventana para corregir amplitud |
| Frenado descendente puede emitir UP | Tres reproducciones del `processSample` real, velocidad física conocida | Alta en simulación ideal; no identifica fase de los eventos humanos existentes |
| Unity no filtra fase | `BiofitSingleFootDiagnostic.Observe` y `BiofitBilateralJumpDetector.ProcessImpulse` reciben amplitud/tiempo/ID, sin trayectoria | Alta por código |
| BLE explica ~0,7s de retraso | Informe: cola Unity 17,4ms mediana/33,3ms máximo, sin pérdidas reportadas | No respaldado; no descarta toda demora externa ni mide gesto→píxel |
| Fallo exclusivo del firmware derecho | Sketches equivalentes salvo ID; contrato ejecutado | No demostrado; gesto, montaje y NVS real pueden diferir |

La frase del informe anterior «el firmware emite UP incluso para movimiento asociado al descenso» debe leerse ahora como **posibilidad reproducida sintéticamente**, no como fase física demostrada en aquel CSV.

## Qué hace realmente v2.2.7

`processSample` proyecta aceleración sobre la vertical propagada con giroscopio, resta gravedad y aplica paso bajo (default 22Hz). Integra `shortV = clamp(shortV*exp(-dt/0,40s) + fUp*g*dt, ±0,65m/s)`. Exige `fUp >= upOnG`, actividad suficiente, `shortV >= upVms` y dos muestras consecutivas. Defaults de firmware: 0,09g / actividad 0,035g / 0,022m/s; **son distintos de los cortes Unity** y NVS puede sustituirlos.

Ya existe histéresis: después de UP se desarma; marca `downSeen` con `fUp < -0,55*upOnG` y rearma después de `upGapMs` (default 260ms) en quietud o tras ese negativo con `fUp < 0,35*upOnG`. Aceleración negativa puede frenar una subida: `downSeen` tampoco certifica descenso. `MOV` usa actividad/gyro y tiempos de confirmación/liberación; no representa contacto con suelo. La quietud puede darse con pie elevado y también durante traslación ideal a velocidad constante.

El valor del paquete UP es `fUp` **al disparar**, no el pico posterior de todo el gesto. Por ello un primer evento 0,172g no prueba que la aceleración máxima de aquella subida fuera inferior a 0,25g. Unity no recibe una actualización de ese mismo evento cuando la aceleración crece. La relación entre umbral firmware y corte Unity merece medir RAW antes de cambiar criterios.

En Unity, el rechazo por umbral ocurre antes de registrar aceptación y su antirrebote de 320ms. Un segundo impulso a ~700ms puede pasar. `game.Jump()` se intenta inmediatamente; cubo ocupado se registra y no se difiere como salto pendiente. El bilateral también tiene cooldown de par de 900ms, separado de la ventana 180ms; conservarlo y cuantificar su efecto en repeticiones rápidas, sin atribuirle los tres rechazos débiles.

## Contraejemplo reproducible

La prueba reutiliza extractor y mocks host del firmware; compila el código real con `g++`, sin copiar una reimplementación del detector. Checkout limpio fijado por SHA, parámetros predeterminados y AUTO sin modificar. Empieza con el sensor elevado y en reposo, orientación constante y gyro cero; acelera hacia abajo 100ms, continúa 400ms a velocidad constante y frena 100ms con igual aceleración positiva. La integral física parte de cero, permanece negativa hasta detenerse; no hay subida. Son escalones ideales, no mediciones humanas.

| Aceleración de frenado | UP desde inicio de descenso | Velocidad física al UP | `shortV` al UP | `fUp` emitido |
| --- | ---: | ---: | ---: | ---: |
| 0,15g | 530ms | −0,102970m/s | +0,033069m/s | 0,143588g |
| 0,25g | 520ms | −0,196133m/s | +0,032964m/s | 0,219436g |
| 0,35g | 520ms | −0,274586m/s | +0,046149m/s | 0,307210g |

Los tres califican por amplitud bilateral; el último también unilateral normal. La prueba confirma además UP durante una aceleración de subida genuina. La fuga y los resets de quietud impiden interpretar `shortV` como velocidad absoluta: puede ser positivo durante frenado descendente. No se elimina la fuga sin controlar deriva ni se convierte este ejemplo en una regla fija de 700ms.

## Comparación de alternativas

| Alternativa | Aporte posible | Coste / riesgo | Decisión |
| --- | --- | --- | --- |
| Más histéresis o cooldown | Reduce repeticiones cercanas | Ya existe; puede perder elevaciones rápidas, no resuelve signo de velocidad | No ampliar sin datos |
| Estado de movimiento MOV/quiet | Delimita actividad y ventanas de reposo | Pie alto quieto no es suelo; traslación constante puede parecer quietud | Característica auxiliar |
| Estimación causal de fase y velocidad | Conserva historial de subida/descenso y puede vetar frenado | Deriva, referencia inicial y reposos ambiguos; no basta cambiar `shortV` | Opción principal en modo sombra |
| Acelerómetro + gyro anatómico | Diferencia rotación, mejora orientación y evidencia de fase | Algunos gestos casi sin giro; tobillo no es planta del pie; no exigir un signo fijo universal | Complemento, no condición obligatoria aislada |
| Correlación bilateral | Refuerza simultaneidad dentro de 180ms | Ambos pies también frenan juntos; excluye unilateral y puede aumentar espera | Solo contexto bilateral, nunca rescatar bajo umbral automáticamente |
| Clasificación temporal causal | Usa orden y forma de señales, no solo amplitud | Requiere RAW etiquetado, control de sobreajuste y latencia | Máquina de estados primero; modelo aprendido después si aporta |
| Bajar umbral | Más primeros eventos elegibles | También acepta frenados; no identifica fase | Experimento A/B existente, no corrección de producción |

La literatura sobre IMU **en pie** utiliza fases de apoyo y actualizaciones de velocidad cero, pero no valida directamente un montaje en tobillo ni elevaciones en el lugar. [Artículo original sobre PDR y ZUPT](https://www.mdpi.com/2072-666x/13/4/610). La [documentación primaria de Fusion](https://github.com/xioTechnologies/Fusion/blob/main/README.md) explica el compromiso entre deriva giroscópica y contaminación por aceleración en la estimación de orientación. No se propone instalar esa biblioteca ni transferir parámetros de otro montaje.

## Detector propuesto: fase causal en modo sombra

**Propuesta, todavía no implementada ni validada.** Mantener control actual como referencia; computar candidato paralelo sin controlar saltos ni escribir NVS.

1. Entrada por muestra: seis ejes, tiempo sensor y dt real, sesgo/referencia vigentes, calidad de orientación. Inicializar velocidad solo desde reposo inicial confirmado; no asumir contacto porque gyro sea bajo.
2. Estados `DESCONOCIDA`, `REPOSO_REFERENCIADO`, `ASCENSO`, `ALTO_O_PAUSA`, `DESCENSO`, `FRENADO`. Una pausa elevada conserva historial de altura/fase; no equivale a rearme en suelo. Si faltan muestras o crece incertidumbre, pasar a DESCONOCIDA.
3. Estimar aceleración vertical con orientación y velocidad de corto intervalo **con incertidumbre**, separada del integrador con fuga actual. Mantener orden de lóbulos positivos/negativos, cambios de velocidad y giro anatómico. Un intervalo de velocidad completamente negativo más aceleración positiva implica candidato FRENADO, no ASCENSO. Si el intervalo incluye cero, no inventar certeza.
4. Desde reposo referenciado, evidencia positiva persistente permite proponer ASCENSO. Desde DESCENSO, un lóbulo positivo conserva veto de frenado hasta resolver el cambio de fase. Registrar motivo, instante de primer indicio y confirmación. Ventana causal inicial a explorar offline: 20–60ms, no valor de producción; medir el retraso añadido y falsos negativos.
5. Aplicar los cortes **0,25/0,25/0,12g y 180ms** a la referencia existente. Para un futuro detector que observe aceleración continua, evaluar el primer cruce dentro de ASCENSO, sin usar picos futuros ni atrasar el timestamp artificialmente. Eso sería un cambio de semántica respecto al evento puntual: requerirá validación explícita antes de promoverlo.
6. Un solo candidato por ascenso; rearme por transición de fase confirmada, no solo tiempo. Sin evidencia suficiente, registrar «indeterminado». En sombra no quitar controles existentes; una política de veto en producción quedaría pendiente de evaluar sensibilidad.

**Límite ineludible:** si toda la subida queda realmente por debajo de 0,25g y ese corte sigue siendo obligatorio, no puede prometerse un salto temprano sin cambiar el criterio. Un filtro de fase puede suprimir el salto tardío y dejar un gesto sin salto; eso no sería una mejora suficiente. Debe mejorar oportunidad sin degradar detección. Los CSV actuales no dicen si el pico posterior del primer ascenso cruzó 0,25g.

## Qué datos faltan

CSV EV/APP permite verificar amplitudes emitidas, orden, separación, decisión Unity y ocupación. DIAG de baja frecuencia puede apoyar hipótesis de estado, no reconstruir cada transición. No permite medir trayectoria, máximo entre eventos, inicio biomecánico, contacto, signo de velocidad ni falsos negativos sin conteo humano.

Firmware ya tiene RAW seis ejes: `STREAM,1`, característica terminada `0005`, paquetes V2 de 20 bytes, secuencia u16 en offset2, millis u32 en4, aceleración i16 mg en8/10/12 y gyro i16 0,1°/s en14/16/18. Publica una instantánea cada50ms (20Hz), **no todas las muestras del detector**. Unity v0.9.4 solo suscribe EVENT/STATUS/TRANSPORT: **no guarda ese RAW ni ofrece aquí un botón para activarlo**. No prometer RAW al pulsar INICIAR. AXES exige mapa anatómico válido; no recalibrarlo solo para este ensayo.

20Hz más vídeo puede distinguir fases separadas 700ms; no reproduce decisiones de dos muestras ni garantiza capturar picos. Para validar el clasificador fino hacen falta RAW ≥100Hz, idealmente cadencia IMU original, secuencias/pérdidas y dt, fUp/fActivity/shortV, vertical/sesgo, ARM/MOV/CAL/rearme, configuración efectiva y decisiones con timestamps. Exige instrumentación diagnóstica futura y ensayo de presupuesto BLE; esta auditoría no cambia tasas ni protocolo. Alinear vídeo con marcas visibles repetidas y reloj de aplicación; conservar incertidumbre de offset y deriva, no usar hora de llegada como tiempo de muestreo.

## Una sesión física mínima, sin flasheo ni cambio de umbrales

Objetivo realizable con v0.9.4 actual: **asignar fase a los eventos y comprobar el mecanismo de selección**, no certificar el detector propuesto ni RAW inexistente. Una APK, misma firma/paquete y actualización conservando datos; si ya está instalada v0.9.4 no hace falta otra por este PR, que solo añade pruebas/documentación.

1. Mantener v2.2.7, montaje habitual, EXP NO, 0,25/0,25/0,12g y180ms; ambos sensores conectados, reloj y CAL OK, sin colisión. Grabar vídeo continuo de pies y pantalla con otra cámara, preferiblemente60fps.
2. INICIAR y QUIETO10s. Filmar las pulsaciones de las etiquetas de bloque (sus MARCA quedan en CSV) al principio y final; usarlas para alinear reloj de aplicación y vídeo. Anotar precisión de alineación, al menos un frame más incertidumbre de UI/callback; no declarar exactitud de milisegundos.
3. SOLO DER/ELEVACION: seis ciclos separados por reposo. Tres subidas lentas y tres normales; en cada una sostener arriba ~1s y bajar con control. Numerar en voz alta. Así se separan ascenso, pausa y frenado, sin necesidad de impactos.
4. SOLO IZQ: repetir seis ciclos como control. BILATERAL/DASH: seis ciclos iguales, tres lentos y tres normales. Mantener180ms y todos los umbrales. Total18 ciclos.
5. QUIETO10s, FINALIZAR y ARCHIVO. Conservar CSV original íntegro y vídeo. Etiquetar inicio/fin de subida, pausa y descenso, salto visible, ausencia y duplicado por intento. No emparejar automáticamente «cada dos UP» con un gesto.
6. Comparar tiempos sensor alineados/APP con vídeo; buscar primer UP débil durante ascenso y posterior UP durante frenado. Informar aciertos tempranos/18 por bloque, tardíos, omitidos, dobles y falsos en quietud, además de distribución de retraso con incertidumbre. Este ensayo no sustituye RAW: si la causa requiere trayectoria o un nuevo clasificador, preparar primero instrumentación y combinar estos mismos bloques con RAW **antes** de pedir otra sesión. No ejecutar dos protocolos A/B/A y fase redundantes.

Para validar el detector propuesto completo en una sola sesión futura, la captura RAW sincronizada es requisito previo pendiente. Usar sus bloques para ajustar parámetros y reservar otros ciclos completos para evaluación; nunca dividir muestras del mismo gesto entre ajuste y prueba. Exigir ausencia de regresión a cortes normales, detección bilateral conservada y menor proporción tardía sin más omisiones/dobles. Con18 ciclos no hay certificación estadística: resultados exploratorios.

## Verificaciones y entrega

Comandos nuevos:
```
dotnet run --project tests/PhaseAudit
python3 tests/PhaseAudit/replay_firmware.py ../firmware-audit-v227
```
El segundo exige checkout limpio en el SHA indicado, usa directorio temporal y no modifica fuente. CI incluye checkout fijado del firmware para reproducirlo. C# vincula las clases reales, no mocks de sus decisiones; prueba umbrales, ambos pies, antirrebote, cubo ocupado como contador, ventana180/181ms, primeros pares débiles y posteriores fuertes. **No prueba el render ni BLE físico**.

También se ejecutan regresiones existentes LatencyRegression, ReceiverRegression, AndroidDownloadsRegression, DashUiRegression y contrato Android, además del contrato/replay firmware (120s/90 gestos). Los resultados efectivos se registran en el PR y Actions; nunca interpretar compilación C++ host como compilación ESP32 o APK. No hay cambio de algoritmo de producción en esta revisión.

Se puede desarrollar el nivel con controles manuales y capa de entrada desacoplada. El bloqueo para declarar control inercial fiable sigue siendo **fase biomecánica y sensibilidad temporal sin validar**, no la construcción del escenario de juego.

### Ejecución local efectiva

- .NET8.0.428: PhaseAudit22, Latency58+9+31, Receiver34, AndroidDownloads16, DashUi66: **236 comprobaciones aprobadas**.
- `python3 tests/check_android_export.py`: PASS estático.
- En firmware: `python3 tests/check_contract.py` y `python3 tests/host/run_replay.py`: PASS; nuevo replay de frenado: tres contraejemplos y control de subida PASS.
- `git diff --check`: sin errores.
- Incidencia de entorno: primer arranque concurrente del SDK recién instalado produjo errores de mutex de migración NuGet y una salida139. Tras completar inicialización se repitieron secuencialmente los proyectos afectados y aprobaron. No se alteró código para ocultar el fallo.
- No se ejecutó Unity Editor, compilación APK/ESP32, instrumentación Android ni prueba física en esta auditoría. CI remoto se enlaza en el PR al finalizar.
