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


## BIOFIT DASH - paquete de UNA SOLA prueba fisica (2026-11-15)

El usuario confirmo que desde `DiagnosticoBLE` funciona el boton
`JUGAR BIOFIT DASH (PROTOTIPO)` y que `DIAGNOSTICO BLE` vuelve a
la pantalla de sensores. La jugabilidad manual en Editor tambien
ha sido confirmada; NO pedir mas validaciones repetidas de teclado.

Esta revision consolida antes de compilar Android:
1. Una unica conexion BLE de ambos tobillos compartida por diagnostico
   y juego (sin duplicar suscripciones GATT).
2. Salto BLE opcional, **OFF por defecto**. Solo se puede activar
   con los **dos trackers conectados**. Los pares son candidatos,
   NO saltos biomecanicos confirmados.
3. Desde el juego: boton `MOSTRAR/OCULTAR PANEL DE PRUEBAS`.
   Panel con `1. INICIAR REGISTRO`, `2. FINALIZAR`,
   `3. COPIAR REGISTRO`.
4. Actividades con botones de marca: `QUIETO`, `CAMINAR`,
   `CORRER`, `ELEVACION`, `DASH`. Una captura completa.
5. Sensibilidad BLE ajustable SIN recompilar:
   `UP - / UP +` en pasos de 0,02 g (rango 0,08-0,50 g),
   `PAR - / PAR +` en pasos de 20 ms (rango 120-400 ms).
   Valores iniciales 0,12 g y 180 ms. Cada cambio se anota
   en el CSV, para no perder trazabilidad.
6. Clasificador usa el reloj del **callback BLE**, aproximado
   como tiempo Unity actual menos espera en la cola Unity.
   CSV conserva `reloj_unity_procesado_s`, el valor crudo
   `reloj_callback_monotonico_s`, demora de cola
   `espera_cola_unity_ms` y millis independientes de ESP32.
   **NO** corrige los retrasos previos a callbacks BLE del SO,
   NI sincroniza relojes de ambos trackers.
7. El CSV registra `APP` para salto manual, pares detectados,
   intentos de salto BLE y activacion/desactivacion del modo,
   y `CONFIG` para los umbrales, ademas de `EV` de IMU.
   Todas las filas regulares tienen 12 columnas separadas
   por punto y coma.

### Ultima verificacion SIN APK
- Cerrar Unity, `Fetch origin` y `Pull origin` en GitHub Desktop.
- Abrir Unity y entrar a `BIOFIT > Verificar BIOFIT DASH (sin APK)`.
- Resultado esperado: **35 pruebas sinteticas superadas**.
- El antiguo `BIOFIT > Verificar salto bilateral (sin APK)`
  conserva sus **35** pruebas, ya aprobadas; no hace falta
  repetirlas.
- Si la prueba nueva falla o hay errores rojos en Console,
  corregir el codigo ANTES de una nueva compilacion Android.

### Compilar UNA sola APK
- Desde `File > Build Profiles > Android`, verificar Active.
- Poner `BIOFIT/Scenes/DiagnosticoBLE` marcada en la
  primera posicion (escena 0).
- Puede desmarcarse la escena secundaria `BiofitDash`
  y `SampleScene`; la vista del juego se abre por el boton
  en la misma escena `DiagnosticoBLE`.
- `Build App Bundle (Google Play)` OFF. Elegir `Build`
  y guardar como `BIOFIT_GAME_DASH_BLE_v0_3.apk`.
- No necesita reflashear ni recalibrar firmware, mientras
  ambas Waveshare conserven sus ejes y funcionen.
- Verificar version/certificado al actualizar la APK anterior;
  si Android no permite instalar encima, investigar el error
  y solo despues considerar desinstalar la version anterior.

### Una sesion Android de validacion combinada
1. Encender BLE, desconectar nRF Connect, alimentar placas.
2. Abrir APK, autorizar dispositivos cercanos,
   verificar `CONECTADO - NOTIFICACIONES ACTIVAS` en ambas.
3. Entrar a `JUGAR BIOFIT DASH`. Comprobar `2/2 conectados`.
4. `MOSTRAR PANEL` > `INICIAR REGISTRO`.
5. En `QUIETO`, estar quieto alrededor de 10 s.
   Marcar `CAMINAR`, caminar en el lugar 10-15 s.
   Marcar `CORRER`, movimiento mas rapido de 10-15 s
   solo si resulta comodo. Marcar `ELEVACION`, sentado,
   elevar ambos pies a la vez 5-8 veces sin salto de impacto.
   Marcar `DASH`, activar `SALTAR POR BLE` y probar
   algunos gestos bilaterales controlados cerca de obstaculos,
   registrar si hay salto e intentos erroneos; salto manual
   como control adicional.
6. Terminar con `FINALIZAR` y `COPIAR REGISTRO`,
   pegar resumen+CSV en un texto/nota UNA SOLA VEZ;
   **guardar antes de cerrar app**.
7. Enviar CSV junto con observacion visual simple:
   si se movio el cubo al detectar el par y si hubo
   falsos positivos/negativos.
8. Dejar los parametros por defecto inicialmente. Solo
   variar sensibilidad si realmente se necesita, porque
   cada ajuste se registra y afecta la comparabilidad.

IMPORTANTE: los gestos de elevacion desde sentado no
prueban saltos reales. La prueba permite detectar
problemas de sensor, BLE, calidad temporal y respuesta
del juego con el menor numero razonable de ensayos.


## Diagnostico de primer ensayo BLE + DASH y siguiente revision v0.4 (15-11-2026)

**Sesion Android real v0.3 enviada por usuario:**
- `IZQ MOV_ON=21 UP=69`; `DER MOV_ON=6 UP=58`.
- `PARES UP=0`; `EVENTOS=181`; `DESCARTADOS=0`
  (esta ultima cifra NO certifica ausencia de pérdidas en toda la ruta).
- Umbral `0,12g` y ventana `180ms`.
- Registro `APP;DASH;358.953;;BLE_ACTIVADO` confirma que el usuario
  activó el salto BLE. No existen `PAR_BLE_DETECTADO` ni
  `SALTO_BLE_REALIZADO` posteriores: por eso el cubo no saltó.
- Intervalos de recepcion muy superiores a 180ms incluso al levantar
  ambos pies: CAMINAR 233ms, CORRER 234ms, ELEVACION 235ms,
  DASH 567ms, 568ms. Son impulsos cercanos, no pares físicos
  verificados.
- Compresion temporal PROBADA: dos UP derecho en tiempos de
  ESP32 `3540104` y `3541069` difieren **965ms**, aunque
  Unity recibe ambos cerca de 382,678-382,679s y sus callbacks
  difieren ~25ms. Por tanto, el reloj del callback **no basta** para
  inferir tiempo fisico de cada evento. La demora de cola Unity es
  generalmente 0-34ms, por lo que el problema ocurre antes o durante
  la llegada de notificaciones; NO se conoce aun si es por firmware,
  enlace BLE o pila Android.
- `UP_IMPULSE` aun no equivale a paso o salto. Ensanchar la ventana
  a 400/600ms sin control puede confundir marcha alternada con salto.

**Correcciones agrupadas v0.4, sin cambios de firmware:**
1. `BiofitClockAligner` se suscribe opcionalmente a STATUS BLE
   (UUID `...0003`, paquete de 20bytes; tiempo IMU en bytes 6..9).
   Estima el desfase de cada reloj ESP32 usando observaciones de
   callback de baja latencia. Es una estimacion probabilistica,
   NO sincronizacion exacta ni garantía de latencia BLE uniforme.
   Si STATUS falla, mantiene el procesamiento de callback anterior.
2. El detector bilateral recuerda los ultimos 16 UP por pie y
   permite emparejamiento de notificaciones fuera de orden sin
   reutilizar pulsos. Conserva umbral 0,12g, ventana 180ms,
   antirrebote 320ms y cooldown bilateral 900ms.
3. El CSV permite diagnosticar compresion de intervalos temporal:
   resumen `TIEMPOS_COMPRIMIDOS`, `EVENTOS_ALINEADOS` y
   `ESTIMACION_RELOJ`. No confundir eventos entregados con
   simultaneidad fisiologica verdadera.
4. **MODO PRACTICA SIN COLISION**: el cubo no termina la partida;
   se puede grabar, caminar o elevar pies sin reiniciar el juego.
5. Control de juego BLE con cuatro modos mediante el mismo boton:
   `OFF -> BILATERAL EXP -> SOLO IZQ DIAG ->
   SOLO DER DIAG -> OFF`.
   En modo SOLO, un UP superior al umbral configurable inicia un
   salto demo tras cooldown 1,2s, incluso si nunca se forma un par.
   Esto permite aislar BLE/Unity/juego de la sincronizacion. NO es
   deteccion de salto real, y puede reaccionar a caminar.
6. Ajuste `SOLO UP +/-` desde panel (inicial 0,45g, rango
   0,25..1,00g). La configuracion y los comandos se anotan en CSV.
7. Actualizada `BIOFIT > Verificar BIOFIT DASH (sin APK)`:
   **51 verificaciones sintéticas** previstas en la nueva rama:
   incluye modo practica, controles CSV, sincronizacion aproximada,
   desorden BLE y rechazo de paso alternado a 270ms.
   Hasta que el usuario ejecute la prueba en Unity y no haya errores,
   la revision NO ESTA COMPILADA/VALIDADA en Android.

**Protocolo de próxima y unica prueba tras pasar las 51 pruebas:**
- Exportar APK **v0.4**, una sola compilacion.
- Conectar 2/2 sensores; entrar DASH, activar
  `PRACTICA SIN COLISION: SI`.
- Marcar INICIAR / QUIETO unos segundos / ELEVACION.
- Probar 5-8 elevaciones sentadas en `BILATERAL EXP`, sin modificar
  inicialmente umbrales. Observar `PARES`, saltos y
  `TIEMPOS COMPRIMIDOS`.
- Sin detener la sesion: cambiar a `SOLO IZQ DIAG` y levantar solo
  el pie izquierdo unas 3-5 veces, luego `SOLO DER DIAG` con el
  derecho. Estos modos sirven para identificar si los impulsos llegan
  a animar el cubo independientemente del par.
- Finalizar y copiar una sola vez; anotar observacion de respuesta
  visual real frente al movimiento. NO repetir CAMINAR/CORRER de
  v0.3 salvo que el diagnostico posterior lo requiera.
- Enviar CSV; decidir si hay que intervenir en firmware para
  reducir batching de notificaciones y/o añadir protocolo de sync
  de tiempo robusto (reloj RTT). Hasta entonces el estimador de
  STATUS es experimental.


## BIOFIT DASH v0.4 - diagnostico de sincronizacion de dos tobillos (2026-11-15)

### Evidencia que motivo el cambio
En la APK v0.3, 181 notificaciones EVENT, 0 descartes CSV,
IZQ 69 UP, DER 58 UP, pero 0 pares UP y 0 saltos BLE.
Las separaciones de callbacks en ELEVACION fueron aprox.
235-301 ms (umbral 180 ms); en DASH se observaron aprox.
568-570 ms. El CSV tambien muestra notificaciones de un
mismo sensor con intervalos IMU muy superiores al intervalo
de callback: evidencia de agrupamiento temporal antes de Unity.
No atribuir sin pruebas el cuello de botella a la IMU, BLE,
Android o UniBLE.

### Implementacion
- Suscripcion a **STATUS** GATT V2, UUID f971...0003, paquete
  binario de 20 bytes (p[0]=2, p[1]=ID, uint32 LE en p[6..9]).
  Firmware envia STATUS aprox. cada 50 ms. Se retiene la
  referencia a la caracteristica para ambas placas.
- BiofitClockAligner estima por separado el desplazamiento
  entre millis ESP32 y Stopwatch del callback, usando el
  offset MINIMO de los ultimos 12 s de STATUS; requiere al
  menos 8 muestras recientes por tracker. Metodo aproximado:
  el offset queda contaminado por la menor demora BLE/Android
  observada de cada placa. NO sincronizacion exacta ni
  medicion absoluta de latencia; status puede perderse.
- Cada UP puede usar la marca ESP32 alineada si AMBAS placas
  tienen anclas recientes. Caso contrario recurre a tiempo
  de callback; limpia candidatos al cambiar de dominio
  temporal. Procesamiento por reloj double para no perder
  precision.
- BiofitBilateralJumpDetector conserva multiples pendientes
  por tobillo, compara diferencias absolutas aunque la
  entrega BLE este fuera de orden, rechaza g bajo, duplicados
  del mismo tobillo, y pares dentro del cooldown. Reporta
  tambien candidatos cercanos RECHAZADOS (>ventana y <1.2s).
  No confirma un salto humano.
- Nuevo CSV DE 15 COLUMNAS: las 12 anteriores mas
  reloj_evento_estimado_s, reloj_alineado,
  demora_relativa_estimacion_ms. La demora es EXCESO frente al
  mejor STATUS observado, NO latencia BLE absoluta. Valores
  vacios si no hay alineacion. APP/MARCA/CONFIG preservan
  el mismo ancho.
- Resumen: ESTIMACION_RELOJ, EVENTOS_ALINEADOS, STATUS por
  tobillo, PARES_CERCANOS_RECHAZADOS y TIEMPOS_COMPRIMIDOS.
  La pantalla tambien informa STATUS y casi-pares.
- El juego incluye PRACTICA SIN COLISION: SI/NO y el
  selector de control BLE ciclico:
  OFF -> BILATERAL -> SOLO IZQ -> SOLO DER -> OFF.
  El modo unilateral es un CONTROL DE DIAGNOSTICO, no
  una prueba de saltos fisicos. Umbral unilateral inicial
  0.45 g, ajustable desde el panel sin recompilar.

### Compilacion unica para Android
1. Cerrar Unity; GitHub Desktop > Fetch origin > Pull origin
   (si el boton aparece).
2. Abrir Unity. BIOFIT > Verificar BIOFIT DASH (sin APK):
   resultado esperado **55 pruebas sinteticas superadas**.
   Revisar Console: 0 errores rojos; la advertencia CS1998
   sin await no impide compilar.
3. Si falla una prueba NO compilar Android; informar el error.
4. File > Build Profiles > Android Active; SOLO
   BIOFIT/Scenes/DiagnosticoBLE marcada, escena 0.
   Build App Bundle OFF; Build APK como
   BIOFIT_GAME_DASH_BLE_v0_4.apk.
5. Instalar en celular (si ya existe APK de mismo identificador
   y firma, Android puede permitir actualizar sin desinstalar).

### Una sola prueba corta; NO repetir caminar/correr
1. Encender dos Waveshare, cerrar nRF Connect y abrir APK.
   Esperar 2/2 conectados y **reloj: IMU estimado**.
   Si queda 'sin ancla STATUS', registrar y NO suponer
   que la sincronizacion funciona.
2. JUGAR BIOFIT DASH > PRACTICA SIN COLISION: SI.
   Mostrar panel; INICIAR REGISTRO; seleccionar DASH.
3. Selector BLE -> SOLO IZQ. Mover/elevar el tobillo
   izquierdo varias veces; ver si sube el contador 'Solo'
   y si el cuadrado ejecuta algun salto.
4. Selector BLE -> SOLO DER. Repetir con tobillo derecho.
5. Selector BLE -> OFF y luego BILATERAL. Ejecutar
   6-8 elevaciones simultaneas controladas desde una
   postura comoda, sin saltos de impacto.
6. Verificar si aparecen PARES, CASI PARES, INTENTOS,
   y si salta el cubo. Dejar umbrales iniciales 0.12 g /
   180 ms; si no funciona, probar una sola vez PAR+ hasta
   300 ms y anotar en CSV.
7. FINALIZAR, COPIAR REGISTRO y pegarlo en una nota.
   Compartir el resumen y el CSV para evaluar:
   (a) si STATUS estuvo disponible; (b) si relojes
   se alinearon; (c) si hubieron pares; (d) respuesta
   del juego; (e) falsos positivos caminando.
8. No reflashear ni recalibrar firmware para esta prueba.


## BIOFIT DASH v0.5 — ajuste de anclas STATUS (2026-11-15)

### Evidencia y limites
- v0.4: izquierdo 23 UP sin salto unilateral; pico izquierdo
  +0.442 g < umbral SOLO_UP=0.45 g. Es bloqueo de configuracion
  unilateral, no ausencia de senal: probar SOLO_UP=0.35 g.
- Derecho: 17 UP, 9 SALTO_SOLO_REALIZADO registrados; valida
  el control BLE unilateral, no la discriminacion biomecanica.
- Temporizacion: sucesivos EVENT se recibieron en tandas aprox.
  cada 2 s, pese a tener marcas internas ESP32 entre ellas.
  No queda identificado si la causa es firmware, radio BLE,
  Android, la biblioteca o el procesamiento. La alineacion
  temporal NO elimina ese retardo de interactividad.
- STATUS total izq/der >300, reloj INACTIVO. Hipotesis:
  ventana de 12 s tenia <8 STATUS recientes por tracker;
  los contadores totales historicos NO probaban preparacion.
  La nueva ventana de 32 s sigue requiriendo >=8 STATUS,
  ahora visibles como recientes(32s). Maxima antiguedad
  del ultimo STATUS: 4 s; no aceptar relojes sin STATUS vivo.
  La alineacion es APROXIMADA, no sincronizacion de laboratorio.
- Cambio NO afecta firmware, memoria de calibracion ni
  configuracion del ESP32.

### Validacion local ANTES de compilar
Cerrar Unity, GitHub Desktop Fetch/Pull, abrir editor.
Menu BIOFIT > Verificar BIOFIT DASH (sin APK).
Resultado esperado: **58 pruebas sinteticas superadas**, 0 errores rojos.
En particular prueba 8 STATUS/32 s con recepcion espaciada 2 s
y rechazo del reloj caducado. Esto no sustituye la prueba real.

### Siguiente APK y unica prueba fisica REDUCIDA
Compilar solo si pasan pruebas:
BIOFIT_GAME_DASH_BLE_v0_5.apk.
Mantener marcada solo DiagnosticoBLE como escena inicial.

1. Encender Waveshare, abrir APK, confirmar 2/2 conectados.
   Anotar reloj y contador **recientes(32s)** tras ~40s.
2. JUGAR BIOFIT DASH; PRACTICA SIN COLISION: SI;
   INICIAR REGISTRO una sola vez, seleccionar ELEVACION.
3. BLE SOLO IZQ; pulsar SOLO UP - DOS veces (0.45 -> 0.35 g).
   Elevar SOLO pie izquierdo 5-6 veces, con >2s entre gestos;
   observar saltos o intentos. No usar 'UP -' bilateral.
4. Pulsar DASH en registro; cambiar BLE SOLO IZQ ->
   SOLO DER -> OFF -> BILATERAL (TRES toques).
   Dejar UP=0.12 g y PAR=180 ms.
   Elevar ambos pies al mismo tiempo 6 veces.
5. Si no hay pares, presionar PAR + SEIS veces para
   subir a 300 ms y probar 4 elevaciones bilaterales mas.
6. FINALIZAR una sola vez, COPIAR REGISTRO, guardar CSV.
   Informar: saltos izq, saltos bilaterales, ancla STATUS
   y si el cubo responde con retraso visible.
7. No repetir la prueba derecha: ya hay evidencia de 9
   saltos unilaterales en v0.4.


## REQUISITO PRIORITARIO: latencia gestual de 1 a 1,5 s (observacion reportada, 2026-11-15)

**Hecho observado por usuario**: elevacion del pie real produce salto del cubo
aproximadamente 1–1,5 s despues; en ocasiones ocurre solo al terminar de
elevar y volver a apoyar el pie. Demasiado lento para un plataformas reflexivo.
Corregir **antes** de declarar jugabilidad validada. No confundir
emparejamiento temporal STATUS con disminucion de latencia real.

**Evidencia de CSV v0.4**: dos EVENT del tracker izquierdo con timestamps
ESP32 606954 y 607736 ms, separados 782 ms, llegaron al callback monotónico
Android 438939.820 y 438939.832 s, separados 12 ms. En tracker derecho
timestamps ESP32 682334 y 683031 ms (697 ms) llegaron a callbacks
439009.533 y 439009.556 s (23 ms). Hay compresion temporal severa
PREVIA al callback de Unity; espera_colas_Unity ~2–32 ms en esos ejemplos.
Diagnostico probable: buffering/agrupamiento de BLE o envio del firmware,
no animacion o frame rate Unity. No atribuir con certeza a una capa
particular sin instrumentacion.

**Plan de diagnostico y accion, sin ensayos repetitivos**:
1. Capturar timestamp del momento en que la QMI8658 genera UP_IMPULSE,
   cuando el firmware encola ENVIA notify, cuando Android recibe callback
   y cuando game.Jump() se ejecuta. Comparar intervalos con relojes
   alineados; idealmente registrar secuencia, muestras y contadores de
   overflow/cola y renderizar debug en una unica APK.
2. Revisar frecuencia real de notify, intervalos y param BLE de conexion,
   buffering, manejo de tareas y event queue en firmware y la biblioteca
   UniBLE/Android. Verificar que no haya polling/flush cercano a 2 s.
3. Inspeccionar si detector activa UP en despegue o aterrizaje; registrar
   giro, aceleracion vertical y tiempo de cruce umbral. No confundir
   retardo del detector con transporte.
4. Si hay batching de origen firmware, corregir firmware (sin borrar NVS
   de calibracion); si ocurre tras ESP32 notify, investigar Android/UniBLE.
5. Meta orientativa: <100-150 ms desde gesto hasta salto visible, a
   VALIDAR; no hacer promesas sin medicion.
6. No compilar sucesivas APK por cada parametro. Consolidar
   instrumentacion y medidas de mitigacion en una sola revision futura,
   junto con las mejoras de sincronizacion existentes en v0.5.
