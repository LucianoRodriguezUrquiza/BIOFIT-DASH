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


## Correccion de laboratorio 2D (teclado y presentacion)

La primera version presentaba dos problemas de UX: A/D no tenian respuesta
visible hasta estimar cadencia con varias alternancias, y la pista ocultaba
los textos de cadencia y actividad.

Actualizacion:
- Las teclas `A` y `D` aparecen en contadores individuales de
  **teclas recibidas**, separados de los candidatos aceptados por filtro.
- Cada pulsacion mueve el obstaculo 52 unidades visuales en Editor,
  sin esperar a que la cadencia sea calculable.
- Repetir `A` y `D` alternadamente a ~0,5 s muestra tambien
  candidatos, alternancia y cadencia experimental.
- Los textos ya no se superponen con la pista: el laboratorio es
  desplazable con la rueda del mouse y se puede maximizar la pestaña
  Game haciendo doble clic.
- `ESPACIO` solo salta con el cuadrado turquesa. El salto desde
  Waveshare aun no esta implementado.

Probar cambios SIN APK: detener Play, cerrar Unity, `Fetch origin` y
`Pull origin` en GitHub Desktop, reabrir Unity, presionar Play.
El componente `Biofit Movement Lab` que ya esta en la escena sigue
vigente; no hay que crearlo nuevamente.


## Ajuste del clasificador tras simulacion A-D en Unity (2026-11-15)

El usuario observo que la primera version podia seguir indicando CAMINANDO
o CORRIENDO despues de pasar de A-D-A-D a pulsar solo A o solo D.
**Causa:** retenia alternancias antiguas dentro de una ventana temporal;
las pulsaciones de un solo pie prolongaban la vida de esa cadencia.

Correccion:
- La cadena de alternancias debe ser consecutiva y temporalmente valida.
- Un candidato repetido del mismo lado (A-A / D-D), o una pausa mayor
  que 1,50 s entre candidatos, corta inmediatamente la cadena.
- Se necesitan 4 candidatos consecutivos alternando (3 intervalos)
  para estimar cadencia provisional.
- Solo un lado repetido -> MOVIMIENTO SIN CLASIFICAR, no caminar/correr.
- Sin candidatos recientes -> QUIETO. Los umbrales de cadencia siguen
  siendo experimentales, NO validados con personas.

Autocomprobacion sintetica, sin Android ni tobilleras:
`BIOFIT > Verificar Input Engine (sin APK)`.
El menu ejecuta comprobaciones de patrones alternados, unilaterales,
pausas, inactividad e impulsos UP aislados. Un resultado satisfactorio
verifica casos de logica, **no su validez biomecanica en datos reales**.

No es necesario generar otra APK por este cambio. Se incorporara al
proximo lote de compilacion de Android.


## Siguiente lote consolidado: par bilateral + registro para futura APK

Archivos nuevos:
- `Scripts/BiofitBilateralJumpDetector.cs`: compara `UP_IMPULSE` de
  izquierda/derecha por tiempo de llegada al mismo celular. Requiere
  aceleracion vertical positiva de al menos +0,12 g por lado, intervalo
  entre tobillos <= 180 ms, antirrebote por tobillo 320 ms y separacion
  minima entre pares 900 ms. **Todos son parametros experimentales**.
- `Scripts/BiofitEventCapture.cs`: guarda hasta 1500 filas BLE en memoria
  para copiar como CSV delimitado por punto y coma desde la APK,
  incluyendo hora relativa del telefono, `millis` por placa, secuencia,
  tipo de evento, giro y Z. **Los relojes ESP32 no estan sincronizados**.
- `Editor/BiofitVerificarSaltoBilateral.cs`: menu
  `BIOFIT > Verificar salto bilateral (sin APK)`; ejecuta pruebas
  sinteticas de coincidencia, rechazo de impulsos debiles, repeticiones,
  cooldown y calidad del CSV. Estas pruebas NO sustituyen la validacion
  fisica.

Cambios de interfaz:
- En `BiofitMovementLab.cs` (Unity Editor):
  `A` / `D` = MOV_ON simulado, `Q` / `E` = UP aislados,
  `J` = dos UP simultaneos artificiales; `ESPACIO` = salto MANUAL
  del cubo. Q/E/J **no activan el salto del cubo**.
- Pantalla diagnostica BLE: contadores de PARES UP y su ultimo intervalo,
  siempre etiquetados como **posibles saltos, no confirmados**.
- Una sola sesion Android permite pulsar:
  `INICIAR` -> actividad caminar -> `FIN CAMINAR` ->
  actividad correr -> `FIN CORRER` ->
  actividad de prueba bilateral -> `FIN SALTOS` ->
  `COPIAR RESUMEN` y/o `COPIAR CSV`.
  Cada fin de etapa deja marcas en el CSV y el resumen muestra
  deltas por tobillo, picos de cadencia y pares UP detectados.
- Los datos solo viven en memoria durante la sesion, no son un archivo
  permanente ni se suben automaticamente. El portapapeles Android
  debera comprobarse durante la siguiente prueba en telefono.

**ADVERTENCIAS:** las notificaciones de UP de dos tobillos pueden
coincidir casualmente al caminar, sin salto real. Sin contacto con el suelo,
sensores adicionales o validacion clinica no se debe afirmar confirmacion
de salto. El clasificador aun es exploratorio. La distancia y el recorrido
no se miden. No hay que saltar usando placas y cables sueltos; fijar
adecuadamente los dispositivos antes de cualquier ensayo de impacto.

Para probar en Unity sin una nueva APK: detener Play, guardar escena,
cerrar Unity, `Fetch origin` y (si corresponde) `Pull origin`,
reabrir proyecto, y usar `BIOFIT > Verificar salto bilateral (sin APK)`.
Luego en `Game` abrir el laboratorio y pulsar Q/E/J si se desea.
No volver a agregar el laboratorio a la escena si ya aparece en Inspector.
**No generar APK Android hasta la proxima validacion combinada.**


## Correccion de laboratorio 2D tras prueba de teclas (2026-11-15)

El usuario comprobo que A/D simulaban eventos, Q/E aumentaban los contadores
de impulsos y J generaba pares bilaterales. Detecto dos problemas: el
obstaculo daba saltos por pulsacion antes de moverse suavemente y, en
ciertas resoluciones, los cuadrados aparecian cortados en el borde inferior.

**Causas y soluciones:**
- Se elimino el desplazamiento artificial `obstacleX -= 52` en cada
  pulsacion de A o D. Ahora el obstaculo **solo avanza con cadencia
  alternada reciente**, usando `Mathf.MoveTowards` para acelerar y
  desacelerar suavemente. Pulsaciones unilaterales muestran un indicador
  A/D y actualizan contadores, pero NO crean avance continuo.
- Una secuencia A/D demasiado rapida puede ser filtrada por los limites
  temporales del clasificador: **no se flexibilizaron umbrales de IMU
  usando solo teclas de PC**, porque eso podria generar falsos pasos
  durante las pruebas reales.
- La pista ya no esta al final de una pagina vertical desplazable:
  se usa un lienzo virtual de 1120x740 escalado uniformemente.
  El cuadrado turquesa y el obstaculo salmon se dibujan completos
  sobre el suelo en la pantalla Game, incluyendo cuando no hay salto.
- `Q` y `E` por separado solo registran UP. Si se pulsan en una
  ventana de 180 ms y el detector acepta el par, el cubo hace un
  **salto demostrativo**. `J` genera ambos UP en Editor; si se acepta
  el par, tambien dispara el salto de demostracion.
- `ESPACIO` / `SALTO MANUAL` siguen funcionando independientemente
  del detector.
- En una futura APK Android, el control
  `SALTAR CON PARES BLE: NO/SI (DEMO)` permite elegir voluntariamente
  si los candidatos recibidos de las placas animan al cubo.
  Esta opcion empieza DESACTIVADA: los pares UP NO prueban un salto real.
- La pantalla de diagnostico BLE es ahora desplazable, para acceder a
  `INICIAR`, `FIN CAMINAR`, `FIN CORRER`, `FIN SALTOS`,
  `COPIAR RESUMEN` y `COPIAR CSV` en celulares de distintas alturas.
- `BIOFIT > Verificar salto bilateral (sin APK)` agrega comprobaciones
  sinteticas de la velocidad de vista previa, pares Q/E y posicion
  de la pista (29 casos totales); siguen pendientes las pruebas con
  datos BLE reales y la compilacion Android.

**Proximo paso:** detener Play, guardar, cerrar Unity, actualizar
en GitHub Desktop (Fetch -> Pull), reabrir Unity y ejecutar una sola
vez la verificacion automatica del salto bilateral. Si pasa,
probar brevemente `J` y `ESPACIO` en la pestaña Game (sin sensores).
No generar APK todavia, salvo que se programe una futura sesion
de validacion fisica combinada.
