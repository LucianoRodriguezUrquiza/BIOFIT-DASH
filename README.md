# BIOFIT DASH — V6.5 desarrollo (rama v6.5-desarrollo)

26/11/2026: rama creada por autorización expresa desde main V6.4 estable. [Guía V6.5](docs/LEVEL_EDITOR_V65.md): vista real del motor, prueba de borrador desde X/Y, catálogo local ampliado y alineado, Red Pad 9.5, Mini Cube experimental (escala .60, velocidad 7.8, salto 10.5), skins Ball/Spider y carrera reconstruida a partir de recursos locales. Código publicado; CI de host 5/5 en a6f08fe, instalación/pruebas Unity y APK pendientes. Los mapas y borradores se conservan; main no se modifica. Consultar la guía antes de continuar. No reinstalar ni subir arte original. Los textos siguientes son historial V6.4. Esperar instrucciones del usuario.

---

# BIOFIT DASH — V6.4 aprobada como versión estable de trabajo

**26/11/2026:** el usuario probó V6.4, aprobó su funcionamiento y autorizó incorporar todos los avances a main y retirar niveles-mejorados después de corregir CI. Main será la única rama de trabajo tras completar la integración. Leer [CONSOLIDACION_V64.md](docs/CONSOLIDACION_V64.md), [guía del editor](docs/LEVEL_EDITOR_V64.md), [índice de deltas](tools/level_editor/changes/INDEX.md) y [validación](tools/level_editor/VALIDACION_V64.json). Las secciones siguientes son historial (V6.3 y V6.4 experimental), no instrucciones vigentes para la rama. Verificar HEAD remoto para confirmar integración y retirada. Estado objetivo: Stereo Madness 2991, Back on Track 1920, Polargeist 2111, Can't Let Go 1871, Jumper 3351; total 12244 registros reconstruibles a partir de bases locales y deltas. No recrear ramas retiradas ni reinstalar desde cero. Mantener ICC/DIPL/RFD/BRO/FLM/RCA. Próximas modificaciones: esperar indicaciones del usuario. La aprobación es de trabajo, no certificación exhaustiva Unity/Android/BLE; no afirmar que Active local coincide sin comprobar sus hashes.

---

# BIOFIT DASH — V6.4 experimental / Level Editor 1.0

Rama niveles-mejorados: herramienta de edición de niveles exclusiva de Unity y corrección visual inicial de Cube. **La versión estable sigue siendo V6.3 en main.** V6.4 está publicada en esta rama; el usuario ya abrió el editor, editó Stereo Madness y publicó su primer delta (2998 registros). Las pruebas completas de jugabilidad, APK, BLE y aplicación a Active local permanecen pendientes. Leer [guía del editor](docs/LEVEL_EDITOR_V64.md), [contexto actual](docs/CONTEXTO_ACTUAL.md) y [deltas experimentales](tools/level_editor/changes/INDEX.md). No fusionar sin validación y autorización.

---

# BIOFIT DASH — V6.3 Campaña Clásica / niveles mejorados

Versión estable de trabajo aprobada por el usuario el 24/11/2026. Leer [contexto actual](docs/CONTEXTO_ACTUAL.md), [relevo](docs/CONTEXTO_VIVO.md) y [referencias visuales](docs/referencias/V63/README.md). Los mapas originales y otros recursos de terceros se conservan localmente; consultar la reconstrucción documentada.

---

# BIOFIT DASH — Campana Clasica V5

Version principal aprobada provisionalmente por el usuario el 24/11/2026. Leer primero [Contexto actual](docs/CONTEXTO_ACTUAL.md) y [relevo](docs/CONTEXTO_VIVO.md). Los recursos locales se conservan intactos. Esta consolidacion publica codigo y configuracion; las decisiones sobre publicacion de recursos de terceros quedan pendientes. El texto siguiente es el README historico.

---

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

- **[CONTEXTO VIVO — iniciar nueva conversación](docs/CONTEXTO_VIVO.md)**: último estado verificado, novedades, pendientes, próximas tareas y protocolo de actualización por paso.
- **[CONTEXTO ACTUAL](docs/CONTEXTO_ACTUAL.md)**: arquitectura completa, estructura de assets, BLE, mapas, instalación, validaciones y restricciones.
- [Contexto maestro histórico completo](docs/CONTEXTO_MAESTRO_HISTORICO.md): documentación detallada previa, preservada en `main` como archivo de referencia.
- [Diseño de los cinco niveles y tres modos](docs/TRES_MODOS_60S_BILATERAL.md).
- [Niveles progresivos (candidata de 1,5 s)](docs/NIVELES_PROGRESIVOS_1500MS.md): diseño, arte local y validación pendiente.
- `Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs`: Ball, Spider, portales y gráficos temporales.
- `Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs`: entrada bilateral hacia personaje activo.
- `Assets/BIOFIT/Resources/BiofitThreeModes/`: cinco mapas JSON.
- `ThirdParty/GeomeTryOriginal`: fuente original como submódulo; se copia mediante preparador, sin modificar fuente.
- `tools/restore_local_biofit_ui.ps1`: recuperación segura de los PNG de interfaz desde un proyecto local anterior.

El firmware ESP32 se mantiene en el repositorio separado [BIOFIT-GAME-Firmware](https://github.com/LucianoRodriguezUrquiza/BIOFIT-GAME-Firmware). No se deben confundir los 900 ms de cooldown con la latencia del BLE.
