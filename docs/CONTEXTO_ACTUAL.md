# BIOFIT DASH — Campaña Clásica V5 (principal provisional)
Actualización: 2026-11-24. El usuario informó que probó físicamente la V5 y funciona bien, y autorizó su consolidación en main. No se dispone de una matriz de pruebas por nivel ni de registros nuevos de BLE/APK; no afirmar que se certificó todo el recorrido, la latencia o Android.

## Inicio de una conversación nueva
Leer este archivo, `docs/CONTEXTO_VIVO.md`, `docs/ASSET_SOURCES.md`, y `tools/campaign_v5/patches/V5/VALIDACION_V5.json`. Consultar el código real de main antes de modificar. El proyecto Unity local validado es `C:\Users\Capo\Documents\BIOFIT-DASH-STEREO-ORIGINAL` (Unity 6000.3.28f1).
Repositorio Unity: https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY
Firmware: https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware
Base de los instaladores históricos: 73cd148b9f3747b736eb4972c88ddee5a4fbe517.
Submódulo: https://github.com/Christ0u/GeomeTry, ThirdParty/GeomeTryOriginal, d922caddb4d21ecb791a266143e279bdb969d42c.

## ICC — Invariabilidad del control corporal (prioridad absoluta)
Conservar el firmware ESP32-S3/QMI8658, adquisición y procesamiento, BLE BILATERAL LEGACY, detector bilateral UP_IMPULSE (umbral 0,12 g y ventana 180 ms de referencia), sincronización/requisitos de conexión, y cooldown configurable desde el menú: 400, 500, 600, 700, 800 y 900 ms. Consultar los parámetros reales en código. No modificar, reemplazar o desactivar sin autorización explícita. Una pareja BLE aceptada equivale a una acción del personaje, no una entrada sostenida nueva. El teclado, joystick Bluetooth y táctil son secundarios. Mantener físicas del Cube normal (velocidad, gravedad, fuerza de salto). El adaptador de Cube invertido, Ball y Spider debe consumir los eventos existentes; no cambiar el detector para resolver problemas de nivel. Validar con sensores físicos y registrar resultados.

## DIPL — Desarrollo Incremental mediante Parches Locales
Una sola carpeta Unity; ZIP con cambios mínimos e instalador Python/PowerShell. Comprobar versión y hashes, realizar copia de seguridad, aplicar cambios de forma transaccional, documentar, permitir reversión y validar antes de commit. No volver a clonar para cada actualización. Git local para control de versiones; main para las versiones aprobadas; no fusionar sin autorización. Orden del usuario 24/11/2026: mantener el repositorio público durante el desarrollo y no modificar todavía las reglas de exclusión ni eliminar recursos originales. La publicación de recursos de terceros queda pendiente de decisión y permisos; esta consolidación comprende código, configuración y documentación. Conservar el RAR local y los respaldos. Los parches históricos son código de referencia; sus comprobaciones de HEAD requieren la base histórica, NO se ejecutan encima de main V5.

## Campaña validada provisionalmente
| Slot | Nivel original | Archivo fuente | Originales | Activos | Vehículos |
|---|---|---|---:|---:|---|
|1|Stereo Madness|levels/1.txt|2291|2785|Cube/Ball/Cube/Spider|
|2|Back on Track|levels/2.txt|1545|1734|Cube/Ball/Cube|
|3|Polargeist|levels/3.txt|1531|2111|Cube/Spider/Cube|
|4|Can't Let Go|levels/6.txt|1704|1871|Cube/Ball/Cube|
|5|Jumper|levels/7.txt|2813|3351|Cube/Ball/Cube/Spider/Cube|
Total 9884 registros de fuente, 11852 activos; 7 corredores Ship adaptados, 15 monedas. Ship sigue en el código. Los sectores Cube se conservan salvo transiciones y adaptación de mecánicas; los corredores se basan en patrones de Cycles (9) y Fingerdash (21), no son copias literales. La V5 añade gravedad invertida, pads, orbes y modo práctica en los cinco niveles. Intervalos nominales entre acciones 2,26–3,28 s según informe; no son medidas BLE.
V1: importación exacta de los datos de Stereo Madness; V2: colisiones, fuentes bitmap, HUD, práctica; V3: transición Ship y checkpoints manuales Z/X/táctil, relación de aspecto y separación de pulsación/salto, validación del usuario; V4: sustitución de Ship en nivel 1 (experimental, no probada por separado); V5: campaña acumulativa, aprobada provisionalmente por el usuario. 5/5 pruebas host reportadas en VALIDACION_V5.json; no certificación de Unity/Android por esas pruebas.
Conservar fuentes originales, botones, barra de progreso, CD visible sin solapamiento, modo práctica, monedas y portales. Validar toda la campaña, colisiones, checkpoints, pads/orbes, música, BLE, menú cooldown, Android a 60 FPS objetivo (no garantizado). No afirmar resultados que no consten.

## RFD — Repositorios y fuentes de desarrollo
- GD Assets: https://github.com/kenzieshane/Assets (ref 1.0).
- GD Textures: https://github.com/Weebifying/gd-textures.
- Kenney: https://github.com/shorepine/kenney y https://kenney.nl/assets (verificar CC0 por pack).
- Catálogo técnico de objetos: https://github.com/TalhaMudo/GDBuilderAI (mcp/src/data/objects.json).
- Referencias adicionales por verificar antes de usar: https://github.com/AngusA13/geometry-dash-full y https://github.com/Aadem-Isai/Final-Geometry-Dash-main.
- Instalación local Geometry Dash 2.207: `C:\Users\Capo\Desktop\Geometry.Dash.v2.207\Resources`. RAR de consulta en el proyecto ChatGPT si se encuentra disponible; no asumir acceso desde otra conversación. Los datos, fuentes, sprites y música originales se conservan en la PC y respaldo local; no se cambia su tratamiento en Git en esta consolidación.
- Catálogo: docs/ASSET_SOURCES.md; herramientas de recursos: tools/assets/Preparar_Biblioteca_BIOFIT.ps1.

## Archivos técnicos y reconstrucción
Los scripts activos están en Assets/BIOFIT/GeomeTry (BiofitStereoLocalRuntime, BiofitStereoSolidContact, BiofitStereoPractice, BiofitStereoPracticeButton, BiofitV2Hud, BiofitV2BitmapText, BiofitV5Interaction, BiofitThreeModes, BiofitGeometryUX, verificadores Editor). El puente BLE existente BiofitGeomeTryLegacyBridge y el detector se preservan.
Los recursos generados existen localmente (se mantienen las reglas de Git ya existentes, sin introducir nuevas exclusiones): BiofitStereoLocal (CampaignV5/{1..5}/Original.json, Active.json, Level.json y sprites), BiofitStereoV2 (UI/fuentes), Songs/Musics/BiofitLocalV5, arte local BiofitUi y BiofitThreeModes/Artwork. El Character.cs copiado de GeomeTry tiene un ajuste de puntero de práctica de V3; el submódulo se conserva sin cambios. No perder ese ajuste al regenerar la copia.
Para otro equipo: clonar main con submódulos, ejecutar tools/prepare_geometry_original.ps1 con Unity cerrado en el clon nuevo, y ejecutar tools/campaign_v5/restaurar_local_v5.py con el respaldo local privado generado por la consolidación. No distribuir ese respaldo. Si no se dispone del respaldo, reconstruir en una carpeta temporal en la base histórica, con los ZIP de recursos V1 y V2, el original GD 2.207 y la cadena de instaladores V1→V2→V3→V5 en tools/campaign_v5/patches. V4 no necesaria. Copiar solo los recursos locales y el Character.cs adaptado a la copia de main. Las rutas y comandos de cada instalador están en sus LEEME.txt. Abrir Main Menu.unity, verificar BIOFIT → GeomeTry ORIGINAL → 8. Verificar niveles 3 MODOS (60 s), y probar.
Configuración Unity aprobada para consolidación tras revisar BIOFIT_REVISION_V5.txt: packages-lock.json (UniBLE v0.1.1, URP 17.3.0), GraphicsSettings, ProjectSettings, QualitySettings, EditorBuildSettings, ShaderGraphSettings y URPProjectSettings. No restaurar los valores anteriores indiscriminadamente.
Los documentos previos se conservan en docs/history; el nombre histórico del verificador no implica duración de 60 s.
