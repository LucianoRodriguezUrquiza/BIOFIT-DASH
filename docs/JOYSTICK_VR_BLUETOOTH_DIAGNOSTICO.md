# BIOFIT DASH — diagnóstico de joystick Bluetooth 3.0 VR (etapa 1)

**Fecha inicial:** 2026-11-23. **Actualización física:** 2026-11-24. **Estado:** rama `feat/vr-joystick-input-diagnostics`, todavía NO integrada a `main`; **sí se comprobó físicamente en APK Android** la detección del mando `VR BOX` y su matriz de botones. Remapeo definitivo pendiente. **Base:** `main` con PR #29 ya fusionado; cooldown 900/800/700/600/500/400 ms conservado.

## Corrección con evidencia primaria (2026-11-24)

**El usuario confirmó MODO JUEGO = MODO CAMBIO + C en su joystick real.** El manual genérico que sugería `MODO+B` no coincide con la prueba de este aparato; por tanto **no usar `+B` como instrucción de juego**. `MODO+D` funciona como mouse pero induce controles de volumen/Atrás y a veces salida de la aplicación, por lo que no se recomienda como perfil principal.

Con el mando identificado por Unity como `VR BOX` en APK Android, el usuario fotografió las entradas: **A y R1 = JoystickButton0 + JoystickButton13; B y R2 = JoystickButton0; C = JoystickButton13; D = JoystickButton1**. Los alias `Joystick1ButtonX` son el mismo código referido específicamente al joystick 1. En cuatro capturas el registro contenía dos renglones repetidos; esto no demuestra dobles acciones de juego. **El stick no respondió en MODO+C según el usuario;** los valores de ejes a cero en capturas no bastan para descartarlo en otros reportes/ejes.

**Registro canónico y análisis de colisiones (en `main`):** [JOYSTICK - Modo Juego.md](../../JOYSTICK%20-%20Modo%20Juego.md). Si se mapea 0 como Volver y 13 como Pausa sin distinguir la combinación simultánea, A/R1 podría disparar ambas acciones. Diseñar prioridades para los patrones `{0,13}`, `{0}`, `{13}`, `{1}`. Los códigos definitivos para gameplay **todavía no están implementados**, solo diagnosticados.

## Hardware informado por el usuario

Mando comercializado como DUAITEK/TodoMicro `JOYSTICK-VR`, Bluetooth clásico 3.0, Android, 2 pilas AAA. El usuario comunicó en una prueba fuera del juego:

- `A` y botón lateral `R1`: clic/acción.
- `B` (pulsación larga) y `R2`: Atrás.
- `C`: volumen arriba.
- `D`: volumen abajo.
- Tiene control de dirección 360°, botón de cambio de modo y botón de encendido.

**No asumir que A/B/C/D coinciden con los `KeyCode` de Unity.** Esas funciones sugieren que actualmente podría estar en modo mouse/remoto. Un mando HID puede reportarse como mouse, teclado o gamepad según el modo; algunos eventos de volumen los consume Android antes de llegar al juego.

## Fuentes externas consultadas

- Manual de dispositivo semejante VR-PARK Y1 (NO confirmado como firmware idéntico): https://device.report/m/742ee4d8a01bd4958f1edcb5121a8abefa52d006ab092d9d2823c2ec0fd4a89c — cambio de modo manteniendo `@` y pulsando `A`, `B`, `C` o `D`; `@+B` modo gamepad horizontal, `@+D` modo mouse. La correspondencia depende de cada modelo y teléfono.
- Android Developers, entrada de gamepads, eventos `KeyEvent`/`MotionEvent`: https://developer.android.com/games/sdk/game-controller/controller-input
- Documentación Unity sobre Android, Legacy Input Manager y Input System: https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/android/developing/input
- Android Open Source, `getevent`: https://source.android.com/docs/core/interaction/input/getevent
- Unity community, modo gamepad `@+B` y mouse `@+D`: https://discussions.unity.com/t/working-bluetooth-controller-for-making-games-in-vr-on-android/661905

## Hallazgo físico posterior — diagnosticar NO equivale a jugar

El usuario comprobó en la APK Android con **CAMBIO + C** que Unity detecta los botones del mando `VR BOX` en la pantalla JOYSTICK, **pero pulsarlos dentro de un nivel no provoca saltos del Cube**. Al seleccionar **CAMBIO + D** (modo mouse), A/R1 **sí producen el salto**, por emulación del clic ya soportado por el juego; el modo Mouse sigue siendo inapropiado como perfil principal debido a volumen/Atrás/salida.

**Esta diferencia es esperable con el código actual:** el submenú `BiofitGeometryUX.RefreshJoystickDiagnostics` únicamente lee `KeyCode` para mostrar el registro y no despacha comandos; en gameplay la implementación manual escucha `Space`/`Mouse0` (Ball/Spider) y Cube tiene su ruta original de clic. El puente `BiofitGeomeTryLegacyBridge` aplica por separado los eventos bilaterales de los ESP32, pero no recibe los botones del mando. **La conexión HID funciona; la traducción de sus `JoystickButton0/13/1` al gameplay todavía no existe.**

Próxima fase: módulo `VR BOX input → Cube/Ball/Spider` que procese antes el patrón 0+13 y evite activaciones simultáneas de los perfiles 0 y 13. Mantener controles táctiles, BLE, física y cooldown configurables sin cambios. Requiere pruebas físicas en APK tras implementar. Registro fuente: [JOYSTICK - Modo Juego.md](../../JOYSTICK%20-%20Modo%20Juego.md), ya actualizado en `main`.

## Auditoría del proyecto

El `ProjectSettings/ProjectSettings.asset` real indica `activeInputHandler: 0` (Legacy Input Manager), aunque `Packages/manifest.json` también incluye `com.unity.inputsystem`. La lógica de Cube usa `onClick` original; Ball/Spider ya tienen `Input.GetKeyDown(KeyCode.Space)` y `Input.GetMouseButtonDown(0)`. El puente `BiofitGeomeTryLegacyBridge` aplica los pares de tobillos al personaje. Cambiar el backend de input, sobreescribir clicks o simular eventos duplicados puede romper esta cadena. **No hacerlo en esta etapa.**

## Implementación de la etapa 1

Se añadió en `Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs` el botón **JOYSTICK** en `Configuración`, sin cambiar escenas ni los botones existentes. Abre **DIAGNÓSTICO DE JOYSTICK**, donde se muestran:

1. `Input.GetJoystickNames()` (vacío en algunos remotos de tipo teclado/mouse aunque estén conectados).
2. Últimas cinco pulsaciones `KeyCode` detectadas por `Input.anyKeyDown` + `Input.GetKeyDown` únicamente mientras está abierta la subpágina, con contador y registro `BIOFIT JOYSTICK DIAGNOSTIC` en el log.
3. Valores de dos ejes NUEVOS y exclusivos de observación, `BIOFIT VR Stick X` y `BIOFIT VR Stick Y` (`Joystick Axis` 1/2, tipo 2, primer par X/Y), añadidos a `ProjectSettings/InputManager.asset` **sin cambiar** `Horizontal`/`Vertical` existentes. Se muestran junto con los ejes de teclado `Horizontal` y `Vertical`. Esto mejora la cobertura de sticks HID analógicos, pero algunos mandos usan un D-pad, teclas o ejes con numeración diferente.
4. Botón **LIMPIAR EVENTOS** y botón **VOLVER** compatible con los submenús existentes.

No se agregaron bindings de gameplay ni se alteraron las físicas, cooldown, BLE, Android Activity o PlayerPrefs. **El diagnóstico no identifica por sí solo el código Android `KEYCODE_VOLUME_UP` si el sistema lo intercepta.**

## Protocolo de ensayo físico — teléfono Android

1. Emparejar el joystick desde Ajustes → Bluetooth (modo Android, LED azul cuando corresponda).
2. Desde el clon aislado de esta rama, preparar el submódulo original con `tools/prepare_geometry_original.ps1`, abrir Unity **6000.3.28f1** y compilar desde `BIOFIT → GeomeTry ORIGINAL → 9. Compilar BIOFIT 3 MODOS (BLE LEGACY)`. El Editor Windows por sí solo no prueba la recepción del mando en Android.
3. En la APK, ir a **Configuración → JOYSTICK**. Tocar **LIMPIAR EVENTOS** y dejar el puntero sin presionar botones de interfaz.
4. Mantener pulsado el botón `@` (cambio de modo) y presionar **`C` (MODO DE JUEGO COMPROBADO)**. Comprobar nombre del dispositivo y pulsar por separado A, B, C, D, R1, R2 y las cuatro direcciones. Anotar lo que se ve en el panel; una pulsación de `B` puede actuar como Atrás, y una de `C` puede cambiar el volumen sin aparecer.
5. Repetir con `@+D` (mouse), en caso de que el modo gamepad no genere eventos útiles. **No asumir que `Input.GetJoystickNames()` vacío significa no conectado.**
6. Para señales que no aparecen, conectar Android por USB con depuración habilitada y ejecutar `adb devices`, `adb shell getevent -lp`, `adb shell getevent -lt`. Identificar el nodo del mando entre los listados y observar `EV_KEY`/`EV_ABS`/`EV_REL`. Algunos Android restringen `getevent` sin root y podrían responder Permission denied. No instalar apps de accesibilidad ni modificar el volumen del sistema como workaround por defecto.
7. Con los códigos de botones ya obtenidos, definir perfil explícito de mando y **binding por acción**: `CONFIRMAR/ACCION`, `VOLVER`, `PAUSA`, `REINICIAR`, `NAVEGACION`. Persistencia de asignaciones en `PlayerPrefs`, detección de conflictos y teclado/pantalla/BLE funcionando simultáneamente. No se implementa aún porque falta validar el comportamiento temporal de combinaciones/duplicados y la semántica definitiva de cada acción, aunque ya se conocen los `KeyCode` de los seis botones.

## Riesgos y límites

- Un clic `Mouse0` puede provenir del dedo o de A/R1; no hay identificación de hardware solo mediante `Input.GetMouseButtonDown`.
- `KEYCODE_BACK` puede convertirse en `KeyCode.Escape` o gestionarse fuera de Unity; probarlo.
- Los botones de volumen suelen ser controles del sistema; no prometer reasignación sin interceptación a nivel nativo, que no es recomendable en esta primera integración.
- El `stick` puede enviar teclado, mouse relativo, D-pad o ejes analógicos. Los dos nuevos ejes miden el primer par estándar del joystick; no garantizan detectar ejes de numeración distinta. `Horizontal/Vertical` existentes permanecen intactos.
- Si Android ya emula un clic `Mouse0` para A, añadir otra ruta de salto con el mismo botón causaría dobles acciones. Los bindings se aplicarán con prioridad y deduplicación.
- **La APK ya registró capturas útiles, pero no fusionar la lógica de joystick a `main` hasta probar las futuras asignaciones de juego y evitar regresiones.** No se cambió el carácter físicamente validado de `main` ni el cooldown de 400 ms preferido por el usuario.
