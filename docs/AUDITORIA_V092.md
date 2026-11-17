# Auditoría v0.9.2: CSV y tobillo derecho

Base Unity: 54ff33ad48ffebde83b26dac37cb9b1d2097f9b0 (`fix/automatic-download-csv-v092`). Referencia firmware: 9cb5b7ac06315b0b657a33b8e608aca335eccb9e (`fix/imu-state-rest-v227`). Firmware sin modificaciones.

## Hallazgos

| Hallazgo | Confianza | Resultado |
|---|---|---|
| AndroidJavaObject.Dispose libera referencias JNI, no cierra streams/descriptores Java | Alta: código oficial Unity y revisión del puente | Cierre explícito en finally; fallo al cerrar impide anunciar éxito y elimina la entrada incompleta |
| El tamaño igual no demuestra integridad | Alta | Relectura mediante ContentResolver y Scanner UTF-8; compara contenido completo y valida footer SHA256 antes de publicar |
| Archivo visible mientras se escribía | Alta: faltaba IS_PENDING | Inserta pendiente, publica con update solo tras cerrar/verificar; limpia fallos |
| Verificación y escritura usaban dos lecturas del original | Alta | Una sola instantánea de bytes y decodificación UTF-8 estricta |
| API Downloads requiere Android 10+ | Alta, documentación | Conserva rechazo para API<29, sin permiso amplio de almacenamiento |
| Fallos derechos durante descenso | Hipótesis, no diagnóstico físico confirmado | Aceleración hacia arriba también ocurre frenando un descenso. shortV es un integrador con fuga, no velocidad física medida. No se añade filtro especulativo |
| Últimos diez intentos derechos sin salto | Indeterminado | Los contadores agregados no ubican esos intentos; no permiten concluir ausencia de UP, bloqueo de ARM, umbral o personaje ocupado |

Fuentes oficiales consultadas:
- https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/AndroidJNI/AndroidJava.cs
- https://developer.android.com/reference/android/content/ContentResolver
- https://developer.android.com/reference/android/provider/MediaStore.Downloads
- https://developer.android.com/reference/android/provider/MediaStore.MediaColumns
- https://developer.android.com/training/data-storage/shared/media

El contrato Android 10+ permite crear archivos propios en MediaStore.Downloads sin permisos generales de almacenamiento. Android 11–14 y posteriores conservan esas API; esto no equivale a ejecutar una matriz de dispositivos. Relative_path=Download/ usa el directorio público Descargas. La visibilidad depende de publicar IS_PENDING=0 y del refresco del gestor de archivos.

JNI: ContentValues.put(String,String), put(String,Integer) con Integer Java explícito; insert retorna Uri, openOutputStream retorna OutputStream, getStatSize retorna long, update/delete retornan int. Scanner usa constructor InputStream/String, delimitador \\A y next retorna String. Los cierres Java son independientes del using C#. Los mocks no verifican la resolución JNI real. Cada pulsación inserta una nueva copia; Android puede desambiguar el nombre. El nombre mostrado es el solicitado, no una consulta del nombre final asignado por el proveedor. El original privado permanece intacto.

## Recuperación y límites

FindNewestValid busca BIOFIT_v0*.csv: incluye v08 y v09. Elige el archivo válido más reciente por mtime y omite truncados; ARCHIVO permite recuperarlo sin INICIAR. Una sesión inválida más reciente puede dar lugar a recuperar una anterior válida. La UI todavía no distingue detalladamente archivo inexistente de todos los archivos inaccesibles/dañados; limitación pendiente. No se recuperó la sesión física desde el teléfono: este entorno no tiene acceso a sus datos.

Actualizar la APK con el mismo applicationId y firma conserva normalmente datos privados. Cambiar paquete crea otro espacio de datos; firma incompatible impide instalar como actualización. No desinstalar, no borrar datos, no cambiar identidad/firma para resolver un error de instalación. No se modificaron escenas, configuración local ni identidad Android.

## Pruebas ejecutadas

- `dotnet run --project tests/LatencyRegression`: 58+9+31 verificaciones.
- `dotnet run --project tests/ReceiverRegression`: 34 verificaciones, incluida recuperación tras reinicio y archivo truncado.
- `dotnet run --project tests/DashUiRegression`: 59 verificaciones, incluidos .25/.25 y botones OnGUI con shims.
- `dotnet run --project tests/AndroidDownloadsRegression`: 16 verificaciones; cierre explícito, fallo de cierre/escritura, tamaño incorrecto, corrupción de igual tamaño, publicación y conservación del original.
- `python3 tests/check_android_export.py`: contrato estático superado.
- Firmware v2.2.7: `python3 tests/check_contract.py` y `python3 tests/host/run_replay.py`: superados; 120 s simulados/90 gestos, rearme, cadencia, deriva inducida y recuperación, rollover. Son pruebas existentes reejecutadas, no una reproducción de los diez fallos derechos físicos.

Total C# host: 207 comprobaciones. SDK .NET 8.0.428. No se ejecutó Unity Editor, compilación APK ni Android real. Hay Java disponible, pero no se ejecutó Gradle/Robolectric/emulador en esta revisión. Robolectric puede probar lógica Java del proveedor simulado, pero no el puente AndroidJavaObject de Unity; una prueba instrumentada dentro de una APK Unity en emulador es el siguiente escalón pertinente. CI conserva la compilación de la Activity heredada, que ya no se usa: tampoco valida el puente MediaStore.

## Una ronda final

1. Preservar cambios locales. GitHub Desktop: Fetch origin y seleccionar la rama nueva del PR; no fusionar ni reemplazar escenas locales.
2. Unity 6000.3.28f1: verificar consola y Play; compilar una sola APK Android con el MISMO applicationId y firma de la instalada. Instalar como actualización, sin desinstalar. No flashear trackers para esta revisión.
3. Antes de INICIAR una captura, abrir DASH y pulsar 3. ARCHIVO. Debe recuperar el último archivo privado válido y mostrar GUARDADO; abrir Descargas y conservar el CSV original. Si falla, conservar el mensaje exacto y no borrar datos. No repetir todavía el experimento extenso.
4. Pulsar ARCHIVO otra vez: se espera otra copia, no sobrescribir el original privado. Comprobar que ambos contenidos/footers son iguales; el gestor puede añadir sufijo al nombre.
5. Solo después de preservar la sesión anterior: INICIAR, QUIETO 15 s; diez elevaciones derechas contadas a 2 s, diez izquierdas, cinco bilaterales; QUIETO 10 s; FINALIZAR y ARCHIVO. Mantener .25/.25/.12/180 y SIN COLISION. Anotar intentos y si falla al subir o al bajar. Video opcional para asociar movimiento físico con evento.
6. Entregar los CSV como archivos sin convertir a Docs/PDF. Comparar EV, decisión, CAL/ARM, restCount, disarmedMs, velocidad/bias y configuración efectiva por tobillo. Para distinguir los diez fallos anteriores hace falta recuperar su registro completo; los 340 eventos resumidos y el corte a37.845 s no bastan.

Decisión: se puede desarrollar un nivel en una rama independiente, pero no dar por validado el control derecho ni la exportación Android. Prioridad antes de integrar el nivel: recuperar la sesión previa y verificar esta exportación en la única APK. El resultado de hardware y la causalidad de los fallos derechos siguen abiertos.
