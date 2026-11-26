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
known_bases = {slot: {digest: count} for slot, (digest, count) in maps.items()}
for file in sorted((root / "tools/level_editor/changes").glob("BIOFIT_level_*.json")):
    d = json.loads(file.read_text())
    slot = d["slot"]
    assert slot in maps and d["schema"] == d["editorRevision"] == 1, file
    assert file.name.startswith(f"BIOFIT_level_{slot}_"), file
    # Accept a new revision from any previously published base of this slot;
    # a cumulative re-export from an older base is also valid.
    base = d["baseSha256"]
    assert base in known_bases[slot], (file, "unpublished base")
    count = known_bases[slot][base]
    assert re.fullmatch(r"[0-9a-f]{64}", d["targetSha256"]), file
    seen.add(file.name)
    order = d["order"]
    added = [o["index"] for o in d["added"]]
    edited = [o["index"] for o in d["edits"]]
    removed = d["removed"]
    assert all(len(x) == len(set(x)) for x in (order, added, edited, removed)), file
    assert set(added).isdisjoint(edited) and set(removed).isdisjoint(order), file
    assert set(added + edited) <= set(order), file
    assert len(order) == count + len(added) - len(removed), file
    assert all("raw" not in o and "raw" not in o.get("fields", {}) for o in d["added"] + d["edits"]), file
    previous_count = known_bases[slot].get(d["targetSha256"], len(order))
    assert previous_count == len(order), file
    known_bases[slot][d["targetSha256"]] = len(order)
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

# Exercise the real publisher against a local bare Git repository; no network,
# credentials, user maps or Unity are needed.
import hashlib
import subprocess
import sys
import tempfile

def run(*args, cwd=None):
    return subprocess.run(args, cwd=cwd, text=True, capture_output=True, check=True).stdout.strip()

def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()

publisher = root / "tools/level_editor/level_delta.py"
base = '{"endGD":300,"objects":[{"index":1,"xGD":15,"raw":{"unknown":42}},{"index":2,"xGD":45}]}'
target = '{"endGD":300,"objects":[{"index":1,"xGD":30,"raw":{"unknown":42}},{"index":100001,"kind":"solid","xGD":75}],"editorRevision":1}'
delta = {"schema": 1, "slot": 1, "editorRevision": 1, "endGD": 300,
         "baseSha256": digest(base), "targetSha256": digest(target),
         "removed": [2], "added": [{"index": 100001, "kind": "solid", "xGD": 75}],
         "edits": [{"index": 1, "fields": {"xGD": 30}}], "order": [1, 100001]}
for branch in ("main", "niveles-mejorados", "v6.5-desarrollo"):
    with tempfile.TemporaryDirectory() as temp:
        tmp = Path(temp)
        remote = tmp / "lucianorodriguezurquiza/biofit-game-unity"
        remote.parent.mkdir()
        run("git", "init", "--bare", "--initial-branch=main", str(remote))
        repo = tmp / "project"
        run("git", "init", "-b", branch, str(repo))
        run("git", "config", "user.name", "BIOFIT CI", cwd=repo)
        run("git", "config", "user.email", "ci@example.invalid", cwd=repo)
        (repo / ".gitignore").write_text("Assets/\n")
        folder = repo / "tools/level_editor/changes"
        folder.mkdir(parents=True)
        (folder / "INDEX.md").write_text("# Test index\n")
        run("git", "add", ".gitignore", "tools/level_editor/changes/INDEX.md", cwd=repo)
        run("git", "commit", "-m", "fixture", cwd=repo)
        run("git", "remote", "add", "origin", str(remote), cwd=repo)
        run("git", "push", "-u", "origin", branch, cwd=repo)
        active = repo / "Assets/BIOFIT/Resources/BiofitStereoLocal/CampaignV5/1/Active.json"
        active.parent.mkdir(parents=True)
        active.write_text(base)
        file = folder / "BIOFIT_level_1_20261126_120000.json"
        file.write_text(json.dumps(delta, separators=(",", ":")))
        cmd = [sys.executable, str(publisher), "--proyecto", str(repo), "--delta", str(file)]
        assert digest(target) in run(*cmd)
        assert "OK DELTA PUBLICADO" in run(*cmd, "--publicar")
        assert active.read_text() == base
        assert run("git", "rev-parse", "HEAD", cwd=repo) == run("git", "-C", str(remote), "rev-parse", branch)
        assert set(run("git", "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD", cwd=repo).splitlines()) == {
            file.relative_to(repo).as_posix(), "tools/level_editor/changes/INDEX.md"}
        if branch != "main":
            run("git", "push", "origin", "--delete", branch, cwd=repo)
            again = folder / "BIOFIT_level_1_20261126_120001.json"
            again.write_text(file.read_text())
            denied = subprocess.run([sys.executable, str(publisher), "--proyecto", str(repo),
                                     "--delta", str(again), "--publicar"], text=True, capture_output=True)
            assert denied.returncode != 0 and "Rama remota ausente" in denied.stderr
            assert not run("git", "ls-remote", "--heads", "origin", branch, cwd=repo)
        # Delta application is separate from publication, and preserves unknown source metadata.
        assert "OK ACTIVE LOCAL" in run(*cmd, "--aplicar")
        assert active.read_text() == target
        denied = subprocess.run(cmd + ["--aplicar"], text=True, capture_output=True)
        assert denied.returncode != 0 and active.read_text() == target
print("PASS: local Git publisher main/legacy branch, selective paths, deleted-branch guard and delta reconstruction")

# V6.5: only the new editor session has development navigation; normal game remains.
session = (root / "Assets/BIOFIT/GeomeTry/BiofitV65EditorSession.cs").read_text()
interaction = (root / "Assets/BIOFIT/GeomeTry/BiofitV5Interaction.cs").read_text()
motor = (root / "Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs").read_text()
skins = motor
assert "public static class BiofitV65Skins" in motor
assert not (root / "Assets/BIOFIT/GeomeTry/BiofitV65Skins.cs").exists()
assert "global::Camera" not in session
assert "Time.timeScale=0" in session
assert session.lstrip().startswith("#if UNITY_EDITOR") and session.rstrip().endswith("#endif")
assert "BIOFIT_EDITOR_TEST_PATH" in session and "SessionState" in session
assert "BiofitV65EditorSession.Tick" in runtime and "BiofitV65EditorSession.TryTakeLaunch" in ui
assert "Vista real" in editor and "Jugar desde aquí" in editor
assert "TextAnchor.MiddleLeft" in editor and "Catalog.json" in editor
assert 'kind=="redPad"?9.5f:kind=="pinkPad"?12.5f:20f' in interaction
assert 'MiniScale=.60f, MiniSpeed=7.8f, MiniJump=10.5f' in motor
assert 'OriginalSpeed = 6.5f' in motor and 'BallSpiderGravity = 4f' in motor
assert 'if(IsMini||wasMini)' in motor and "MiniSupport()" in motor
assert '"miniPortal"' in runtime and '"normalSizePortal"' in runtime
assert "BIOFIT_V65_BALL_SKIN" in skins and "BIOFIT_V65_SPIDER_SKIN" in skins
assert "BuildV65VehicleSkins" in ui and "%7" in skins
import py_compile
py_compile.compile(str(root / "tools/level_editor/importar_arte_v65.py"), doraise=True)
print("PASS: V6.5 static editor isolation, Red Pad, Mini Cube, catalog and skin contracts")

# V6.5 catalog: categories and explicit row bounds, no implicit placement on filter change.
assert 'GUILayout.SelectionGrid(catalogCategory,CatalogCategories,2' in editor
assert 'static int CatalogCategory(Item o)' in editor
assert 'case "hazard":return 3;' in editor and 'case "coin":return 7;' in editor
assert 'GUILayoutUtility.GetRect(120,54' in editor and 'body.y+25,tw,18' in editor
assert 'GUIStyle.none' in editor and 'ResetCatalogFilter()' in editor
assert 'if(placing&&paletteIndex>=0&&paletteIndex<catalog.Count)' in editor
assert 'Mathf.Clamp(paletteIndex,0,catalog.Count-1)' not in editor
print("PASS: catalog categories, row layout and explicit selection")

# V6.5 clipboard: preview is not inserted until commit; cut keeps same-slot IDs.
assert 'case KeyCode.X:Cut();' in editor and 'case KeyCode.V:BeginFloatingPaste();' in editor
assert 'EventType.MouseUp&&e.button==0&&pasteArmed' in editor
assert 'if(inside)Guard(()=>Paste(FloatingDelta(lastWorld)))' in editor
assert 'cutClipboard&&cutSlot==slot&&added.All' in editor
assert 'if(!preserveIds)o.index=id++' in editor
assert 'Mathf.Round(delta.x/grid)*grid' in editor
assert 'CancelFloatingPaste();status="Pegado cancelado.' in editor
assert 'public static class BiofitRedPadArt' in runtime
assert 'if(o.kind=="redPad") sr.sprite=RedPadSprite' in runtime
assert 'new Color32(255,255,255,p.a)' in runtime
assert editor.count('BiofitRedPadArt.Texture(t)')==2
assert 'kind=="redPad"?9.5f:kind=="pinkPad"?12.5f:20f' in interaction
print("PASS: clipboard placement and visual-only Red Pad contract")

# V6.5 compact test controls and existing bodily orb input route.
assert 'if(!IsPreview)' in session and 'Screen.height-margin-height' in session
assert 'GUI.Button(back,"Volver al editor")' in session
assert 'GUI.Button(restart,"Reiniciar desde aquí")' in session
assert 'PRUEBA LOCAL • físicas reales' not in session
assert 'Controles reales • el inicio' not in session
assert 'VISTA REAL • sin física' in session and 'GUILayout.HorizontalSlider(pct,0,100)' in session
orb_bridge = (root / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text()
assert 'orbPulseId++;' in orb_bridge and 'orbPulseDeadline = Time.fixedTime + Time.fixedDeltaTime * 1.5f;' in orb_bridge
assert 'motor.Mode==BiofitMode.Cube && BiofitGeomeTryLegacyBridge.TryConsumeOrbPulse()' in interaction
assert 'interaction.TryInvertedOrb(cube)' in motor
print("PASS: compact editor-only test HUD and existing BLE orb dispatch contract")

# V6.5 configurable editor movement step; no level schema or runtime changes.
assert 'MovePresets={1f,5f,10f,15f,30f,60f}' in editor
assert 'EditorGUILayout.DelayedFloatField(grid' in editor
assert 'MinMoveStep=.1f, MaxMoveStep=1000f' in editor
assert '!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=MinMoveStep&&v<=MaxMoveStep' in editor
assert 'EditorPrefs.SetFloat(MoveStepPref,grid)' in editor
assert 'grid=ValidMoveStep(saved)?saved:30f' in editor
assert 'dragGrid=grid;dragging=false' in editor
assert 'Mathf.Round(dx/dragGrid)*dragGrid' in editor
assert 'Mathf.Round(delta.x/grid)*grid' in editor
assert 'snap?origin+Mathf.Round((v-origin)/grid)*grid:v' in editor
assert 'while(step*zoom<12)step*=2' in editor
assert 'float step=e.shift?1:grid' in editor
print("PASS: custom positive snap step, persistence, drag and paste contracts")

# V6.5 four optional orbs use the existing one-use input and same collision
# zone; the original yellow/pad branch and BLE bridge remain in place.
for i, kind in enumerate(("pinkOrb","redOrb","blueOrb","greenOrb"),900002):
    assert f'value=="{kind}"' in interaction
    assert f'Template({i},"{kind}",30,30,new[]{{"ring_01"}},3,12)' in editor
    assert f'case "{kind}":' in editor
assert 'private bool IsOrb => IsOrbKind(kind)' in interaction
assert 'if(!IsOrb) Activate(other)' in interaction
assert 'used==cube' in interaction and 'used=cube;' in interaction
assert 'PinkOrbImpulse=12.5f, RedOrbImpulse=9.5f, GreenOrbImpulse=13.5f' in interaction
assert 'motor.SetCubeGravity(invert);' in interaction
assert 'kind=="greenOrb"?(invert?1f:-1f)*GreenOrbImpulse:0f' in interaction
assert 'kind=="pinkOrb"?PinkOrbImpulse:RedOrbImpulse' in interaction
assert 'hit.size=BiofitV5Interaction.IsOrbKind(o.kind)?new Vector2(1.05f,1.05f)' in runtime
assert 'if(!motor.IsCubeMode && !(motor.IsBallSpiderMode && (IsPad||IsOrb))) return;' in interaction
assert 'if(motor==null || !motor.IsCubeMode) return;' in interaction
assert 'else if(BiofitOrbArt.IsVariant(o.kind)) sr.sprite=ColoredOrbSprite' in runtime
assert editor.count('BiofitOrbArt.Texture(t,o.kind)')==2
flm = (root / "Assets/BIOFIT/GeomeTry/Editor/BiofitEditorFlm.cs").read_text()
assert 'BiofitOrbArt.Texture(t,o.kind)' in flm and 'colored?o.kind+":"+f:f' in flm
print("PASS: V6.5 optional colored orbs, gravity direction, activation and art contracts")

# V6.5: Ball/Spider use the same optional pad/orb effect and one accepted
# action. The orb takes priority before checking support; the automatic
# pad path never consumes a BLE pulse. Size portals and Ship stay excluded.
assert 'private bool IsPad => kind=="pad" || kind=="pinkPad" || kind=="redPad" || kind=="gravityPad"' in interaction
assert 'public bool IsBallSpiderMode => Mode==BiofitMode.Ball || Mode==BiofitMode.Spider' in motor
assert 'if(!motor.IsCubeMode && !(motor.IsBallSpiderMode && (IsPad||IsOrb))) return;' in interaction
assert 'if(!IsOrb) Activate(other)' in interaction
assert 'if(motor==null || !motor.IsCubeMode) return;' in interaction
assert 'public bool TryBallSpiderOrb(Cube cube)' in interaction
assert 'var distance=trigger.Distance(col);' in interaction
assert 'if(!distance.isValid || distance.distance>0f) return false;' in interaction
assert 'if(!IsOrb || cube==null || used==cube || !cube.isAlive) return false;' in interaction
assert 'else motor.SetBallSpiderGravity(invert);' in interaction
assert 'motor.SetBallSpiderGravity(!motor.StereoInverted);' in interaction
assert 'kind=="greenOrb"?(invert?1f:-1f)*GreenOrbImpulse:0f' in interaction
assert 'public void SetBallSpiderGravity(bool inverted)' in motor
gravity_method=motor.split('public void SetBallSpiderGravity(bool inverted)',1)[1].split('public bool StereoInverted',1)[0]
assert 'body.gravityScale=flipped?-BallSpiderGravity:BallSpiderGravity;' in gravity_method
assert 'if(Mode==BiofitMode.Spider) icon.flipY=flipped;' in gravity_method
assert 'SetMode(' not in gravity_method and 'body.position' not in gravity_method
pulse=motor.split('public bool TryPulse()',1)[1].split('private IEnumerator GravityFlipEffect',1)[0]
assert pulse.index('Time.unscaledTime - lastManualAction < .12f') < pulse.index('interaction.TryBallSpiderOrb(cube)') < pulse.index('if (!SurfaceContact(support)) return false;')
assert 'lastManualAction=Time.unscaledTime;\n                        return true;' in pulse
assert 'if (Vector2.Distance(destination, body.position) < 1.0f) return false;' in pulse
assert 'body.position = destination;' in pulse and 'StartCoroutine(SpiderEffect(oldPosition, transform.position));' in pulse
assert 'bool applied = motor.TryPulse();' in orb_bridge
print("PASS: Ball/Spider pad and orb dispatch, actual contact, priority and preserved teleport contracts")

# Editor-only analytic trajectory, selected single pad/orb, no runtime writes.
assert 'showTrajectory && selection.Count==1 && !HasFloatingPaste' in editor
assert 'IsTrajectoryObject(selected[0])' in editor
assert 'TrajectorySpeed=6.5f, TrajectoryGravityScale=4f, TrajectoryGD=30f' in editor
assert 'private const float OriginalSpeed = 6.5f;' in motor
assert 'private const float BallSpiderGravity = 4f;' in motor
assert 'Physics2D.gravity.y' in editor
assert 'm_Gravity: {x: 0, y: -9.81}' in (root / "ProjectSettings/Physics2DSettings.asset").read_text()
assert 'speed*time,vy*time+.5f*acceleration*time*time' in editor
assert '2f*TrajectoryImpulse(kind)/gravity' in editor
assert 'float acceleration=(flips?side:-side)*gravity' in editor
assert 'BiofitV5Interaction.PinkOrbImpulse' in editor and 'BiofitV5Interaction.RedOrbImpulse' in editor and 'BiofitV5Interaction.GreenOrbImpulse' in editor
assert 'case "gravityPad":case "blueOrb":return 0f;' in editor
assert '"Cube / Ball / Spider"' in editor and '"Mini Cube"' in editor
assert 'BiofitModeMotor.MiniSpeed' in editor and 'BiofitModeMotor.MiniScale' in editor
assert 'trajectoryGravityTime,.25f,2f' in editor
assert 'finally {GUI.matrix=saved;}' in editor
# Independent numerical checks of the documented ideal model.
import math
for v, expected_h, expected_range in ((20,5.10,6.63),(12.5,1.99,4.14),(9.5,1.15,3.15)):
    g=9.81*4
    t=2*v/g
    assert abs(v*t-.5*g*t*t)<1e-9
    assert abs(v*v/(2*g)-expected_h)<.01
    assert abs(6.5*t-expected_range)<.01
    assert 7.8*t>6.5*t
    for side in (-1,1):
        assert abs(side*v*t-.5*side*g*t*t)<1e-9
assert math.isclose(.5*g,19.62)
assert math.isclose(13.5+.5*g,33.12)
print("PASS: selected-only trajectory guide and ideal kinematics")
