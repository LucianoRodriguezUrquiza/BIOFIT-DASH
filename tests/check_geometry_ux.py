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

# 3. One paged selector for all three modes. The exact atlas source cuts
# remain shared by UI UVs and the gameplay sprite. No lock or economy gate.
assert 'SkinColumns=10, SkinRows=3, SkinsPerPage=SkinColumns*SkinRows' in ui
assert 'int index=page*SkinsPerPage+j;if(index>=count)break;' in ui
assert 'PlayerPrefs.SetInt(PrefSkin, skinIndex);' in ui
assert 'PlayerPrefs.Save();' in ui
assert 'Sprite.Create(atlas, region' in ui
assert 'cubeRenderer.sprite = selected;' in ui
assert 'CubeSkinRegion(index)' in ui and 'raw.uvRect=new Rect(region.x/atlas.width' in ui
assert 'BiofitV65Skins.Select(SkinMode(skinTab),index)' in ui
assert 'string[] labels={"CUBO","BALL","SPIDER"}' in ui
assert 'skinPages[choice]=SkinSelected(choice)/SkinsPerPage' in ui
assert 'SkinArrow("Pagina anterior",-1,pages);SkinArrow("Pagina siguiente",1,pages);' in ui
assert 'if(index==selected)SkinFrame(tile.transform);' in ui
assert 'SkinBackground()' in ui
assert 'Resources.Load<Texture2D>("BiofitUi/VehicleMenu/vehicle_menu_bg")' in ui
assert 'bg.texture=vehicleBackground!=null?vehicleBackground:SkinBackground();' in ui
assert 'bg.raycastTarget=false;' in ui
assert 'RoundedMenuPanel("Panel de skins"' in ui
selector=ui[ui.index("private void BuildSkinSelection()"):ui.index("private void BuildSettings()")]
for excluded in ("Lock", "Shop", "Monedas", "Diamond", "Unlock", "PlayerName", "ArcadeCorners(", 'SkinDisc("Pagina"', 'Hotspot("Pagina '):
    assert excluded not in selector
for plate in ("Fondo titulo personaje","Fondo vista previa","Fondo skin actual","Fondo categoria ","Fondo individual skin","Fondo numero pagina"):
    assert 'SkinPlate("'+plate+'"' in selector
plate=ui[ui.index("private Sprite RoundedMenuSprite("):ui.index("private void SkinArrow(")]
for token in ("Image.Type.Sliced","SpriteMeshType.FullRect","roundedMenuSprites.TryGetValue","surface.raycastTarget=false","Centro opaco","Marco arcade","Brillo superior","Sombra inferior","t.enableAutoSizing=true","TextOverflowModes.Ellipsis","BiofitV2BitmapText.ConstrainToRect(t)"):
    assert token in plate,token
assert "size.x-16" in plate and "size.y-8" in plate
assert "Destroy(sprite)" in ui and "Destroy(texture)" in ui
font=(root/"Assets/BIOFIT/GeomeTry/BiofitV2BitmapText.cs").read_text()
for token in ("if(fitInk)FitVisibleInk(vh,r)","vh.PopulateUIVertex","vh.SetUIVertex","r.width-2f","r.height-2f"):
    assert token in font,token
# Actual glyph bearings can extend beyond the advance. Exercise the same
# uniform-fit and translation math on ordinary and extreme ink rectangles.
for W,H in ((50,18),(280,26),(494,38),(1000,100)):
    for lo,hi in (((-W/2-8,-H/2-3),(W/2+17,H/2+5)),((-5,-3),(12,8)),((-W,0),(3*W,4*H))):
        scale=min(1,(W-2)/max(.01,hi[0]-lo[0]),(H-2)/max(.01,hi[1]-lo[1]))
        for dim,extent in enumerate((W,H)):
            low=lo[dim]*scale;high=hi[dim]*scale
            delta=max(-extent/2+1-low,min(0,extent/2-1-high))
            assert low+delta>=-extent/2+1-1e-6 and high+delta<=extent/2-1+1e-6
assert 'RoundedMenuButtonFace(button,size,accent,true)' in ui
arrow=ui[ui.index("private void SkinArrow("):ui.index("private void BuildSkinSelection()")]
assert "backIcon" not in arrow and "ImageButton(" not in arrow
assert 'Chevron pagina' in arrow and 'btn.interactable=available;' in arrow
assert "line.raycastTarget=false" in arrow
assert 'BuildScreen(Custom)' in arrow
for count in (0,1,30,31,69,70,119):
    pages=max(1,(count+29)//30)
    assert [page*30+j for page in range(pages) for j in range(30) if page*30+j<count]==list(range(count))
# Canonical geometry: new contrast plates and navigation do not overlap.
assert .295*900+150 < .535*900-64
assert .535*900+64 < .615*900
assert .89*900-40 > .751*900+77
assert .751*900-77 > .641*900+20
assert .641*900-20 > .615*900
assert .295*900-150 > .072*900+21
assert 94<1076/10 and 84<276/3
assert 74<94-12 and 66<84-12
assert 'new Vector2(140,28)' in selector and 'new Vector2(160,128)' in selector
assert 'new Vector2(128,128)' in selector
assert 'MenuText("Titulo personajes"' in selector
assert .125*1600+38 < .5*1600-550
assert .875*1600-38 > .5*1600+550
assert "GridLayoutGroup layout" not in ui

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
# 7. Modular settings; panels and footer fit the safe stage.
settings_ui=ui[ui.index("private void BuildSettings()"):ui.index("private void BuildBmd()")]
assert 'Resources.Load<Texture2D>("BiofitUi/SettingsMenu/settings_menu_bg")' in settings_ui
for absent in ("ArcadeCorners(", "ArcadeLogo(", "BILATERAL LEGACY", "DETECCIÓN DE MOVIMIENTOS", "FPS medicion"):
    assert absent not in settings_ui
for section in ('"Conexion BLE"', '"Captura y archivos"', '"Herramientas configuracion"', '"CONEXIÓN WAVESHARE"', '"REGISTRO Y EXPORTACIÓN"', '"CONFIGURACIÓN"'):
    assert section in settings_ui
assert 'new Vector2(.18f,.54f),new Vector2(440,574)' in settings_ui
assert 'new Vector2(.5f,.54f),new Vector2(536,574)' in settings_ui
assert 'new Vector2(.82f,.54f),new Vector2(440,574)' in settings_ui
assert .18*1600+220 < .5*1600-268
assert .5*1600+268 < .82*1600-220
assert .54*900+287 < .91*900-41
assert .54*900-287 > .124*900+59
assert all(574*.135>68 for _ in range(5))
assert 'MenuText("Titulo de ajustes"' in settings_ui
assert 'size-new Vector2(26,24)' in ui
assert 508-26-16>0 and 70-24-8>0
assert 400-26-16>0 and 68-24-8>0
for token in ("StartGameCapture()", "FinishAndExportCapture()", "ToggleRawCapture()", "MarkVideoSync()", "RequestExport(saf)", "m.CaptureElapsedSeconds", "m.LeftTrackerConnected", "m.RightTrackerConnected", "m.CaptureSyncCount"):
    assert token in ui,token
monitor=(root/"Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs").read_text()
combined=monitor[monitor.index("public bool FinishAndExportCapture()"):monitor.index("public void OnBiofitExportResult(")]
assert combined.index("StopGameCapture()") < combined.index("ExportBusy=true") < combined.index("await CaptureCompletion") < combined.index("await RequestExportAsync(false,expected)")
assert "SavedCapturePath,expected,StringComparison.Ordinal" in combined
assert "FindNewestValid" not in combined
assert "ExportBusy=false" in combined
assert "exactPath??" in monitor
press = (root / "Assets/BIOFIT/GeomeTry/BiofitSettingsButtonFeedback.cs").read_text()
assert "frame.gameObject.AddComponent<BiofitSettingsButtonFeedback>()" in ui
for token in ["IPointerDownHandler", "IPointerUpHandler", "IPointerExitHandler",
              "Time.unscaledDeltaTime", "Vector3.one * .92f", "1.04f"]:
    assert token in press, token
for action in ["StartCapture", "CapturePrimaryAction", "ToggleRawCapture", "MarkSync", "ExportCapture"]:
    assert ("private void " + action + "(") in ui, action
assert "commandFeedbackLabel.text = message;" in ui
assert '"SYNC NO MARCADO · INICIÁ REGISTRO"' in ui
assert '" SOLICITADA · ESPERÁ CONFIRMACIÓN"' in ui
# FPS instrumentation remains available internally; no misleading settings card.
assert "fpsFrames / fpsElapsed" in ui
assert "Time.unscaledDeltaTime" in ui
assert "fpsElapsed >= .75f" in ui
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
assert "SkinArt" in ui and "%7" in skins
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
assert pulse.index('Time.unscaledTime - lastManualAction < .12f') < pulse.index('interaction.TryBallSpiderOrb(cube)') < pulse.index('if (!SurfaceContact(support)) return RejectPulse("NO_SUPPORT");')
assert 'lastManualAction=Time.unscaledTime;\n                        return true;' in pulse
assert 'if (Vector2.Distance(destination, body.position) < 1.0f) return RejectPulse("DESTINATION_TOO_CLOSE");' in pulse
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

# V6.5 selection transforms: GD clockwise, rigid group pivot, local/world mirror.
assert 'static Vector2 GroupPivot(Item[] items)' in editor
assert 'c*delta.x+sn*delta.y,-sn*delta.x+c*delta.y' in editor
assert 'if(mirrorX!=mirrorY)o.rotationGD=-o.rotationGD;' in editor
assert 'if(mirrorX)o.flipX=!o.flipX;' in editor and 'if(mirrorY)o.flipY=!o.flipY;' in editor
assert 'Shift(o,positions[i].x-o.xGD,positions[i].y-o.yGD);' in editor
assert 'next.x<0||next.x>data.endGD' in editor
assert 'if(group)next=new Vector2(CleanTransform(next.x),CleanTransform(next.y));' in editor
assert editor.count('DrawSelectionTransforms();')==2
assert 'Como grupo (centro común)' in editor
assert 'e.keyCode==KeyCode.Q||e.keyCode==KeyCode.E' in editor
assert 'e.shift?15f:90f' in editor
transform=editor.split('void TransformSelection(',1)[1].split('void DrawSelectionTransforms()',1)[0]
assert transform.count('Push();')==1
assert transform.index('Transformación fuera del nivel')<transform.index('Push();')
assert 'Snap(' not in transform
# Independent math checks for clockwise rigid rotation and world reflection.
import math
def rotate_gd(x,y,deg):
    a=math.radians(deg)
    return (math.cos(a)*x+math.sin(a)*y,-math.sin(a)*x+math.cos(a)*y)
assert all(abs(a-b)<1e-9 for a,b in zip(rotate_gd(30,0,90),(0,-30)))
for angle in (0,15,90,180,270):
    a,b=rotate_gd(12,7,angle)
    x,y=rotate_gd(-12,7,-angle)
    assert abs(x+a)<1e-9 and abs(y-b)<1e-9
    x,y=rotate_gd(12,-7,-angle)
    assert abs(x-a)<1e-9 and abs(y+b)<1e-9
print("PASS: selection transforms, undo and rigid reflection math")

# V6.5 bulk function editing shares the single-object list, preserves geometry,
# checks all changes first, and records one atomic history entry.
assert 'DrawSelectionFunction(selected);' in editor
assert 'EditorGUILayout.Popup("Función",shown,options)' in editor
assert 'new[]{"— Funciones distintas —"}.Concat(Kinds).ToArray()' in editor
assert 'if(next!=shown&&target>=0&&target<Kinds.Length)' in editor
bulk=editor.split('void SetSelectionFunction(',1)[1].split('void DrawSelectionFunction(',1)[0]
assert 'selected.Where(o=>o.kind!=kind)' in bulk
assert 'data.objects.Count(o=>o.kind=="coin")+changed.Length>3' in bulk
assert 'changed.Any(o=>o.frames==null||o.frames.Length==0)' in bulk
assert bulk.count('Push();')==1 and bulk.index('Máximo 3 monedas')<bulk.index('Push();')
assert 'o.kind=kind;' in bulk and 'o.collisionMode=0;' in bulk
# Distinguish field writes from equality/null checks. Keep the invariant
# and also reject compound assignments and prefix/postfix increment/decrement.
field = r"\bo\.(?:frames|xGD|yGD)\b"
field_write = re.compile(
    field + r"\s*(?:=(?!=)|[+\-*/%&|^]=|\?\?=|<<=|>>=|\+\+|--)"
    + r"|(?:\+\+|--)\s*" + field
)
for read in ("o.frames==null", "o.frames != null", "o.xGD <= 0", "o.yGD >= 0", "o.xGD == 0"):
    assert not field_write.search(read), read
for write in ("o.frames=newFrames", "o.xGD = 1", "o.yGD=2", "o.xGD += 30",
              "o.frames ??= frames", "o.yGD++", "--o.xGD"):
    assert field_write.search(write), write
assert not field_write.search(bulk), "Bulk function editing must preserve sprites and positions"
print("PASS: bulk function editing, mixed selection and atomic undo")
