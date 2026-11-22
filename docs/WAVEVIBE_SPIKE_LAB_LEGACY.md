# BIOFIT DASH - WaveVibe plano para probar latencia (BLE BILATERAL LEGACY)

**Estado:** experimento independiente, no fusionado en main. Base: \`750c80ec3727e3dbaab0ea3135f5138c02f909f5\`.
**Rama:** \`feat/wavevibe-spike-test-legacy-v1\`.
**No utilizar** MOV HOLD V2 ni CICLO EXP para este ensayo.

## Objetivo y geometria

El quinto nivel conserva id=4, nombre "Wave Vibe", dificultad=1 y musica "CantLetGo".
Se reemplaza SOLO el JSON importado de WaveVibe por:

- Siete \`spike\` originales en suelo, y=0, x=25,65,105,145,185,225,265 (40 unidades entre pinchos).
- Un unico marcador \`ground\` no renderizado en x=285, y=-2 para fijar el final.
- El propio \`Game\` genera el suelo plano y la meta sin agregar otros prefabs.
- No se cambian prefabs, tiles, escenas, skins, impulsos, gravedad, velocidad, timeScale, UUID, NVS ni firmware.

Duracion **nominal**: (x_final + 1 - x_inicio) / (velocidad * timeScale) =
(285 + 1 - (-10)) / (6.5 * 1.5) = 30.36 segundos. Es una estimacion del recorrido
al jugar sin morir; medir en Android. Aproximadamente 4.1 segundos entre pinchos.

El submodulo \`ThirdParty/GeomeTryOriginal\` sigue en \`d922cadd...\` con su WaveVibe original;
los cuatro mapas restantes estan intactos. Cambia la copia importada mediante un instalador
que rechaza modificaciones inesperadas, conserva \`WaveVibe.json.meta\` y es idempotente.

## Preparacion segura en un clon nuevo (Windows PowerShell)

Cerrar Unity y NO ejecutar este procedimiento sobre carpetas de versiones previas.

\`\`\`powershell
cd "C:\Users\Capo\Documents"
git clone --recurse-submodules --branch feat/wavevibe-spike-test-legacy-v1 https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-SPIKE-LAB-LEGACY
cd ".\BIOFIT-SPIKE-LAB-LEGACY"
git rev-parse --short HEAD
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\install_wavevibe_training.ps1"
\`\`\`

**Importante:** \`prepare_geometry_original.ps1\` solo se ejecuta en un clon NUEVO, una vez.
En cambio, \`install_wavevibe_training.ps1\` se puede repetir si el JSON importado coincide
exactamente con el original o con esta version. Si no coincide, se detiene sin sobreescribirlo.

La apariencia completa del selector requiere el pack UI privado que ya esta en la PC del usuario
(fuera de GitHub). Instalarlo con el procedimiento previo, conservando GUID/escenas originales.
No copiar ninguna escena ni configuracion de Unity encima del proyecto.

Prueba offline opcional con Python:

\`\`\`powershell
python ".\tests\check_wavevibe_training.py"
\`\`\`

## Unity y APK independiente

1. Unity Hub: agregar \`C:\Users\Capo\Documents\BIOFIT-SPIKE-LAB-LEGACY\`, abrir con Unity 6000.3.28f1.
2. \`BIOFIT > GeomeTry ORIGINAL > 1. Verificar fuente y mapas\`.
3. \`BIOFIT > GeomeTry ORIGINAL > 4. Activar URP original para probar en Play\`.
4. \`BIOFIT > GeomeTry UX > 1. Configurar modulo tactil Android\`; luego \`2. Verificar UX y recursos\`.
5. Abrir Main Menu original; en Play seleccionar "Wave Vibe" para observar el recorrido.
6. Detener Play, activar Android en File > Build Profiles y seleccionar
   **\`BIOFIT > GeomeTry ORIGINAL > 7. Compilar BIOFIT SPIKE LAB (BLE LEGACY)\`**.
7. Instalar el resultado \`Builds/BIOFIT_SPIKE_LAB_LEGACY_*.apk\`.

Package ID de laboratorio: \`com.biofitgame.wavevibelab\`. Nombre: \`BIOFIT SPIKE LAB\`.
Este builder no llama al builder original que usa \`com.biofitgame.geometryoriginal\`:
**no reemplaza la APK LEGACY estable** ni sus archivos privados.

## Latencia: separar las etapas antes de cambiar algo

Camino actual segun codigo verificado:

1. QMI8658 / tarea IMU: ODR nominal 224.2 Hz; detector UP con filtro IIR y confirmacion.
2. ESP32: EVENT con marca temporal local, cola FreeRTOS y notificacion BLE por tobillo.
3. Android: callback nativo, \`ConcurrentQueue\`, lectura en \`BiofitBLEMonitor.Update()\`.
4. Alineacion temporal de eventos mediante STATUS ya observados y emparejamiento BLE
   de dos candidatos UP (ventana **maxima** 180 ms, no demora fija de 180 ms).
5. \`BilateralJumpCandidate\` invoca el salto original del cubo en el hilo principal;
   despues intervienen fisica, render y pantalla.

El cooldown LEGACY de 900 ms evita aceptar ciertos pares demasiado seguidos;
**no suma 900 ms de demora** a un par elegible que ya fue aceptado.
STATUS a 10 Hz ancla relojes, pero el evento de salto viaja por EVENT;
no es obligatorio esperar el siguiente STATUS en cada salto una vez alineado.

### Diagnosticos ya existentes (sin cambiar firmware ni RAW)

- CSV \`EV\`: \`espera_cola_unity_ms\`, \`demora_relativa_estimacion_ms\`;
  esta ultima **no** es latencia absoluta BLE ni gesto-pantalla.
- \`DIAG\` \`BILATERAL_DECISION\`: Accepted / Paired, pair gap, cooldown, motivos rechazo.
- \`TX\` / TRANSPORT: \`qMax\`, \`drop\`, \`edadMax\`, \`notifyMax\`, \`cicloMax\`,
  \`rxHz\`, \`rxGapMax\`, \`statusGap\`. No confundir maximos desde arranque con una
  medida puntual del salto actual.
- Script ya existente: \`python tools/analyze_latency.py registro.txt\`
  para p50/p95 de cola y demora relativa, cuando el archivo tiene el encabezado esperado.

### Prioridades de optimizacion despues de la primera captura

A. **Verificar tasa real de cuadros Android**. 60 FPS sostenidos implican fotogramas
   de ~16.7 ms; 30 FPS, ~33.3 ms. En Unity movil \`Application.targetFrameRate\`
   controla el objetivo; cambiarlo por separado para comparar sin tocar fisica.
B. **Distinguir cola/transporte** con datos \`TX\` y \`EV\`; si los eventos BLE llegan en
   lotes, optimizar los parametros GATT/connection priority en Android y ESP32
   (si la biblioteca permite hacerlo). \`requestConnectionPriority(HIGH)\` es una
   solicitud, no una garantia; validar compatibilidad/autonomia.
C. **Revisar los filtros UP** solo si las marcas del evento demuestran deteccion
   tardia, y sin cambiar umbrales a ciegas: preservar reconocimiento bilateral.
D. No habilitar RAW ni el joystick durante la primera medicion; usar una sesion
   consolidada por dispositivo/condicion. Video externo a alta velocidad con pie
   y pantalla en el mismo plano si se necesita latencia fisica gesto-pixel.

**No se implemento ningun ajuste de latencia en esta rama.** La finalidad del nuevo
mapa es producir una referencia estable y aislar cambios posteriores.

## Verificacion pendiente y limites

El mapa JSON y su instalador se prepararon mediante inspeccion del codigo y pruebas
de estructura offline. Aun faltan importar esta rama en Unity Editor real, verificar
PlayMode, compilar APK Android y realizar ensayos fisicos. No atribuir 0.2-0.3 s a
una etapa concreta hasta tener mediciones.
