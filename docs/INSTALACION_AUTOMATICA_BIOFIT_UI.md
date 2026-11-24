# Instalación automática de BiofitUi desde ZIP local

El repositorio público omite deliberadamente los PNG de `Assets/BIOFIT/Resources/BiofitUi/`: la hoja `Skins.png` puede incluir imágenes de terceros (Geometry Dash), por lo que no es adecuado redistribuirla sin permiso. **No se subió ningún PNG ni el RAR del usuario a GitHub.**

El usuario entregó `BiofitUi.rar` en una conversación de ChatGPT el 2026-11-23; se extrajeron y validaron exactamente cuatro archivos. Se generó `BIOFIT_UI_ASSETS.zip` como archivo privado descargable de ESA conversación (no disponible públicamente ni desde Github, por diseño).

## Instalación sin copiar imágenes a mano

1. Descargar el paquete `BIOFIT_UI_ASSETS.zip` desde la conversación y guardarlo **sin extraer** en Descargas (`Downloads`) de Windows, con el mismo nombre.
2. Abrir PowerShell dentro de la carpeta raíz del clon de BIOFIT DASH que tiene esta rama, por ejemplo:
   ```powershell
   cd "C:\Users\Capo\Documents\BIOFIT-DASH-JOYSTICK-TEST"
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\install_biofit_ui.ps1"
   ```
3. El instalador identifica el ZIP en Descargas (también permite `-ArchivePath "C:\otra\ruta\BIOFIT_UI_ASSETS.zip"`), extrae a una carpeta temporal, **verifica SHA-256** de los cuatro PNG, copia los archivos al directorio `Assets\BIOFIT\Resources\BiofitUi`, y verifica su integridad.
4. Si ya existen archivos iguales, los omite. Si existen archivos distintos, antes de reemplazarlos los respalda en `BiofitUi_Asset_Backup_FECHA` dentro del clon (ruta ignorada por Git).
5. Al volver a Unity se importan los archivos automáticamente. `LevelSelect`, `Skins`, `Back` y `Pause` serán cargados mediante `Resources.Load` sin cambiar los scripts.

No ejecutar `git clean -fd` ni comandos destructivos sobre instalaciones locales. Este instalador no modifica firmware ESP32, detección BILATERAL LEGACY, cooldown, escenas ni físicas.

## Publicación opcional de Skins.png en GitHub

**Autorización expresa del usuario:** se permite añadir la hoja `Skins.png` al repositorio público. No se publican `Back.png`, `LevelSelect.png` ni `Pause.png`.

En un clon de la rama `feat/vr-joystick-input-diagnostics`:

```powershell
cd "C:\Users\Capo\Documents\BIOFIT-DASH-JOYSTICK-TEST"
git pull
powershell -NoProfile -ExecutionPolicy Bypass -File ".\tools\install_biofit_ui.ps1" -PublicarSkins
```

El script requiere `BIOFIT_UI_ASSETS.zip` en Descargas y credenciales Git configuradas localmente. Comprueba los SHA-256 de los cuatro PNG, los instala en Unity, verifica que Git esté en la rama experimental, aborta si hay otros cambios ya preparados, ejecuta `git add` y `git commit` **solo para `Assets/BIOFIT/Resources/BiofitUi/Skins.png`**, y finalmente `git push` a la rama experimental. Puede pedir autenticación GitHub; no modificar `main` ni usar `--force`.

**Estado:** la excepción en `.gitignore` y la automatización están en GitHub; **la imagen todavía no se ha subido desde esta conversación** porque la conexión GitHub no consume el archivo binario local. Confirmar el archivo en la URL devuelta por el script antes de afirmar publicación. Si contiene arte derivado, su uso personal no convierte automáticamente esos diseños en material libre de derechos.

## Integridad de los cuatro archivos

| Archivo | Bytes | SHA-256 |
| --- | ---: | --- |
| `Back.png` | 7606 | `37e64b538a7b9f98c2ddf26f6c8474ed16c9ad8116e9cf2eb3d841b1ceae1037` |
| `LevelSelect.png` | 2433590 | `f7e4295c30329990bc9511aef0b08adbd72b0b981efebca787e2b338641ac3b7` |
| `Pause.png` | 158591 | `8b34f47fbf61834f85991357fce814e6a5fdc04e3f8040dafc95cbbfac76a0d5` |
| `Skins.png` | 936729 | `3e9a3675f53e7b02303ced1a0b65aa1bb8ffcb0935fd3aae46597026b32ba7b5` |

**Limitaciones:** el ZIP se entrega desde la conversación, no GitHub; si el usuario abre otro chat sin el archivo deberá conservar el ZIP localmente. La CI verifica la sintaxis del instalador, **no** incluye estos PNG de terceros ni puede comprobar el funcionamiento del instalador en el PC del usuario. Su prueba definitiva corresponde al Windows/Unity real.
