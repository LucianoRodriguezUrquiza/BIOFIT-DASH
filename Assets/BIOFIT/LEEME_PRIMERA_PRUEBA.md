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
