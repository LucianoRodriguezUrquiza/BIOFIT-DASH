# BIOFIT DASH — juego principal (Cube · Ball · Spider)

Proyecto de exergaming en Unity para Android. Dos sensores Waveshare ESP32-S3/QMI8658 en los tobillos controlan **Cube, Ball y Spider** mediante el **mismo detector BLE BILATERAL LEGACY**.

**Rama canónica: `main`.** Esta rama ya incorpora la base GeomeTry original, el código BLE de LEGACY y los cinco niveles de ~60 segundos adaptados a saltos corporales. No se necesitan ramas experimentales para construir el juego.

| Nivel | Modo | Duración nominal |
|---|---|---|
| 1 | Cube | ~60 s |
| 2 | Ball | ~60 s |
| 3 | Spider | ~60 s |
| 4 | Cube → Ball → Spider → Cube | ~60 s |
| 5 | Cube → Ball → Spider → Cube → Ball | ~60 s |

- **Cube:** salto y física originales.
- **Ball:** invierte gravedad y viaja entre piso y techo.
- **Spider:** teletransporte a la superficie opuesta con efecto de transición.
- **Portales:** cambios automáticos de personaje.
- **Control:** umbral UP en Unity 0,12 g, emparejamiento 180 ms, cooldown 900 ms; sin cambios en firmware ni física del cubo.
- **Validación:** el usuario probó la versión en Unity Editor; Android con ambos ESP32 sigue pendiente.

## Descargar y preparar en Windows

Con Unity cerrado y desde PowerShell, en una carpeta nueva:

```powershell
cd "C:\Users\Capo\Documents"
git clone --recurse-submodules https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-UNITY.git BIOFIT-DASH-PRINCIPAL
cd ".\BIOFIT-DASH-PRINCIPAL"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\prepare_geometry_original.ps1"
```

La preparación importa los recursos GeomeTry y busca automáticamente las **cuatro imágenes locales de la interfaz** en copias anteriores del proyecto. Los PNG `LevelSelect.png`, `Skins.png`, `Back.png`, `Pause.png` **no están en GitHub** por su procedencia no aclarada; una PC nueva necesita aportarlos una vez. Los gráficos experimentales de Ball/Spider y portales se generan mediante código y sí están disponibles al clonar.

Unity 6.3 LTS 6000.3.28f1:
1. `BIOFIT > GeomeTry UX > 1. Configurar modulo tactil Android`
2. `BIOFIT > GeomeTry UX > 2. Verificar UX y recursos`
3. `BIOFIT > GeomeTry ORIGINAL > 8. Verificar niveles 3 MODOS (60 s)`
4. `BIOFIT > GeomeTry ORIGINAL > 4. Activar URP original para probar en Play`; abrir Main Menu (opción 2)
5. Android: cambiar plataforma y usar **`BIOFIT > GeomeTry ORIGINAL > 9. Compilar BIOFIT 3 MODOS (BLE LEGACY)`**

La APK independiente es `com.biofitgame.threemodeslab` y se guarda en `Builds/`.

## Documentación y código principal

- **[CONTEXTO ACTUAL](docs/CONTEXTO_ACTUAL.md)**: arquitectura completa, estructura de assets, BLE, mapas, instalación, validaciones y restricciones.
- [Contexto maestro histórico completo](docs/CONTEXTO_MAESTRO_HISTORICO.md): documentación detallada previa, preservada en `main` como archivo de referencia.
- [Diseño de los cinco niveles y tres modos](docs/TRES_MODOS_60S_BILATERAL.md).
- `Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs`: Ball, Spider, portales y gráficos temporales.
- `Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs`: entrada bilateral hacia personaje activo.
- `Assets/BIOFIT/Resources/BiofitThreeModes/`: cinco mapas JSON.
- `ThirdParty/GeomeTryOriginal`: fuente original como submódulo; se copia mediante preparador, sin modificar fuente.
- `tools/restore_local_biofit_ui.ps1`: recuperación segura de los PNG de interfaz desde un proyecto local anterior.

El firmware ESP32 se mantiene en el repositorio separado [BIOFIT-GAME-Firmware](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware). No se deben confundir los 900 ms de cooldown con la latencia del BLE.
