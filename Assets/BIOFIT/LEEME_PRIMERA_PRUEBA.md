# BIOFIT GAME — prueba inicial BLE de ambos tobillos

Esta etapa solamente valida la recepción BLE en una APK Android.
No se modifica el firmware v2.2.5, ni la calibración de las Waveshare.

## Qué ya trae este repositorio

- `Packages/manifest.json`: UniBLE v0.1.1 (se instala al abrir el proyecto).
- `Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs`: conexión de las dos Waveshare, suscripción a EVENT y pantalla de contadores.
- `Assets/BIOFIT/Editor/BiofitPrepararEscenaBLE.cs`: opción en el menú BIOFIT para preparar la escena.
- Android mínimo API 29 (Android 10), requerido por UniBLE.

## En Windows (primera vez)

1. Abrí GitHub Desktop con el repositorio `BIOFIT-GAME-UNITY`.
2. Presioná `Fetch origin` y, si aparece, `Pull origin`.
3. Abrí el proyecto desde Unity Hub con Unity 6.3 LTS.
4. Permití que Unity descargue e importe los paquetes. Si solicita Git, instalá Git para Windows y volvé a abrir Unity.
5. En la barra superior de Unity Editor, elegí `BIOFIT > Preparar escena de diagnostico BLE`.
6. Unity creará `Assets/BIOFIT/Scenes/DiagnosticoBLE.unity`, agregará el objeto `BIOFIT_BLE` y la pondrá primera en las escenas de compilación.
7. En `Game`, si apretás Play en Windows, solo verás una vista previa; es normal que no conecte BLE desde el Editor.

## Prueba real con teléfono Android

1. En nRF Connect, desconectá ambos trackers y cerrá la app.
2. Encendé las dos Waveshare y mantenelas cerca del teléfono.
3. En Unity: `File > Build Profiles > Android > Switch Platform` (según la interfaz puede decir `Archivo > Perfiles de compilación`).
4. Comprobá que `DiagnosticoBLE` esté incluida y primera en la lista de escenas.
5. Con el celular conectado por USB y la depuración USB activada, elegí `Build And Run`. También podés generar la APK e instalarla manualmente.
6. En el celular aceptá el permiso de dispositivos cercanos.
7. Mové cada tobillo. Deben aparecer eventos y contadores independientes de MOV_ON, MOV_OFF y UP.

## Datos

- MAC izquierda: 80:45:6B:35:DE:61
- MAC derecha: 28:84:85:55:6B:B9
- Servicio: f9710000-6d68-4b7d-a742-6fae7b5f0001
- Eventos: f9710000-6d68-4b7d-a742-6fae7b5f0002
- Evento: 16 bytes little endian, primer byte=2, segundo byte=tracker 1/2 y tercero=tipo 1 MOV_ON / 2 MOV_OFF / 3 UP_IMPULSE.

## Limitaciones actuales

- El script usa las dos MAC verificadas en nRF Connect. Si cambian las placas, actualizarlas en el Inspector.
- Las conexiones se intentan al comenzar. La reconexión automática y el watchdog de BLE se incorporarán después de probar la primera APK.
- UP_IMPULSE es un candidato, no un salto confirmado. No hay lógica de JUMP ni de caminar/correr todavía.
- No se ha compilado ni probado esta APK en el dispositivo real desde este entorno.
- UniBLE se distribuye con licencia MIT en https://github.com/yoshidan/UniBLE


## BIOFIT Input Engine - primera version experimental

Incorporado: `Assets/BIOFIT/Scripts/BiofitInputEngine.cs`, invocado desde
`BiofitBLEMonitor.cs` en el hilo principal. No hay cambios de firmware.

- Registra candidatos desde `MOV_ON` de cada tracker, con minimo 300 ms
  entre candidatos de un mismo pie.
- Cuenta cambios izquierda-derecha y calcula una **cadencia tentativa**
  usando diferencias entre recepciones BLE en el reloj del telefono
  (no los `millis` independientes de los dos ESP32).
- Muestra `QUIETO`, `MOVIMIENTO SIN CLASIFICAR`, `CAMINANDO (TENTATIVO)`
  y `CORRIENDO (TENTATIVO)`. Los umbrales 55 y 150/min son solo
  parametros de prototipo, no estan validados.
- Los candidatos NO son pasos reales demostrados. La cantidad de `MOV_ON`
  puede no coincidir con el numero de pasos, y el BLE puede tener latencia.
- `UP_IMPULSE` se conserva como evento crudo, no se convierte a `JUMP`.

La primera APK BLE de solo diagnostico sigue siendo valida y puede
usarse sin instalar una nueva version. Si se desea ver la nueva seccion
experimental, hacer `Fetch origin > Pull origin` con Unity cerrado,
abrir el proyecto y volver a compilar la APK. Una futura validacion
fijara umbrales y evaluara tasas de error con datos temporalmente
alineados de ambos tobillos.


## Proximo lote consolidado (todavia NO hace falta compilar)

Esta actualizacion incluye TRES mejoras en una sola entrega:

1. `BiofitInputEngine.cs`: candidatos MOV_ON, alternancia, cadencia,
   clasificacion experimental (no pasos confirmados).
2. `BiofitSessionLogger.cs`: en la misma pantalla BLE, botones
   `INICIAR`, `FIN CAMINAR`, `FIN CORRER`, `COPIAR RESULTADOS`.
   Guarda diferencias de contadores y el pico de cadencia estimada.
   Evita reiniciar la APK entre etapas y permite pegar el reporte en un chat.
3. `BiofitMovementLab.cs`: laboratorio 2D dentro de la misma escena y
   conexion BLE. Para probarlo en PC sin sensores, al hacer la UNICA
   actualizacion del repositorio:
   - `BIOFIT > Agregar laboratorio 2D a DiagnosticoBLE` (una vez).
   - `Play` en Unity, en pantalla `Game` hacer clic en
     `ABRIR LABORATORIO 2D`.
   - Pulsar `A` y `D` alternadamente = SIMULACION de inicio de movimiento
     de tobillos; `ESPACIO` = SALTO ARTIFICIAL del personaje.
   - `TAB` vuelve del laboratorio al diagnostico en Editor.
   - A/D no sustituyen una prueba fisica; el salto de ESPACIO no valida
     salto con IMU.

El laboratorio usa el MISMO `BiofitBLEMonitor` de la escena.
Se puede alternar entre vistas sin reconectar ni reiniciar las Waveshare.
En Android usa los datos BLE reales para mover la representacion en funcion
de la cadencia tentativa; el boton de salto es una entrada manual.
El sistema de registro permite obtener un resumen textual copiable al finalizar
varias actividades en una sola sesion.

**Limitaciones:** estos scripts no han sido compilados ni probados en una
APK real desde este entorno. La importacion y la simulacion en el Editor
deben verificarse primero, y los clasificadores deben validarse
experimentalmente en una futura prueba agrupada.

Cuando el usuario decida actualizar, hacerlo con Unity cerrado y
`Fetch origin > Pull origin` desde GitHub Desktop.
**No recompilar Android por cada cambio menor.**
