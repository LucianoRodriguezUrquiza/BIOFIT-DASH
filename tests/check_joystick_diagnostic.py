#!/usr/bin/env python3
"""Lightweight contract for a non-invasive Unity legacy-input HID diagnostic."""
from pathlib import Path

root = Path(__file__).resolve().parent.parent
ux = (root / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text(encoding="utf-8")
settings = (root / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")
assert "activeInputHandler: 0" in settings, "Project uses legacy Input Manager; don't switch input backend."
assert '"JOYSTICK"' in ux and "BuildJoystickDiagnostics()" in ux
assert "RefreshJoystickDiagnostics()" in ux
assert "Input.GetJoystickNames()" in ux
assert "MODO JUEGO COMPROBADO: CAMBIO + C." in ux, "The real VR BOX uses MODE+C, not MODE+B"
assert "Input.anyKeyDown" in ux and "Input.GetKeyDown(code)" in ux
assert 'Input.GetAxisRaw("Horizontal")' in ux
assert 'Input.GetAxisRaw("Vertical")' in ux
assert 'Input.GetAxisRaw("BIOFIT VR Stick X")' in ux
assert 'Input.GetAxisRaw("BIOFIT VR Stick Y")' in ux
axes = (root / "ProjectSettings/InputManager.asset").read_text(encoding="utf-8")
assert "m_Name: BIOFIT VR Stick X" in axes and "m_Name: BIOFIT VR Stick Y" in axes
assert "m_Name: BIOFIT VR Stick X" in axes and "type: 2" in axes

assert "if (scene == Settings && (cooldownPage || joystickPage))" in ux
assert 'new Vector2(.820f, .078f)' in ux
assert '"COOLDOWN"' in ux and "CooldownOptionsMs = { 900, 800, 700, 600, 500, 400 }" in ux
assert 'Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)' not in ux, "Do not add gameplay input to UX diagnostic."
assert '"BIOFIT JOYSTICK DIAGNOSTIC (Unity KeyCode): "' in ux
print("PASS joystick diagnostic UI, legacy key scan, navigation, cooldown preservation")

# User explicitly approved publishing only Skins.png from local verified ZIP.
ignore = (root / ".gitignore").read_text(encoding="utf-8")
assert "!/Assets/BIOFIT/Resources/BiofitUi/Skins.png" in ignore
installer = (root / "tools/install_biofit_ui.ps1").read_text(encoding="utf-8")
assert "[switch]$PublicarSkins" in installer
assert "'feat/vr-joystick-input-diagnostics'" in installer
assert "push origin $expectedBranch" in installer
assert "add -- $relative" in installer
print("PASS optional Skins.png-only upload contract")
