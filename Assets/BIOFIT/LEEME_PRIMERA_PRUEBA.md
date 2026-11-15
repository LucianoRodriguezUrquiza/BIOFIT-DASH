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


## Diagnostico tras primer CSV Android real (15-11-2026)

Primera sesion recibida (APK `BIOFIT_GAME_Prueba_Combinada_v0_2.apk`):
- `INICIO` telefono 192.613 s, `FIN_SALTOS` 225.763 s:
  **33,15 s** en el registro.
- **83 EVENT** (47 izquierda, 36 derecha), **0 descartes**
  por limite de memoria.
- Resumen delta: izquierda MOV_ON=11, MOV_OFF=11, UP=25;
  derecha MOV_ON=7, MOV_OFF=6, UP=23; pares nuevos de UP=0.
- Secuencias de eventos recibidos continuas dentro del recorte:
  izquierda 86..132, derecha 78..113. Esto NO prueba que nunca
  se pierda un evento en otras sesiones.
- Screenshot de laboratorio `PARES BILATERALES: 7` es
  acumulado desde inicio de app; `PARES UP=0` del resumen
  significa **cero pares nuevos desde INICIAR**, no contradiccion.
- CSV solo incluyo `MARCA;INICIO` y `MARCA;FIN_SALTOS`.
  Faltaron FIN_CAMINAR y FIN_CORRER, de modo que TODOS los
  eventos de la captura fueron etiquetados `CAMINAR`.
  No se pueden separar ni validar las tres actividades.
- Anomalia temporal grave: `reloj_unity_procesado_s=208.720`
  para dos eventos derecho con tiempo sensor 547837 y 548373 ms
  (separados 536 ms reales del sensor), y otro sensor 549594 ms
  (121? no, 1221 ms despues) llega en el mismo frame.
  Intervalo global de sensor 1757 ms, registrado como 0 ms en Unity.
  Esto sesga falsamente la cadencia y la correlacion de UP bilaterales.

Implementacion de diagnostico para **proxima** compilacion Android:
1. Se captura `Stopwatch.GetTimestamp()` en el callback BLE
   ANTES de encolar el paquete y se calcula espera en cola Unity.
2. El CSV ahora anade:
   `espera_cola_unity_ms`,
   `reloj_callback_monotonico_s`.
   Este ultimo comparte origen **solo consigo mismo**; no
   debe restarse directamente a `reloj_unity_procesado_s`.
   Comparar intervalos dentro de cada columna y
   `reloj_esp32_ms` dentro de cada placa.
3. Los botones de etapa se validan en el orden obligatorio:
   INICIAR > FIN CAMINAR > FIN CORRER > FIN SALTOS.
   Si se intenta saltar una etapa se informa en pantalla y
   NO se altera el registro.
4. `BIOFIT > Verificar salto bilateral (sin APK)`
   incorpora ahora **35** verificaciones sintéticas; las
   nuevas pruebas revisan etapas y campos temporales.
5. **No volver a pedir ensayos fisicos ni recompilar**
   hasta preparar todos los ajustes del proximo lote.
   Se necesita investigar retraso entre ESP32, callback
   Android y proceso Unity antes de ajustar umbrales de clasificador.

Precaucion: el firmware incluye `millis` por evento, pero
estos relojes NO tienen origen comun. La causa exacta de
agrupamiento puede ser transmision, BLE Android, callback o
cola Unity. Los nuevos campos discriminan al menos la
latencia de cola Unity vs latencia anterior.


## BIOFIT DASH - primer videojuego jugable 2D (fase Editor, 15-11-2026)

El proyecto incorpora un juego ORIGINAL de avance automatico estilo
plataformas rítmicas, sin copiar recursos ni niveles de Geometry Dash.

Archivos:
- `Scripts/BiofitDashCore.cs`: avance automatico, salto con gravedad,
  obstáculos, colisiones, puntos, mejor puntuacion y pausa/reinicio.
- `Scripts/BiofitDashGame.cs`: interfaz 2D de Unity con salto por
  ESPACIO, boton tactil, opcion de pares BLE experimental (OFF por
  defecto), y vista escalada a distintas resoluciones.
- `Editor/BiofitPrepararDash.cs`: comando de menu para crear/abrir
  `Assets/BIOFIT/Scenes/BiofitDash.unity`, manteniendo intacta
  `DiagnosticoBLE.unity`; agrega Dash a la lista de escenas.
- `Editor/BiofitVerificarDash.cs`: 18 comprobaciones sinteticas
  del motor de juego, independientes de Android.

Para probar en Windows:
1. Detener Play, guardar y cerrar Unity.
2. GitHub Desktop > Fetch origin > Pull origin.
3. Abrir proyecto en Unity Hub.
4. Menu `BIOFIT > Verificar BIOFIT DASH (sin APK)`.
   Debe informar **18 pruebas sinteticas superadas**.
5. Menu `BIOFIT > Crear o abrir BIOFIT DASH (primer juego)`.
   Ese comando CREA la escena si no existe y la abre.
6. Presionar `Play`, luego elegir `Game`.
   `ESPACIO` o boton SALTAR = salto; `R` = reiniciar;
   `P` = pausa. El mundo avanza automaticamente.
7. El modo `SALTAR POR BLE` queda OFF hasta que se valide
   la temporizacion real. En Editor no se conecta BLE Android.

Importante:
- NO es necesario volver a calibrar/reprogramar las Waveshare.
- Las 35 verificaciones del salto bilateral corresponden a la
  etapa ANTERIOR y no incluyen estas 18 nuevas verificaciones.
- Si funcionan estas pruebas de Editor, antes de compilar Android
  hay que integrar el acceso al juego desde el menu de la app
  (o establecer la escena del juego como entrada). No generar
  APK adicional solo para comprobar la vista en Windows.
- El detector de pares BLE es EXPERIMENTAL y los `UP_IMPULSE`
  no equivalen necesariamente a saltos reales.
- Sigue pendiente analizar el agrupamiento temporal de eventos
  constatado en el ultimo CSV de Android. No confiar aun en la
  clasificacion biomecanica.


## BIOFIT DASH desde la misma APK de diagnostico (15-11-2026)

El primer Dash fue probado visualmente en Unity: el usuario mostro
cubo, punta, puntuacion y mejor puntuacion=6, con salto manual.
La fase siguiente no requiere cargar otra escena ni firmware.

**Cambios de integracion:**
- La escena `DiagnosticoBLE` sigue siendo la entrada (indice 0).
- Dentro de ella aparece el boton `JUGAR BIOFIT DASH (PROTOTIPO)`.
- El boton agrega `BiofitDashGame` al MISMO objeto con
  `BiofitBLEMonitor`, sin destruir ni duplicar la conexion GATT:
  conserva contadores, sesiones y CSV del monitor.
- Dash incluye boton `DIAGNOSTICO BLE` para volver a la pantalla
  de datos y continuar la misma sesion.
- En la pantalla Dash aparece el estado BLE (`2/2 conectados`
  o `esperando trackers`), los pares recibidos y los intentos de salto.
- `SALTAR POR BLE: NO` es el valor inicial; cuando se activa
  `SI (EXPERIMENTAL)` los pares `UP_IMPULSE` pueden saltar en el juego,
  pero siguen siendo candidatos no confirmados y la llegada BLE puede
  tener retardos/paquetes agrupados.
- Correccion de GUI.matrix al cambiar de vista; titulo abreviado
  para no cortarse a resoluciones menores.

**Prueba en Unity Editor SIN otra APK:**
1. `Fetch origin`, `Pull origin`, abrir Unity.
2. Abrir `Assets/BIOFIT/Scenes/DiagnosticoBLE`, no `BiofitDash`.
3. Play -> Game -> `JUGAR BIOFIT DASH (PROTOTIPO)`.
   Salto por barra espaciadora, avanzar automaticamente,
   boton `DIAGNOSTICO BLE` para volver.
4. En Editor solo comprueba navegacion y juego; BLE real solo Android.
5. Tras esta prueba, preparar una SOLA APK combinada con
   `DiagnosticoBLE` como primera/única escena si se desea.
   No hace falta añadir una segunda escena de Dash al build.

**Validacion pendiente antes de declararlo terminado:** comprobar
que Android conecta dos trackers, cambia de vista sin desconectarlos,
puede habilitar salto BLE para ensayos controlados y exporta CSV con
las marcas de callback y cola añadidas a la version previa.
No atribuir falsas detecciones a saltos reales.
