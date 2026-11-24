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
    [("biofitBallPortal", 185), ("biofitSpiderPortal", 385), ("biofitCubePortal", 515)],
    [("biofitBallPortal", 117), ("biofitSpiderPortal", 275), ("biofitCubePortal", 405), ("biofitBallPortal", 515)],
]
ALLOWED = {"spike", "ground", "biofitRoof", "biofitBallPortal",
           "biofitSpiderPortal", "biofitCubePortal",
           "biofitDecoPulse", "biofitDecoCrystal", "biofitDecoStar"}
SPEED_U_S = 6.5 * 1.5
MIN_BILATERAL_S = .9
MIN_COMFORT_S = 1.5

for i, name in enumerate(NAMES, 1):
    data = json.loads((LEVELS / (name + ".json")).read_text("utf-8"))
    assert data["id"] == i and data["name"] == f"Nivel {i}", (name, data["id"], data["name"])
    items = data["map"]
    if i == 1:
        # The first map was deliberately replaced: complete ~85-second
        # video-guided Cube/Ship phase layout with 3 optional coins.
        allowed_stereo = ALLOWED | {"tile", "smallTile",
                                    "biofitShipPortal", "biofitStereoCoin"}
        assert all(it["type"] in allowed_stereo for it in items)
        assert [it["x"] for it in items] == sorted(it["x"] for it in items)
        assert items[-1]["type"] == "ground" and items[-1]["x"] == 825
        assert [(it["type"], it["x"]) for it in items
                if it["type"].endswith("Portal")] == [
            ("biofitShipPortal", 245), ("biofitCubePortal", 381),
            ("biofitShipPortal", 724), ("biofitCubePortal", 786)
        ]
        assert sum(it["type"] == "biofitStereoCoin" for it in items) == 3
        assert sum(it["type"] == "spike" for it in items) >= 100
        assert sum(it["type"] in ("tile", "smallTile") for it in items) >= 280
        solid = {(it["x"], it["y"]) for it in items if it["type"] == "tile"}
        assert all((it["x"], it["y"]) not in solid for it in items
                   if it["type"] == "spike")
        nominal = (825 - (-10)) / SPEED_U_S
        assert 85 < nominal < 87, nominal
        print(f"L1 video Stereo: {len(items)} objects, two ship phases, "
              f"3 coins, {nominal:.1f}s, static only")
        continue
    assert all(it["type"] in ALLOWED for it in items)
    assert len([it for it in items if it["type"] == "ground" and it["x"] == 575]) == 1
    assert data["difficulty"] == i
    assert (sum(it["type"] == "biofitRoof" for it in items) == int(i != 1))
    portals = [(it["type"], it["x"]) for it in items if it["type"].endswith("Portal")]
    assert portals == EXPECTED_PORTALS[i - 1], (name, portals)
    spikes = [it for it in items if it["type"] == "spike"]
    xs = [it["x"] for it in spikes]
    assert xs == sorted(xs)
    assert len(xs) >= 8
    assert xs[0] >= 25 and xs[-1] <= 565
    gaps = [b - a for a, b in zip(xs, xs[1:])]
    assert all(dx >= 15 and dx / SPEED_U_S >= MIN_COMFORT_S for dx in gaps)
    if i == 1:
        assert all(it["y"] == 0 for it in spikes)
    elif i in (2, 3):
        assert [it["y"] for it in spikes] == [0 if j % 2 == 0 else 7 for j in range(len(spikes))]
        assert all(it.get("rotation", 0) == (180 if it["y"] == 7 else 0) for it in spikes)
    for typ, x in portals:
        assert all(abs(x - s) >= 15 for s in xs), (name, typ, x)
    # Symbolic lane/walkthrough check. This does not model collision geometry.
    mode = "Ball" if i == 2 else "Spider" if i == 3 else "Cube"
    surface = 0
    actions = []
    for item in sorted(items, key=lambda obj: obj["x"]):
        typ, x = item["type"], item["x"]
        if typ.endswith("Portal"):
            mode = typ.removeprefix("biofit").removesuffix("Portal")
            # Runtime transitions reset gravity and settle onto ground well
            # ahead of the next spike in the manually spaced maps.
            surface = 0
        if typ != "spike":
            continue
        if mode == "Cube":
            assert item["y"] == 0, (name, x, "ceiling spike in cube section")
            # Nominal launch 3u before spike; collider-aware envelope below.
            actions.append(x - 3)
        else:
            if item["y"] == surface:
                actions.append(x - 12)
                surface = 7 if surface == 0 else 0
    # Continuous-time envelope on level, not a Unity/Box2D simulation.
    vy = 13.5 * 1.5
    gravity_real = 9.81 * 4 * 1.5 ** 2
    flight_s = 2 * vy / gravity_real
    horizontal_flight = SPEED_U_S * flight_s
    assert 4.3 < horizontal_flight < 4.6
    for spike in spikes:
        x = spike["x"]
        is_cube = i == 1 or (
            i == 4 and (x < 185 or x > 515)) or (
            i == 5 and (x < 117 or 405 < x < 515))
        if not is_cube:
            continue
        launch = x - 3.0
        # Approximate overlap region: half-width player .5, hazard .125.
        for boundary in (x - .625, x + .625):
            t = (boundary - launch) / SPEED_U_S
            elevation = vy * t - .5 * gravity_real * t * t
            assert 0 < t < flight_s and elevation > .65, (name, x, t, elevation)
    action_gaps = [(b - a) / SPEED_U_S for a, b in zip(actions, actions[1:])]
    assert all(dt >= MIN_COMFORT_S for dt in action_gaps), (name, action_gaps)
    if i >= 2:
        assert len(actions) >= 7, (name, "not enough mode actions")
    nominal = (576 - (-10)) / SPEED_U_S
    assert 59 <= nominal <= 61
    assert 6 <= sum(it["type"].startswith("biofitDeco") for it in items) <= 12
    print(f"L{i} {name}: {len(spikes)} spikes, {len(portals)} portals, "
          f"min-gap={min(gaps)/SPEED_U_S:.2f}s, nominal={nominal:.1f}s")

bridge = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text("utf-8")
motor = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs").read_text("utf-8")
ux = (ROOT / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text("utf-8")
build = (ROOT / "Assets/BIOFIT/GeomeTry/Editor/BiofitThreeModesBuild.cs").read_text("utf-8")
assert "BilateralJumpCandidate += OnBilateralLegacyCandidate" in bridge
assert "cube.onClick();" in bridge
assert "motor.TryPulse()" in bridge
assert "body.gravityScale = flipped ? -BallSpiderGravity : BallSpiderGravity;" in motor
assert "Physics2D.Raycast(" in motor
assert "body.position = destination;" in motor
assert "BiofitThreeModes/" in ux
assert "com.biofitgame.threemodeslab" in build and "com.biofitgame.threemodeslab" in motor
assert "BiofitGeomeTryOriginalBuild.Verify()" in build
print("PASS: 5 named maps + >=1.5 s nominal spike/action spacing + mode transitions (STATIC ONLY)")
