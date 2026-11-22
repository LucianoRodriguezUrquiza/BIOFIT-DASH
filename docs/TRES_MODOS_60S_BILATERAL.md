# BIOFIT DASH — Tres modos (BILATERAL LEGACY), plan experimental

## Contrato de niveles
- Longitud de todos los mapas: marcador final x=575; spawn original en x~-10; velocidad original 6.5u/s, Time.timeScale=1.5 -> ~60s reales. No se modifica velocidad, gravedad del Cube ni impulso original de salto.
- L1 Nivel 1 - Cubo: solo Cube, pinchos individuales separados 38 unidades (~3.90s).
- L2 Nivel 2 - Ball: Ball desde el inicio; obstáculos alternados en piso y techo cada 52u (~5.33s).
- L3 Nivel 3 - Spider: Spider desde el inicio; obstáculos alternados cada 46u (~4.72s).
- L4 Nivel 4 - Tres modos: Cube -> Ball (x180) -> Spider (x380) -> Cube (x515).
- L5 Nivel 5 - Tres modos avanzado: Cube -> Ball (x110) -> Spider (x276) -> Cube (x404) -> Ball (x515).
- El detector bilateral conserva: 0.12g, ventana 180ms, cooldown 900ms. 900ms es un bloqueo entre pares, **no** la latencia de la radio BLE.
- Acciones separadas de manera deliberada por varias decenas de unidades, no requiere salto doble ni activaciones subsegundo; el objetivo es compatibilidad con movimiento corporal real, no dificultad de rhythm game original.
- Ceiling propio Biofit en capa Ground (layer 3) a y=8, espesor 1, para los niveles 2-5.
- Portales físicos con Collider2D isTrigger y cambio de modo automático; no requieren saltar.

## Implementación
- El submódulo Christ0u/GeomeTry d922cadd..., sus 5 niveles originales y sus 10 escenas se preservan. Los nuevos mapas son copias separadas en Assets/BIOFIT/Resources/BiofitThreeModes.
- Los recursos oficiales extraídos de Geometry Dash **no se redistribuyen**: no hay licencia válida aportada. Se utilizan sprites vectoriales-procedimentales originales (inspirados en las formas) para que la prueba no dependa de imágenes externas.
- Ball: movimiento horizontal heredado, Rigidbody2D con gravedad invertida en impulsos válidos cuando hay contacto; rota mientras rueda.
- Spider: desde una superficie válida, Physics2D.Raycast encuentra la más cercana opuesta; teleportación con velocidad vertical anulada y rastro temporal + destello; invierte gravedad.
- No hay cambio de firmware, ni manipulación de los parámetros de detección BLE.

## Validación pendiente
Estos archivos se pueden revisar estáticamente. Sin ejecutar Unity Editor ni probar el paquete Android con dos sensores **no se afirma** que la mecánica o las colisiones están validadas en el dispositivo. Revisar especialmente altura de techo, punto de aterrizaje Spider, portales desde techo y cambios de escena/respawn. No fusionar en main sin pruebas reales.
