# v0.9.11 / firmware v2.2.9 EXP — control de ciclos consecutivos

**Candidata experimental implementada; no es un control físicamente validado ni una APK entregada.** El defecto temporal del modo anterior está demostrado; la discriminación humana del nuevo canal sigue abierta, especialmente su sensibilidad. No afirmar 95 % ni ausencia universal de falsos positivos.

Base Unity: `feat/phase-gated-jump-v0910`, commit `88b825b46f3f0a19233cee41799f86aab10a8764` ([PR16](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/16)). Rama nueva `feat/reliable-consecutive-jumps-v0911`. Firmware instalado al realizar los CSV: v2.2.8 EXP, base `0947a509a32ae3b17390d20cfe21ba0f508d0d03`; candidata [PR firmware4](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware/pull/4), rama `feat/lift-cycle-v229`, commit `274625f605ea92189abfb16275012f34ee14371f`. Main, ramas anteriores y escenas/configuración local no se modificaron. No se accedió al teléfono.

## 1. Evidencia recalculada, sin etiquetas humanas inventadas

Se leyeron completos los cuatro CSV originales adjuntos de las sesiones `4a917563` (A, BILATERAL) y `0693ef50` (B, FASE EXP), incluidos sus RAW. SHA256 del cuerpo, finalizada=1 y filas declaradas coinciden. No se publican los CSV privados. Resultados reproducibles: [evidence_v0910_control_metrics.json](evidence_v0910_control_metrics.json).

| Indicador | A | B |
|---|---:|---:|
| Filas principal | 7.532 | 6.412 |
| EV / UP | 340 / 264 | 280 / 254 |
| Pares aceptados / comandos Accepted | 59 / 59 | 59 / 59 |
| PairCooldown | 50 | 53 |
| OutsideWindow / BelowThreshold | 32 / 2 | 14 / 11 |
| Pares rápidos / Buffered / ejecución del buffer | 0 / 0 / 0 | 0 / 0 / 0 |
| Hints individuales izq / der | 46 / 31 | 24 / 17 |
| Pares emparejados con ambos hints | 18 | 6 |
| Ambos hints entre 600–900 ms | 2 (modo OFF) | **0** |
| Cooldowns entre 600–900 ms, cualquier hint | 21 | 33 |
| Rechazos por antigüedad | 0 | 2 |
| RAW total; huecos / duplicados o desorden | 8.411; 0 / 0 | 6.757; 0 / 0 |

Los conteos de hints emparejados se obtienen ejecutando la clase C# real sobre todos los UP, con los flags registrados y rechazando primero los eventos marcados antiguos. Se reproducen exactamente los 59 pares por sesión. **FASE EXP no cambió la admisión rápida en B**; no puede atribuirse la mejor percepción de B1 a una función que no llegó a disparar.

| Bloque entre SYNC | EV / UP | Comandos | Cooldown | OutsideWindow | Observación humana declarada |
|---|---:|---:|---:|---:|---|
| A1: 85.709–185.476 s | 176 / 134 | 29 | 20 | 28 | 11 respuestas en descenso, de 30 intentos |
| A2: 185.476–237.355 s | 164 / 130 | 30 | 30 | 4 | 5 respuestas en descenso, de 30 |
| B1: 355.814–401.464 s | 129 / 125 | 29 | 28 | 3 | 1 tardía + 1 omisión, de 30 |
| B2: 401.464–487.794 s | 151 / 129 | 30 | 25 | 11 | 5–7 errores, clasificación incierta |

B2 contiene cambios manuales ELEVACION 478.739, DASH 481.446 y QUIETO 482.314 antes de SYNC003. No se recortó automáticamente a la etiqueta DASH. La cadencia dependía del aterrizaje y de la recuperación tras errores: **no se trató como una serie periódica**. Los 118 Accepted en 120 intentos no son 98,3 % de reconocimiento correcto; pueden compensarse omisiones y dobles/tardíos.

### Latencia observable

| Medida, sobre UP | A mediana / p95 / máximo | B mediana / p95 / máximo |
|---|---:|---:|
| Cola Unity, ms | 17,50 / 31,60 / 33,50 | 16,95 / 32,14 / 35,20 |
| Demora relativa al ancla, ms | 36,85 / 93,46 / 207,90 | 33,75 / 92,37 / 336,20 |
| Decisión → primer estado simulado, ms (59 aceptados) | 0,133 / 0,636 / 0,947 | 0,294 / 1,023 / 1,889 |

TX reporta en ambos archivos qMax=2, drop=0, edadMax=3 ms, notifyMax=1 ms, cicloMax=4 ms, err=0 (máximos acumulativos del firmware, no cronometraje de radio). RAW ~19,85–19,88 Hz por sensor; cada EV tiene un RAW cercano a ≤27 ms en el mismo reloj sensor. Eso permite asociación aproximada, **no recuperar el inicio biomecánico** entre muestras. Norma de aceleración máxima RAW de A ~8,1 g y de B ~6,5 g: un reset de historia a 3 g sería inadecuado para estos datos.

El primer estado simulado no es el primer píxel presentado. Las anclas no miden el desfase absoluto ESP32→Android. No sumar esas columnas para fabricar una latencia total; falta instante físico, envío radio real y presentación. Los ~100 ms percibidos son plausibles como hipótesis, no un resultado medido. El filtro IIR actual a 22 Hz tiene constante ~7,2 ms: por sí solo no explica respuestas desplazadas a otra fase del movimiento.

## 2. Diagnóstico y confianza

| Conclusión | Evidencia | Confianza / límite |
|---|---|---|
| FASE EXP no abrió la vía 600–900 ms | CSV completos + replay C# exacto | Alta para estas sesiones |
| El buffer no se usó | DIAG de comandos, ejecución y contadores; no solo APP | Alta |
| El UP puede emitirse al frenar descenso | Tres contraejemplos con processSample real del firmware v227, cuyo UP se conserva en v228 | Alta para el mecanismo; sin etiqueta de cada error humano |
| 900 ms limita movimientos nuevos a .8 s | Producción real: 5 pares limpios →3 acciones | Alta, sintética |
| Quietud conjunta/historia de fase restringe la vía rápida | Hay hints individuales, ninguno coincidente en B dentro de 600–900 ms | Alta para efecto observado; no conocemos qué subcondición falló en cada muestra |
| BLE sea la causa principal de saltos en descenso | Cola baja, TX sin drops, solo 2 antiguos; inicio físico no observado | No demostrado; no descartar latencia absoluta desconocida |
| Cambiar umbral/ventana mejore la precisión | Pocos BelowThreshold, pero faltan etiquetas y negativos verdaderos | Insuficiente; se preservan .25/.25/.12 g y 180 ms |
| Deriva gravitatoria/orientación cause los errores concretos | Posible por integración; RAW decimado y estado/NVS inicial incompletos | Hipótesis abierta |

El UP legacy usa aceleración proyectada positiva + actividad + integral con fuga `shortV`. Aceleración positiva también frena velocidad descendente. `MOV_OFF`, baja actividad y el signo instantáneo de a **no identifican por sí solos apoyo ni elevación**. El hint v228 agrega información a UP, pero no cambia el instante ni elimina los UP sin hint: después de 900 ms se aceptan igualmente.

## 3. Alternativas comparadas y elección

| Alternativa | Beneficio | Problema / decisión |
|---|---|---|
| Solo bajar cooldown a 600/500 ms | Mayor tasa de pares | Admite segundos impulsos del mismo movimiento. No seleccionada |
| Solo ampliar buffer 180→250/300 ms | Más margen cerca del aterrizaje | No recupera candidatos rechazados antes. Insuficiente sola |
| Aflojar quietud de fase v228 | Más hints posibles | Mantiene UP tardío como origen; sin prueba de fase real. No seleccionada |
| Histéresis o energía/jerk solo | Barato; detecta cambios rápidos | Frenado/impacto también tiene energía y jerk; podría amplificar ruido |
| Giroscopio como umbral único | Detecta rotación precoz | Elevaciones casi verticales pueden tener poco giro; dependencia del gesto |
| Correlación bilateral RAW | Exige concordancia entre piernas | 20 Hz y espera de ventana añaden incertidumbre/latencia; ambos pies también frenan juntos |
| Velocidad/posición integrada + ZUPT estricto | Modelo físico más explícito | Deriva y falsa quietud; prototipo resultó demasiado restrictivo. Descartado como gate de rearme |
| **Canal de onset con memoria causal y veto del retorno** | Separa inicio y segundo lóbulo antes de Unity; una emisión por ciclo del controlador | Requiere validar sensibilidad y patrones no ideales; **seleccionado como único modo nuevo** |

No se importan umbrales de estudios de marcha a este ejercicio ni se usa ML. La memoria conserva el orden de evidencia negativa y frenado positivo; el rearme es dinámico, no una afirmación de contacto con suelo.

### Firmware v2.2.9 EXP

[Especificación completa y guía Arduino](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware/blob/feat/lift-cycle-v229/docs/REVISION_V229_CICLO.md). Nuevo helper en paralelo al legacy: anclaje inicial estable160 ms → seguimiento desde |a|.045 g → onset≥.12 g con actividad≥.035 g, integral corta≥.020 m/s, persistencia8 ms y ventana inicial160 ms → veto tras área negativa .004 g·s → consume lóbulo de frenado y rearma al caer a≤.035 g durante24 ms. Mínimo400 ms por tobillo; hueco>30 ms/horizonte2,5 s invalida estado. Nunca usa MOV_OFF como suelo.

EVENT conserva V2/16 bytes, añade tipo4 `LIFT_ONSET` + bit0x20. Secuencia común, reloj y edad TX conservados. Tipo3 UP no cambia. TRANSPORT V3 opcional a5 Hz registra estado, razón, integral, mínimo, onsets/rearmes/fallos. Incremento nominal≤280 B/s de payload entre ambos trackers mientras rige el guard; no incluye overhead BLE ni reinicios de historia. RAW20 Hz intacto. La carga real requiere medición física; ya se registran colas/edad/drops/notify.

### Unity v0.9.11

- LEGACY usa UP original, 900/320 ms, .12 g/180 ms y buffer180 ms. Unilaterales .25 g, EXP A/B y controles manuales conservados.
- CICLO EXP usa exclusivamente eventos tipo4 con marca de algoritmo y versión229 confirmada en **ambos** trackers. Pasa primero por secuencia, edad y reloj existentes; empareja a≤180 ms y mantiene separación mínima600 ms. Se consumen ambos onsets incluso cuando un par es rechazado. Pérdidas no fabrican el tobillo ausente.
- Una ranura con límite **300 ms desde el tiempo alineado del segundo onset**, no 300 ms nuevos al recibir. Solo se admite en descenso del cubo y si cabe el aterrizaje estimado más dos pasos de margen Euler. Ejecución única, sin renovar ni acumular, expira con reloj real aunque se congele Unity. Cada primer onset puede preceder al segundo hasta180 ms: este techo no es latencia física gesto-pantalla.
- Se cancela ante modo, desconexión, pérdida de anclas/versiones, pausa, reinicio y game-over. Las decisiones registran Accepted/Airborne/Buffered/AlreadyBuffered/InputExpired/Expired/Cancelled.
- UI inequívoca: BILATERAL LEGACY vs CICLO EXP v1/FW229/min600/buf300; .12 g/180 ms fijos en nuevo modo. Con firmware anterior se omite CICLO y se permite apagar BLE. `EXP: SI/NO` sigue siendo el experimento unilateral, no el selector de algoritmo.
- APP ahora conserva `BLE_CICLO_EXPERIMENTAL` y `SALTO_BLE_BUFFERED`; CSV añade nombre LIFT_ONSET sin cambiar las15 columnas. Resumen separa pares legacy/ciclo. RAW, guardado privado, SHA, MediaStore, SAF e historial no se reimplementan.

### Cadencia y elección del buffer

Con el core real: vuelo30FPS=.800 s,60FPS=.767 s,120FPS=.750 s;10FPS=1.600 s por clamp de dt. La física575/1560 no cambia.

Caso concreto reproducible: al recibir el nuevo candidato, el cubo lleva550 ms en vuelo y el onset tiene50 ms de edad. Con el margen de integración, 180 y250 ms no garantizan alcanzar el aterrizaje antes de vencer; 300 ms permite reservar y ejecutar una sola vez. Cuando está en suelo, no espera esos300 ms: salta inmediatamente. Una ventana dinámica más larga ocultaría inputs antiguos; no se seleccionó.

| Entrada sintética nueva, 30 pares (20 ms entre tobillos +50 ms de recepción) | 30FPS | 60FPS | 120FPS |
|---|---:|---:|---:|
| cada .6 s | 15 acciones | 20 | 20 |
| cada .8/.9/1.0/1.2 s | 30 acciones | 30 | 30 |

A .6 s se aceptan30 pares, pero el personaje no soporta esa tasa sostenida; no se acumula una deuda de saltos. A60/120FPS10 comandos se reservaron y ejecutaron,10 fueron rechazados por aire. El cooldown legacy aún produce3/5 a.8 s y5/5 a.9/1/1.2 s (sin margen frente a jitter a.9). Ninguno de estos ensayos etiqueta movimientos humanos.

## 4. Pruebas ejecutadas y límites

Entorno local: .NET8.0.428, g++, Python; sin Unity Editor, licencia de compilación Unity ni Android Build Support instalado. **No se generó APK, no hubo Unity Play, instrumentación Android ni flasheo.**

| Suite host | Resultado |
|---|---|
| LatencyRegression | 58 core +9 input +31 latency PASS |
| ReceiverRegression | 50: parser real, dos versiones, tipo4, flags, antigüedad/duplicados, CSV |
| DashUiRegression | 86: GUI con shims, modos, callback/buffer/Update, desconexión, exportación host |
| LiftCycle | 157 sintéticas;159 al incluir replay de los dos CSV reales |
| JumpControl | 258 legacy/phase/buffer/cadencia PASS |
| PhaseAudit | 22 PASS;3 contraejemplos adicionales de frenado con firmware real extraído |
| RawRegression | 36 PASS;stress24.000 RAW +12.000 APP |
| ArchiveRecovery096 | 79.016 comprobaciones, muchas por fila;48.000 principal +31.000 RAW |
| AndroidDownloadsRegression | 16 **mocks** MediaStore; no prueba de Android real |
| Firmware | espejo roles/NVS; baseline y processSample actuales; helpers antiguo/nuevo PASS |

`tests/LiftCycle` recorre 5/10/30 entradas a .6/.8/.9/1/1.2 s y30/60/120FPS. El helper nuevo y el .ino real producen30 LIFT en30 ciclos balanceados a800 ms; legacy produce31 UP en esa misma secuencia sintética. Helper: primer onset20 ms después del escalón, excluye transporte/render. Descenso puro, frenado, pausa elevada, espiga, impacto6 g, quietud, gyro residual, pérdida de muestras, rollover y NaN cubiertos. No equivalen a movimientos etiquetados de una persona.

**Prueba negativa importante:** el replay de RAW20 Hz (interpolación5 ms, calibración inicial/defaults) produce UP120/84 en A y105/118 en B, frente a originales124/140 y127/127. El canal nuevo produce43/26 y28/39 onsets respectivamente. **No reproduce la referencia y no demuestra una mejora.** Sus bajos recuentos advierten riesgo de omisión en el experimento. No se eligieron constantes para forzar60 eventos. La verdad de sensibilidad requiere la IMU real y vídeo. Las muestras existentes no recuperan los ~224 Hz, bias/orientación/estado inicial ni NVS usados.

CI separado: [firmware, build izquierdo/derecho y host](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware/actions/runs/44444682589). Unity ejecuta host, compila fuentes Java contra android.jar35 y replay del firmware227 en el workflow del PR. Compilar Java no prueba JNI/IL2CPP, empaquetado APK ni integración MediaStore real. El resultado final y SHA de CI se enlazan en cada PR.

Comandos (desde Unity; reemplazar rutas):

```sh
python3 tools/analyze_control_sessions.py --main A.csv B.csv --raw A_RAW.csv B_RAW.csv --raw-tsv-dir /tmp/replay
dotnet run --project tests/LiftCycle -- A.csv B.csv
# Suites restantes: dotnet run --project tests/<nombre_de_tabla>
python3 tests/JumpControl/compare_cooldowns.py
python3 tests/check_android_export.py
python3 tests/PhaseAudit/replay_firmware.py /ruta/firmware-v227
# Desde Firmware: comandos completos en docs/REVISION_V229_CICLO.md
python3 tests/host/run_replay.py --raw-input /tmp/replay/A_RAW-1.tsv
```

Incidencias del desarrollo corregidas antes de publicar: el primer prototipo basado en integral casi nula era demasiado restrictivo; descartar reset a3 g/500 dps de norma porque impactos/giro válidos lo superan; buffer250 ms no alcanzaba el aterrizaje discreto en el caso probado; selector de modo con firmware antiguo no debía quedar atrapado en SOLO DER. `dotnet` local requirió ruta explícita; no se confunde una herramienta ausente con prueba aprobada.

## 5. Una actualización preparada, sin perder proyecto ni registros

### GitHub Desktop / Unity (Windows)

1. Cerrar Unity y guardar una **copia completa del proyecto actual**, incluidas Assets con .meta, Packages y ProjectSettings. Es el respaldo de los nueve archivos locales; aquí no se pudieron inspeccionar los de tu PC.
2. GitHub Desktop → repositorio Unity → **Changes**: revisar esos archivos. **Fetch origin** → **Current branch** → `feat/reliable-consecutive-jumps-v0911`. Si pregunta por cambios sin commit, elegir **Bring my changes**. No usar Discard. Si hay conflicto, conservar la copia local de escenas/configuración y resolver antes de abrir Unity; no reemplazar carpetas completas desde un ZIP. Si Desktop no permite trasladarlos, conservar la rama anterior y el respaldo, sin forzar el cambio.
3. Abrir el mismo proyecto en **Unity6000.3.28f1**. Esperar importación y revisar Console: ningún error rojo. No ejecutar generadores de escenas ni crear otro proyecto.
4. **File → Build Profiles → Android → Switch Profile** si es necesario. Usar el perfil Android existente y verificar su **Scene List**: mismas escenas/habilitación/orden que la APK funcional. Los perfiles pueden tener lista propia; el menú auxiliar BIOFIT usa la lista global, por eso se recomienda Build Profiles para respetar tu perfil local.
5. Mantener identificador de paquete y firma/keystore usados en la APK instalada. **Export Project desactivado**, **Build App Bundle desactivado**, **Build** → guardar `BIOFIT_v0911_CICLO_EXP.apk`. Si el módulo falta, Unity Hub → instalación6000.3.28f1 → Add modules → Android Build Support, SDK/NDK/OpenJDK. No cambiar ajustes de gráficos/escenas para esta prueba.
6. Instalar como **actualización** sobre la app existente. No desinstalar ni borrar almacenamiento. Si Android rechaza la firma, usar la clave original; no resolverlo borrando la app. Confirmar título v0.9.11 y que **SESIONES** todavía lista capturas anteriores.

Los códigos nuevos usan C# y API ya presentes; host compila con shims, no demuestra compatibilidad del proyecto Unity/Android local. Si aparece error de compilación, guardar texto de Console/Editor.log: no hacer pruebas físicas con otra versión creyendo que es la candidata.

### Trackers

Seguir [guía FW229](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware/blob/feat/lift-cycle-v229/docs/REVISION_V229_CICLO.md#preparación-arduino-y-reversión), **una carga por lado**, sin erase-all y conservando particiones. No se requiere recalibrar ejes ni NVS. Dejar quietos durante el arranque normal. Ambos LCD deben mostrar v2.2.9 EXP y el rol correcto. Nueva APK con firmware228 funciona en LEGACY; no habilita CICLO.

## 6. Una sesión física progresiva, botón por botón

Se hace con esta única APK y ambos FW229. Los criterios5/10/30 son metas, no resultados actuales. **Si falla el bloque corto, finalizar y analizar esa misma sesión; no completar una batería larga ni reajustar umbrales.**

1. Conectar ambos sensores mediante el diagnóstico habitual; esperar2/2 y reloj ALINEADO, CAL/ARM/IMU correctos. Entrar con **JUGAR BIOFIT DASH (PROTOTIPO)** si aparece el diagnóstico. Dejar5 s para recibir ambas versiones.
2. **SIN COLISION: SI** → **REINICIAR**. **EXP: NO**. Pulsar el selector BLE hasta **BLE: CICLO EXP**; comprobar v1/FW229/min600/buf300 y .12 g/180 ms fijos. Si vuelve a OFF, aún falta versión229 confirmada de algún tracker; no interpretar ese estado como fallo de detección humana.
3. Preparar vídeo externo que vea ambos pies y pantalla, preferentemente60FPS o más. Sin vídeo se pueden contar comandos, pero no resolver con precisión ascenso vs frenado ni gesto→píxel.
4. **INICIAR → RAW: OFF** (pasa aON). Comprobar que aumentan contadores de **ambos** sensores. **QUIETO**,5 s sin SALTAR manual. **DASH → SYNC VIDEO**: apareceSYNC001 en pantalla y CSV; decir “cinco, ciclo”.
5. Realizar **cinco elevaciones distintas** con el gesto habitual, contando en voz alta, siguiendo el aterrizaje del cubo y sin imponer un cronómetro. Quedarse quieto → **SYNC** (002) → **QUIETO**,5 s. Esperado5 saltos oportunos, cero extra. Si falla, ir directamente al paso8.
6. Si pasó: **DASH → SYNC** (003), decir “diez, ciclo”. Diez movimientos fluidos, pudiendo anticipar ligeramente el aterrizaje cuando resulte natural, sin aumentar intensidad. **SYNC** (004) → **QUIETO**,5 s. Si falla, paso8.
7. Si pasó: comparar dos condiciones de30 en la misma captura. Seleccionar **BILATERAL LEGACY**, confirmar .12/180 sin alterar valores → **DASH → SYNC** (005), decir “treinta, legacy”,30 movimientos → **SYNC** (006) → **QUIETO**,5 s. Seleccionar **CICLO EXP → DASH → SYNC** (007), decir “treinta, ciclo”,30 con el mismo gesto → **SYNC** (008) → **QUIETO**,5 s. No perseguir una cadencia exacta ni repetir bloques fallidos. Orden fijo y número pequeño no constituyen un ensayo estadístico controlado.
8. **FINALIZAR**. Esperar confirmación de guardado privado y cierre RAW. **ARCHIVO**: exporta principal y `_RAW.csv`. Confirmar ambos archivos en Descargas y conservar originales. Si falla la exportación, **SESIONES → seleccionar sesión → RESCATE**; no iniciar otra captura para intentar recuperar esta ni desinstalar.

Para cada intento del vídeo registrar inicio visible, ascenso/descenso, primer salto del cubo, omisión/extra y bloque. Asociar por SYNC y reloj: `EV LIFT_ONSET` → `LIFT_PAIR` → `JUMP_COMMAND` → `JUMP_BUFFER_*` → `PRIMER_ESTADO_SALTO`. Diagnóstico por eslabón:

- No onset de un lado: estado/reason/faults y RAW; no culpar a Unity.
- Ambos onsets pero sin par: frescura, secuencia, gap180 y guard600.
- Par sin acción: Airborne, Paused, InputExpired o anclas/conexión.
- Buffered sin acción: Expired/Cancelled y FRAME_CLAMP.
- Salto al descender: comprobar si onset ya nació tarde o si un onset temprano esperó en buffer; separar ambas causas.
- Extra: cotejar si hubo dos ciclos nuevos del firmware o reejecución del mismo comando. Identificar la ambigüedad, no reclasificarla automáticamente como gesto válido.

Latencia física: diferencia entre fotograma de inicio del gesto y primer cambio visible del cubo; declarar FPS y margen temporal (a60FPS cada fotograma≈16,7 ms). Informar por separado comandos inmediatos y reservados. No usar hora UTC del archivo como reloj de sincronización.

Criterio:5/5 y10/10 sin extras como filtro inicial; en30,≥29 oportunos representa≥95 % observado, con cero extras y sin tardíos sistemáticos. No es límite de confianza poblacional ni certificación. Si no se alcanza, el CSV/vídeo de ese bloque basta para el siguiente diagnóstico; no repetir las ocho pruebas viejas.

## 7. Reversión y paso hacia niveles

Recuperación inmediata: elegir **BLE: BILATERAL LEGACY** en esta APK; vacía reserva y mantiene el detector/umbral/cooldown anteriores. Reversión completa: FW `feat/phase-evidence-v228` por ambos lados sin borrar NVS; APK v0.9.10 conservada, misma firma/paquete. Android puede impedir bajar versionCode: en ese caso recompilar el código anterior con versionCode admisible y misma clave; no desinstalar para forzar downgrade. Mantener la copia del proyecto y las capturas originales.

Arquitectura conservada: productor de eventos → filtro temporal/emparejador → comando compartido/ranura → `BiofitDashCore.TryJump` → nivel. Teclado y SALTAR usan el mismo núcleo. Primer paso independiente: una rama de nivel original determinista con10–15 obstáculos, final y reinicio; usar teclado y el contrato de salto existente, sin otra refactorización grande. **No congelar todavía CICLO EXP como control validado.** Bloqueo concreto: demostrar que los onsets nuevos representan elevaciones humanas diferentes sin omisiones ni extras; la reconstrucción RAW señala riesgo de sensibilidad. El código de niveles puede avanzar en paralelo con entrada manual.

## 8. Fuentes externas y alcance de su respaldo

- [Rebula et al., Measurement of Foot Placement and its Variability with Inertial Sensors](https://pmc.ncbi.nlm.nih.gov/articles/PMC4284057/): usa ventanas estacionarias y reconoce falsos estados por velocidad casi constante y poco giro. Respalda no igualar quietud IMU a apoyo; sus parámetros de marcha con sensor en pie no se trasladan al tobillo BIOFIT.
- [Peruzzi et al., validación de la hipótesis de velocidad cero](https://www.sciencedirect.com/science/article/abs/pii/S0021929011003666): la localización pie/pierna afecta la validez de ZUPT. No justifica imponer velocidad integrada cero como gate universal.
- [Espressif, conexión BLE ESP32-S3](https://docs.espressif.com/projects/esp-idf/en/release-v5.0/esp32s3/api-guides/ble/get-started/ble-connection.html): la conexión se programa por intervalos negociados; duración de notify no mide la entrega visible. No se cambiaron parámetros de radio sin medirlos.
- [Unity Input System, timing and latency](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/timing-and-latency.md): procesamiento de entradas, actualización y presentación son etapas distintas. Nuestra BLE es una entrada propia, no un evento físico automáticamente cronometrado por Input System.
- [Discusión del proyecto oficial de demos Godot sobre buffer/coyote](https://github.com/godotengine/godot-demo-projects/issues/336): mecanismos diferentes para tolerar proximidad al suelo y pulsación. Se adopta el patrón de comando discreto con caducidad; no se importan tiempos ni se habilita salto aéreo/coyote.
- [Unity6000.3: compilación Android](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/building-and-delivering/build-process), [perfiles](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/build-profiles), [GitHub Desktop: cambios entre ramas](https://docs.github.com/en/desktop/making-changes-in-a-branch/managing-branches-in-github-desktop), [Arduino ESP32: herramientas](https://docs.espressif.com/projects/arduino-esp32/en/latest/guides/tools_menu.html).

Estas fuentes respaldan decisiones arquitectónicas y límites de inferencia; **ninguna demuestra que los umbrales del nuevo helper funcionen en los movimientos del usuario**.
