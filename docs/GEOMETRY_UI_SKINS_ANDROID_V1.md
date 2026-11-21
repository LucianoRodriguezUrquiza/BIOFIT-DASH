# BIOFIT GeomeTry — UX, 70 skins, back/pause y 60 FPS

Esta rama deriva de feat/geometry-original-legacy-bridge y preserva el submodulo original completo en ThirdParty/GeomeTryOriginal, los JSON, escenas, movimiento, velocidad, salto, fisica y BLE BILATERAL LEGACY.

Cambios: nuevo controlador Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs; Main Menu conserva sus tres botones centrales, habilita Settings y Customisation (eventos originales desactivados) y oculta los controles inferiores y Credits. En Level Selection aparece selector visual de los cinco mapas con botones funcionales. Hay back en todas las escenas salvo Main Menu y pausa/reanudar/reiniciar/regresar en Level. La pausa restaura Time.timeScale y audio de antes, sin ajustar los parametros del cubo. Settings contiene estados BLE, registros, exportacion y FPS estimados. Customisation tiene 70 skins en una grilla de 10x7, persistidas en PlayerPrefs; solamente se sustituye el SpriteRenderer del cubo.

El panel IMGUI antiguo de BiofitGeomeTryLegacyBridge fue retirado sin cambiar el handler de BilateralJumpCandidate ni los umbrales/firmware del detector. Exposicion read-only de contadores para Settings.

Android solicita Application.targetFrameRate=60 y desactiva vSync. No cambia Time.fixedDeltaTime, velocidad, gravedad ni el 1.5x original de GeomeTry. Se necesita prueba real en dispositivo; no se garantizan 60 FPS.

## Archivos graficos proporcionados por el usuario

El pack entregado en el chat BIOFIT_GeomeTry_UI_Assets.zip contiene LevelSelect.png (reproduccion visual del selector indicado), Back.png (icono sin azul), Pause.png y Skins.png (70 imagenes exactamente del collage). El pack es necesario para reproducir las imagenes solicitadas, pero hay botones de reserva si no esta instalado. No se suben al repositorio remoto por tratarse de archivos graficos aportados por el usuario y para conservar el repositorio reutilizable.

## Instalacion sin tocar tu proyecto actual

1. Cerrar Unity; nunca resetear/descartar los cambios de tu copia existente.
2. Crear una copia separada desde PowerShell:
   cd "C:\Users\Capo\Documents"
   git clone --recurse-submodules --branch feat/geometry-ui-skins-60fps-v1 https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-GeomeTry-UI-V1
   cd ".\BIOFIT-GeomeTry-UI-V1"
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
3. Guardar el pack descargado desde el chat en Descargas y ejecutar en la nueva carpeta:
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\install_geometry_ui_assets.ps1"
   Si el archivo esta en otra ubicacion, agregar -ZipPath "RUTA".
4. Abrir BIOFIT-GeomeTry-UI-V1 con Unity 6000.3.28f1; comprobar consola, permisos, las diez escenas en Build Profiles, Player Settings con Input Manager (Old), y el EventSystem original adaptado a StandaloneInputModule (no modificar main ni firmware).
5. Probar todos los botones y niveles en Play, pausa/reinicio, retorno, skins, Settings/registro y guardar/reabrir.
6. Android > Switch Platform > BIOFIT > GeomeTry ORIGINAL > Compilar APK Android original + BLE LEGACY.
7. Probar primero tactil, luego dos WaveShare BLE y medir FPS reales en telefono.

## Limitaciones

Los scripts y tests host NO constituyen una prueba de compilacion en Unity Editor, ejecucion APK Android, autonomia ni 60 FPS reales. Las escenas originales Settings, Profile, Stats, Success y Customisation casi no contienen UI: el controlador aporta las pantallas solicitadas y un retorno seguro, no crea estadisticas o editor de niveles nuevos.
