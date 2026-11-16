# BIOFIT DASH v0.9 — consolidación de sensibilidad y observabilidad

## Base de procedencia
- Unity base: `fix/stability-warmup-v08` (PR #3, revisión v0.8).
- Firmware de referencia: `fix/imu-state-rest-v227` (PR firmware #2; dos equipos físicos con v2.2.7).
- **Firmware NO modificado en v0.9**. Se preservan protocolo BLE, NVS, calibraciones, IMU, detector actual de UP, emparejamiento bilateral y UUID.
- Los archivos del usuario `CSV (3).pdf` y `BIOFIT_CSV3_RECONSTRUIDOS.zip` son evidencia de tres sesiones distintas, no una sesión controlada A/B; los CSV son reconstrucciones de texto y no tienen el footer SHA256 original. No se incorporan los datos personales ni crudos al repositorio.

## Evidencia verificada por sesiones

| Sesión y bloque | UP recibidos | Debajo de umbral | Antirrebote | Cubo ocupado | Saltos |
| --- | ---: | ---: | ---: | ---: | ---: |
| Inicial v0.8, SOLO IZQ .35g | 24 | 8 | 1 | 3 | 12 |
| Inicial v0.8, SOLO DER .35g | 20 | 15 | 0 | 0 | 5 |
| Posterior SOLO DER .25g | 73 | 18 | 0 | 14 | 41 |
| Posterior SOLO IZQ .25g | 25 | 9 | 0 | 5 | 11 |

Son conteos de decisiones sobre **eventos UP**, NO porcentajes de acierto por elevación humana.
Primera sesión total: 3066 filas / 351 EV; segunda: 1565 / 153; tercera: 450 / 51.
El último ensayo izquierdo pasó de .35g a .30g y finalmente .25g ANTES de marcar ELEVACION. No consta prueba física a .25g de 20 elevaciones con denominador de gestos, solo estimación del usuario cercana a 95% (derecho ~90%).
Bilateral se conserva en .12g y ventana 180ms.

### Fase de elevación y retardo
El usuario observa una minoría de saltos al terminar el descenso (~20–25% de las elevaciones izquierdas que saltan y menos en el derecho). **No está demostrado por los eventos UP** si corresponde a la fase del despegue, un impacto en el descenso o una espera previa en el firmware. La edad de cola callback/Unity no mide desde el inicio biomecánico.
En esta revisión se registra una nueva fila `DIAG;...;0|1|2;PRIMER_ESTADO_SALTO origen=... decisionToFrameMs=...` en el primer `Update` que simula altitud positiva tras `game.Jump`. Esa cifra mide decisión → primer estado de animación simulado, **NO** visualización Android fotón-a-ojo ni inicio del movimiento humano. No se añadió cola de salto al aterrizaje ni se redujeron cooldown/histéresis/filtros.

### `CUBO_OCUPADO`
`BiofitDashCore.Jump()` rechaza mientras el avatar está en el aire. Con v0=575 y g=1560, el vuelo ideal dura unos 0,74 segundos. Un UP aceptado durante ese intervalo sigue siendo un candidato válido de sensor; `CUBO_OCUPADO` es rechazo del estado de juego, no error BLE. No se implementa buffer de saltos para evitar saltos diferidos al descenso.

## Cambios en código
1. `BiofitSingleFootDiagnostic.cs`: umbrales iniciales 0,25g para ambos pies; ajuste por pie y mínimo 0,25g intactos. No se guarda preferencia antigua de .35g; cada nueva instancia del juego usa los nuevos valores. `Reset()` de captura sólo reinicia contadores/refractario.
2. `BiofitDashGame.cs` y `BiofitBLEMonitor.cs`: trazabilidad de decisión aceptada al primer estado de animación positivo, origen IZQ/DER/BILATERAL/MANUAL; panel muestra resultado asíncrono de la exportación en vez de confirmar guardado prematuramente.
3. `BiofitAndroidExport.cs` + `CsvExportActivity.java`: selector SAF, estados diferenciados, prevalidación del CSV y cotejo SHA256 y tamaño del documento copiado contra lectura del destino. La actividad debe estar presente mediante `.androidlib`. No solicita acceso general a almacenamiento. Una exportación puede fallar y mantener su original privado.
4. `BiofitBLEMonitor.cs`: `FINALIZAR` indica fallo si falla el guardado privado y `COPIAR` ahora genera el mismo archivo UTF-8 lógico con resumen/footer SHA256 (el portapapeles no es el fichero original). Los descartes por límite permanecen explicitados.
5. Regresiones C# host para umbrales, selección del pie, reset, primera frame de animación, estados del SAF y fallo de escritura; validación estática de la integración Android y compilación Java en CI.

**IMPORTANTE**: el fallo en un Android real del botón ARCHIVO no ha sido reproducido en este entorno. La nueva integración aporta mensajes de error y verifica el archivo cuando el proveedor SAF lo permite, pero NO demuestra que el selector se abra correctamente en la próxima APK. Puede fallar por empaquetado/manifest/actividad del dispositivo, lo que deberá inspeccionarse mediante el nuevo estado visible y Android logcat si hace falta.

## Pruebas requeridas por GitHub Actions (no equivalen a APK)
- `dotnet run --project tests/LatencyRegression`
- `dotnet run --project tests/ReceiverRegression`
- `dotnet run --project tests/DashUiRegression`
- `python3 tests/check_android_export.py`
- Compilación de `CsvExportActivity.java` contra Android SDK 35.
- Las regresiones C# usan sustitutos de API; no son pruebas Unity Editor ni Android.

## Procedimiento único en PC y celular
1. **No tocar firmware ni reflashear las Waveshare.** Ambas siguen con v2.2.7; comprobar `CAL:OK`, `ARM:SI`, 2/2 BLE al conectarlas.
2. En GitHub Desktop: cerrar Unity. Seleccionar `BIOFIT-GAME-UNITY`; antes de cambiar rama, **inspeccionar los ocho archivos locales** de escenas, `.meta`, Package y ProjectSettings. Si los cambios son los generados por crear las escenas, preservarlos con Stash All Changes sólo tras revisar; no borrarlos. Fetch origin, cambiar a la nueva rama. En caso de que las escenas no aparezcan al cambiar, recuperar el stash sólo cuando no provoque sobrescritura/conflictos; verificar diff antes de aplicar.
3. Abrir Unity 6.3 LTS; en Play abrir `BiofitDash`, panel y botón IZQ/DER: ambos deben mostrar 0,25g al iniciar; `UP:0,12g` y `PAR:180ms`. Comprobar Play con pantalla horizontal, botones completos, salto manual, consola sin errores y las pruebas BIOFIT del Editor. Recuperar escenas localmente si faltan: no asumir que se publicaron.
4. Build Profiles Android: `DiagnosticoBLE` activa primero, `BiofitDash` activa segunda, `SampleScene` desactivada; Android Active, Export Project OFF, App Bundle OFF. Construir **una única APK** `BIOFIT_DASH_v09.apk`.
5. Instalar/abrir Android con ambas placas colocadas, USB-C atrás. En juego seleccionar `SIN COLISION: SI`. Mantener .25g por pie, .12g bilateral, PAR 180ms, no cambiar parámetros durante bloques.
6. Captura única: `1.INICIAR`; `QUIETO` 20s (sin falsos saltos), `ELEVACION` + BLE SOLO IZQ: 20 elevaciones a ~2s, anotar `temprano/tardío/no/doble/ocupado`; SOLO DER: 20 elevaciones a ~2s con mismo conteo; `DASH` + BLE BILATERAL: 20 elevaciones simultáneas a ~2s y contar, anotando retardo; `QUIETO` 20s final. Los cambios de modo y de etapa son eventos registrados. Registrar primeros 3 de cada bloque aparte.
7. `2.FINALIZAR`: comprobar mensaje de guardado privado y descartes. `3.ARCHIVO`: observar estado; si abre selector elegir Descargas y guardar. El mensaje debe pasar a `GUARDADO: ...` sólo después de retorno y verificación. Si no abre o hay error, fotografiar mensaje visible y usar `COPIAR`; no desinstalar ni borrar datos de la app. Verificar fila `# END_BIOFIT sha256=` del archivo, guardarlo sin PDF. Adjuntar conteos humanos y screenshot final. Video lateral sincronizado opcional para determinar fase física (sin confundir con timestamps de transmisión).

## Pendientes físicos
- Android real: selector SAF, manifest fusionado en APK, hash de destino.
- Diferenciar UP tardío de frenado/descenso vs BLE vs ocupación a través de video/IMU real.
- Sensibilidad y especificidad por número de **gestos humanos contados**; no prometer >95%.
- Marcha/carrera: etiquetas `CAMINAR/CORRER` ya existen; todavía no hay clasificador de pasos confirmado.
