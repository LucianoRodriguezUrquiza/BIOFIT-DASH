# Estado vigente — BIOFIT DASH V6.6

Repositorio oficial https://github.com/LucianoRodriguezUrquiza/BIOFIT-DASH. Usuario confirma sincronización local en feat/bmd-v231 a 42c755b y solicita consolidar en main. Leer [CONSOLIDACION_V66.md](CONSOLIDACION_V66.md) antes de continuar. Mantener proyecto local único, archivos excluidos y dos deltas locales sin seguimiento. No alterar firmware ni niveles. Runner tendrá repositorio independiente.

---

# Estado vigente — 27/11/2026

Aceptación de trabajo en Unity y CI 5/5: [ACEPTACION_BMD231_UX.md](ACEPTACION_BMD231_UX.md). La rama activa es feat/bmd-v231; main y firmware intactos. Se conservan deltas de niveles, arte local y detector. La APK final y ensayos de hardware no se certifican mediante esta aceptación. No reinstalar ni consolidar sin autorización.

---

## RCA 27/11/2026 — sistema visual arcade común (Configuración y Personajes)
Usuario solicita eliminar apariencia plana, conservar estructura C y selector, usar esquinas redondeadas y fuente del juego; prioridad texto contenido. Se implementan máscaras redondeadas RGBA generadas en ejecución y sprites de nueve cortes, cache por radio y liberación al destruir UI. Doble marco oscuro/cian, base opaca azul petróleo, relieve superior e inferior en botones verde/cian, mismos componentes para los dos menús. Se conservan PNG locales, navegación, 30 skins por página, selección, BMD y lógica de registro/exportación. Etiquetas con padding 8 px horizontal/4 vertical adicionales, TMP autosize y ellipsis de respaldo, y ajuste uniforme de vértices reales BMFont (no solo avances), habilitado exclusivamente en las etiquetas de estos menús. Se reduce ligeramente arte de preview y celdas para respetar márgenes de marco; nombre de categoría queda dentro del mismo botón. Pruebas de geometría, estilo y límites de tinta actualizadas. No se distribuyen assets externos ni se requiere copiar nuevos PNG. No se alteran niveles, físicas, BLE, firmware ni main. Validación visual Unity y Android pendiente. Actualizar con Unity cerrado mediante git pull --ff-only en feat/bmd-v231.

---

## RCA 27/11/2026 — configuración modular C
Usuario elige propuesta C, título CONFIGURACIÓN, fondo laboratorio suministrado (PNG local BiofitUi/SettingsMenu/settings_menu_bg.png, 1678x937, SHA256 121a733142e291ae6c874bc504f4539697535d076290a21dbb6c7181c79aa35a). Se mantiene exclusión local de BiofitUi; fallback azul oscuro. Paneles opacos oscuros, sin logo/esquinas/de detección/FPS. Estado individual de conexiones FW, sincronía estimada, temporizador real de sesión (realtime), CSV, RAW, GFA configurado, perfil, ubicación, sync y estado de archivo. Botón único INICIAR/FINALIZAR Y DESCARGAR: monitor espera CaptureCompletion, bloquea operaciones duplicadas vía ExportBusy, verifica identidad exacta del archivo guardado, usa exportador existente con SHA; nunca recupera una sesión vieja si falla el cierre. Reexportar CSV y SAF independientes; TRACE/GFA todavía se descargan dentro de BMD, no automáticamente desde el registro convencional. BMD, joystick y cooldown mantienen submenús. Se preservan física, firmware, detector, mapas y arte de personajes. Tests de contrato y host añadidos; validar Unity y Android. Con Unity cerrado git pull --ff-only y copiar PNG local; no recompilar hasta comprobar.

---

## RCA 27/11/2026 — contraste del selector futurista de vehículos
Tras captura real aportada por el usuario, se elimina ArcadeCorners exclusivamente de Customisation. Se conservan el PNG local, su aspect-fill y brillo, el botón VOLVER original, los sprites y el layout de 30 skins por página. Placas gris oscuro casi opacas independientes detrás de título PERSONAJE, preview, texto de skin activa, cada categoría, cada skin y contador de página. No interceptan raycasts; se mantienen marco blanco de selección y botones existentes. Se eliminan únicamente los puntos circulares de paginación del selector de personajes (no las categorías ni indicadores de selección de nivel); navegación por flechas rectangulares gris oscuro con chevrón blanco y borde cian, deshabilitadas visual/funcionalmente en extremos, y contador numérico. No se usa backIcon para paginación. Sin cambios a física, mapas, deltas, firmware, BLE/GFA ni otros menús. No se requiere reinstalar arte: git pull --ff-only con Unity cerrado. Contratos y geometría revisados; prueba visual Unity y APK pendientes.

---

## RCA 27/11/2026 — restauración Red Pad y fondo de vehículos
El usuario revoca el ajuste 80cab9c antes de descargarlo localmente. Se restauran exactamente los blobs de interacción y Level Editor de ce0e288 (rojo 9.5 u/s, rosa 12.5, amarillo 20, gravedad sin velocidad vertical fija; orbes intactos). Se recupera el contrato de pruebas anterior; el historial RCA conserva la autorización y su revocación. No se reescribe historia ni se altera ningún delta de niveles. El menú Customisation ahora carga opcionalmente Resources/BiofitUi/VehicleMenu/vehicle_menu_bg.png, imagen futurista de 1672x941 suministrada por el usuario. SHA256 original a6f77e5bc89d7eb6b2cd47121f315d5ecc7b2b385c47069f8d53e2a7099e6d62. Mantener recurso local según exclusiones BiofitUi, copiar PNG desde Descargas a esa ruta con Unity cerrado; no se publica el binario. Se aplica ajuste aspect-fill centrado y multiplicador RGB .82 para contraste, panel de skins negro alfa .66; si falta el PNG se conserva el degradado gris. Sin cambios en otros menús, sprites, preferencias, geometría, BLE, GFA o firmware. git pull --ff-only en feat/bmd-v231 trae el estado final directamente, sin aplicar manualmente el commit intermedio. Validación visual y de APK pendiente.

---

## RCA 27/11/2026 — Red Pad igual al salto normal de Cube
Autorización explícita del usuario: cambiar exclusivamente el pad rojo (redPad, ID 900001), de 9.5 a 13.5 u/s de velocidad vertical inicial opuesta a la gravedad. Fuente de referencia: Character.jumpForce=13.5f en GeomeTry fijado a d922cadd; Cube.Jump asigna Vy directamente, Rigidbody2D gravityScale=4, linear damping=0 y avance normal 6.5 u/s. RedPadImpulse se comparte entre runtime y guía analítica del Level Editor; se actualiza texto del catálogo. El orbe rojo conserva 9.5, pad/orbe rosa 12.5, amarillo 20, MiniJump 10.5, los otros orbes y portales no cambian. El pad conserva activación automática por contacto, sin BLE, y no modifica velocidad horizontal ni gravedad. En Cube/Ball/Spider bajo g=39.24 y vx=6.5, altura ideal 2.322 bloques, alcance 4.472 bloques, vuelo 0.688 s de simulación a igual altura; mismo modelo que el salto normal del Cube. Mini conserva su propia velocidad de 7.8, por lo que su alcance con el pad es distinto de Cube normal y de su salto Mini. La igualdad exacta de aterrizaje en el juego depende del punto de activación, colisiones y física discreta; no prometer igualdad de coordenadas. Rosa es menor que Cube, no se modifica. Cambio global afecta pads rojos existentes; revisar niveles diseñados con alcance anterior. Se preservan los deltas publicados de niveles 1/2/3, mapas locales, borradores, arte, detector corporal, firmware, BMD/GFA y main. Pruebas de contrato actualizadas con comparación de referencia y modelo cinemático. Validación Unity/Android pendiente. Con Unity cerrado git pull --ff-only en feat/bmd-v231; no ejecutar instaladores ni importar niveles de nuevo.

---

## RCA 27/11/2026 — publicación del nivel 3 en BMD V1.1
El usuario informó que el delta de ranura 3 se reconstruyó (163 ediciones, 723 altas, 767 bajas, SHA objetivo e124ad3a5fdf05e13c59ca8d2910a50e8df6253cf610341c9179a87bb4517b3e) pero se detuvo antes de commit/push por la lista de ramas autorizadas. Se añade exclusivamente feat/bmd-v231 a dicha lista y se actualiza la confirmación del editor. Se conservan guardas de origin, rama remota existente, HEAD sincronizado, staging vacío y verificación de SHA; se publican solo delta e índice, no mapas completos, borradores ni PNG. Usuario debe respaldar Builds/BiofitLevelEditor/3 y el delta exportado, sincronizar por fast-forward y publicar el mismo JSON sin recargar el mapa. Aplicar al juego y publicar son operaciones distintas; no aplicar dos veces. No se modifica ningún nivel ni se afirma que el delta local esté publicado antes de recibir confirmación remota. Main intacta.

---

## 27/11/2026 — Selector unificado de personajes (Cube / Ball / Spider)
- Usuario solicita estructura inspirada en sus tres capturas de Geometry Dash, sin candados, tienda, monedas, estrellas, diamantes ni categorías ajenas. Todos los skins disponibles desde el comienzo. Se reutiliza el atlas propio de 70 Cube, y los manifiestos y sprites locales V6.5 Ball/Spider (instalación previamente reportada: 119/69). No se publican ni se vuelven a importar recursos propietarios; no se cambian los PNG locales. Fondo gris recreado proceduralmente (aprox. RGB 149 a 66 según referencia; no es la textura original extraída), esquinas y flecha existentes, fila horizontal de tres categorías, preview, grilla 10x3, selección blanca y paginación interactiva. Flechas limitadas a primera/última página; página independiente y retorno a la skin elegida. Persistencia usa las tres claves previas, no se reinicia elección. Para Cube se comparten los recortes medidos del atlas entre UV de interfaz y Sprite.Create de juego, sin variar PPU/colliders; las celdas ya no muestran toda la imagen íntegra, posible revisión visual si algún contorno sobresale del recorte. No se altera arte original de los cubos. Fallback informativo si faltan recursos. No cambia física, control, mapas, BMD/GFA, BLE ni firmware. CI anterior de reconexión 5/5 (46037751060). Validación visual y APK pendientes. Sin ZIP, con Unity cerrado git pull --ff-only en feat/bmd-v231; no volver a preparar/importar.

---

## RCA — conexión manual BLE (27/11/2026)
- Usuario informa que abrir la APK con Bluetooth apagado obliga a reiniciar la aplicación. Causa: ConnectBothAsync sólo se invocaba en Start y retornaba si el adaptador no estaba disponible. Nuevo botón BUSCAR Y CONECTAR en Configuración/Conexión Waveshare, indicador 0/2, 1/2, 2/2, estado de búsqueda/permisos y mensajes de reintento. Reutiliza la conexión directa por MAC conocida (no escaneo indiscriminado ni emparejamiento Android), conserva enlaces sanos y vuelve a suscribir EVENT/STATUS del faltante. Controla doble pulsación, generación de paquetes y callbacks antiguos; no ejecuta durante registro/operación BMD/RAW/exportación. Los relojes y perfiles deben confirmarse de nuevo si hubo desconexión. No activa Bluetooth por sí solo. UniBLE v0.1.1 no cancela el GATT nativo pendiente mediante su token; se limita la espera del cliente y se evita iniciar otra conexión mientras ese dispositivo siga Connecting/Disconnecting. Reintentar cuando Android resuelva el enlace. Pruebas host con mocks, validación física Android pendiente. No cambia firmware, calibración, niveles, físicas ni detector. Referencia APK aprobada a234fb2 permanece en historial; rama main intacta.

---
## 27/11/2026 — Aceptación de APK y referencia para siguientes cambios
- Usuario confirma que la APK compilada e instalada funciona bien y autoriza considerarla validada como APK/base de trabajo de Unity (V6.5 + BMD V1.1/GFA), antes de continuar con el diseño de niveles y estética de menús.
- Referencia de código instalada: `a234fb2dc676f3069b408dc010f0b84e20d4a213` en `feat/bmd-v231`. Capturas previas acreditan migración PowerShell satisfactoria con conservación del guard de práctica y hooks GFA, ejecución del editor e interfaz BMD V1.1. CI de esa revisión: [46035142939](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/46035142939), 5/5. La aprobación de la APK es declarada por el usuario, no una verificación independiente del binario; no se dispone de su hash ni registro exhaustivo de casos.
- Alcance: aceptación funcional de la APK como referencia de desarrollo, no certificación de GFA, TRACE, PSRAM, rendimiento BLE o detección biomecánica. El usuario anuncia que ahora instalará ambos firmwares FW231; todavía no informa instalación exitosa ni ensayos físicos de FW231. Firmware de referencia: `58372c5927c75792395a68606ff9f40fd061fc5b`, CI 3/3 [46026399937](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware/actions/runs/46026399937).
- Próximos cambios previstos por el usuario: niveles y estética de menús. Mantener ICC, DIPL, RFD, BRO, FLM y RCA, conservar la referencia aprobada y el detector corporal, registrar los deltas y verificar regresiones. No se aprueba aún la promoción de toda la infraestructura diagnóstica a main. No se modifican código, mapas, recursos, firmware ni main en este registro.

---

# BMD V1.1 + GFA — rama experimental feat/bmd-v231

Actualización diagnóstica sobre V6.5, compatible con FW230 (legacy) y FW231 (bloques CRC32, PSRAM, banco GFA). [Contrato, instalación y validación](BMD_V1_1.md). Conserva main, detector y físicas; locomoción definitiva pendiente de montaje de tobillos. No promover a estable antes de comprobar APK y placas físicas.

---

# BIOFIT DASH — V6.5 + BMD experimental

Rama feat/bmd-v230. Se añade BMD V1 con configuración FW230, diagnóstico sombra y exportación de trace; [procedimiento, alcance y límites](BMD_V1.md). Main V6.5 intacta. Pruebas host y APK/ensayo físico pendientes al publicar.

---

# BIOFIT DASH — V6.5 aprobada como versión estable de trabajo

26/11/2026: el usuario informa que compiló y probó satisfactoriamente su última APK, y autoriza cerrar esta versión tras corregir CI. La captura de PowerShell acredita HEAD local y remoto `ffa907d6baa7974b8152b9622026679f2981a52a` en `v6.5-desarrollo`, diff versionado vacío y coincidencia de los cinco SHA-256 de Active.json con deltas/manifiestos remotos. No acredita compilación independiente ni todos los casos BLE. CI 5/5 verificado en `aad9ca3edcbb7106a662870ca78c932605bbe30e` ([ejecución 45906168297](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY/actions/runs/45906168297)). Consolidación autorizada por fast-forward de `main` a este cierre documental, descendiente directo del código verificado; confirmar HEAD remoto. La rama v6.5-desarrollo se conserva. La corrección no cambió código de juego ni mapas. Leer [CONSOLIDACION_V65.md](CONSOLIDACION_V65.md), [guía V6.5](LEVEL_EDITOR_V65.md) y [validación](../tools/level_editor/VALIDACION_V65.json). Las secciones siguientes son históricas; conservar ICC/DIPL/RFD/BRO/FLM/RCA. No reinstalar, no subir recursos locales no versionados ni modificar firmware, físicas o mapas. Pendiente: diagnosticar degradación de saltos consecutivos, sin atribuir todavía causa ni modificar detector.

---

## V6.5 — función en selección múltiple (26/11/2026)

El usuario solicita cambiar la función de varios objetos a la vez. Se añade al inspector de selección múltiple el mismo listado Kinds que usa la selección individual. Si los tipos difieren, se muestra «Funciones distintas» sin modificar nada; elegir una función la aplica a todos los seleccionados que tengan un tipo diferente. Una operación equivale a un paso Undo/Redo. Los objetos que cambian recuperan collisionMode=0 (colisión original del tipo elegido), evitando que un override previo contradiga solid/hazard/decoration; los que ya tienen el tipo elegido no cambian. Se conservan sprites, ID, posiciones, rotaciones, dimensiones, capas, índices y demás campos. Se rechaza toda la operación antes de mutar si excede tres monedas o convierte en visible un objeto sin sprites. No se alteran mapas o borradores durante la instalación ni se modifica el inspector individual, el runtime, las físicas, BLE, firmware, APK o main. Misma rama v6.5-desarrollo. Sin ZIP, sincronizar con Unity cerrado. Pruebas estructurales añadidas, prueba de interfaz Unity pendiente.

---

## V6.5 — transformar selección individual o múltiple (26/11/2026)

Se agregan controles al inspector para girar -90/+90/180 grados, ángulo manual, Espejo X (izquierda/derecha) y Espejo Y (arriba/abajo), tanto en selección individual como múltiple. Opción Como grupo activada por defecto: pivote en el centro de la caja delimitadora de los centros GD seleccionados; rotación rígida de posiciones y orientaciones según convención GD horaria, reflejo global con conjugación de ángulo y cambio de flip local. Desactivada: transforma cada objeto en su centro sin mover coordenadas. Q/E giran -/+90, Shift+Q/E -/+15, fuera de edición de texto y sin Ctrl/Alt. No se aplica snap a la transformación rígida para conservar la geometría relativa; se elimina ruido numérico cercano a enteros. Se comprueban coordenadas antes de escribir; si salen del intervalo horizontal del nivel se rechaza toda la operación. Shift conserva desplazamiento local de trigger extendido de portal respecto a su centro; no se cambia tipo de portal, dirección semántica de gravedad, propiedades físicas, índices ni capas. Una operación equivale a un paso Undo. Solo código Editor y pruebas/documentación; no cambia mapas existentes, runtime, BLE, firmware, APK ni main. Validación visual Unity pendiente. Sincronizar v6.5-desarrollo con Unity cerrado, sin ZIP.

---

## V6.5 — guía de trayectorias en el editor (26/11/2026)

El usuario solicitó visualizar el recorrido al seleccionar un pad u orbe, sin entrar a Play Mode. Se añade exclusivamente a BiofitLevelEditor.cs: una selección individual de pad/orbe dibuja curva discontinua con flecha, punto de altura máxima para impulsos ordinarios, etiquetas y alcance; desaparece al deseleccionar, seleccionar varios u ocultarla. Cube/Ball/Spider comparten exactamente la curva ideal de velocidad 6,5 u/s y gravedad 4×Physics2D.gravity.y (39,24 u/s² con configuración actual), por eso se etiquetan juntos en amarillo, no se inventan diferencias. Mini Cube usa 7,8 u/s, curva celeste opcional y centro inicial escalado .60. Las constantes de orbes rosa/rojo/verde se leen del código de interacción; amarillo 20 y celeste 0 reflejan implementación actual. Modelo en GD (30 GD por unidad): x=x0+30 vx t; y=y0+30(vy t+0.5 ay t²). Sin alterar físicas, motor ni BLE. Orbes parten de su centro ideal; pads del centro estimado del personaje a ±15 GD respecto del objeto (Mini ±9 GD), no se calcula contacto exacto. Gravedad inicial seleccionable normal/invertida en propiedades, independiente del spawn; no se infiere automáticamente de portales. Impulsos regulares terminan a la misma altura inicial; celeste/verde y pad gravitatorio invierten aceleración y muestran horizonte configurable 0,25–2 s de simulación (1 s inicial), no un aterrizaje calculado. Verde 13,5 u/s hacia nueva superficie. Guía ignora colisiones, integración discreta, portales, acciones posteriores, latencia y cooldown; las posiciones reales de activación varían. Usa el lienzo existente, no persiste datos ni se incluye en APK; no modifica mapas ni recursos. No sustituye Jugar desde aquí. Se mantiene rama v6.5-desarrollo y main V6.4 intacta. Pruebas de contratos y comprobaciones numéricas ideales añadidas; compilación y validación visual en Unity pendientes. Sin ZIP, sincronizar con git pull --ff-only origin v6.5-desarrollo con Unity cerrado.

---

## V6.5 — pads y orbes para Ball y Spider (26/11/2026)

El usuario autorizó extender los cuatro pads y cinco orbes a Ball y Spider. Se limita la ampliación a IsPad/IsOrb: no cambia los portales de tamaño/gravedad, Ship, ni Cube normal/mini/invertido. Pads amarillo 20, rosa 12,5, rojo 9,5 u/s contra gravedad actual; celeste invierte gravedad y pone Vy=0. Orbes amarillo/rosa/rojo aplican esas mismas magnitudes; celeste invierte y pone Vy=0; verde invierte y aplica 13,5 u/s hacia la nueva superficie. Ball/Spider mantienen vehículo, tamaño, velocidad horizontal, hitbox y magnitud de gravedad; Spider actualiza flipY. Los impulsos gravitatorios no teletransportan al Spider: lo hacen caer/acelerar físicamente hacia la nueva superficie. Un pad se activa por contacto sin evento corporal; orbes de Ball/Spider usan la acción discreta existente TryPulse (BLE aceptado, teclado, joystick, mouse), con prioridad antes de SurfaceContact: exige intersección real del trigger y collider mediante Collider2D.Distance, consume un solo orbe disponible y retorna antes de la inversión/teletransporte ordinario. Se conserva la guardia .12s del motor, el cooldown BLE configurable y el comportamiento de un uso por instancia de personaje. Sin contacto con orbe disponible se conserva la acción habitual. No hay salto en cola ni se aumenta la zona del orbe. No se modifican firmware, bridge BLE, detector, constantes previas, recursos, niveles, borradores, main ni exclusiones. Pruebas estructurales actualizadas; validación semántica Unity, Android y sensores pendiente. Sincronizar v6.5-desarrollo con Unity cerrado; no requiere ZIP ni importación de arte. Probar cada vehículo con gravedad normal e invertida; confirmar que un pulso no activa orbe y teletransporte a la vez.

---

## V6.5 — cuatro orbes adicionales (26/11/2026)

Usuario autorizó orbes rosa, rojo, celeste y verde para diseño didáctico de niveles. Se agregan kinds pinkOrb/redOrb/blueOrb/greenOrb e IDs locales reservados 900002–900005 (no IDs originales), plantillas 30×30 GD con ring_01, categoría Impulsos / orbes. Todos usan la zona existente de 1,05×1,05 Unity, activación voluntaria, una vez por instancia de Cube, no por mero contacto. Cube normal usa el pulso BLE existente; mini/invertido usan TryPulse existente. Teclado, mouse y joystick mantienen comportamiento del amarillo. Ball/Spider no activan estos orbes. Rosa 12,5 u/s y rojo 9,5 u/s opuestas a gravedad; celeste cambia gravedad y pone velocidad Y en cero; verde cambia gravedad y aplica 13,5 u/s hacia la nueva superficie de apoyo (valor experimental BIOFIT, no física original de GD). Se conserva tamaño mini mediante SetCubeGravity. Amarillo 20, pads, físicas normales, cooldown y bridge BLE no se modifican. Texturas recoloreadas del ring_01 ya instalado, preservando blancos y alfa; catálogo, lienzo, runtime y FLM usan el mismo color. Sin ZIP, sin nuevo arte propietario, sin tocar exclusiones, mapas o borradores; solo git pull de v6.5-desarrollo. Main V6.4 intacta. Validar en Unity con teclado y ambos trackers, respetando cooldown, tanto gravedad normal como invertida. CI de host verifica contratos estructurales, no gameplay ni hardware. No se agregaron orbes automáticamente a los niveles.

---

## V6.5 — paso de desplazamiento personalizable (26/11/2026)

El usuario solicita precisión arbitraria al mover objetos con mouse. Se reemplaza el selector rígido 15/30/60 por presets 1/5/10/15/30/60 GD y campo numérico editable (confirmar Enter). Admite valores positivos entre 0,1 y 1000 GD, incluso decimales; rechaza cero, negativos, NaN e infinito sin alterar el paso anterior. Se recuerda localmente con EditorPrefs, valor inicial 30 GD. Snap activo aplica el paso al arrastre (relativo a posición original), colocación, pegado flotante y marcador de prueba; flechas usan paso elegido y Shift+flecha conserva 1 GD. Snap desactivado permite arrastre libre. La grilla visual sigue adaptándose al zoom y puede ocultarse sin desactivar Snap. Se mantienen offsets relativos de selecciones múltiples. No modifica mapas, borradores, arte, APK, físicas, BLE ni main. Actualizar por git pull --ff-only origin v6.5-desarrollo con Unity cerrado; comprobación real en editor pendiente. Misma versión y rama.

---

## V6.5 — controles compactos de prueba y orbe BLE (26/11/2026)

El usuario confirmó que Jugar desde aquí inicia en el punto elegido. Se reduce exclusivamente su OnGUI: dos botones de 24 px de alto en esquina inferior derecha (Volver al editor / Reiniciar desde aquí), sin panel ni textos explicativos. Vista real conserva controles de navegación. Código UNITY_EDITOR, no APK. Revisión del orbe amarillo: BiofitV5Interaction.OnTriggerStay2D acepta TryConsumeOrbPulse para Cube normal; BiofitGeomeTryLegacyBridge genera un pulso de un uso por par BLE aceptado con vencimiento 1.5 pasos físicos, respetando cooldown/detector y sin cola; Mini/InvertedCube reciben TryPulse y prueban orbe en cercanía. Impulso 20 u/s; no automático, debe coincidir con zona de activación. No se modificó la lógica BLE ni las físicas. Tutorial de prueba: usar orbe existente, probar primero teclado, luego ambos tobillos conectados/alineados y salto bilateral al pasar por el orbe; ajustar geometría y separación temporal para que el cooldown permita el segundo salto. Con 400 ms y velocidad real normal 9.75 u/s, separación entre eventos aceptados equivalente a 3.9 bloques de avance (estimación, no margen garantizado). No se añadió un nivel tutorial ni se comprobó hardware. Sin cambios en main ni mapas; sincronizar rama v6.5-desarrollo con Unity cerrado. Validación visual y BLE real pendientes.

---

## V6.5 — portapapeles y contorno del Red Pad (26/11/2026)

Ctrl+X / Cortar copia la selección y la elimina del borrador (Undo restaura); Ctrl+C conserva original. Ctrl+V / Pegar activa previsualización flotante, sigue el mouse, conserva desplazamientos relativos y snap de grupo; clic izquierdo y soltar dentro del lienzo confirma una sola inserción, Esc cancela solo la previsualización y conserva el portapapeles. El corte dentro del mismo nivel reutiliza índices si siguen libres para conservar metadatos originales del baseline; tras pegar, el portapapeles permite copias nuevas. Duplicar +1 / Ctrl+D mantiene desplazamiento fijo. Pegar no modifica Active ni publica; guarda borrador después de confirmar. Red Pad: textura recoloreada con contorno blanco fino, compartida entre catálogo, lienzo y runtime, sin cambiar tamaño de sprite, collider ni impulso 9.5 u/s. Sin recursos nuevos ni modificaciones a mapas, física o BLE. Se mantienen main V6.4 y rama v6.5-desarrollo; sincronizar por fast-forward con Unity cerrado. Prueba real de interacción y aspecto pendiente.

---

## V6.5 — mejora del catálogo (26/11/2026)

El usuario confirmó que Unity abre tras V6.5.1 y el editor muestra 1978 entradas de catálogo; las demás funciones le parecen correctas a priori, sin prueba exhaustiva. Reportó texto cortado y fondo de botón demasiado bajo. Se sustituyen miniButton/Toggle por filas de 54 px con fondo explícito de 50 px, miniatura y dos líneas independientes con alto reservado y tooltip. Filtros visibles: Todos, Bloques, Plataformas, Pinchos/peligros, Decoración, Portales, Impulsos/orbes, Monedas y Eventos/colores. Clasificación por kind existente; sólidos bajos (<=18 GD) o plank van a plataformas, demás a bloques. Arte importado decorativo sigue siendo decoración: no se le asignan colisiones automáticamente. Búsqueda y paginación dentro de categoría, objetos con ID funcional primero. Cambiar filtro cancela colocación para evitar insertar un objeto oculto. Solo cambia UI del editor y pruebas; no modifica catálogo en disco, mapas, borradores, gameplay, BLE ni main. Mantener rama v6.5-desarrollo y misma versión; sincronizar con git pull --ff-only desde proyecto local, con Unity cerrado. Verificación visual pendiente.

---

## V6.5.1 — corrección de compilación (26/11/2026)

El usuario instaló V6.5 correctamente (23 mapas/borradores preservados; 1931 sprites, 119 Ball, 69 Spider, cero variantes omitidas). Unity entró en Safe Mode: CS0400 en BiofitV65EditorSession.cs por referencia a global::Camera y seis CS0103 por BiofitV65Skins no resuelta. La clase existía en GitHub; la causa exacta de su ausencia en la compilación local no está confirmada. Se integra la clase estática sin cambios funcionales en BiofitThreeModes.cs (unidad de compilación ya reconocida), se elimina su archivo separado y se actualiza la cobertura CI. La vista previa pausa el FixedUpdate de la cámara original mediante Time.timeScale=0 sin referenciar su tipo. No se modifican mapas, arte, físicas, BLE ni firmware. Las dos advertencias TMP CS0618 no bloquean. Cerrar Unity, desde la carpeta del proyecto comprobar rama v6.5-desarrollo, git pull --ff-only origin v6.5-desarrollo y reabrir. No repetir importación de arte. Compilación Unity y prueba funcional pendientes hasta confirmación del usuario; CI sintáctico no equivale a compilación semántica.

---

# BIOFIT DASH — V6.5 desarrollo (rama v6.5-desarrollo)

26/11/2026: rama creada por autorización expresa desde main V6.4 estable. [Guía V6.5](LEVEL_EDITOR_V65.md): vista real del motor, prueba de borrador desde X/Y, catálogo local ampliado y alineado, Red Pad 9.5, Mini Cube experimental (escala .60, velocidad 7.8, salto 10.5), skins Ball/Spider y carrera reconstruida a partir de recursos locales. Código publicado; CI de host 5/5 en a6f08fe, instalación/pruebas Unity y APK pendientes. Los mapas y borradores se conservan; main no se modifica. Consultar la guía antes de continuar. No reinstalar ni subir arte original. Los textos siguientes son historial V6.4. Esperar instrucciones del usuario.

---

# BIOFIT DASH — V6.4 aprobada como versión estable de trabajo

**26/11/2026:** el usuario probó V6.4, aprobó su funcionamiento y autorizó incorporar todos los avances a main y retirar niveles-mejorados después de corregir CI. Main será la única rama de trabajo tras completar la integración. Leer [CONSOLIDACION_V64.md](CONSOLIDACION_V64.md), [guía del editor](LEVEL_EDITOR_V64.md), [índice de deltas](../tools/level_editor/changes/INDEX.md) y [validación](../tools/level_editor/VALIDACION_V64.json). Las secciones siguientes son historial (V6.3 y V6.4 experimental), no instrucciones vigentes para la rama. Verificar HEAD remoto para confirmar integración y retirada. Estado objetivo: Stereo Madness 2991, Back on Track 1920, Polargeist 2111, Can't Let Go 1871, Jumper 3351; total 12244 registros reconstruibles a partir de bases locales y deltas. No recrear ramas retiradas ni reinstalar desde cero. Mantener ICC/DIPL/RFD/BRO/FLM/RCA. Próximas modificaciones: esperar indicaciones del usuario. La aprobación es de trabajo, no certificación exhaustiva Unity/Android/BLE; no afirmar que Active local coincide sin comprobar sus hashes.

---

## Avance 26/11/2026 — segundo delta de Stereo Madness y primer delta de Back on Track

Verificados remotamente en `082ea260382f144a38d3f8c17285e5b8f0671615`. Stereo Madness: aplicar primero `BIOFIT_level_1_20261126_001459.json` y después `BIOFIT_level_1_20261126_004628.json` (base 8c7adbe0f89959dcf86b89e70c10ed62f084ebbb2407420a56a4ffb159798f0f, objetivo bd2d104cfc88b79751e9cde421f097d6f8413da80847a7ee38c7583379cbfc43; 2991 registros, 16 altas, 23 bajas). El usuario informó que solucionó el fallo de teletransporte del Spider en 86–88% aumentando ligeramente el espacio vertical libre. El motor exige distancia entre centros de al menos 1 unidad Unity (30 GD), además de apoyo/destino y espacio libre de colliders; no confundir con separación mínima entre caras, que depende del tamaño del personaje. Back on Track: `BIOFIT_level_2_20261126_114359.json` (base V6.3, objetivo 39e8beeb17c3a549a6eb452587da86b0655b9e81b0e650ffddc0e9c4cd151651; 1920 registros, 135 ediciones, 268 altas, 82 bajas). Integridad de índices y altas/bajas verificada; no se recalcularon aquí los hashes de los mapas objetivo ni se probó la APK. Usuario declara finalizada la edición de Back on Track; no equivale a certificación integral. Main V6.3 sin cambios.

Físicas de referencia verificadas en GeomeTry Character/Cube y BiofitV5Interaction: velocidad horizontal 6.5 u/s de simulación; salto normal 13.5 u/s vertical inicial; pad amarillo 20, rosa 12.5, gravedad celeste invierte gravedad (no asigna un impulso vertical fijo en el adaptador). Time.timeScale 1.5: velocidades respecto al tiempo real 9.75 horizontal, 20.25/30/18.75 vertical inicial. No modificar estas físicas ni ICC.

---

# Último avance experimental — Stereo Madness editado por el usuario (26/11/2026)

El usuario abrió Level Editor V6.4 y publicó el primer delta de Stereo Madness: [registro y verificación](../tools/level_editor/changes/INDEX.md), commit `9f1dba739338759ef27f6dfc5b2ab5e169e3586e`. Geometría objetivo: 2998 registros (69 ediciones, 86 altas, 103 bajas), cero monedas. Revisar cuatro grupos de sólidos superpuestos. El delta remoto y la salida del publicador coinciden. No asumir aplicación a Active local ni validación completa de juego. Los otros cuatro mapas conservan V5 y main sigue V6.3. Para próximos trabajos, recuperar este delta antes de reconstruir Stereo Madness experimental. Sincronizar rama local por fast-forward después de actualizaciones documentales remotas.

---

# Relevo actual — V6.4 experimental (niveles-mejorados)

V6.3 sigue siendo la versión estable de main. El último avance experimental es V6.4 Level Editor 1.0 y corrección visual inicial del Cube. Leer [guía de V6.4](LEVEL_EDITOR_V64.md), [validación](../tools/level_editor/VALIDACION_V64.json), [índice de deltas](../tools/level_editor/changes/INDEX.md). El usuario ya abrió el editor, editó Stereo Madness y publicó el primer delta. Quedan pendientes las pruebas completas de Unity, APK y BLE y verificar Active local; no cambiar main. Para continuar, verificar HEAD de ambas ramas y el estado local. El editor solo existe en Unity Editor; los mapas editados se aplican mediante datos JSON con runtime mínimo. Firmware, BLE BILATERAL LEGACY, parámetros, cooldown, física y submódulo no modificados. RCA obligatorio: registrar nuevos deltas y pruebas; no publicar recursos originales. Después de instalación, Unity > BIOFIT > Level Editor > Abrir editor. La siguiente tarea la define el usuario.

---

# BIOFIT DASH — relevo de desarrollo V6.3
La versión V6.3 fue aprobada como estable de trabajo por el usuario el 24/11/2026. Leer primero [CONTEXTO_ACTUAL.md](CONTEXTO_ACTUAL.md), [ACEPTACION_V63.md](ACEPTACION_V63.md), [ASSET_SOURCES.md](ASSET_SOURCES.md), [referencias de diseño](referencias/V63/README.md) y [validación](../tools/campaign_v63/VALIDACION_V63.json). Verificar el SHA remoto de main antes de afirmar que está consolidada. El menú V6.1 es runtime; la escena antigua en Edit Mode es normal. Los mapas 2–5 conservan V5 y Stereo Madness tiene 3015 registros. DIPL, ICC, RFD, BRO y FLM obligatorios. Usar siempre el mismo proyecto local, preservar BLE BILATERAL LEGACY, firmware y cooldown, no modificar exclusiones. Contexto histórico: [V5](history/CONTEXTO_ACTUAL_V5.md), [relevo V5](history/CONTEXTO_VIVO_V5.md). La siguiente tarea la define el usuario.


## 27/11/2026 — BMD físico, GFA y locomoción universal
Ver [registro RCA y hoja de ruta](RCA_20261127_BMD_GFA_LOCOMOCION.md): perfiles remotos y adquisición/exportación verificados por usuario, transferencia lenta, ensayos CSV pendientes, próxima revisión BMD/GFA y futuro motor común STEP/WALK/RUN/JUMP. Solo documentación, sin cambio de código ni promoción.

- Seguimiento CI: el primer ensayo de configuración modular (46059376309) pasó los contratos del nuevo layout, navegación, sintaxis C#, exportador Android y compatibilidad Windows, pero falló un contrato histórico que exigía coordenadas del antiguo botón JOYSTICK. Se actualiza a verificar posición nueva y navegación preservada; no se modifica la lógica HID. El cronómetro usa Math.Floor/Max para compatibilidad con shims y se prueba avance real y congelación. Se distinguen cancelaciones y relojes pendientes. Validación final Unity/Android pendiente.
