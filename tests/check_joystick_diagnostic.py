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
assert "Input.anyKeyDown" in ux and "Input.GetKeyDown(code)" in ux
assert 'Input.GetAxisRaw("Horizontal")' in ux
assert 'Input.GetAxisRaw("Vertical")' in ux
assert "if (scene == Settings && (cooldownPage || joystickPage))" in ux
assert 'new Vector2(.820f, .078f)' in ux
assert '"COOLDOWN"' in ux and "CooldownOptionsMs = { 900, 800, 700, 600, 500, 400 }" in ux
assert 'Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)' not in ux, "Do not add gameplay input to UX diagnostic."
assert '"BIOFIT JOYSTICK DIAGNOSTIC (Unity KeyCode): "' in ux
print("PASS joystick diagnostic UI, legacy key scan, navigation, cooldown preservation")
