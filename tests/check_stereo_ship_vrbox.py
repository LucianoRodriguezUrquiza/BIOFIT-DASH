"""Static contracts for VR BOX held actions, Ship mode and Stereo video map.

Does not replace Unity Editor compilation or physical Android/BLE gameplay.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
src = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs").read_text(encoding="utf-8")
bridge = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text(encoding="utf-8")
ui = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text(encoding="utf-8")
build = (ROOT / "Assets/BIOFIT/GeomeTry/Editor/BiofitThreeModesBuild.cs").read_text(encoding="utf-8")
data = json.loads((ROOT / "Assets/BIOFIT/Resources/BiofitThreeModes/BackOnTrack.json").read_text(encoding="utf-8"))
assert data["id"] == 1 and data["name"] == "Nivel 1"
assert data["music"] == "StereoMadness"
assert data["map"][-1]["x"] == 825
assert data["map"][-1]["type"] == "ground"
assert sum(it["type"] == "biofitShipPortal" for it in data["map"]) == 2
assert sum(it["type"] == "biofitCubePortal" for it in data["map"]) == 2
assert sum(it["type"] == "biofitStereoCoin" for it in data["map"]) == 3
assert len(data["map"]) >= 400
assert all(it["x"] >= 0 and it["x"] <= 825 for it in data["map"])

# Held OR: A/R1=0+13, B/R2=0, C=13, D=1. No separate
# KeyDown -> action for either member of the combined 0+13 pair.
for code in ("JoystickButton0", "JoystickButton1", "JoystickButton13"):
    assert "Input.GetKey(KeyCode." + code + ")" in src
assert "Input.GetJoystickNames()" in src
assert "name.IndexOf(\"VR BOX\"" in src
assert "BiofitVrBoxInput.AnyHeld" in src
assert "BiofitVrBoxController" in src
assert "cube.onClick();" in src
assert 'KeyPressed.SetValue(cube, true);' in src
assert 'KeyPressed.SetValue(cube, prior);' in src
assert "GameManager.Instance.PlayMode" in src
assert "cube.isAlive" in src

# Ship must be a fourth mode of the same avatar. Gravity
# is held; discrete BLE pairs create finite thrust pulses.
for token in (
    "public enum BiofitMode { Cube, Ball, Spider, Ship }",
    'Register("biofitShipPortal", BiofitMode.Ship);',
    "private const float ShipGravity = 2.5f;",
    "private const float ShipMaxVerticalSpeed = 10f;",
    "private const float ShipMaxAngle = 40f;",
    "body.gravityScale = held ? -ShipGravity : ShipGravity;",
    "Time.unscaledTime < shipBleHeldUntil",
    "body.linearVelocity = new Vector2(body.linearVelocity.x, vy);",
    "shipBleHeldUntil = Mathf.Max(shipBleHeldUntil,",
    "if (Mode == BiofitMode.Ship)",
    "biofitStereoCoin",
    "BiofitStereoCoinPickup",
):
    assert token in src, token
assert "motor.TryPulse();" in bridge
assert "BilateralJumpCandidate += OnBilateralLegacyCandidate" in bridge
assert "cube.onClick();" in bridge
assert 'if (motor != null && motor.Mode != BiofitMode.Cube)' in bridge
assert '"CUBO / NAVE"' in ui
assert '"MONEDAS " + BiofitStereoCoinPickup.Collected + " / 3"' in ui
assert 'stereoCoinHudLabel = null;' in ui
assert 'gameObject.SetActive(false);' in src and 'coin.gameObject.SetActive(true);' in src
assert 'if (id == 1) BiofitStereoCoinPickup.ResetRun();' in src

assert "(i == 0 ? 825 : 575)" in build
assert '"biofitShipPortal", "biofitStereoCoin"' in build
assert "BiofitGeomeTryOriginalBuild.Verify();" in build

# Reproducible video-derived design source, not a claimed
# exact original GMD/GD level object-string.
generator = ROOT / "tools/assets/generate_stereo_video_map.js"
assert generator.is_file()
source = generator.read_text(encoding="utf-8")
assert "NOT original serialized" in source
print("PASS: all-button VR BOX hold, Ship gravity/BLE pulse, Stereo map/coin structure (static)")
