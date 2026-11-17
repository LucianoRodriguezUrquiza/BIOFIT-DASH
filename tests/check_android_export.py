#!/usr/bin/env python3
"""Source-level checks only; Android UI still requires on-device validation."""
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
lib = root / "Assets/Plugins/Android/BiofitExport.androidlib"
manifest = lib / "src/main/AndroidManifest.xml"
java = lib / "src/main/java/com/biofit/export/CsvExportActivity.java"
bridge = root / "Assets/BIOFIT/Scripts/BiofitAndroidExport.cs"
screen = root / "Assets/BIOFIT/Scripts/BiofitDashGame.cs"

assert (lib / "project.properties").read_text().strip() == "android.library=true"
assert not (lib / "src/com/biofit/export/CsvExportActivity.java").exists(), "obsolete Java source path"
assert not (lib / "AndroidManifest.xml").exists(), "obsolete root manifest"
ns = "{http://schemas.android.com/apk/res/android}"
tree = ET.parse(manifest).getroot()
acts = tree.findall("./application/activity")
assert len(acts) == 1, "expected one Java export Activity"
assert acts[0].attrib.get(ns + "name") == "com.biofit.export.CsvExportActivity"
assert acts[0].attrib.get(ns + "exported") == "false"
assert not tree.findall("uses-permission"), "do not request broad storage permissions"

src = java.read_text()
for token in ("ACTION_CREATE_DOCUMENT", "onActivityResult(", "SHA-256",
              "openOutputStream(", "openInputStream(", "GUARDADO:",
              "CANCELADO:", "ERROR:", "status", "public static boolean start("):
    assert token in src, f"missing SAF export contract: {token}"
csharp = bridge.read_text()
for token in ("CallStatic<bool>(\"start\"", "GetStatic<string>(\"status\"",
              "IsFinalStatus(", "LastError"):
    assert token in csharp, f"missing C# export bridge: {token}"
ui = screen.read_text()
assert "PollStatus()" in ui and "exportPending" in ui, "no async outcome in game UI"
# Main production path: no custom Java class, picker or clipboard.
assert 'com.biofit.export.CsvExportActivity' not in csharp, "export still depends on custom Java"
assert "ACTION_CREATE_DOCUMENT" not in csharp
for token in ("MediaStore$Downloads", "openOutputStream", "relative_path",
              "DIRECTORY_DOWNLOADS", "SDK_INT", "getStatSize",
              "BiofitCaptureArchive.Verify", "SaveToDownloads"):
    assert token in csharp, f"required direct Downloads API missing: {token}"
ui = screen.read_text()
assert 'monitor.ExportGameCapture()' in ui
assert 'exportPending' not in ui, "ARCHIVO should not wait for a picker"
assert "BiofitAndroidExport.LastStatus" in (root / "Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs").read_text()
print("PASS: direct MediaStore Downloads bridge and unused legacy Java source syntax")
print("Note: static checks do not prove an Android APK runs on a phone")
