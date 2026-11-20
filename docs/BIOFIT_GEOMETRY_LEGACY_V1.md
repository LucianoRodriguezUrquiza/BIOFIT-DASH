# BIOFIT GEOMETRY LEGACY — primera integración aislada

## Decisión funcional

El usuario probó BILATERAL LEGACY (UP_IMPULSE) y CICLO EXP (LIFT_ONSET).
Según su ensayo, **LEGACY fue más sensible y se elige para jugar**.
La comparación no incluyó un análisis nuevo de CSV ni vídeo. No atribuir
precisión biomecánica ni latencia gesto-pantalla medida.

## Código y alcance

- Base Unity: `feat/reliable-consecutive-jumps-v0911` (`5616fc4571193d4a2d076550a909089344144384`), PR #17 Draft.
- Nueva rama: `feat/biofit-geometry-legacy-v1`.
- GeomeTry original: `Christ0u/GeomeTry`, `d922caddb4d21ecb791a266143e279bdb969d42c`.
- Se adaptaron solo las mecánicas de cubo de `Character.cs` y `Cube.cs`.
  **No es una importación del prefab original** ni del nivel de GeomeTry.
- Se añadió `BiofitGeomeTryCube` con Rigidbody2D (velocidad X=6.5,
  impulso Y=13.5, gravityScale=4, rotación visual 280°/s).
- El nivel se genera al ejecutar, sin materiales, canciones ni prefabs externos:
  suelo, cubo, 10 obstáculos y fin. Obstáculos espaciados 8.4 unidades,
  equivalentes a ~1.29 s a velocidad 6.5 (supuesto nominal, sin pausas).
- Se fija `Time.timeScale=1` para facilitar el control físico inicial.
  GeomeTry utilizaba 1.5. Duración de vuelo ideal de ~0.69 s con
  Physics2D.gravity=-9.81. NO importar predicción/colisión de BiofitDashCore.
- Licencia y créditos originales en `docs/THIRD_PARTY_GEOMETRY_LICENSE.md`.

## Recepción y ejecución (sin alterar el firmware)

`BiofitBLEMonitor` conserva el evento `BilateralJumpCandidate` existente;
añade `LegacyBilateralCandidateTimed(float gapMs, double pairSeconds)`,
solo emitido para el par legacy `UP_IMPULSE`. `pairSeconds` es la estimación
alineada al reloj monotónico, no el tiempo de inicio del gesto humano.

`BiofitGeomeTryCommandGate` consume cada ID una sola vez. Para BLE,
una orden de edad mayor que 180 ms se descarta **sin renovar su edad**.
No hay buffer ni entrada mantenida; si el cubo está en el aire, la orden
se rechaza y no se ejecuta después al aterrizar. Esto evita saltos tardíos
por acumulación, aunque puede producir omisiones si BLE entrega ráfagas
antiguas. Se registra separadamente `JUMP_REJECTED`/ `JUMP_EXECUTED`.
No se pretende demostrar que cada candidato sea un salto humano distinto.

El botón `SALTAR` y `ESPACIO` utilizan la misma ruta de comando discreto.
`BLE LEGACY` comienza habilitado y solo actúa con 2/2 trackers conectados.
Se fuerza `LiftCycleMode=false` y `PhaseFastPairs=false`.
Umbral 0.12 g, ventana de par 180 ms, refractario por tobillo 320 ms
y cooldown de par 900 ms quedan en `BiofitBilateralJumpDetector`.

## Preparación de escena en Unity 6.3

1. Desde GitHub Desktop, confirmar cuál es la rama activa y **revisar
   los 8–9 archivos locales no publicados** antes de cambiar de rama.
   No descartar ni sobrescribir modificaciones.
2. Traer la nueva rama de integración sin hacer merge con `main`.
3. Abrir Unity Hub / Unity **6000.3.28f1**. Comprobar Console.
4. Menú **BIOFIT → Crear o abrir GEOMETRY (BLE LEGACY)**.
   Crea la escena **solo si no existe** en
   `Assets/BIOFIT/Scenes/BiofitGeometryLegacy.unity`; si ya existe
   la abre sin sobrescribirla. Agrega esa escena a Build Settings sin
   retirar ni reordenar escenas anteriores.
5. En Play: **ESPACIO** / **ARRIBA** salta; **R** reinicia; **P** pausa.
   En la interfaz: **SALTAR**, **BLE LEGACY**, **PAUSA**, **REINICIAR**,
   **DIAG BLE**.
6. Verificar manualmente nivel, pausa, muerte/reaparición, victoria,
   y que una pulsación mantenida no genera saltos al aterrizar.
7. Comprobar permisos Android, Build Profiles, scene index, identidad
   del paquete y firma antes de generar APK. No desinstalar la app.
8. Si se ensaya con sensores, usar una sesión breve y conservar
   exportación RAW/CSV y calibraciones existentes.

## Preservación y límites

- No se modifican firmware, NVS, UUID, umbrales, detector, RAW,
  MediaStore, SAF, firmas ni archivos Android existentes.
- Nuevo nivel tiene un `BiofitBLEMonitor` en un solo GameObject.
  No abrir otra escena con monitor en modo aditivo; cambiar de escena
  requerirá una nueva conexión BLE. Reutilización persistente entre
  escenas queda pendiente, sin duplicar un servicio activo.
- El panel de diagnósticos del monitor se muestra dentro de la misma escena.
- No se implementaron coyote time, buffer al aterrizaje, orbes, pads,
  nave, onda, portales, marcha, lateralidad, UI final ni APK.
- **Verificación disponible:** revisión estática remota y tests host de
  antigüedad/ID de comandos en CI (a comprobar tras ejecutar Actions).
  No hay compilación Unity, Play Mode ni validación en Android o física
  ejecutada por el asistente.
- Si falla importación, conservar la nueva rama y registrar los errores
  exactos de Console antes de modificar escenas o código.
