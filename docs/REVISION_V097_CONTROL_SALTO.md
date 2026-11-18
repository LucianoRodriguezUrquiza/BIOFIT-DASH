# v0.9.7 — control bilateral y cadencia

Base exacta: `fix/raw-ux-v0961`, `4006bcc50383aad22998682f0c20e3b84e01ec37` (PR #12). Rama candidata: `feat/final-jump-control-v097`. Sin merge. Firmware v2.2.7 `9cb5b7ac06315b0b657a33b8e608aca335eccb9e` intacto.

## Resultado y alcance

Se corrigió una interacción reproducible entre los dos antirrebotes, **sin bajar los 900 ms**. Un par secundario consumido y rechazado por cooldown reiniciaba el bloqueo de cada tobillo (320 ms), ocultando un nuevo par a 1 s. Ahora se restauran los timestamps anteriores de ese par; no se borra un impulso más nuevo recibido por la otra conexión. El filtro de secuencia/antigüedad sigue delante del detector.

Reproducción compilando las clases reales anteriores y actuales: cinco pares en t=0,1,2,3,4 s, cada uno seguido por otro a +0,7 s. Antes: comandos en **0;1,7;2,7;3,7;4,7**. Después: **0;1;2;3;4**. Ambos podían sumar cinco: el conteo solo escondía el desplazamiento hacia los impulsos secundarios. Son estímulos sintéticos etiquetados por construcción, no gestos humanos observados.

**No se corrigió la ambigüedad física de un segundo UP a +2 s**: puede ser frenado de descenso o un gesto nuevo. No hay clasificación de contacto, fase o salto humano en esta revisión. Caso D se satisface para pares secundarios dentro de 900 ms; su versión general permanece abierta. No declarar cumplido «un comando por gesto» hasta comprobar los cinco movimientos reales.

## Evidencia original leída

Se abrieron los **14 CSV originales** del ZIP: 8 principales y 6 RAW. `audit_csv.py` verifica tamaño/SHA del manifiesto, SHA del footer, finalizada=1, filas totales, EV y cero descartes de almacenamiento.

| Bloque | Filas principal | EV | APP salto aceptado | RAW | Huecos RAW |
|---|---:|---:|---:|---:|---:|
| 1 | 668 | 21 | 0 | — | — |
| A | 2611 | 48 | 6 | 3344 | 0 |
| B | 2828 | 43 | 8 | 3433 | 0 |
| A' | 2684 | 51 | 4 | 3348 | 0 |
| C | 2524 | 61 | 4 | 3073 | 0 |
| D | 3841 | 103 | 6 | 4916 | 11 |
| E | 5882 | 110 | 8 | 7945 | 21 |
| 8 | 546 | 17 | 2 | — | — |

Total: 21.608 filas principales, 454 EV, 26.059 RAW, 38 comandos. Ningún duplicado/desorden RAW observado; 32 muestras faltantes. **38 no equivale a 38 aciertos ni 95 %.** Se reprodujo el emparejamiento en orden de CSV con configuración y rechazos explícitos: D=6, E=8, restantes=0 pares; coincide con `par_bilateral`. La revisión no cambia las decisiones de esos ocho registros. No se publican los CSV privados en GitHub; los scripts reciben su ruta local.

En D los seis comandos normales están separados 1,934/2,105/1,971/1,806/2,037 s; en E, 1,905/1,720/1,788/1,905/1,805 s. Son demasiado separados para atribuirlos al antirrebote de 900 ms. La atribución ascenso/frenado procede de observaciones humanas, no de contacto medido. Los CSV D/E contienen 2/3 rechazos ANTIGUO (todos los tipos EV), conservados en el replay. No se extrapola latencia gesto-píxel desde demora estimada BLE o estado interno de Unity.

## Cadencia de toda la cadena: límites comprobables

- Firmware real en replay C++ host: 10/10 impulsos a 500/1000/2000 ms y 5/10 a 250 ms, con señal sintética y parámetros predeterminados. El rearme requiere señal además de tiempo (gap predeterminado 260 ms). No certifica cadencia humana ni parámetros efectivos NVS del teléfono.
- BLE: ambos UP frescos, secuencias válidas, relojes alineados y separación <=180 ms. Gate conserva límite de edad estimada/colas de 250 ms. Pérdida de un tobillo no genera un par inventado. Dos conexiones pueden llegar desordenadas entre sí; se emparejan timestamps, no orden de recepción.
- Detector: intervalo mínimo entre pares aceptados 900 ms => techo ideal ~1,11 comandos/s; no es garantía en radio real. 320 ms entre UP del mismo tobillo, excepto el rollback localizado del par suprimido. La ventana de 180 ms **no es una espera agregada**: decide al recibir el segundo UP.
- Cubo: 575 unidades/s, gravedad 1560 unidades/s², vuelo continuo teórico 0,737 s. Euler y frames lo prolongan. Medido con `Step` real: 30 FPS=0,800 s; 60=0,767; 120=0,750. A 15 FPS=1,067 s y 10 FPS=1,600 s porque `dt` se limita a 50 ms. Se conserva física; `FRAME_CLAMP` deja evidencia de frames lentos, como máximo una línea por segundo.

| Cinco pares limpios | Pares aceptados | Saltos a 30/60/120 FPS | Rechazos detector / aire |
|---|---:|---:|---|
| cada 0,8 s | 3 | 3 | 2 / 0 |
| cada 0,9 s exactos | 5 | 5 | 0 / 0 |
| cada 1,0 s | 5 | 5 | 0 / 0 |
| cada 1,2 s | 5 | 5 | 0 / 0 |
| cada 1,0 s a 10 FPS | 5 | 3 | 0 / 2 |

Conservar ~1 s como objetivo exploratorio: 0,9 s está en el borde, sin margen para variación de timestamp/gesto. La cadencia máxima física extremo a extremo **no está medida**; la tabla demuestra el límite software bajo sus condiciones. Una notificación fresca puede llegar cuando el personaje aún está en vuelo: se rechaza inmediatamente, no se guarda para saltar automáticamente al aterrizar.

## Alternativas comparadas

| Opción | Ganancia | Riesgo / decisión |
|---|---|---|
| Mantener ambos bloqueos sin cambio | Conserva conducta previa | Reproduce desplazamiento de cuatro comandos hacia secundarios a +700 ms |
| Bajar cooldown a 750 ms | Replay aislado acepta cinco pares cada 800 ms | También acepta segundo impulso a +800 ms; no corrige dobles a +2 s. Compilado como contrafactual temporal, **no incorporado** |
| Alargar cooldown para tapar ~2 s | Oculta algunos segundos comandos del ZIP | Impide cadencia de 1 s; descartado |
| Rearmar por MOV_OFF/reposo | Señal ya disponible | Pausa elevada y suelo pueden producir la misma señal; descartado como requisito |
| **No prolongar bloqueo con par suprimido** | Recupera siguiente par a 1 s sin aceptar pares separados <900 ms | Seleccionado; no determina fase ni garantiza supresión de un secundario posterior a 900 ms |
| Buffer de salto aéreo | Ejecutaría algunos comandos al tocar suelo | Puede crear salto fantasma al apoyar, oculta desfase; no implementado |

Confianza alta en defecto software/replay/integridad/cadencia simulada. Hipótesis de frenado compatible con relato y replay real de firmware; asignación por gesto sin referencia externa y rendimiento físico de v097 pendientes. El rollback también puede hacer elegible un impulso sin identidad humana conocida: esa es la razón de mantener PR borrador y verificar una sola sesión corta.

## Cambios concretos

- `BiofitBilateralJumpDetector.cs`: rollback limitado al par consumido, preservando timestamps posteriores; enum de decisión y contador de cooldown. Constantes intactas.
- `BiofitDashCore.cs`: `TryJump()` devuelve Accepted/Airborne/Paused/GameOver/NotPlaying. `Jump()` conserva su API booleana. Misma física, colisiones y obstáculos.
- `BiofitDashGame.cs`: manual, unilateral y bilateral pasan por `SubmitJump` → `TryJump`. Registro `JUMP_COMMAND` con ID, origen, resultado y contador; rechazos por pausa/fin ya no se etiquetan como aire. Contadores bilaterales de sesión (pares/intentos/saltos), snapshot de modo y versión, aviso de clamp temporal. UI v0.9.7.
- `BiofitBLEMonitor.cs`: `BILATERAL_DECISION`, secuencia/reloj/gap/intervalo y contador para reconstruir decisiones. Nuevos nombres BIOFIT_v097; captura/exportación/recuperación v096 sin cambios.
- Pruebas `JumpControl`, extensiones de receptor/UI y job host CI. No cambia firmware, UUID, formato BLE, RAW20Hz, NVS, calibración, defaults .25/.25/.12g/180ms, EXP, MediaStore, SAF, escenas ni ProjectSettings.

## Verificación reproducible

.NET SDK 8.0.428 local. Ejecutados:

```sh
for p in LatencyRegression ReceiverRegression AndroidDownloadsRegression DashUiRegression PhaseAudit RawRegression ArchiveRecovery096 JumpControl; do
  dotnet run --project tests/$p
done
python3 tests/JumpControl/audit_csv.py /ruta/al/zip-extraido
dotnet run --project tests/JumpControl -- /ruta/al/zip-extraido/CSV
python3 tests/JumpControl/compare_cooldowns.py --dotnet dotnet
python3 tests/check_android_export.py
python3 tests/PhaseAudit/replay_firmware.py /ruta/firmware-v227
python3 /ruta/firmware-v227/tests/check_contract.py
python3 /ruta/firmware-v227/tests/host/run_replay.py
git diff --check
```

Resultados: Latency 58+9+31; Receiver 37; AndroidDownloads 16; DashUI 78; PhaseAudit 22; RAW 36; ArchiveRecovery 79.016; JumpControl 186 (194 con ocho CSV). Los 79.016 incluyen comprobaciones por fila, **no 79.016 escenarios independientes**. RAW sintético: 24.000 RAW +12.000 APP; recuperación: 48.000 principal +31.000 RAW. Sin fallos finales. Primer test nuevo falló justamente ante el bloqueo residual a 1 s; tras corregirlo pasa. El contrafactual se corrigió a la salida realmente medida de la base: cinco comandos, cuatro desplazados, no tres comandos.

GitHub Actions del PR ejecuta host, replay del firmware fijado y compilación Java contra Android SDK35. **Java con stub de UnityPlayer no es APK ni integración Android física.** No hay Unity Editor/licencia operativa en este entorno; no se compiló APK/ESP32 ni se ejecutó Play real. Resultados remotos y enlace se publican en el PR. Pruebas sintéticas no sustituyen la sesión indicada abajo.

## Una compilación APK, preservando los nueve archivos locales

1. Cerrar Unity. En Explorador de Windows copiar la carpeta completa del proyecto a una carpeta de respaldo (incluye Assets con `.meta`, Packages, ProjectSettings y cambios locales). No usar Discard, reset, clean ni desinstalar la app. Este trabajo remoto no puede comprobar los nueve archivos de tu PC.
2. GitHub Desktop: seleccionar BIOFIT-GAME-UNITY → **Fetch origin** → **Current branch** → `feat/final-jump-control-v097`. Si muestra **Switch Branch**, elegir **Bring my changes to feat/final-jump-control-v097**. Revisar **Changes**: los nueve cambios deben seguir presentes. Si Git impide el cambio por conflicto, cancelar; no sobrescribirlos. Como alternativa conservar respaldo completo y aplicar únicamente los scripts del diff del PR en una copia del proyecto funcional. No hace falta merge ni Publish branch de tus cambios locales.
3. Abrir la misma carpeta en Unity **6000.3.28f1**, esperar importación. Conservar la escena y el perfil Android usados en la APK funcional. Console sin errores; verificar rótulo v0.9.7 en Play y salir de Play. No regenerar escenas/configuración para esta actualización.
4. **File → Build Profiles → perfil Android**. Mantener Scene List, package identifier, backend, arquitectura y **la misma clave/firma** de la APK instalada. Aumentar Bundle Version Code respecto de la instalada; versión visible del paquete 0.9.7 si se usa. **Build App Bundle (Google Play)** desactivado, **Export Project** desactivado, salida APK; Build y guardar `BIOFIT-v097.apk`. No activar Deep Profiling/Script Debugging para medir cadencia.
5. Instalar esa única APK **como actualización** sobre la existente. Si Android denuncia firma/paquete incompatibles, no desinstalar para forzarla: usar la firma original. Abrir **SESIONES** y comprobar que las capturas anteriores siguen disponibles antes del ensayo. No borrar ni exportar de nuevo todo el histórico.

Referencias oficiales: [GitHub Desktop: conservar cambios al cambiar rama](https://docs.github.com/en/desktop/making-changes-in-a-branch/managing-branches-in-github-desktop), [Unity 6000.3 Build Profiles](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/create-build-profile), [Android Build settings](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/building-and-delivering/build-settings). La interfaz exacta puede localizarse en inglés/español; no cambiar settings funcionales ajenos a la revisión.

## Única sesión física de cinco movimientos

No repetir A/B/A ni los ocho bloques. No reflashear ni recalibrar. Conservar montaje habitual.

1. Conectar ambos trackers, abrir DASH y confirmar **Sensores 2/2**, **Reloj ALINEADO**, CAL/ARM/IMU OK. Pulsar el selector BLE hasta **BLE: BILATERAL**. Dejar **EXP: NO**, UP **0,12 g**, PAR **180 ms**, ambos unilaterales **0,25 g**.
2. Pulsar **SIN COLISION: NO** si hace falta para dejar **SIN COLISION: SI**, **REINICIAR** una vez y verificar que el botón indica **PAUSA** (no REANUDAR). Así los obstáculos no interrumpen el ensayo.
3. Pulsar **INICIAR**, después **RAW: OFF**. Esperar **RAW: ON**, recepción creciendo de ambos sensores y GRABANDO. Esto conserva la exportación principal+RAW. Permanecer quieto unos 5 s.
4. Pulsar **DASH**, luego **SYNC VIDEO** (aparece SYNC 001). Un observador cuenta o anota los cinco intentos y los saltos visibles; si dispone de cámara, grabar pies y pantalla y decir «uno…cinco» para asignarlos sin pulsar botones durante el movimiento. Sin vídeo, no reportar latencia gesto-pantalla en milisegundos.
5. Hacer **cinco elevaciones bilaterales distintas**, cadencia cómoda cercana a **1 s entre inicios**, volviendo a apoyar entre ellas, sin pausa elevada larga ni buscar impactos. No pulsar SALTAR ni REINICIAR durante la serie. Anotar por intento si saltó oportunamente, tarde, dos veces o no saltó. Contadores de software no sustituyen esa anotación.
6. Quedarse quieto **5 s después del quinto apoyo** para detectar extras. Pulsar SYNC otra vez (SYNC 002), **QUIETO**, **FINALIZAR**. Esperar cierre SHA OK principal+RAW. FINALIZAR solicita STREAM,0; si no se confirma, conservar el mensaje de error sin asumir apagado remoto.
7. Pulsar **ARCHIVO**, esperar exportación verificada, comprobar en Descargas **dos CSV con el mismo ID** (uno `_RAW.csv`). Si falla ese destino, **RESCATE** y guardar los dos en sus selectores sucesivos. Conservar privados y aportar ambos originales más las cinco anotaciones.

Éxito exploratorio: cinco intentos físicos identificados, cinco comandos Accepted bilaterales oportunos, cinco saltos visibles y ningún adicional durante apoyos/reposo. Si no cumple, examinar en la misma captura: EV UP por pie/umbral → BILATERAL_DECISION → JUMP_COMMAND → PRIMER_ESTADO_SALTO. `PairCooldown`/`SameAnkleRefractory` indican filtros; `WaitingForOther`/`OutsideWindow`, emparejamiento; RECHAZO_ANTIGUO/SECUENCIA, transporte; Airborne/Paused/GameOver, juego. Revisar FRAME_CLAMP y diagnósticos RAW para rendimiento. Si no hubo UP, no culpar a Unity. Un salto extra requiere la anotación humana para asociarlo al mismo gesto: no deducirlo solo de su distancia temporal. No ordenar otra batería extensa ni reajustar todos los umbrales.

## Comenzar niveles

La separación ya es pequeña y útil: ESP32 → BLE/gate → detector → `SubmitJump` → `BiofitDashCore.TryJump` → Step/obstáculos/colisiones. El teclado y botón usan el mismo comando. Primer paso inmediato: un primer nivel corto determinista con lista de posiciones de obstáculos, inicio/fin y reinicio, validado con teclado, sin acoplar scripts de nivel a BLE. Mantener la física y diseñar inicialmente oportunidades de salto >=1,2 s, dejando margen al control de 1 s. Reutilizar y luego extraer la lista de obstáculos existente; no incorporar muñecas/joystick/VR aún.

Se puede empezar ese nivel ahora en rama separada. **Congelar el control como físicamente validado sigue condicionado al ensayo de cinco**; esta revisión no cierra por decreto los dobles tardíos ni acredita confiabilidad estadística universal.
