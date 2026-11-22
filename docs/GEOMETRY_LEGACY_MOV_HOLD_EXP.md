# BIOFIT DASH GeomeTry — MOV HOLD v1 EXP (22/11/2026)

**Rama experimental:** feat/geometry-legacy-held-motion-v1  
**Base estable:** main @ 750c80ec3727e3dbaab0ea3135f5138c02f909f5.

## Objetivo

Movimiento bilateral continuo = botón de salto mantenido. Reposo = botón liberado. Con el botón mantenido, cada aterrizaje provoca otro salto por medio del código original Cube.onClick() -> Cube.Jump() -> Rigidbody2D.

NO se modifican firmware, umbrales, NVS, UUIDs, detector UP_IMPULSE, niveles, físicas, velocidad, GameManager ni menú original. Se conserva UI, 70 skins y objetivo Android de 60 FPS. NO se activa FASE/CICLO EXP.

## Fuente real del movimiento

Las Waveshare transmiten EVENT MOV_ON (1), MOV_OFF (2) y STATUS V2 (20 bytes) nominalmente cada 100 ms, cuyo bit0 (0x01) indica movimiento sostenido. El firmware v2.2.9 usa como valores por defecto MOV_ON = 0,075 g de actividad o 24°/s de giro o aceleración vertical 0,090 g, confirmación 12 ms; MOV_OFF = 0,035 g y 11°/s, liberación de 130 ms (junto con condiciones internas de quietud). **La configuración real guardada en NVS puede diferir.** Ninguno de estos valores se altera.

El monitor BLE solo publica tres eventos genéricos: MotionStatusReceived, MotionTransitionReceived y MotionEpochReset; mantiene intacto todo el transporte, la clasificación temporal y el detector bilateral. El controlador BiofitGeometryHeldMotion vive solo en GeomeTry.

## Reglas de control

1. Arranque desde reposo: se exige MOV de **ambos** tobillos, conexiones sanas, sincronización y STATUS recientes, sin calibración en curso.
2. Una vez activado, el botón permanece mantenido mientras al menos un tobillo continúe con MOV (permite cambios breves en qué tobillo se mueve).
3. Solo si **ambos** indican quietud durante 160 ms adicionales se libera el botón; la histéresis propia del firmware ya existía.
4. Si cualquier STATUS supera 550 ms de antigüedad, se pierde conexión o sincronización, se interrumpe juego, se pausa la app, cambian escenas, reinicia el reloj o la calibración deja de estar lista: el control se libera y necesita datos frescos.
5. Los EVENT tardíos, obsoletos o en desorden no sustituyen un STATUS más nuevo; se comparan los millis de cada ESP32 con soporte de overflow.
6. El flujo UP_IMPULSE/900 ms de BILATERAL LEGACY sigue disponible **solo para diagnóstico**. Ya no determina los nuevos saltos. No se elimina su cooldown de la librería general, para no afectar otros prototipos.
7. El juego recibe el mismo Cube.onClick() por fotograma mientras el botón está sostenido; su CheckGrounded() original impide saltos en el aire. JumpOrb consume ahora el estado mantenido en OnTriggerStay2D, igual que al mantener pulsado el mouse. Ship/Wave permanecen con control táctil original.

## Riesgos, mitigaciones y verificaciones

| Riesgo | Medida | Prueba necesaria |
|---|---|---|
| Activación accidental en reposo | Dos tobillos deben moverse al inicio; umbral original conservado | Quietud 10 s, movimientos pequeños, un tobillo aislado |
| Cortes de continuidad en un salto real | STATUS a ~10 Hz y tolerancia 160 ms con MOV_OFF; sigue activo si un tobillo se mueve | Mantener movimiento por al menos 3 aterrizajes |
| Salto después de detenerse | Reposo bilateral y liberación; sin cola de saltos | Dejar de moverse y comprobar que el cubo no salte al próximo aterrizaje |
| BLE atrasado o desconectado | Watchdog 550 ms, reset en pausa/epoch/escena | Desconectar un tracker, reanudar, cambiar de nivel |
| Saltos no deseados al caer | Se permite deliberadamente continuar saltando mientras haya MOV | Medir falsos positivos y diferenciar actividad voluntaria e impactos |
| Primer salto tardío | EVENT MOV_ON validado adelanta al STATUS; no se altera transporte | Comparar vídeo de tobillos y APK |
| Portales/orbes y reinicios | Método TryConsumeOrbPulse reproduce hold, filtro isAlive | Probar los cinco niveles |
| Rendimiento Android | Procesamiento constante O(1), sin RAW permanente | Medir FPS y tasa BLE reales |

Nota técnica: el estado MOV detecta actividad inercial, **no** verifica contacto plantar, intención consciente ni salto humano. Si aparece vibración falsa, se debe contrastar con datos antes de ajustar firmware.

## Validación de software y límites

- tests/HeldMotion ejecuta 13 casos deterministas: reposo, activación bilateral, continuidad durante 8 s, alternancia, liberación, STATUS viejo, evento atrasado, desconexión, pausa, pérdida de reloj, calibración y rollover.
- tests/check_geometry_held.py verifica correspondencia de interfaces, rutas seguras, ausencia de nuevo modo y de cambios físicos.
- tests/check_geometry_original.py compara el BLEMonitor byte a byte con la base v0.9.11, después de descontar exclusivamente los hooks de telemetría añadidos.
- tests/GeometryUxSyntax revisa sintaxis C#; otros CI comprueban Windows, submódulo y exportación.
- CI NO prueba una APK, 60 FPS sostenidos ni comportamiento real BLE. La rama sigue experimental hasta pruebas en teléfono.

## Pruebas en Android

Crear una copia separada del proyecto local estable antes de cambiar ramas. No utilizar reset/clean/discard. Cerrar Unity para la descarga de la rama, reutilizar las cuatro PNG del pack HD previamente instalado y verificar Android > Build Profiles, escenas y la compilación mediante el menú BIOFIT > GeomeTry ORIGINAL > 3. Compilar APK Android original + BLE LEGACY.

Protocolo inicial: conectar dos tobillos y esperar 2/2 + reloj; 10 s en quietud, 5 s con un solo tobillo, 10 s de movimiento bilateral sostenido en tramo libre, 3 aterrizajes seguidos, parada, pausa, vuelta al menú, muerte/respawn y reconexión. Revisar Configuración: BOTÓN EN NIVEL: MANTENIDO/LIBERADO (en Configuración está liberado), PARES LEGACY y SALTOS. Si hay errores, conservar vídeo y CSV con GEOMETRY_HOLD_ON/OFF y GEOMETRY_HELD_JUMP_APPLIED.

**No se modifica main ni se sugiere reflashear trackers en esta primera prueba.**
