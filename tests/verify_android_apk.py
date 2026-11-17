#!/usr/bin/env python3
"""Verify that the actual Android APK includes BIOFIT's SAF Java class.

Usage: python tests/verify_android_apk.py path/to/BIOFIT_DASH_v091.apk
No device/USB/Unity Editor needed. Does not verify AndroidManifest runtime behavior.
"""
from pathlib import Path
import sys
import zipfile

CLASS = b"Lcom/biofit/export/CsvExportActivity;"


def verify(path: Path) -> None:
    if not path.is_file():
        raise ValueError(f"APK no existe: {path}")
    with zipfile.ZipFile(path) as apk:
        names = apk.namelist()
        dex = [n for n in names if n.startswith("classes") and n.endswith(".dex")]
        if not dex:
            raise ValueError("APK sin classes.dex")
        if not any(CLASS in apk.read(n) for n in dex):
            raise ValueError("NO contiene com.biofit.export.CsvExportActivity; no instalar")
    print(f"PASS: el APK contiene clase SAF ({len(dex)} archivos DEX)")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("Uso: python tests/verify_android_apk.py RUTA_APK")
    try:
        verify(Path(sys.argv[1]))
    except (ValueError, OSError, zipfile.BadZipFile) as exc:
        sys.exit("ERROR: " + str(exc))
