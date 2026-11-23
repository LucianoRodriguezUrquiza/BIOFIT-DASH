"""Static contract for the independent experimental BIOFIT Three Modes maps.

It is NOT a physics engine, Unity Editor test, or Android/BLE end-to-end proof.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEVELS = ROOT / "Assets/BIOFIT/Resources/BiofitThreeModes"
NAMES = ["BackOnTrack", "JumpyNightmare", "Polargeist", "StereoMadness", "WaveVibe"]
EXPECTED_PORTALS = [
    [],
    [],
    [],
    [("biofitBallPortal", 180), ("biofitSpiderPortal", 380), ("biofitCubePortal", 515)],
    [("biofitBallPortal", 110), ("biofitSpiderPortal", 276), ("biofitCubePortal", 404), ("biofitBallPortal", 515)],
]
ALLOWED = {"spike", "ground", "biofitRoof", "biofitBallPortal",
           "biofitSpiderPortal", "biofitCubePortal"}
SPEED_U_S = 6.5 * 1.5
MIN_BILATERAL_S = .9
MIN_COMFORT_S = 1.5

for i, name in enumerate(NAMES, 1):
    data = json.loads((LEVELS / (name + ".json")).read_text("utf-8"))
    assert data["id"] == i, (name, data["id"])
    items = data["map"]
    assert all(it["type"] in ALLOWED for it in items)
    assert len([it for it in items if it["type"] == "ground" and it["x"] == 575]) == 1
    assert (sum(it["type"] == "biofitRoof" for it in items) == int(i != 1))
    portals = [(it["type"], it["x"]) for it in items if it["type"].endswith("Portal")]
    assert portals == EXPECTED_PORTALS[i - 1], (name, portals)
    spikes = [it for it in items if it["type"] == "spike"]
    xs = [it["x"] for it in spikes]
    assert xs == sorted(xs)
    assert len(xs) >= 8
    assert xs[0] >= 25 and xs[-1] <= 565
    gaps = [b - a for a, b in zip(xs, xs[1:])]
    assert all(dx / SPEED_U_S >= MIN_COMFORT_S for dx in gaps)
    if i == 1:
        assert all(it["y"] == 0 for it in spikes)
    elif i in (2, 3):
        assert [it["y"] for it in spikes] == [0 if j % 2 == 0 else 7 for j in range(len(spikes))]
        assert all(it.get("rotation", 0) == (180 if it["y"] == 7 else 0) for it in spikes)
    for typ, x in portals:
        assert all(abs(x - s) >= 15 for s in xs), (name, typ, x)
    nominal = (576 - (-10)) / SPEED_U_S
    assert 59 <= nominal <= 61
    print(f"L{i} {name}: {len(spikes)} spikes, {len(portals)} portals, "
          f"min-gap={min(gaps)/SPEED_U_S:.2f}s, nominal={nominal:.1f}s")

bridge = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text("utf-8")
motor = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs").read_text("utf-8")
ux = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text("utf-8")
build = (ROOT / "Assets/BIOFIT/GeomeTry/Editor/BiofitThreeModesBuild.cs").read_text("utf-8")
assert "BilateralJumpCandidate += OnBilateralLegacyCandidate" in bridge
assert "cube.onClick();" in bridge
assert "motor.TryPulse()" in bridge
assert "Body" not in ""  # Intentional: no fabricated claim of runtime physical validation.
assert "body.gravityScale = flipped ? -BallSpiderGravity : BallSpiderGravity;" in motor
assert "Physics2D.Raycast(" in motor
assert "body.position = destination;" in motor
assert "BiofitThreeModes/" in ux
assert "com.biofitgame.threemodeslab" in build and "com.biofitgame.threemodeslab" in motor
assert "BiofitGeomeTryOriginalBuild.Verify()" in build
print("PASS: 5 maps + mode portal sequence + conserved cube/legacy paths (STATIC ONLY)")
