# BIOFIT DASH — MOV HOLD V2

Fecha: 2026-11-22. **Candidata experimental; no validada en Android físico.**
Rama: `feat/geometry-continuous-motion-jump-v2`, derivada de MOV HOLD v1.
El objetivo actual es **actividad sostenida = botón mantenido**, no un salto del cubo por cada salto humano.

## Referencias preservadas

| Referencia auditada | Commit |
| --- | --- |
| Unity estable `main` | `750c80ec3727e3dbaab0ea3135f5138c02f909f5` |
| MOV HOLD v1 / PR22 | `0d83449a5fa9257a642e123d1b160c553eaaf9d2` |
| Unity v0.9.10 | `88b825b46f3f0a19233cee41799f86aab10a8764` |
| Unity v0.9.11 | `5616fc4571193d4a2d076550a909089344144384` |
| Firmware v2.2.9 de referencia | `274625f605ea92189abfb16275012f34ee14371f` |
| GeomeTry original, submódulo | `d922caddb4d21ecb791a266143e279bdb969d42c` |

No se modificaron estas ramas, firmware, NVS, UUID, calibración ni archivos del teléfono.
Se trabajó en un clon separado: los nueve archivos locales del usuario no están disponibles aquí y no se sobrescribieron.
La versión flasheada no se deduce del repositorio: se registra `fw` por tracker desde TRANSPORT, o `unknown` si falta.

## Diagnóstico A: por qué puede perderse la intención continua

1. **Defecto reproducido, confianza alta:** v1 usa una sola marca temporal para EVENT y STATUS.
   Si EVENT de la muestra t llega antes que STATUS de esa misma muestra, descarta ese STATUS.
   Repetir esta secuencia deja vencer el watchdog de 550 ms aunque lleguen STATUS válidos a 10 Hz.
   V2 separa las marcas de salud y movimiento; un EVENT reciente no impide actualizar el heartbeat.
2. **Limitación reproducida, confianza alta:** v1 exige solapamiento instantáneo MOV izquierdo/derecho para armar.
   Izquierdo ON a 10 ms, OFF a 100 ms y derecho ON a 120 ms no arma v1, aunque ambos tuvieron actividad reciente.
   V2 permite iniciar con evidencia reciente de ambos dentro de 250 ms, sin esperar a completar la ventana.
3. **Desacople del juego, riesgo de arquitectura:** v1 escribía `keyPressed` y llamaba `onClick` por reflexión desde LateUpdate,
   además del Update original que lee mouse/espacio. V2 combina entradas dentro del Update original y llama una sola vez a `onClick`.
   El aterrizaje y la velocidad del salto los sigue decidiendo el código original.

Los primeros dos casos fallaron antes de corregir mientras los 13 tests originales de v1 seguían verdes.
`tests/verify_motion_v1_failures.py` extrae el código exacto del commit v1 y vuelve a demostrar los fallos esperados.
Esto prueba defectos de software, **no que sean la única causa de lo observado en el teléfono**.

## Diagnóstico B: pérdida total tras muertes/reinicios

| Hipótesis / mecanismo | Evidencia y confianza | Resolución / diagnóstico |
| --- | --- | --- |
| STATUS descartado por EVENT | Reproducido; alta sobre el defecto, causa de la sesión desconocida | Heartbeat independiente; edades y rechazos en CSV |
| Conectado sin notificaciones / desconexión | v1 no reconecta desde Update; reproducción con GATT simulado | Corte de entrada a 550 ms; recuperación del enlace tras 2 s sin STATUS nativo |
| Listeners acumulados | 101 llamadas a conectar dejan 101 listeners por dispositivo en v1; reproducido | Desuscripción y generación por enlace; 100 reconexiones sin acumulación |
| Callback `Connected` redundante | Sobrescribe texto de disponibilidad y deja `BothTrackersConnected=false` en v1; reproducido | Conservar estado de suscripciones ya activas |
| STATUS viejo reinicia reloj | v1 no usa el resultado del filtro de secuencia de STATUS | Descartar duplicados/desorden antes de alterar reloj; razón explícita |
| Referencia al cubo destruido | No se demostró bloqueo permanente de v1: Unity invalida objetos destruidos y v1 reintentaba búsqueda | V2 enlaza Start/Die/OnDestroy y el propio llamador vivo; OnDestroy anterior no borra la nueva instancia |
| Calibración, IMU o MOV dejan de ser válidos | Posible, no demostrado en la sesión fallida | CSV: CAL/IMU/MOV, Hz IMU, I2C, aceleración vertical, actividad/gyro, últimas transiciones |
| Juego/colisión/suelo rechaza entrada | Posible; no hay captura del fallo que lo identifique | CSV: identidad, vivo, lecturas, suelo, entrada entregada y asignación real de velocidad |

**No se encontró evidencia de que la muerte número 20 descalibre los sensores.** La muerte del personaje no envía CAL ni cambia NVS.
La acumulación de listeners se reproduce por reconexiones repetidas, no por afirmar que cada muerte reconecta.
La pérdida física después de muchos intentos sigue siendo una hipótesis que la nueva instrumentación debe localizar.

Firmware leído: estima gravedad/vertical, integra una velocidad corta y calcula MOV con acelerómetro/giroscopio.
El auto-reposo puede reanclar estado volátil tras reposo continuo (~2 s); no es borrar calibración NVS ni demostrar contacto plantar.
Replays del código `.ino` conservaron rearme, reposo, rollover y recuperación de deriva sintética.
Las versiones 0.9.10/0.9.11 añaden buffers y filtros de fase/ciclo, pero esos caminos no intervienen en MOV HOLD V2.
No se reactivó CICLO EXP ni se agregó un selector.

No hay CSV de la sesión MOV HOLD v1 fallida. Los 14 CSV históricos v0.9.6 contienen firmware 227 y otro controlador.
En ellos se observaron irregularidades y un máximo acumulado `rxGapMax=968 ms`; ese máximo no prueba una interrupción durante el fallo actual.
No se aumentó el watchdog para esconderlas ni se utilizaron esos CSV como validación física de V2.

## Alternativas y decisión

| Alternativa | Ventaja | Problema / decisión |
| --- | --- | --- |
| Corregir solo heartbeat y conservar MOV instantáneo v1 | Cambio menor | Mantiene la exigencia de solapamiento y la segunda llamada por reflexión; insuficiente |
| Historial corto de actividad + entrada nativa + recuperación observable | No depende de un nuevo par UP al aterrizar; mantiene física y datos disponibles | **Elegida**, con política de liberación experimental explícita |
| RAW permanente, fases biomecánicas o temporizador adaptativo | Más señales potenciales | No hay etiquetas ni evidencia para calibrarlo; añade tráfico/riesgo; descartada para esta revisión |

Cuatro capas: STATUS/EVENT válidos → envolvente de actividad → botón virtual → `Character.Update`/`Cube.Jump` original.
La ventana de **250 ms** es una decisión de ingeniería: dos periodos nominales STATUS de 100 ms + 50 ms de margen.
No es una duración de gesto medida. Antes era una gracia de 160 ms tras quietud simultánea; no se presenta como mejora física demostrada.
El primer salto no espera 250 ms: arma al observar actividad elegible de ambos pies.
Tras armar, actividad de cualquiera mantiene la intención; ambos quietos durante 250 ms la liberan.
Un único tobillo no inicia desde reposo. No equivale a clasificar salto, apoyo o intención voluntaria con certeza.

| Situación | Política V2 |
| --- | --- |
| Cubo en aire | Botón puede seguir pulsado; suelo original impide doble salto |
| Muerte/respawn dentro del nivel activo | Mantener intención con sensores sanos; no entregar al objeto muerto |
| Pausa, menú, cambio de escena, aplicación en segundo plano | Liberar; exigir observación nueva al reanudar |
| STATUS sin muestra nueva durante >550 ms, reloj o CAL/IMU inválidos | Liberar; recuperar con nuevas muestras saludables, sin exigir otro MOV_ON |
| STATUS duplicado, atrasado o de generación anterior | No renueva actividad ni reloj |
| GATT sin STATUS nativo >2 s o desconectado | Recuperación automática serial del enlace afectado, permisos ya concedidos, app activa |
| Enlace irrecuperable | Reintentos espaciados 2/4/8/15 s; alternancia si fallan ambos, sin bloquear al sano |
| RAW activo y recuperación BLE | Finalizar RAW con STREAM,0 al mejor esfuerzo; no reactivarlo silenciosamente |

Los timeouts de operaciones GATT son cancelables (12 s conexión, 3 s limpieza), sin esperas bloqueantes en Update.
Su cumplimiento por la implementación nativa de UniBLE todavía necesita Android. Permisos/Bluetooth disponibles siguen siendo requisitos.
El watchdog no se extiende mientras se intenta reconectar. No hay cola de saltos ni reproducción de comandos viejos.

**Riesgo de liberación:** firmware (~130 ms de MOV_OFF), muestreo STATUS, transporte, ventana y frame contribuyen al tiempo de soltar.
Puede comenzar un salto si aterriza antes de liberarse. Hay que medirlo; no prometer parada instantánea, cero latencia o cero saltos extra.
Una quietud mayor que la ventana puede cortar una intención humana continua. Si sucede, usar el CSV para medir el hueco antes de ajustar.

## Código y límites preservados

| Archivos | Cambio |
| --- | --- |
| `BiofitGeometryHeldMotion.cs` | Estado puro, salud/movimiento separados, envolvente y recuperación |
| `BiofitGeomeTryLegacyBridge.cs` | Entrada tipada, enlace explícito al personaje, telemetría limitada |
| `BiofitBLEMonitor.cs`, `.Motion.cs` | Orden de dispatch, rechazo STATUS antiguo, salud, generaciones y reconexión |
| `tools/geometry_compat/{Character,Cube,JumpOrb}.MotionV2.cs` | Overlays mínimos de entrada y observación sobre copias importadas; submódulo intacto |
| `tools/apply_geometry_input_v2.ps1`, `apply_geometry_compat.ps1` | Preflight, conservación GUID, rechazo de cambios locales no reconocidos, repetición idempotente |
| `BiofitGeomeTryOriginalBuild.cs` | Verifica overlays y genera APK independiente |
| `BiofitContinuousMotionValidation.cs` | Ensayo nativo Unity preparado, aún no ejecutado aquí |
| `tests/Continuous*`, `verify_motion_v1_failures.py`, workflow y contratos | Regresiones y reproducción del fallo original |

Se mantienen mapas completos (764/225/447/110/306 objetos), 70 skins, UI/navegación, objetivo 60 FPS,
RAW, exportación/recuperación privada, MediaStore/SAF, Packages y ProjectSettings versionados.
Umbrales 0,25/0,25/0,12 g, emparejamiento 180 ms y filtros UP legacy no se cambian.
Los pares UP quedan como diagnóstico, **no son la condición de salto de este experimento de entrada mantenida**.
Se conserva velocidad original 6,5, salto 13,5, gravedad local 4, timeScale 1,5, raycasts, pads, orbes y respawn.
El test compara todo Character/Cube/JumpOrb contra el original, quitando exclusivamente los hooks enumerados y la compatibilidad previa Camera.

## Instrumentación para reconstruir el fallo

CSV principal: líneas DIAG con prefijo `MOTION_V2`. Tres snapshots por segundo como máximo habitual (HEALTH y dos SENSOR), más transiciones;
límite global 24 líneas/s del puente, contador `suppressed`. No logs por frame ni nuevo stream BLE.

- SENSOR: llegada nativa vs procesamiento, edades STATUS/paquete, tasa, máximo de hueco acumulado, cola/demora relativa,
  secuencias faltantes/rechazadas, CAL/IMU/MOV, firmware, señal vertical/gyro/actividad, Hz/I2C, MOV_ON/OFF, generación/reconexiones.
- HEALTH: escena, PlayMode efectivo, reloj, epoch/razón de reset, estado/intención/duración/edad de actividad,
  ID actual/anterior, vivo, suelo, lecturas/entregas, fuente manual, saltos/asignaciones, muertes/respawns/destrucciones y listeners.
- Transiciones: `INTENT`, `SPAWN`, `DEATH`, `DESTROY`, `REBIND`, `JUMP_APPLIED`, `RECEIVER_RESET`.
- `JUMP_APPLIED` significa que **el código asignó la velocidad original**, no primer píxel presentado ni evento humano.
  Se agrupan asignaciones consecutivas del mismo episodio de suelo; `writes` conserva el total sin ocultarlo.

Interpretación de una interrupción:
1. Sin nuevas llegadas nativas → sensor/radio/GATT. Llegadas sin procesamiento → hilo principal/cola.
2. STATUS nuevo con CAL/IMU inválido o MOV falso → revisar esas señales, no culpar al personaje.
3. MOV saludable y `held=false` → estado/reloj/edades/pausa muestran la razón.
4. `held=true` sin `delivered`/sin actor vivo → enlace al personaje/escena.
5. Entrega con `grounded=false` → juego no permite salto. Suelo verdadero sin asignación → revisar ejecución del personaje.
6. Asignación registrada sin respuesta visible → física/render/colisión; probar táctil en ese mismo instante.

## Verificación ejecutada

Todos los siguientes resultados son host, datos sintéticos o inspección de fuentes:

| Comando desde la raíz (SDK .NET 8 salvo indicación) | Resultado |
| --- | --- |
| `dotnet run --project tests/ContinuousMotion` | 12.421 checks; 20 min sintéticos, gaps 100/300/600/1000 ms, reloj, CAL/IMU, alternancia y recuperación |
| `dotnet run --project tests/ContinuousReceiver` | 315 checks, receptor real con GATT simulado; 100 reconexiones, generaciones, no starvation del otro pie |
| `dotnet run --project tests/ContinuousRuntime` | 736 checks del puente/Character/Cube reales con **ENGINE SHIMS**, incluidos grupos 10/20/50/100 respawns y 100 enable/disable |
| `python3 tests/verify_motion_v1_failures.py` | Reproduce defectos v1 de actividad, heartbeat, disponibilidad, listeners y ausencia de reconexión |
| `dotnet run --project tests/HeldMotion` | 13 casos; expectativas de liberación actualizadas a la política de 250 ms |
| `dotnet run --project tests/{LatencyRegression,ReceiverRegression,AndroidDownloadsRegression,DashUiRegression}` (cada proyecto por separado) | 40 / 50 / 16 / 86 checks |
| `dotnet run --project tests/{LiftCycle,JumpControl,PhaseAudit,RawRegression,ArchiveRecovery096}` (por separado) | 157 / 258 / 22 / 36 / 79.016 checks; último conteo incluye comprobaciones por fila |
| `python3 tests/check_geometry_{original,navigation,ux,held}.py` (por separado) | Contratos de originales/mapas/UI, física y límites protegidos PASS |
| `python3 tests/JumpControl/compare_cooldowns.py` | Comparación legacy y alternativa 750 ms offline PASS; no se instala esa alternativa |
| `python3 tests/check_android_export.py` | Contratos MediaStore/SAF PASS; no ejecuta Android |
| `.NET 10: dotnet run --project tests/GeometryUxSyntax` | Sintaxis C#9 con símbolos UNITY_EDITOR y UNITY_ANDROID PASS; no es compilación de API Unity |
| `python3 tests/PhaseAudit/replay_firmware.py <clon-v227>` | 3 contraejemplos de frenado + elevación genuina con detector real y muestras sintéticas |
| En clon FW229: `python3 tests/check_contract.py`, `python3 tests/host/run_replay.py`, C++17 `lift_cycle.cpp` y `phase_evidence.cpp` con `-Wall -Wextra -Werror` | PASS, firmware sin editar; 120 s/90 gestos sintéticos, reposo, deriva/rollover y 30 ciclos de helper |
| `git diff --check` | PASS |

Invocaciones locales iniciales corregidas: `--nologo` pasó como argumento al programa LiftCycle y se interpretó como archivo;
se repitió sin ese argumento. `compare_cooldowns.py` necesitó `--dotnet <ruta-sdk>` porque dotnet no estaba en PATH.
No son resultados fallidos del detector ni se ocultaron fallos de compilación Unity.

CI añade los tests a las verificaciones existentes y prueba en **Windows PowerShell** la preparación y su repetición.
También compila los exportadores Java contra Android SDK35. Los enlaces y estados reales de Actions están en el PR.
**Pendiente:** Editor/PlayMode nativo, build Unity/APK, radio Android, latencia gesto-pantalla, FPS del teléfono y sesión humana.
No se hizo un flasheo ni se instaló Unity en este entorno.

## Una preparación y una APK, sin tocar la copia estable

1. Cerrar Unity. Conservar la carpeta local actual y sus nueve archivos tal cual. En GitHub Desktop usar **File > Clone repository**
   para una carpeta **nueva**, por ejemplo `BIOFIT-MOV-HOLD-V2`. No cambiar la rama de la carpeta donde están los cambios locales.
2. En ese clon: **Fetch origin > Current branch > feat/geometry-continuous-motion-jump-v2**.
   **Repository > Open in PowerShell**, ejecutar `git submodule update --init --recursive` y luego
   `powershell -ExecutionPolicy Bypass -File .\tools\prepare_geometry_original.ps1`.
   Si informa cambios locales inesperados o destino existente, detenerse y conservarlo; no borrar carpetas para forzar el script.
3. Unity Hub > **Add > Add project from disk**: elegir la raíz del clon, no `ThirdParty`.
   Abrir con Unity **6000.3.28f1** y Android Build Support ya disponible. Esperar importación y comprobar Console sin errores.
4. Menú **BIOFIT > GeomeTry ORIGINAL > 1. Verificar fuente y mapas**;
   **4. Activar URP original para probar en Play**; **2. Abrir Main Menu original**.
5. Una sola comprobación de motor antes de compilar: Play y menú **6. Validar MOV HOLD V2 (en Play)**.
   No tocar mouse/espacio durante ~2 minutos. Debe informar saltos consecutivos y checkpoints 10/20/50/100 con `MOV V2 NATIVE PASS` final.
   Esta herramienta usa GameObjects/Rigidbody2D/Die/Invoke/Destroy reales y entrada simulada; está preparada, no ejecutada aquí.
   Si falla, conservar Console; no construir una APK cuya comprobación nativa falló. No altera mapas ni guarda la escena temporal.
6. Salir de Play. **File > Build Profiles > Android > Switch Platform**.
   **BIOFIT > GeomeTry ORIGINAL > 3. Compilar APK MOV HOLD V2 EXP + BLE LEGACY**.
   Salida `Builds/BIOFIT_GeomeTry_MOV_HOLD_V2_<fecha>.apk`; app **BIOFIT MOV HOLD V2**, paquete `com.biofitgame.geometrymotionv2`.
   El builder restaura los ajustes temporales; no obliga a eliminar la app estable ni MOV HOLD v1.
7. Instalar esta APK aparte. Sus capturas están en su propio directorio privado: no recuperará automáticamente los registros de otro paquete.
   Esos registros permanecen en la app anterior. No desinstalarla ni borrar sus datos. No flashear/recalibrar como parte de este procedimiento.

## Única sesión física consolidada

Cerrar las otras apps BIOFIT para que no compitan por los trackers. Mantener la colocación y calibración habituales.
El objetivo es comprobar continuidad y persistencia; no repetir los ocho bloques ni buscar precisión biomecánica.

1. Abrir **BIOFIT MOV HOLD V2**, entrar en **CONFIGURACIÓN** con el engranaje del menú.
   Esperar **2 / 2 CONECTADOS** y **RELOJES: SINCRONIZADOS**. La conexión es automática; no hay selector nuevo.
2. Pulsar **INICIAR REGISTRO**; confirmar captura ACTIVA. Dejar RAW apagado inicialmente para probar la ruta normal.
   Iniciar vídeo de pies y pantalla si está disponible y pulsar **MARCAR SYNC**. No es necesario activar RAW para diagnosticar los estados de V2.
3. **VOLVER > JUGAR > StereoMadness**. Comparar brevemente táctil mantenido y luego actividad de ambos tobillos sin tocar pantalla.
   En un tramo que lo permita, buscar al menos tres aterrizajes con salto consecutivo. Anotar si falla por pincho/colisión antes de aterrizar.
4. Detener ambos pies y observar liberación. Reanudar varias veces. Incluir una pausa corta del juego y **CONTINUAR**.
   No confundir tiempo de vuelo con demora de detección ni un nuevo salto al soltar con una prueba de fase humana.
5. Continuar en **la misma captura** atravesando al menos 30 muertes/respawns automáticos, sin cerrar la app ni recalibrar.
   Registrar intentos al principio, alrededor del 10, 20 y 30; movimientos cómodos y pausas cuando sean necesarias.
   No hace falta forzar 100 intentos humanos: los 100 del menú anterior verifican el ciclo nativo de objetos.
6. Si se pierde respuesta, mantener la captura y probar **táctil** inmediatamente, antes de reiniciar la app.
   Decir en el vídeo el intento/instante y si el táctil funciona. **Pausa > SELECCIÓN DE NIVELES > VOLVER > CONFIGURACIÓN**:
   observar conexión/reloj, pulsar **MARCAR SYNC**. No iniciar otra captura sobre la anterior.
7. Opcional al final, si la parte principal funciona: una desconexión breve de un tracker y reconexión, sin borrar NVS;
   comprobar que se libera durante el corte y responde después de STATUS sanos. La recuperación puede tardar varios segundos por backoff/GATT.
   Si se necesita RAW para el análisis, activarlo con **RAW** en Configuración y hacer solo un tramo corto adicional en esta misma sesión.
8. Regresar a Configuración, pulsar **FINALIZAR**, esperar archivo finalizado y luego **DESCARGAS**.
   Si falla, **GUARDAR ARCHIVOS (SAF)** y elegir destino. Conservar CSV principal y RAW si se activó, junto al vídeo/notas.
   No desinstalar ni eliminar capturas privadas. Un fallo se analiza con estos datos antes de ordenar otra batería.

Criterios: saltos consecutivos mientras hay intención, liberación observable al detenerse y respuesta después del intento 30,
sin comandos a instancias muertas, sin listeners crecientes y recuperación de datos válidos tras una interrupción.
Una sesión corta confirma funcionamiento exploratorio, no confiabilidad estadística universal.

## Decisión

La candidata corrige defectos reproducidos y deja ambos problemas observables. **Todavía no es una versión de control físicamente congelada.**
El bloqueo concreto para declararla estable es comprobar continuidad y persistencia en Unity/Android real con la misma APK.
La física, los niveles y la UI pueden mantenerse; no corresponde cambiar de nuevo firmware o umbrales antes de esa evidencia.
