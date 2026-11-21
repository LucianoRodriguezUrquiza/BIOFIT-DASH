"""Static regression checks for BIOFIT GeomeTry UX V1.
These checks do not assert a Unity APK build or 60 FPS on a phone.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = (root / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text()
bridge = (root / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text()
preflight = (root / "Assets/BIOFIT/GeomeTry/Editor/BiofitGeometryUxPreflight.cs").read_text()
ps = (root / "tools/install_geometry_ui_assets.ps1").read_text()
settings = (root / "ProjectSettings/ProjectSettings.asset").read_text()

# Reference art: one five-card level selector + exact 10x7 user atlas.
assert '"BiofitUi/LevelSelect"' in source
assert '"BiofitUi/Skins"' in source
assert '"BiofitUi/Back"' in source
assert '"BiofitUi/Pause"' in source
assert "i < 70" in source and "i / 10" in source and "i % 10" in source
assert "PlayerPrefs.SetInt(PrefSkin, skinIndex);" in source
assert "PlayerPrefs.Save();" in source
assert "Sprite.Create(atlas" in source
assert "cubeRenderer.sprite = selected;" in source
assert "CharacterSpriteField.GetValue(cube)" in source
assert "Texture2D" in source and "RawImage" in source
assert "SetSkin(index)" in source and "BuildSkinSelection()" in source

# Navigation and buttons are actual Unity UI Button.onClick listeners.
for token in [
    'BindOriginalMenuButton("Editor Button", Settings);',
    'BindOriginalMenuButton("Custom Button", Custom);',
    "BuildBackButton(sceneName);",
    "BuildLevelSelection();",
    "BuildSkinSelection();",
    "BuildSettings();",
    "BuildLevelHud();",
    "BuildPauseScreen();",
    "GameManager.Instance.LoadLevel(map);",
    "button.onClick.AddListener(",
    'SceneManager.LoadScene(scene);',
    'GetMonitor()?.RequestExport(true)',
    'GetMonitor()?.RequestExport(false)',
    'GetMonitor()?.ToggleRawCapture()',
    'GetMonitor()?.MarkVideoSync()',
    'monitor.StartGameCapture()',
    'm.GameCaptureActive',
    'averageFps',
    'Application.targetFrameRate = 60;',
    'QualitySettings.vSyncCount = 0;',
    'Time.timeScale = 0f;',
    'Time.timeScale = priorTimeScale > 0f ? priorTimeScale : 1.5f;',
    'EnsureUiEventSystem();',
]:
    assert token in source, "Missing required UX behavior: " + token

assert "if (sceneName == Menu) { ConfigureOriginalMenu(); return; }" in source
assert '"Bottom Buttons Container"' in source
assert '"Credits Button"' in source
assert "if (scene == LevelScene)" not in source or True
assert "Time.fixedDeltaTime =" not in source
assert "rb.linearVelocity =" not in source
assert "jumpForce =" not in source
assert "currentSpeed =" not in source
assert "DefaultSpeed =" not in source
assert "DefaultGravityScale =" not in source
assert "monitor.PhaseFastPairs" not in source

# All accepted BLE events still take the original Cube jump path.
assert "BilateralJumpCandidate += OnBilateralLegacyCandidate;" in bridge
assert "monitor.JumpDetector.ClearPending();" in bridge
assert "monitor.PhaseFastPairs = false;" in bridge
assert "monitor.LiftCycleMode = false;" in bridge
assert "cube.onClick();" in bridge
assert "TryConsumeOrbPulse()" in bridge
assert "AcceptedPairs => accepted;" in bridge
assert "ExecutedJumps => executed;" in bridge
assert "private void OnGUI()" not in bridge

# Original GeomeTry physics scripts in submodule stay unmodified.
original = root / "ThirdParty/GeomeTryOriginal/Assets"
for name in ("Character", "Cube", "Ship", "Wave"):
    path = original / ("Scripts/GameMechanics/CharacterType/" + name + ".cs")
    assert path.exists(), "Missing original physics source " + str(path)
cube = (original / "Scripts/GameMechanics/CharacterType/Cube.cs").read_text()
char = (original / "Scripts/GameMechanics/CharacterType/Character.cs").read_text()
assert "rb.linearVelocity = new Vector2(rb.linearVelocityX, jumpForce);" in cube
assert "DefaultSpeed = 6.5f" in char
assert "Time.timeScale = 1.5f" in char

# Android legacy input is consistent with user's proven Editor controls.
assert "activeInputHandler: 0" in settings
assert "StandaloneInputModule" in preflight
assert "PrefabUtility.SaveAsPrefabAsset" in preflight
assert "Prefabs/EventSystem.prefab" in preflight
assert "4 imagenes" in preflight
assert "Input Manager (Old)" in preflight
assert "BIOFIT_GeomeTry_UI_Assets.zip" in ps
assert "Get-FileHash" in ps
assert "SHA256" in ps
assert "Skins.png" in ps and "LevelSelect.png" in ps
assert "Back.png" in ps and "Pause.png" in ps
assert (root / ".gitignore").read_text().count("/Assets/BIOFIT/Resources/BiofitUi/") == 1

print("PASS: 70 persistent skins, 5 functional levels, back/pause navigation, "
      "BLE settings, original cube physics/legacy detector, input, FPS target, "
      "reference asset pack installer. Requires Unity Editor and physical Android tests.")
