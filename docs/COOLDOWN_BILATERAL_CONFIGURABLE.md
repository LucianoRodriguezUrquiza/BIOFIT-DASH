# BIOFIT DASH — Cooldown bilateral configurable (rama experimental)

**Fecha:** 2026-11-23. **Rama:** `feat/configurable-bilateral-cooldown-400-900ms`. **Base estable:** PR #28, integración `017eb093a670703591479228307817db51c8197b`.

## Alcance y estado

Se implementó un selector táctil en **Configuración → COOLDOWN** con seis valores exactos: **900, 800, 700, 600, 500, 400 ms**. El valor predeterminado es **900 ms**; solo 900 ms cuenta con validación física comunicada por el usuario **antes** de esta modificación. Los otros cinco son experimentales. El nuevo código requiere verificación en Unity Editor y prueba Android con ambos trackers antes de cualquier integración a `main`.

No se cambiaron firmware ESP32, UUID, conectividad BLE, estimación de relojes, umbral UP **0,12 g**, ventana bilateral **180 ms**, refractario de tobillo **320 ms**, física Cube, modos Ball/Spider, geometría de niveles ni música. `PhaseFastPairs` y `LiftCycleMode` permanecen desactivados en el puente del videojuego.

## Auditoría de la implementación original

- `Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs` contenía `PairCooldownSeconds = 0.90f` como constante empleada por `ProcessImpulse`.
- Un par se forma si el desfase entre los dos impulsos es `<= MaxPairGapWindowSeconds`. Se toma `pairTime = Max(eventSeconds, otherTime)`, después del alineamiento en `BiofitClockAligner` (o tiempo de callback cuando corresponde).
- Originalmente se rechazaba un par cuando `pairTime - lastPairSeconds < 0.90f`. `lastPairSeconds` se modifica solo al **aceptar** un par. Un par rechazado no reinicia el cooldown.
- `BiofitBLEMonitor.ReadEvent` procesa y registra `BILATERAL_DECISION`; el evento `BilateralJumpCandidate` llega a `BiofitGeomeTryLegacyBridge`, que utiliza `cube.onClick()` para Cube o `BiofitModeMotor.TryPulse()` para Ball/Spider.
- `BiofitGeometryUX` administra Configuración con botones construidos programáticamente y la animación `BiofitSettingsButtonFeedback`. Ya existía `PlayerPrefs` para la selección de skin.

## Implementación elegida

1. El detector conserva `PairCooldownSeconds = 0.90f` como constante histórica/default y agrega la propiedad real `PairCooldownMilliseconds`, con setter validado a los seis valores. La decisión temporal en modo LEGACY utiliza esa propiedad dividida por 1000. El reloj y el emparejamiento no se cambian.
2. Un vencimiento interno `nextPairAllowedSeconds` conserva el bloqueo **ya iniciado** ante una reducción del valor. Si se aumenta el valor, se extiende el bloqueo en curso cuando corresponde. El valor nuevo gobierna el siguiente par aceptado. Una comparación con tolerancia de **1 nanosegundo** evita falsos rechazos por redondeo de `double` exactamente en un límite milisegundo.
3. `BiofitCooldownConfiguration` conecta el detector real con un almacén de enteros, inyectado por delegados. En Android se utiliza `PlayerPrefs`: clave `BIOFIT_BILATERAL_COOLDOWN_MS_V1`. Ante primer inicio o valor inválido se usa 900 ms. El mismo adaptador se prueba con almacenamiento en memoria sin cargar Unity.
4. `BiofitBLEMonitor` es la única puerta de entrada pública a la selección y expone el valor **leído del detector**; el selector no mantiene un segundo temporizador. La entrada no desconecta ni reinicia BLE.
5. En Configuración se agrega botón `COOLDOWN` en el espacio libre de la fila inferior y una subpágina sin escena nueva. Hay seis botones, resaltado del activo, indicador `COOLDOWN ACTUAL`, `RESTABLECER A 900 ms` y `VOLVER` a Configuración.
6. Un texto `CD: NNN ms` aparece en el borde superior derecho de los niveles y consulta el monitor. Si el valor de `PlayerPrefs` no coincide con el detector, se señala `!`; la subpágina también muestra un aviso.
7. Los CSV conservan sus **15 columnas y el campo de CONFIG original** (compatible con los replays). Se agregan filas de diagnóstico `COOLDOWN_MS=NNN` al comenzar una captura, al cambiar el valor y en `BILATERAL_DECISION`. El resumen final agrega `COOLDOWN_MS_FINAL`.

### Advertencia biomecánica

El cooldown es una condición temporal entre **pares de eventos UP aceptados**, no latencia BLE ni confirmación fisiológica de un salto. Con 400 ms, dos pares UP biomecánicamente generados por **un único salto** y separados más de 400 ms pueden resultar aceptados. El algoritmo no incluye evidencia biomecánica suficiente para garantizar lo contrario, y aquí no se alteran otros filtros. Por eso 400 ms requiere ensayo físico, registro de duplicados y contraste con 900 ms.

## Pruebas de regresión

Suite host: `dotnet run --project tests/CooldownRegression`. Cubre seis valores, cinco casos de frontera por valor (-100 ms, -1 ms, 0, +1 ms, +100 ms), rechazo/aceptación, cambios durante bloqueo activo en ambas direcciones, mantenimiento del bloqueo tras cambiar de pantalla, rebotes, entradas débiles, sensores inválidos, pares incompletos, ventana de 180 ms, almacenamiento y relanzamiento simulado, recuperación de preferencias inválidas, botón de restablecer, y presencia de la cadena selector → monitor → detector → puente legacy. Se incorpora a `.github/workflows/latency-regression.yml`.

Para cobertura complementaria siguen presentes las suites previas de `JumpControl`, `LatencyRegression`, `ReceiverRegression`, `DashUiRegression`, las verificaciones de geometría y la prueba de sintaxis C# con Roslyn. **La ejecución de CI no equivale a ejecutar Unity ni a validar los sensores reales.** **Resultado comprobado:** [GitHub Actions #45276413840](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45276413840): 5/5 tareas aprobadas, incluyendo **251 verificaciones** de `tests/CooldownRegression`. El ensayo de 400 ms utiliza 350 ms como evento claramente anterior al límite, porque 300 ms caería dentro del refractario independiente por tobillo de 320 ms.

## Instalación experimental sin tocar la copia validada

1. Mantener intacta `C:\Users\Capo\Documents\BIOFIT-DASH-NUEVOS-NIVELES`. Crear una carpeta **nueva** para experimentos.
2. En PowerShell, desde `C:\Users\Capo\Documents`, ejecutar:

   ```powershell
   git clone --recurse-submodules --branch feat/configurable-bilateral-cooldown-400-900ms https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-DASH-COOLDOWN-EXPERIMENTAL
   cd BIOFIT-DASH-COOLDOWN-EXPERIMENTAL
   ```

3. Solo en **ese clon nuevo**, preparar los assets originales si faltan siguiendo `tools/prepare_geometry_original.ps1`. Los cuatro PNG de `Assets/BIOFIT/Resources/BiofitUi/` no están en GitHub: copiar únicamente los archivos propios existentes desde la instalación validada, sin borrar ni cambiar el origen.
4. Abrir la nueva carpeta con **Unity 6.3 LTS 6000.3.28f1**. Revisar Console. Ejecutar `BIOFIT → GeomeTry ORIGINAL → 8. Verificar niveles 3 MODOS (60 s)` y el menú Configuración → Cooldown en el Editor.
5. Cambiar plataforma a Android en `File → Build Profiles` y ejecutar `BIOFIT → GeomeTry ORIGINAL → 9. Compilar BIOFIT 3 MODOS (BLE LEGACY)`. La APK queda bajo `Builds/`. Antes de instalar, conservar una copia de la APK estable.
6. Probar físicamente el selector con ambos trackers y cada valor entre partidas. Verificar UI, HUD, emparejamientos aceptados, duplicados, falsos positivos y registro CSV `COOLDOWN_MS`. Cerrar y reabrir para confirmar persistencia. Al probar 900 ms debe comportarse como la versión base.

**Estado de entrega:** PR [#29 (borrador)](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/29), rama experimental; CI completo verificado en verde (run #45276413840), pero Unity Editor, compilación APK y validación física siguen pendientes hasta prueba explícita.

## Política de cierre

No fusionar a `main` sin confirmación expresa del usuario y sin conservar evidencia de pruebas. Actualizar `docs/CONTEXTO_VIVO.md` en esta rama con resultados efectivos y número de PR. No tratar este módulo experimental como parte del baseline estable antes del merge.
