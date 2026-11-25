#!/usr/bin/env python3
from pathlib import Path
import argparse, shutil, sys

PNG_NAMES = [
    "Boton_Skin.png",
    "Boton_Play.png",
    "Boton_Configuracion.png",
    "Logo_Biofit_Dash.png",
    "Fondo_Menu.png",
    "LEEME.txt",
]

SCRIPTS = {
    "scripts/BiofitMenuPressFeedback.cs": "Assets/BIOFIT/GeomeTry/BiofitMenuPressFeedback.cs",
    "scripts/BiofitMenuV6Installer.cs": "Assets/BIOFIT/GeomeTry/Editor/BiofitMenuV6Installer.cs",
}

ASSET_SRC_DIR = Path(__file__).resolve().parent / "assets" / "BiofitUiV6"


def require(cond, msg):
    if not cond:
        raise SystemExit(msg)


def main():
    ap = argparse.ArgumentParser(description="Instala los assets y scripts del menú V6 en el proyecto BIOFIT DASH.")
    ap.add_argument("--proyecto", required=True, help="Ruta al proyecto Unity")
    ap.add_argument("--verificar", action="store_true")
    args = ap.parse_args()

    root = Path(args.proyecto).resolve()
    require((root / "ProjectSettings/ProjectVersion.txt").is_file(), "La ruta no parece un proyecto Unity válido.")
    require((root / "Assets/BIOFIT/GeomeTry").is_dir(), "No se encontró Assets/BIOFIT/GeomeTry.")
    require(ASSET_SRC_DIR.is_dir(), "No se encontró la carpeta de assets del menú V6.")

    dst_assets = root / "Assets/BIOFIT/Resources/BiofitUiV6"
    missing = [n for n in PNG_NAMES if not (ASSET_SRC_DIR / n).is_file()]
    require(not missing, "Faltan assets: " + ", ".join(missing))

    print("Proyecto:", root)
    print("Destino de assets:", dst_assets)
    print("Archivos UI:", len(PNG_NAMES)-1, "sprites + LEEME")
    print("Scripts:", len(SCRIPTS))
    if args.verificar:
        print("OK PREVUELO MENU V6 - sin escritura")
        return

    dst_assets.mkdir(parents=True, exist_ok=True)
    for name in PNG_NAMES:
        shutil.copy2(ASSET_SRC_DIR / name, dst_assets / name)
    for src_rel, dst_rel in SCRIPTS.items():
        src = Path(__file__).resolve().parent / src_rel
        dst = root / dst_rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)

    print("OK MENU V6 LOCAL - assets copiados y scripts instalados.")
    print("Siguiente paso en Unity: abrir Main Menu.unity y ejecutar BIOFIT -> Menu V6 -> Aplicar visual del menú principal")

if __name__ == "__main__":
    main()
