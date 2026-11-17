# BIOFIT DASH v0.9.1 — reparación de ARCHIVO y recuperación de sesión

## Contexto
- Basado en `fix/defaults-025-phase-export-v09` (PR #4), sin modificar Unity bilateral, umbrales (ambos .25g) ni firmware v2.2.7.
- Última sesión física: captura `REGISTRO FINAL | 340 eventos`, `Sensores:2/2`, `Reloj:ALINEADO`, `CAL:OK ARM:SI`, `Pares:20`, `Rechazos BLE:0`; izquierda `UP=59, bajo=14, anti=2, salto=32, ocupado=11`; derecha `UP=15, bajo=4, anti=0, salto=9, ocupado=2`.
- Observación del usuario: derecho dos primeros gestos tardíos, aproximadamente ocho siguientes bien y diez últimos sin salto; izquierdo y bilateral satisfactorios. No interpretar candidatos UP como gestos humanos independientes.
- El documento compartido `CSV v9` accesible en Drive contiene **20.151 bytes** y termina en mitad de la fila correspondiente a 37.845 s. No contiene los segmentos de derecha/bilateral ni footer de integridad. No permite localizar el fallo derecho. No considerar ese archivo como la captura original íntegra.

## Diagnóstico del exportador
En dispositivo real `ARCHIVO` devolvió `java.lang.ClassNotFoundException` al resolver `com.biofit.export.CsvExportActivity`. Se encontró que la biblioteca tenía Java en la ruta obsoleta `src/com/biofit/export` y manifest en la raíz. El fuente Java compilaba por `javac` con ruta explícita pero **NO** validaba el empaquetado Unity/Gradle de la clase.
La estructura ahora sigue el [esquema documentado por Unity](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/developing/plugins-for/plugin-types/aarplugins/library-plugin-create):
- `Assets/Plugins/Android/BiofitExport.androidlib/src/main/java/com/biofit/export/CsvExportActivity.java`
- `Assets/Plugins/Android/BiofitExport.androidlib/src/main/AndroidManifest.xml`
- `project.properties` mantiene `android.library=true`.
Se quitaron los equivalentes antiguos para evitar fuentes/manifest duplicados. Se conservaron los GUID Unity `.meta` del archivo Java y del manifest reubicados.

## Recuperar CSV anterior sin repetir ensayo
`BiofitCaptureArchive.FindNewestValid(Application.persistentDataPath)` identifica el archivo privado BIOFIT_v08_*.csv o BIOFIT_v09_*.csv más reciente que pase SHA256, conteos y marcador FIN. La búsqueda no lee carpetas ajenas. En una instancia nueva, `ARCHIVO` lo recupera automáticamente si no hay grabación ni resumen activo y abre Android SAF. `COPIAR` también puede recuperar el último archivo, pero el portapapeles o la app de destino pueden truncar datos largos: **priorizar ARCHIVO y verificar el footer**.
La recuperación requiere **actualizar encima de la APK existente**, sin desinstalar ni borrar datos y con el mismo package ID/firma de Android. Si cambian firma o appId, no asumir que están disponibles los archivos privados. **Si Android solicita desinstalar para instalar, detenerse y no aceptar** hasta evaluar alternativas.

## Verificaciones offline
- `python3 tests/check_android_export.py`: manifest/ruta de fuente bajo src/main y puente C#; rechaza ubicación vieja.
- `dotnet run --project tests/ReceiverRegression`: archivo privado válido vs corrupto, recuperación tras nueva instancia y copia con hash.
- `dotnet run --project tests/LatencyRegression`, `dotnet run --project tests/DashUiRegression`.
- GitHub Actions: compila fuente Java real desde `src/main/java` con Android SDK y exige archivo `.class`; no es una compilación Unity.
- Tras construir la APK en PC, **antes de instalar**: `python tests/verify_android_apk.py "RUTA\\BIOFIT_DASH_v091.apk"`. Si falla, no instalar: la clase Java no está empaquetada. Esto comprueba DEX, no que la Activity aparezca en el manifest fusionado.

## Procedimiento mínimo posterior (no hacerlo antes de pasar Play)
1. Cerrar Unity, preservar archivos locales `Scenes/*.unity/.meta`, Package/ProjectSettings (no descartar automáticamente, no hacer stash si querés moverlos). GitHub Desktop: Fetch origin y seleccionar `fix/export-package-recover-v091`; si pregunta, **Bring my changes**, salvo conflictos.
2. Unity 6.3 LTS: Play con `BiofitDash` (0.25g IZQ/DER, 0.12g, 180ms) y consola sin errores.
3. Una sola APK v0.9.1, Android activo, escenas `DiagnosticoBLE` (0), `BiofitDash` (1), SampleScene desactivada, Export Project OFF y Build App Bundle OFF. Ejecutar `verify_android_apk.py` sobre APK recién compilada.
4. **Instalar como actualización**, nunca desinstalar para instalar. No ejecutar una sesión nueva aún.
5. Abrir BIOFIT DASH, pulsar `3. ARCHIVO` **sin presionar INICIAR**. Debe buscar la última sesión válida en almacenamiento privado y abrir selector. Elegir Descargas y confirmar. Comprobar mensaje GUARDADO, archivo completo, footer `# END_BIOFIT sha256=` y contenido de etapas DERECHA y BILATERAL.
6. Pasar el CSV original íntegro y el conteo físico de la sesión a análisis antes de decidir cambios de detección derecha. En caso de fallo de manifest/clase o firma, detenerse; no desinstalar ni borrar datos.

## Estado y límites
- El problema exacto de `ClassNotFoundException` tiene una causa de estructura de fuente coherente con las directrices de Unity; **solución aún no probada en APK física**.
- La lógica de recuperación está probada por host, pero no se confirmó que el archivo privado previo siga presente en el dispositivo.
- El fallo de detección derecha al final del bloque puede provenir de ausencia de UP en firmware, bloqueo/rearme, caída de amplitud, selección de modo o actividad del avatar. Los 15 UP en modo derecho, con solo 4 bajo umbral, 2 ocupado y ninguno anti, dan una pista pero **no identifican causa sin CSV completo**. No modificar NVS, cambiar umbral menor a 0.25g ni reflashear firmware en esta revisión.
