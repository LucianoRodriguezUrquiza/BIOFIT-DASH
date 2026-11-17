# Análisis experimental 2026-11-17 — BIOFIT DASH v0.9.3

## Evidencia y límites
Archivo facilitado por usuario: `BIOFIT_v09_20261117_135943_b05673f0.csv` (se analizó offline, **no se publica el archivo crudo en GitHub**). Tamaño: **240.111 bytes**. Footer `# END_BIOFIT sha256=80c7004300652188c11a6cfce26362ed347ef56fcf4ad996562c5bc0bb1026bf`, validación SHA256 OK. 1.970 filas de registros (7 MARCA, 1 CONFIG, 1.612 DIAG, 215 EV, 135 APP); cero registros descartados por límite. Captura 138,592 a 326,500 s de reloj `Time.realtimeSinceStartup`.

**Observación física informada por usuario:** 20 elevaciones derechas, las primeras ~10 con salto tardío cuando pie elevado y ~10 posteriores inmediatamente al subir; 10 izquierdas sin latencia perceptible; 5 bilaterales ejecutadas pero con salto tardío. Conteo humano y EV no son equivalentes. No hay vídeo sincronizado ni inicio físico por intento marcado en CSV, así que no se calcula latencia gesto→pantalla.

### Conteos en modos correspondientes

| Modo / tramo | EV UP | APP bajo umbral | APP candidatos aceptados | APP salto ejecutado | APP cubo ocupado |
| --- | ---: | ---: | ---: | ---: | ---: |
| SOLO DER (171–214 s) | 43 | 9 | 34 | 23 | 11 |
| SOLO IZQ (242–261 s) | 23 | 4 | 19 | 11 | 8 |
| BILATERAL DASH (288–300 s) | 21 entre los dos | no se registra rechazo APP por cada UP | 5 pares | 5 | 0 |

El firmware emite UP incluso para movimiento asociado al descenso. El juego ejecuta `game.Jump()` tras el umbral unilateral; si el cubo ya está en el aire, `SALTO_SOLO_NO_REALIZADO`, no necesariamente un error BLE. No confundir `23` saltos del cubo derecho con `20/20` gestos físicos.

### Patrón derecho: impulsos dobles separados ~0,7 s

En 10 primeras secuencias de UP compatibles con las primeras 10 elevaciones, los primeros impulsos medidos fueron:
`0,165; 0,260; 0,211; 0,253; 0,172; 0,161; 0,202; 0,252; 0,167; 0,198 g`.

De esos **7/10 primeros impulsos están bajo umbral unilateral 0,25 g**; los segundos (en varios casos ~0,635–0,835 s después) superan el umbral y activan salto cuando el cubo ya no está en el comienzo de la elevación. Ejemplo:
- `180,550 s`: derecho UP `0,172 g`, APP `SOLO_DER_DESCARTADO_UMBRAL`.
- `181,319 s`: derecho UP `0,373 g`, APP `SALTO_SOLO_REALIZADO`.
- Diferencia `769 ms` entre **dos impulsos registrados**, NO latencia BLE ni comienzo biomecánico→pantalla.

En las diez secuencias posteriores visibles, la amplitud del primer impulso fue en general mayor y superó 0,25 g, coherente con la respuesta temprana descrita. Esto **apoya fuertemente** la hipótesis de rechazo del primer pulso como componente de latencia percibida, pero no demuestra por sí solo que el segundo impulso ocurriera en la fase exacta de descenso. La fase física requiere vídeo/observador/IMU RAW y referencia temporal.

### Bilateral: primeros pares perdidos por umbral de un tobillo

Umbral bilateral: 0,12 g por pie y ventana 180 ms para los eventos con tiempo de sensor alineado.

- `288,771 s` IZQ `0,191g` y `288,804 s` DER `0,111g`: primer par casi sincrónico pero **derecho bajo umbral**. El par que sí dispara ocurre con DER `0,240g` a `289,641s` e IZQ `0,421g` a `289,808s`.
- `291,845 s` DER `0,114g`, IZQ `0,496g` a `291,913s`: primer par rechazado, segundo aceptado cerca de `292,715s`.
- `296,390 s` DER `0,100g`, IZQ `0,683g` a `296,457s`: primer par rechazado, segundo aceptado cerca de `297,193s`.

Así, **3 de los 5 bloques bilaterales** muestran un primer candidato derecho inferior a 0,12g, compatible con la sensación de salto tardío. Los saltos se ejecutaron en 5/5 pares **según Unity**, pero no se demuestra el instante biomecánico.

### Transporte, ejecución y calidad
- `Rechazos BLE=0`, `dropApp=0`, `huecos seq=0`, `duplicados/desorden=0`, `tiempos_comprimidos=0`; dos trackers conectados y reloj alineado.
- Para UP derecho unilateral: cola Unity mediana `17,4ms`, máximo `33,3ms`; no hay evidencia de retraso de transporte de `700–1000ms`. `rxGapMax` STATUS puede ser cientos de ms y NO equivale al retardo de eventos UP.
- Registro `PRIMER_ESTADO_SALTO` representa el primer estado positivo de simulación tras decisión, **no luz del píxel**; no estimar latencia física con esa métrica.
- Durante QUIETO no se registraron EV UP; esto no acredita ausencia de falsos positivos con umbral experimental menor.
- Firmware v2.2.7: para `UP_IMPULSE` requiere `fUp>=upOnG`, `fActivity>=upActG`, `shortV>=upVms`, evidencia de 2 muestras y rearme. `shortV` es integrador con fuga, no trayectoria real. Las señales UP positivas pueden producirse tanto al elevar el pie como al frenar un descenso. No modificar firmware a ciegas.

## Decisión técnica
**No hay justificación para aumentar BLE o reprogramar IMU por esta observación.** La parte de latencia explicable por umbral puede probarse con una A/B controlada, sin cambios de firmware y sin bajar los valores de producción por defecto.

La revisión `feat/diagnostic-threshold-ab-v094` propone un **modo EXP explícitamente activable**: umbrales unilaterales iniciales siguen 0,25 g, pero EXP habilita 0,20 g y 0,15 g mediante los controles existentes; al salir de EXP, cualquier valor inferior se restaura automáticamente a 0,25 g. El bilateral mantiene 0,12 g por defecto y se puede ensayar temporalmente a 0,10 g con su control existente. Cada cambio se registra con `SOLO_UP_...`, `UP=..._PAIR...` y marcas de activación en CSV; no toca NVS ni firmware.

**Contrafactual descriptivo, sin pretensión de precisión física:** de los diez primeros impulsos derechos muestreados, a 0,25g superarían el corte **3/10**; a 0,20g **5/10**; a 0,15g **10/10**. Se ignoran otros factores como anti-rebote, saltos falsos, disponibilidad del cubo y variabilidad humana. A 0,10g los tres candidatos derechos bilaterales `0,111;0,114;0,100g` serían elegibles, pero la comparación real debe comprobar falsos positivos y acoplamiento temporal.

## ÚNICA sesión A/B/A v0.9.4 propuesta (solo tras pasar pruebas y compilar una APK)

**Seguridad:** movimientos cómodos; no realizar saltos altos obligatorios. Si las piernas se fatigan o aparece dolor, detener y conservar la captura parcial. Colocación anatómica y USB-C atrás, mismos sensores, sin reflashear. Solo una grabación, SIN COLISIÓN: SI, ambas conexiones 2/2, reloj alineado, CAL OK.

1. Base: `IZQ=0,25g`, `DER=0,25g`, `UP=0,12g`, `PAR=180ms`, `EXP: NO`.
2. `1.INICIAR → QUIETO 20s`; anotar saltos falsos.
3. `ELEVACION + BLE:SOLO DER`. **A**: `EXP:NO`, `DER=0,25`; 5 elevaciones lentas + 5 normales; ~2 s entre intentos. Registrar por gesto `inmediato / tardío / sin respuesta / doble`.
4. Activar `EXP: SI`, seleccionar `DER` y usar `-` de la fila `DER` **dos veces**: `0,25→0,20→0,15g`. **B**: repetir 5 lentas + 5 normales; mantener bilateral `UP=0,12`.
5. Desactivar `EXP: NO`: el `DER` debe **volver a 0,25g automáticamente**. **A'**: repetir 5 lentas + 5 normales. Este diseño A/B/A ayuda a identificar efectos de adaptación temporal; la prueba no es aleatorizada.
6. `BLE:SOLO IZQ`, `IZQ=0,25g`: 5 lentas + 5 normales.
7. `BLE:BILATERAL + DASH`, `UP=0,12g, PAR=180ms`: 5 bilaterales lentas + 5 normales.
8. Presionar `-` de la fila **UP bilateral** UNA vez `0,12→0,10g`: 5 lentas + 5 normales. Volver con `+` a 0,12g.
9. `QUIETO 20s`; anotar saltos falsos; `FINALIZAR→ARCHIVO`; guardar CSV real y no convertir en PDF/Docs.
10. Registrar los **60 intentos humanos** por bloque (10 A der + 10 B der + 10 A' der + 10 IZQ + 10 bilateral base + 10 bilateral .10) y la latencia percibida. Si se puede, una **grabación de video** que muestre pies y pantalla en simultáneo, aunque sea una toma lateral, proporciona fase física ausente en CSV.

**Criterio para comparar:** más disparos *al comenzar a subir* sin aumentar los saltos falsos en quietud ni los dobles. No reemplazar automáticamente el 0,25 g basal por 0,15 g aunque un bloque parezca mejor.

## Flujo de desarrollo sin bucle de APK
- Publicar y revisar esta instrumentación v0.9.4 con regresiones host.
- Compilar **una** APK con EXP A/B y, cuando esté listo, ejecutarla **una sola vez**.
- En paralelo (otra rama/PR), desarrollar el primer nivel 2D completo con controles manuales y receptor BLE inalterado. Validar el juego en Unity Play; no esperar a resolver cada detalle de firmware.
- Astra Media es útil para auditar diseño de clasificador de fase, cuantificar falsos positivos y revisar heurísticas con CSV; no se requiere para repetir cálculos elementales.
