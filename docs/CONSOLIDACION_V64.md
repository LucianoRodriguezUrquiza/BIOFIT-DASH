# V6.4 — aprobación de trabajo y consolidación de la rama principal (26/11/2026)

El usuario comunicó que probó V6.4 y funciona bastante bien. Autorizó explícitamente consolidar todos los cambios de niveles-mejorados en main y conservar una sola rama, después de corregir las pruebas CI. Esto reemplaza la instrucción anterior de no fusionar main para esta tarea concreta. No supone autorización para cambios ajenos a la solicitud ni validación exhaustiva en APK o hardware.

## Referencias y alcance
- Base histórica V6.3: `597631a69eed15a064c96baa14759a06704d22ce`. Estado de trabajo previo a la corrección CI: `08d2194807aadd82a97adf4c1ecbbf061efd661d`. Mantener el historial Git completo, sin force push, squash destructivo ni eliminación de commits.
- Versión aprobada de trabajo: V6.4 — Campaña Clásica / Level Editor 1.0. Rama canónica tras la integración: main. niveles-mejorados debe retirarse únicamente después de verificar que su HEAD completo está incorporado en main. No crear una nueva copia del proyecto.
- Editor exclusivo de Unity, borradores, catálogo local, selección/movimiento/rotación, colisiones por vehículo, FLM, exportación y publicación de deltas. Corrección visual inicial del Cube incluida; su prueba específica no está documentada. Los cinco niveles y las funcionalidades previas permanecen.
- ICC, DIPL, RFD, BRO, FLM y RCA vigentes; preservar firmware QMI8658/ESP32, BLE BILATERAL LEGACY, detector, umbrales, ventanas, cooldowns 400–900 ms y físicas. No se modifican .gitignore, submódulo original, velocidades, motor, escenas ni geometría para satisfacer CI.
- Proyecto local único: `C:\Users\Capo\Documents\BIOFIT-DASH-STEREO-ORIGINAL`, Unity 6000.3.28f1. Los mapas Active/Original y recursos originales de terceros permanecen locales. La rama remota contiene deltas reproducibles, no todos los mapas completos. No borrar recursos locales.

## Estado geométrico autorizado
| Ranura | Nivel | Registros V6.3 | Registros objetivo V6.4 | Estado |
|---|---|---:|---:|---|
| 1 | Stereo Madness | 3015 | 2991 | Dos deltas publicados; usuario corrigió gap del Spider 86–88% |
| 2 | Back on Track | 1734 | 1920 | Delta publicado por el usuario |
| 3 | Polargeist | 2111 | 2111 | Geometría V5 |
| 4 | Can't Let Go | 1871 | 1871 | Geometría V5 |
| 5 | Jumper | 3351 | 3351 | Geometría V5 |
| Total | Campaña | 12082 | 12244 | Objetivo reconstruible mediante deltas |

Fuente: [índice de deltas](../tools/level_editor/changes/INDEX.md). Aplicar los dos deltas de ranura 1 en orden: 001459 y 004628; ranura 2: 114359. Huellas objetivo: ranura 1 `bd2d104cfc88b79751e9cde421f097d6f8413da80847a7ee38c7583379cbfc43`; ranura 2 `39e8beeb17c3a549a6eb452587da86b0655b9e81b0e650ffddc0e9c4cd151651`. Los otros tres mapas conservan hashes del manifiesto V6.3. Los deltas se aplican con `tools/level_editor/level_delta.py --proyecto <ruta> --delta <archivo> --aplicar` exclusivamente con Unity cerrado y base SHA coincidente; si Active ya coincide con el objetivo, no aplicar de nuevo. No afirmar que Active local coincide sin verificar. El usuario informó que arregló el Spider aumentando el gap vertical; se conserva el código físico.

## Corrección de GitHub Actions
El fallo original de regression se producía en tests/check_geometry_ux.py línea 31: nombres genéricos NIVEL 1–5, antiguos modos y dimensión histórica del botón volver. Se comprobó mediante código actual y logs que la campaña anuncia STEREO MADNESS, BACK ON TRACK, POLARGEIST, CAN'T LET GO, JUMPER y modos Cube/Ball/Spider según ranura. Se mantienen pruebas de arte, layout, skins, navegación, FPS y BLE. La prueba posterior check_stereo_ship_vrbox.py fallaba por enum que omitía InvertedCube; también se actualizan contrato de títulos, total dinámico de monedas y reset por nivel local. Se conservan las verificaciones de mapas históricos Cube/Ship, joystick y física. GeometryUxSyntax incluye runtime y scripts V6.4 y activa UNITY_EDITOR al analizar sintaxis. Se añade verificación de bases publicadas y metadatos de deltas, conservación de recursos originales, reconstrucción y publicación selectiva en un repositorio Git temporal; permite publicar desde main y evita recrear una rama eliminada. Los 5 jobs originales permanecen sin cambios en workflow. No se alteran mecánicas ni código de juego para superar assertions.
- Ejecución inicial corregida (antes de adaptar el publicador): [45847125910](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45847125910), SHA 73c6745c9fb8f8b734b468c6d579b7c2f6e6b221: 5/5 success, todas las 20 órdenes de regression ejecutadas. Cualquier verificación posterior debe referirse al SHA exacto del HEAD y al run de Actions correspondiente, no extrapolar automáticamente este resultado.
- CI de host no equivale a compilación Unity, ejecución APK, prueba física BLE ni certificación completa de jugabilidad.
- La única adaptación funcional de esta consolidación es del flujo de publicación del editor, no del juego: `level_delta.py` acepta main y niveles-mejorados mientras exista y verifica que la rama remota exista y esté sincronizada; el diálogo informa el destino actual. Botón sigue llamándose Publicar delta experimental. Solo versiona el JSON y su índice, sin force, sin staging indiscriminado, sin publicar recursos originales. Tras retirar la rama experimental, usar main. Una publicación en main registra el delta explícitamente confirmado; no equivale a aprobación de todas las modificaciones ni a cambio de versión automática.
- Historial: docs/CONTEXTO_ACTUAL.md y CONTEXTO_VIVO.md contienen los estados anteriores como historia; esta decisión es la referencia vigente tras la fusión. No volver a ejecutar el instalador histórico V6.4 que exige main V6.3 y niveles-mejorados. Sincronizar por Git sobre el proyecto existente. 
- Continuidad: esperar instrucciones del usuario antes de editar Polargeist, Can't Let Go o Jumper. Documentar cada avance y validación (RCA). No proponer cambios de control corporal sin autorización.

