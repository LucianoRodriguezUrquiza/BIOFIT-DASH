# BIOFIT DASH v0.9.2 — Descarga automática de CSV sin selector

## Objetivo y alcance
El usuario requiere que `3. ARCHIVO` genere un **archivo CSV real** en la carpeta **Descargas del celular**, sin usar portapapeles ni ventanas del selector. En v0.9 el botón falló con `java.lang.ClassNotFoundException` porque la exportación requería una Activity Java propia. La revisión v0.9.1 reubicó el plugin, pero aún no hay APK probada; v0.9.2 sustituye la **ruta activa de exportación** por las API del sistema `MediaStore.Downloads`, llamadas desde C# con `AndroidJavaObject`.

Referencias:
- Android MediaStore.Downloads: https://developer.android.com/reference/android/provider/MediaStore.Downloads
- Android almacenamiento sin permisos generales para archivos propios en API >=29: https://developer.android.com/training/data-storage/shared/media

## Funcionamiento previsto
1. `1.INICIAR`: comienza una única captura (no exporta).
2. `2.FINALIZAR`: termina la sesión y guarda copia íntegra de CSV con footer SHA256 en `Application.persistentDataPath`.
3. `3.ARCHIVO`: verifica el CSV privado mediante `BiofitCaptureArchive.Verify`, ejecuta `ContentResolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, ContentValues)` con `DISPLAY_NAME`, `MIME_TYPE=text/csv`, `RELATIVE_PATH=Download/`, escribe todos los bytes, cierra el stream y consulta `ParcelFileDescriptor.getStatSize` cuando el proveedor lo informa. Se muestra `GUARDADO en Descargas (N bytes)` solo si no hubo error. No se abre ningún selector y no se utiliza `GUIUtility.systemCopyBuffer`.
4. Tras reiniciar la app, si no hay sesión actual, `ARCHIVO` recupera el último CSV privado válido `BIOFIT_v0*.csv` (incluida v0.9) y repite la exportación directa. No iniciar otra captura antes de recuperar una previa.
5. `COPIAR` permanece como rescate opcional, pero **no es necesario** para `ARCHIVO`.

En el Editor de Unity, `ARCHIVO` crea un archivo real en `Application.persistentDataPath/BiofitEditorExports` y muestra explícitamente `EDITOR` (no afirma que se guardó en Android). Esto permite validar flujo UI sin compilar.

## Compatibilidad y seguridad
- Guardado directo en **Android 10+ (API 29+)**, incluido el dispositivo Android 12 del usuario. No hace falta permiso `WRITE_EXTERNAL_STORAGE` ni `MANAGE_EXTERNAL_STORAGE`.
- Android 9 o anterior: el botón comunica error de compatibilidad, preserva el CSV privado; requerirá otra estrategia si se adopta ese sistema.
- Se comprueba integridad SHA256 del **archivo de origen privado**, y que el destino sea reabrible y de igual longitud cuando el proveedor reporta el tamaño. **NO** se ha demostrado comparabilidad criptográfica de bytes del archivo publicado mediante lectura completa de destino; su footer permite verificar integridad posteriormente.
- En caso de error de escritura, se intenta borrar la entrada parcial de MediaStore. Nunca se modifica ni elimina el CSV privado validado.
- Múltiples pulsaciones de `ARCHIVO` pueden crear copias adicionales en Descargas; no sobreescribe a ciegas datos ajenos.
- La biblioteca Java `BiofitExport.androidlib` anterior permanece por compatibilidad histórica, pero **la clase `CsvExportActivity` ya no se invoca** desde el botón ARCHIVO. No es necesario comprobarla en `classes.dex` para que funcione el nuevo flujo. El verificador APK de v0.9.1 queda obsoleto para este flujo.

## Qué está validado offline
- Compilación y ejecución de C# con shims en GitHub Actions (no Unity real), incluyendo guardar al presionar `ARCHIVO` en entorno host y recuperar una sesión tras reinicio simulado.
- Verificador estático confirma invocación directa `MediaStore$Downloads`, ausencia de dependencia de `com.biofit.export.CsvExportActivity` y ruta `ContentResolver`.
- No se compiló APK Unity ni se verificó la API Android JNI en teléfono; el éxito en CI no sustituye esa prueba.

## Procedimiento para recuperar la sesión v0.9 sin ensayo nuevo
1. **NO desinstalar ni borrar datos de BIOFIT DASH v0.9 actual.** Si Android pide desinstalar para instalar una revisión, detenerse.
2. En PC, cerrar Unity. GitHub Desktop: `BIOFIT-GAME-UNITY`, `Fetch origin`, elegir rama `fix/automatic-download-csv-v092`. Conservar las escenas y otros archivos locales; si aparece la opción de cambiar de rama, elegir `Bring my changes`. **Stash: NO; Discard: NO; Squash: NO; Commit: NO** (salvo conflictos).
3. Abrir Unity 6.3 LTS con el proyecto; Play, panel, `IZQ=0,25g`, `DER=0,25g`, `UP=0,12g`, `PAR=180ms`, sin errores. Si se desea, crear sesión sintética desde Play y comprobar que ARCHIVO escribe en `BiofitEditorExports`.
4. Build Profiles Android Active, `DiagnosticoBLE` 0, `BiofitDash` 1, `SampleScene` desactivada, Export Project OFF, App Bundle OFF. Generar **una APK** `BIOFIT_DASH_v092.apk`.
5. Instalar como **actualización de la aplicación existente**. Mantener package name, firma, datos.
6. Abrir juego directamente, **SIN presionar `1.INICIAR`**. Pulsar **`3. ARCHIVO`** una sola vez. Esperar `GUARDADO en Descargas (N bytes)`. Abrir Archivos > Descargas y buscar un `BIOFIT_v09_YYYYMMDD_HHMMSS_....csv`. La captura previa debía tener 340 eventos; confirmar las marcas y el footer SHA256.
7. Si muestra `ERROR CSV Descargas` o no aparece el archivo, capturar mensaje íntegro y, si hace falta, Android logcat. No borrar datos ni repetir elevaciones.
8. Adjuntar el CSV descargado original y las observaciones sobre 20 movimientos derechos; recién entonces determinar si es necesario intervenir firmware v2.2.7.

## Sin modificaciones de gameplay
Se preservan firmware v2.2.7, NVS, BLE, saltos bilaterales 0,12g /180ms, umbrales IZQ/DER 0,25g y el detector actual. Los saltos tardíos y la degradación derecha siguen pendientes de analizar con CSV completo.
