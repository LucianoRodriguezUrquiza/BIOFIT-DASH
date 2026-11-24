# JOYSTICK - Modo Juego

> **BIOFIT DASH / BIOFIT GAME — registro de validación física del joystick Bluetooth**  
> **Fecha de registro:** 2026-11-24 (Argentina).  
> **Estado:** comportamiento observado en **Android real** mediante la pantalla de diagnóstico de la APK experimental de la rama `feat/vr-joystick-input-diagnostics` ([PR #30](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/30)).  
> **Este documento sí está en `main`; el código de diagnóstico del PR #30 todavía no se considera parte de `main` hasta su eventual integración.**

## 1. Dispositivo y contexto

- **Dispositivo comercial:** control Bluetooth 3.0 para visor VR, comercializado como DUAITEK/TodoMicro `JOYSTICK-VR` (mando blanco con stick 360°, botones A/B/C/D, dos botones laterales R1/R2, encendido y botón de cambio de modo).
- **Identificación detectada por Unity en Android:** `VR BOX`, confirmada en las **seis capturas aportadas por el usuario**.
- **Aplicación usada en la prueba:** BIOFIT DASH para Android, pantalla `Configuración → JOYSTICK → DIAGNÓSTICO DE JOYSTICK`, implementada experimentalmente en PR #30.
- **Sistema de entrada del proyecto:** `Legacy Input Manager` de Unity (`activeInputHandler: 0`). Las líneas del diagnóstico muestran `KeyCode` recibidos; no representan directamente códigos de eventos de Android (`KEYCODE_...`) ni números de controles físicos garantizados en todos los teléfonos.
- **Control corporal independiente:** dos Waveshare ESP32-S3 con QMI8658 por BLE BILATERAL LEGACY; Cube/Ball/Spider y cooldown seleccionable 900/800/700/600/500/400 ms. **No se modifica ninguno de estos subsistemas al registrar el joystick.**

## 2. Resultado físico decisivo: MODO JUEGO = CAMBIO + C

**Confirmación expresa del usuario:** para **este mando particular, con este teléfono y esta APK**, el perfil de funcionamiento que permite registrar correctamente entradas tipo gamepad es:

**Mantener presionado «MODO CAMBIO» (botón con flecha circular) + pulsar C.**

**Esta combinación es la referencia operativa para BIOFIT DASH a partir de ahora.** No reemplazarla por `CAMBIO + B` por similitud con manuales de otros mandos: las pruebas reales del usuario tienen prioridad sobre las hipótesis basadas en hardware semejante.

Observaciones reportadas:
- **CAMBIO + C:** Unity identifica `VR BOX` y registra las pulsaciones descritas abajo como botones de joystick. El usuario lo identifica como modo juego adecuado. **El stick direccional no funciona de manera utilizable en este modo**, según su ensayo.
- **CAMBIO + D (Mouse):** el stick/puntero funciona como mouse; sin embargo C/D continúan subiendo/bajando volumen y otros controles pueden activar Atrás o incluso sacar al usuario del juego. **No usarlo como perfil principal para BIOFIT DASH por ahora.**
- **CAMBIO + A y CAMBIO + B:** en pruebas previas persistieron comportamientos de control del volumen, navegación Atrás y/o comportamiento errático del puntero. **No tratarlos como modo Gaming confirmado** para esta unidad.
- **No se constató que pueda cambiarse el modo HID desde Unity:** el cambio se realiza con los botones físicos del mando.

**Advertencia sobre nomenclatura:** «C» es el botón físico rotulado C del mando; **no** equivale a `KeyCode.C` ni al código de joystick 13 por definición. Los códigos de abajo son resultados observados, no instrucciones del fabricante.

## 3. Matriz exacta de entradas detectadas

El usuario entregó seis capturas individuales, explícitamente en orden **A, B, C, D, R1, R2**. La siguiente tabla reproduce el contenido del diagnóstico de Unity, eliminando únicamente la redundancia genérico/joystick 1 para facilitar la lectura:

| Botón físico pulsado | `KeyCode` de joystick genérico | Equivalente de `Joystick 1` mostrado | Entradas numeradas visibles | Patrón lógico |
|---|---|---|---:|---|
| **A** | `JoystickButton0`, `JoystickButton13` | `Joystick1Button0`, `Joystick1Button13` | 2 | 0 + 13 |
| **B** | `JoystickButton0` | `Joystick1Button0` | 2 | 0 |
| **C** | `JoystickButton13` | `Joystick1Button13` | 1 | 13 |
| **D** | `JoystickButton1` | `Joystick1Button1` | 1 | 1 |
| **R1** | `JoystickButton0`, `JoystickButton13` | `Joystick1Button0`, `Joystick1Button13` | 2 | 0 + 13 |
| **R2** | `JoystickButton0` | `Joystick1Button0` | 2 | 0 |

**Qué significa «Entradas numeradas visibles»:** la captura de la pantalla mostraba dos renglones numerados (`1:` y `2:`) en A, B, R1 y R2, y solo uno (`1:`) en C y D. La captura **no permite determinar** si cada renglón corresponde a una pulsación física separada, rebote del dispositivo, doble reporte de Android, o a cómo se capturó la prueba. Los códigos repetidos dentro de ambos renglones son idénticos para cada botón. **No asumir automáticamente dos saltos por pulsación.**

### Relación de equivalencia

- **A ≡ R1**, ambos envían **0 + 13** en las capturas.
- **B ≡ R2**, ambos envían **0**.
- **C envía 13**.
- **D envía 1**.
- Por tanto, para los seis controles físicos se observaron **cuatro patrones lógicos diferenciables**: `{0,13}`, `{0}`, `{13}` y `{1}`.
- Un `JoystickButton0` genérico y un `Joystick1Button0` específico **no se deben sumar como dos acciones diferentes**; son dos maneras de consultar una misma entrada del mando número 1. Igual para el código 1 o 13.

### Lecturas del stick en las capturas

En las seis capturas la línea inferior indicaba:
`STICK FÍSICO X: 0.00 Y: 0.00 | TECLADO H: 0.00 V: 0.00`.

Estas cifras son **fotografías instantáneas**, no grabaciones continuas de un desplazamiento. El usuario **sí declara haber probado el stick** y que **no funciona en el modo CAMBIO + C**. No extrapolar que el hardware carece de stick, que el mando no emite movimiento en absoluto o que ningún otro eje/hat/D-pad pueda detectarlo.

## 4. Consecuencias técnicas y diseño de los próximos controles

1. **Usar el modo físico `CAMBIO + C` como punto de partida de la implementación.** Es el único perfil realmente validado en la APK por los seis resultados de captura.
2. **No utilizar el modo Mouse como predeterminado.** Aunque permite mover el cursor, tiene efectos del sistema Android indeseables (volumen, Atrás, abandonar aplicación).
3. **Evitar remapeos ingenuos basados en un botón individual.** Si se asigna, por ejemplo, `JoystickButton0 → Volver` y `JoystickButton13 → Saltar`, pulsar **A/R1** (0+13) podría disparar **ambas acciones**. Se necesita resolver prioridades y leer **combinaciones detectadas en el mismo frame**, no solo `GetKeyDown` de cada código por separado.
4. **Recomendación provisional de cuatro acciones lógicas:** 
   - patrón **0+13** (A/R1): **Acción del personaje** (saltar/invertir gravedad/teletransportarse);
   - patrón **0** (B/R2): **Volver**, con protección contra pulsaciones prolongadas/salida inadvertida;
   - patrón **13** (C): **Pausa / continuar**;
   - patrón **1** (D): **Reiniciar nivel**.
   **Esto es una propuesta, no un mapeo implementado ni probado.** Debe ajustarse a la navegación real y a las acciones disponibles en cada escena.
5. **No prometer reasignación independiente entre A y R1, ni entre B y R2** mientras reporten exactamente el mismo patrón. La UI futura puede agruparlos por equivalencia.
6. **Distinguir el conteo de eventos de las acciones de gameplay.** Necesitamos probar un toque, dos toques separados, pulsación larga, liberación del botón y combinaciones consecutivas para eliminar el riesgo de doble acción.
7. **Investigar stick en otro pase sin romper lo validado:** Android puede entregar ejes `AXIS_X/Y`, hat `AXIS_HAT_X/Y`, D-pad, teclado o desplazamiento de mouse según perfil. Los ejes actuales `BIOFIT VR Stick X/Y` consultan solamente un par de ejes del Legacy Input Manager. No cambiar el backend de entrada ni forzar interceptación global de las teclas de volumen sin necesidad.
8. **Compatibilidad de control:** conservar entrada táctil, joystick y dos ESP32 simultáneamente. No cambiar Cube.onClick, BiofitModeMotor.TryPulse, firmware, UUID BLE, detección bilateral ni sus seis cooldowns.

## 5. Protocolo de próxima validación

**Validado físicamente:** reconocimiento como `VR BOX`, combinación de perfil `CAMBIO + C` según usuario, códigos del conjunto A/B/C/D/R1/R2 y comportamiento indeseable de los otros modos descrito por el usuario.

**Pendiente antes de integrar un sistema definitivo de remapeo:**

1. Presionar cada botón una sola vez y observar cuántos comandos de juego resultan (no solo cuántos renglones aparecen en la UI de diagnóstico).
2. Verificar si A/R1 emiten 0 y 13 **en el mismo frame** y si `GetKeyDown` puede ordenarse por patrón completo antes de ejecutar acciones, evitando que un `0` solo se interprete prematuramente.
3. Probar botones al mantener y soltar, para detectar repeticiones, autorepeat y pulsaciones dobles.
4. Verificar qué hace B/R2 con la pantalla Configuración abierta, el menú principal, el nivel en pausa y la pantalla de Android, para evitar cierres no intencionales.
5. Investigar el stick mientras se **mantiene físicamente desplazado**, con registro de todos los ejes y eventos Android; si no es aprovechable en `CAMBIO+C`, implementar navegación por selección enfocada usando otros controles, o conservar táctil para menús.
6. Realizar un ensayo combinado joystick + BLE bilateral (dos trackers) con cooldown 400 ms como elección actual del usuario, sin perder las otras cinco opciones.
7. Probar APK real tras cada cambio y separar resultados **CI**, **Unity Editor**, **Android** y **validación física**.

## 6. Referencias y alcance del registro

- **Evidencia primaria:** seis capturas aportadas por el usuario (`A.jpg`, `B.jpg`, `C.jpg`, `D.jpg`, `R1.jpg`, `R2.jpg`) de la pantalla de diagnóstico Android. Son archivos de esta conversación, **no** una característica nueva del código GitHub. Se preservan aquí las lecturas textuales observables y su correspondencia por orden.
- **Código de diagnóstico que generó las capturas:** [PR #30 — `feat/vr-joystick-input-diagnostics`](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/30), en particular `Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs` y las opciones de ejes de `ProjectSettings/InputManager.asset`.
- **Versión principal de juego al momento del registro:** `main` con [PR #29](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/pull/29) integrado (cooldown configurable y probado físicamente a 400 ms).
- **Prioridad de evidencia:** las observaciones del mando real **prevalecen** sobre manuales de modelos genéricos similares que sugerían `MODO + B` para modo juego. No extrapolar los resultados de `VR BOX` a todos los joysticks Bluetooth.
- **Archivos de documentación de continuidad:** `docs/CONTEXTO_VIVO.md`, `docs/CONTEXTO_ACTUAL.md`. En futuros cambios de mando o UI, actualizar este registro sin borrar el mapeo comprobado el 24/11/2026.

**Resumen operativo:** `MODO CAMBIO + C` → `VR BOX` → A/R1 = 0+13, B/R2 = 0, C = 13, D = 1; stick no utilizable según el usuario en este modo; no usar Mouse como modo primario por volumen/Atrás. **Las asignaciones de acciones al videojuego siguen pendientes de implementación y prueba física.**
