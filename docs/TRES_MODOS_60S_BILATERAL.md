# BIOFIT DASH — Tres modos (BILATERAL LEGACY), version principal

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
Estos archivos se pueden revisar estáticamente. Sin ejecutar Unity Editor ni probar el paquete Android con dos sensores **no se afirma** que la mecánica o las colisiones están validadas en el dispositivo. Revisar especialmente altura de techo, punto de aterrizaje Spider, portales desde techo y cambios de escena/respawn. La rama principal integra esta version; antes de afirmar su validacion BLE/Android se requieren pruebas reales.


## Instalación segura desde PowerShell (proyecto separado)
Cerrar Unity antes de preparar el nuevo proyecto. En Windows PowerShell ejecutar **uno por uno**:

```powershell
cd "C:\Users\Capo\Documents"
git clone --recurse-submodules https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-DASH-PRINCIPAL
cd ".\BIOFIT-DASH-PRINCIPAL"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
```

- Los cinco mapas nuevos vienen incluidos en `Assets/BIOFIT/Resources/BiofitThreeModes`; no hay instalador separado de SPIKE LAB.
- El preparador busca automáticamente los cuatro PNG de interfaz aportados por el usuario en copias locales anteriores y verifica SHA256. No están distribuidos en GitHub. Si no encuentra fuente, los solicita explícitamente.
- Abrir la carpeta BIOFIT-DASH-PRINCIPAL en Unity 6000.3 LTS, activar Android en Build Profiles, y ejecutar el preflight original/configuración de UI usados en la versión anterior.
- Verificar con **BIOFIT > GeomeTry ORIGINAL > 8. Verificar niveles 3 MODOS (60 s)**.
- Compilar con **BIOFIT > GeomeTry ORIGINAL > 9. Compilar BIOFIT 3 MODOS (BLE LEGACY)**.
- La APK usa package com.biofitgame.threemodeslab y nombre BIOFIT TRES MODOS, independientes de las instalaciones antiguas. Se genera en Builds/BIOFIT_TRES_MODOS_BILATERAL_*.apk.
- En Unity Editor y en la APK de Tres Modos, el selector carga los cinco mapas principales. El constructor antiguo de GeomeTry tiene otro package y cargaría sus mapas originales.

## Rutas de prueba
- L2: tras cada pincho de suelo (requiere cambio a techo), el siguiente pincho está en el techo (requiere retorno a suelo), y así sucesivamente.
- L3: mismo principio de dos superficies, pero con teletransporte instantáneo.
- L4: Cube > Ball(x180) > Spider(x380) > Cube(x515). En Spider, pinchos en piso x432 y techo x482 exigen dos cambios.
- L5: Cube > Ball(x110) > Spider(x276) > Cube(x404) > Ball(x515). En Spider, piso x328 y techo x375 también obligan a dos cambios.
- Todas las pulsaciones BLE usan el mismo detector LEGACY (>=900ms entre pares aceptados). Las ventanas de ejecución de obstáculos son deliberadamente holgadas; no se necesita saltar en el aire con Cube.

## Verificación y limitaciones
- Ejecutar `python tests/check_three_modes_60s.py` para auditar esquema, orden de portales, separación de obstáculos y recorrido simbólico. Verificación estática, no prueba física.
- Executar el proyecto Unity en Editor: entradas táctiles, techo/collision, eventos al reaparecer, 5 mapas, menú, efectos y portales.
- Luego en Android con los dos ESP32 conectados: capturar CSV; probar diez acciones cada modo y treinta respawns; verificar conteo de **ACCIONES PERSONAJE**, FPS y errores.
- El aspecto es una recreación procedural propia. Para aportar iconos con derechos de uso, crear sprites bajo Resources/BiofitThreeModes/Artwork con los nombres IconBall, IconSpider, PortalBall, PortalSpider y PortalCube. El código usa ese PNG si se encuentra; si no, presenta el fallback procedural.
- No es una copia numéricamente exacta del motor propietario de Geometry Dash: el submódulo no incluye física Ball/Spider. El comportamiento público es el objetivo de diseño; falta calibración/validación visual y física sobre Android.
