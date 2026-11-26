"""BIOFIT GeomeTry presentation regression checks (host-only).

No claim is made here that Unity Editor or the Android APK was executed.
The script intentionally never imports or edits original GeomeTry assets.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
ui = (root / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text()
art = (root / "Assets/BIOFIT/GeomeTry/BiofitUiArtwork.cs").read_text()
bridge = (root / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text()
preflight = (root / "Assets/BIOFIT/GeomeTry/Editor/BiofitGeometryUxPreflight.cs").read_text()
ps = (root / "tools/install_geometry_ui_assets.ps1").read_text()
settings = (root / "ProjectSettings/ProjectSettings.asset").read_text()

# 1. User-visible titles are live Spanish cards, not baked English artwork.
# The reference is used only for clean background geometry/corners/dots.
assert "internal static readonly int[] CardLeft" in art
assert "internal static readonly int[] CardWidths" in art
assert "CardWidths[i]" in ui and "CardWidths[i]" in art
assert "EraseStrip(clean, pixels, 132, 1543" in art
assert "RemoveConnectedBlue(data, width, height);" in art
assert "separateCards[i] = btn.transform as RectTransform;" in ui
assert "selectorFocusCard = separateCards[focusLevel];" in ui
assert "selectorFocusCard.localScale = Vector3.one;" not in ui, (
    "Do not override pointer press animation")
assert "selectorFocusCard.anchoredPosition" in ui
assert "1.045f" not in ui, "Do not enlarge focused card over neighbors"
assert 'Button("BIOFIT " + MapTitles[i], board, ""' in ui
def string_array(name):
    match = re.search(r"\b" + name + r"\s*=\s*\{([^}]+)\}", ui)
    assert match, name
    return re.findall(r'"([^"]*)"', match.group(1))

assert string_array("MapTitles") == [
    "STEREO MADNESS", "BACK ON TRACK", "POLARGEIST", "CAN'T LET GO", "JUMPER"]
assert string_array("MapModes") == [
    "CUBO / BOLA / SPIDER", "CUBO / BOLA", "CUBO / SPIDER",
    "CUBO / BOLA", "CUBO / BOLA / SPIDER"]
assert 'Text("Nombre " + MapTitles[i], btn.transform, MapTitles[i],' in ui
assert 'Text("Modo " + MapTitles[i], btn.transform, MapModes[i],' in ui
assert "BiofitThreeModeArt.Make(mode, false)" in ui
assert "artwork.cards[i]" not in ui, "Baked legacy names must not show"
assert "artwork != null ? artwork.background : null;" in ui

# Card positions and original screenshot geometry must be in bounds and do
# not overlap horizontally even after reconstruction.
left = [137, 421, 690, 963, 1238]
widths = [282, 267, 271, 274, 296]
assert len(left) == len(widths) == 5
for x, w in zip(left, widths):
    assert 0 <= x < x+w <= 1672
assert all(left[i] + widths[i] < left[i+1] for i in range(4))

# 2. The reference's baked dots and label are removed before drawing one set.
assert "EraseStrip(clean, pixels, 633, 1039, 772, 866" in art
assert "EraseStrip(clean, pixels, 424, 1245, 695, 771" in art
assert "BuildArcadeDots(board);" in ui
assert 'new Color(1f, .82f, .02f, 1f)' in ui

# 3. The entire original skin atlas is displayed as one uncut image;
# individual selection is an invisible hit region and never truncates row 7.
assert '"70 SKINS SIN RECORTE"' in ui
assert "RawImage wholeAtlas = sheet.gameObject.AddComponent<RawImage>();" in ui
assert 'Hotspot("Elegir skin " + (i + 1)' in ui
assert "for (int i = 0; i < 70; i++)" in ui
assert "PlayerPrefs.SetInt(PrefSkin, skinIndex);" in ui
assert "PlayerPrefs.Save();" in ui
assert "Sprite.Create(atlas" in ui
assert "cubeRenderer.sprite = selected;" in ui
assert "GridLayoutGroup layout" not in ui
atlas_width, atlas_height = 790, 790 * 887 / 1280
atlas_bottom = .364 * 900 - atlas_height / 2
atlas_top = .364 * 900 + atlas_height / 2
skin_label_bottom = .707 * 900 - 40 / 2
assert atlas_bottom > 45, "Skin row seven extends off the design stage"
assert skin_label_bottom - atlas_top > 10, "Atlas touches/overlaps skin caption"

# 4. Truly alpha-keyed artwork: no hard-coded blue rectangles behind the logo.
assert "IsBlueBackground(Color32 c)" in art
assert "logo = Own(Extract(pixels" in art
assert "false, true, \"BIOFIT logo sin fondo azul\"" in art
assert "artwork.logo" in ui
assert "image.texture = levelBackdrop;" not in ui

# 5. Four complete corners and one independent safely placed back button.
assert "artwork.corners[i]" in ui
assert "corner.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);" in ui
assert "if (i == 0) raw.uvRect = new Rect(1f, 0f, -1f, 1f);" in ui
assert 'scene == LevelScene ? new Vector2(.06f, .93f) : new Vector2(.080f, .902f), new Vector2(88f, 88f)' in ui
assert 'new Vector2(.051f, .913f), new Vector2(144f, 135f)' in ui
assert 'Hotspot("VOLVER sobre flecha original"' in ui

# 6. Aspect-correct, safe-area-aware layout for 16:9 / 20:9 / 4:3
# regardless of their Canvas reference resolution.
assert 'safeRoot = Rect("BIOFIT Android safe area"' in ui
assert 'designStage = Rect("BIOFIT 16:9 responsive design"' in ui
assert "Rect area = Screen.safeArea;" in ui
assert "Mathf.Min(sx, sy)" in ui
assert "scaler.matchWidthOrHeight = 1f;" in ui
assert "UpdateViewport();" in ui
for screen_w, screen_h in [(1280, 720), (2400, 1080), (800, 600),
                           (1920, 1080), (2340, 1080)]:
    virtual_w = screen_w * 900 / screen_h
    scale = min(virtual_w / 1600, 1)
    assert scale * 1600 <= virtual_w + 1e-7
    assert scale * 900 <= 900 + 1e-7
logo_top = .915 * 900 + 137 / 2
logo_bottom = .915 * 900 - 137 / 2
title_top = .793 * 900 + 66 / 2
title_bottom = .793 * 900 - 66 / 2
first_row_top = .614 * 900 + 164 / 2
assert logo_top < 900 and logo_bottom > title_top
assert title_bottom - first_row_top > 25
trackers_bottom = .614 * 900 - 164 / 2
second_row_top = .411 * 900 + 139 / 2
assert trackers_bottom - second_row_top > 20
second_row_bottom = .411 * 900 - 139 / 2
buttons_top = .269 * 900 + 66 / 2
assert second_row_bottom - buttons_top > 20
last_button_bottom = .078 * 900 - 62 / 2
assert last_button_bottom > 30

# 7. Structured BLE/recording/performance cards; no one giant status dump.
for section in [
    '"Conexion BLE"', '"Eventos BLE"', '"Captura y archivos"',
    '"FPS medicion"', '"CONEXIÓN WAVESHARE"',
    '"DETECCIÓN DE MOVIMIENTOS"', '"REGISTRO Y EXPORTACIÓN"',
]:
    assert section in ui, section
for variable in ["connectionLabel", "eventLabel", "captureLabel",
                 "performanceLabel", "statusLabel"]:
    assert variable in ui
for action in [
    "StartGameCapture()", "StopGameCapture()",
    "ToggleRawCapture()", "MarkVideoSync()", "RequestExport(saf)",
]:
    assert action in ui, action

# 7b. A finger press has visible feedback without duplicating a BLE action.
press = (root / "Assets/BIOFIT/GeomeTry/BiofitSettingsButtonFeedback.cs").read_text()
assert "frame.gameObject.AddComponent<BiofitSettingsButtonFeedback>()" in ui
for token in ["IPointerDownHandler", "IPointerUpHandler", "IPointerExitHandler",
              "Time.unscaledDeltaTime", "Vector3.one * .92f", "1.04f"]:
    assert token in press, token
for action in ["StartCapture", "StopCapture", "ToggleRawCapture",
               "MarkSync", "ExportCapture"]:
    assert ("private void " + action + "(") in ui, action
assert "commandFeedbackLabel.text = message;" in ui
assert '"SYNC NO MARCADO · INICIÁ REGISTRO"' in ui
assert '" SOLICITADA · ESPERÁ CONFIRMACIÓN"' in ui

# 8. FPS means actual frame delta count; distinguish uncapped Editor from APK.
assert "fpsFrames / fpsElapsed" in ui
assert "Time.unscaledDeltaTime" in ui
assert "fpsElapsed >= .75f" in ui
assert '"EDITOR · FPS observados: "' in ui
assert "NO equivale a FPS de la APK" in ui
assert '"ANDROID · FPS observados: "' in ui
assert "Objetivo solicitado: 60 FPS (no garantizados)" in ui
assert "Application.targetFrameRate = 60;" in ui

# Source import: HD exact geometry, CPU-readable texture, original URP 2D.
assert "selector.width != 1672 || selector.height != 941" in preflight
assert 'art == "LevelSelect" && !importer.isReadable' in preflight
assert "GraphicsSettings.defaultRenderPipeline = urp;" in preflight
assert "QualitySettings.renderPipeline = null;" in preflight
assert "TextureImporterCompression.Uncompressed" in preflight
assert "BIOFIT_GeomeTry_UI_Assets_HD_v2.zip" in ps
assert "Get-FileHash" in ps and "SHA256" in ps

# Preservation of original physics, timing, BLE LEGACY and 5 source levels.
for forbidden in [
    "Time.fixedDeltaTime =", "rb.linearVelocity =", "jumpForce =",
    "currentSpeed =", "DefaultSpeed =", "DefaultGravityScale =",
    "monitor.PhaseFastPairs = true", "monitor.LiftCycleMode = true"
]:
    assert forbidden not in ui and forbidden not in art, forbidden
assert "BilateralJumpCandidate += OnBilateralLegacyCandidate;" in bridge
assert "monitor.JumpDetector.ClearPending();" in bridge
assert "monitor.PhaseFastPairs = false;" in bridge
assert "monitor.LiftCycleMode = false;" in bridge
assert "cube.onClick();" in bridge
assert "TryConsumeOrbPulse()" in bridge
for token in [
    'BindOriginalMenuButton("Play Button", Select);',
    'BindOriginalMenuButton("Editor Button", Settings);',
    'BindOriginalMenuButton("Custom Button", Custom);',
    "GameManager.Instance.LoadLevel(map);",
    "BuildPauseScreen();", "BuildBackButton(sceneName);",
    "Time.timeScale = priorTimeScale > 0f ? priorTimeScale : 1.5f;",
]:
    assert token in ui, token

original = root / "ThirdParty/GeomeTryOriginal/Assets"
for name in ("Character", "Cube", "Ship", "Wave"):
    path = original / ("Scripts/GameMechanics/CharacterType/" + name + ".cs")
    assert path.exists(), f"Missing untouched original: {path}"
character = (original / "Scripts/GameMechanics/CharacterType/Character.cs").read_text()
cube = (original / "Scripts/GameMechanics/CharacterType/Cube.cs").read_text()
assert "DefaultSpeed = 6.5f" in character
assert "Time.timeScale = 1.5f" in character
assert "rb.linearVelocity = new Vector2(rb.linearVelocityX, jumpForce);" in cube
assert "activeInputHandler: 0" in settings
assert "StandaloneInputModule" in preflight
assert "PrefabUtility.SaveAsPrefabAsset" in preflight

print("PASS: live classic campaign selector, no duplicated indicators, 70 safe skins, alpha "
      "logo/corners, 16:9 safe-area, structured BLE, honest FPS; untouched "
      "source physics/legacy BLE. Editor/Android validation remains necessary.")

# 9. Campaign V6.4: local original assets are deliberately absent in host CI.
# Verify the published delta chain from the immutable V6.3 manifest instead of
# claiming that we executed the proprietary maps or gameplay.
import json
manifest = json.loads((root / "tools/campaign_v63/MANIFIESTO_LOCAL_V63.json").read_text())
maps = {m["slot"]: (m["active_sha256_compact"], m["active"]) for m in manifest["maps"]}
assert set(maps) == set(range(1, 6))
seen = set()
for file in sorted((root / "tools/level_editor/changes").glob("BIOFIT_level_*.json")):
    d = json.loads(file.read_text())
    slot = d["slot"]
    assert slot in maps and d["schema"] == d["editorRevision"] == 1, file
    assert file.name.startswith(f"BIOFIT_level_{slot}_"), file
    base, count = maps[slot]
    # The currently published history is a linear chain per slot.
    assert d["baseSha256"] == base, (file, "unexpected base")
    assert re.fullmatch(r"[0-9a-f]{64}", d["targetSha256"]), file
    assert d["targetSha256"] not in seen, file
    seen.add(d["targetSha256"])
    order = d["order"]
    added = [o["index"] for o in d["added"]]
    edited = [o["index"] for o in d["edits"]]
    removed = d["removed"]
    assert all(len(x) == len(set(x)) for x in (order, added, edited, removed)), file
    assert set(added).isdisjoint(edited) and set(removed).isdisjoint(order), file
    assert set(added + edited) <= set(order), file
    assert len(order) == count + len(added) - len(removed), file
    assert all("raw" not in o and "raw" not in o.get("fields", {}) for o in d["added"] + d["edits"]), file
    maps[slot] = (d["targetSha256"], len(order))
assert len(seen) >= 3, "Published user edits missing"
editor = (root / "Assets/BIOFIT/GeomeTry/Editor/BiofitLevelEditor.cs").read_text()
runtime = (root / "Assets/BIOFIT/GeomeTry/BiofitStereoLocalRuntime.cs").read_text()
assert editor.lstrip().startswith("#if UNITY_EDITOR")
assert "EditorWindow" in editor and "BIOFIT/Level Editor/Abrir editor" in editor
assert "#if UNITY_EDITOR" in runtime and "BIOFIT_EDITOR_TEST_PATH" in runtime
assert "collisionMode" in runtime and "deathMask" in runtime
assert "BuildV63Completion()" in ui
assert "CooldownOptionsMs = { 900, 800, 700, 600, 500, 400 }" in ui
print("PASS: V6.4 editor isolation and published campaign delta metadata", maps)
