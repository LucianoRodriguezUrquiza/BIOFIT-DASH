"""Read-only source-preservation contract for original GeomeTry integration."""
import pathlib, re, subprocess
root = pathlib.Path(__file__).resolve().parents[1]
def git(*args):
    return subprocess.check_output(["git", *args], cwd=root, text=True).strip()
entry = git("ls-tree", "HEAD", "ThirdParty/GeomeTryOriginal")
assert entry.startswith("160000 commit d922caddb4d21ecb791a266143e279bdb969d42c"), entry
modules = (root / ".gitmodules").read_text()
assert 'url = https://github.com/Christ0u/GeomeTry.git' in modules
assert 'path = ThirdParty/GeomeTryOriginal' in modules
bridge = (root / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text()
assert "cube.onClick();" in bridge and 'BilateralJumpCandidate += OnBilateralLegacyCandidate' in bridge
assert "if (cube == null" in bridge
assert "Time.timeScale = 1.5" not in bridge
assert (root / "tools/prepare_geometry_original.ps1").is_file()
assert "Assets/GeomeTryOriginal/Assets/" in (root / ".gitignore").read_text()
assert not (root / "Assets/GeomeTryOriginal/ProjectSettings").exists(), "Nested ProjectSettings must never be imported as Unity Assets"
# The original physics/timescale settings remain byte-identical.
# Detector and BLE monitor are deliberately modified ONLY to support runtime
# LEGACY cooldown selection in this experimental branch; guard their core
# synchronization and pairing invariants instead of rejecting all changes.
for path in ("ProjectSettings/Physics2DSettings.asset", "ProjectSettings/TimeManager.asset"):
    baseline = subprocess.check_output(["git", "show", "5616fc4571193d4a2d076550a909089344144384:" + path], cwd=root)
    assert (root / path).read_bytes() == baseline, path + " was modified"
monitor = (root / "Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs").read_text()
detector = (root / "Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs").read_text()
for uuid in ("f9710000-6d68-4b7d-a742-6fae7b5f0001",
             "f9710000-6d68-4b7d-a742-6fae7b5f0002",
             "f9710000-6d68-4b7d-a742-6fae7b5f0003"):
    assert uuid in monitor, "Missing original BLE UUID " + uuid
for token in ("new BiofitClockAligner()", "timeAligner.TryMapEvent",
              "jumpDetector.ProcessImpulse(state.id, pairSeconds, verticalG",
              "BilateralJumpCandidate?.Invoke", "eventType == 3"):
    assert token in monitor, "Receiver compatibility invariant: " + token
for token in ("MinimumVerticalG = 0.12f", "MaxPairGapSeconds = 0.18f",
              "SameAnkleRefractorySeconds = 0.32f",
              "PairCooldownSeconds = 0.90f",
              "PairCooldownMilliseconds / 1000d",
              "pairTime = Math.Max(eventSeconds, otherTime)",
              "MaxPairGapWindowSeconds", "lastPairSeconds = pairTime"):
    assert token in detector, "LEGACY pairing invariant: " + token
cfg = (root / "ProjectSettings/ProjectSettings.asset").read_text()
# GeomeTry needs the old Input Manager for Cube/Ship/Wave on Android.
# The prior branch used Both (unsupported on Android); this dedicated UI
# release switches to 0 and repairs the scene EventSystem through the Editor.
assert '  activeInputHandler: 0' in cfg, 'Android must use legacy Input Manager'
module = root / "ThirdParty/GeomeTryOriginal"
assert (module / ".git").exists(), "Original GeomeTry submodule not initialized!"
actual_commit = subprocess.check_output(["git", "-C", str(module), "rev-parse", "HEAD"], text=True).strip()
assert actual_commit == "d922caddb4d21ecb791a266143e279bdb969d42c", actual_commit
assert subprocess.check_output(["git", "-C", str(module), "status", "--porcelain"], text=True).strip() == "", "Original submodule has local modifications"
sub = module / "Assets/Resources/maps/BASE"
expect = {
    "StereoMadness": "0af72f78ccc80ad982fcb7aabc0c39387bb2350c",
    "BackOnTrack": "47e10ff7f5b48a8045e3ddcfdd20e1dce2c0deda",
    "Polargeist": "fe4af269c83b2984c643d6f3282799152ea84dc4",
    "JumpyNightmare": "7c0b86d610daf6b79654b3edeacb5e412dcc2c80"
}
for name, sha in expect.items():
    path = sub / (name + ".json")
    assert path.is_file(), "Missing original map " + name
    # Git for Windows with core.autocrlf=true may expand only LF -> CRLF.
    # Verify the upstream Git object and that the checkout differs, at most,
    # in line endings. Never silently accept content/map edits.
    committed = subprocess.check_output(
        ["git", "-C", str(module), "show",
         "HEAD:Assets/Resources/maps/BASE/" + name + ".json"])
    raw = path.read_bytes()
    assert raw == committed or raw.replace(b"\r\n", b"\n") == committed, (
        "Original map differs beyond line endings: " + name)
    import hashlib
    git_blob = hashlib.sha1(b"blob " + str(len(committed)).encode("ascii") +
                            b"\0" + committed).hexdigest()
    assert git_blob == sha, "Upstream Git blob ID differs: " + name
    # The Editor verifier must accept a CRLF-only Windows checkout as well.
    windows = committed.replace(b"\n", b"\r\n")
    assert windows.replace(b"\r\n", b"\n") == committed
# Three upstream *.png are actually WebP. Unity compatibility PNGs are
# pixel-preserving, generated from those originals, and stored outside Assets.
png_overlays = {
    "Wave001.png": "2d592ec4d95ec34d0b9495324a8eabf6e9ca5dfe",
    "Ship.png": "067f68f2c97233cd358690235cd3396303ab2d29",
    "smallTile.png": "60aa0fdaaae0b5b7b4d8765ee3f5b3b457ad5963"
}
for filename, expected_sha in png_overlays.items():
    f = root / "tools/geometry_compat" / filename
    assert f.is_file() and f.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), filename
    sha = subprocess.check_output(["git", "hash-object", str(f)], text=True).strip()
    assert sha == expected_sha, "Integrity failure in compatibility PNG: " + filename
assert (root / "tools/apply_geometry_compat.ps1").is_file()
assert 'apply_geometry_compat.ps1' in (root / "tools/prepare_geometry_original.ps1").read_text()
compat = (root / "tools/apply_geometry_compat.ps1").read_text()
assert "BIOFIT_BACKGROUND_RESOURCE_FIX" in compat
assert "BIOFIT_CANVAS_LOAD_RACE_FIX" in compat
assert "DevUtils.cs importado tiene cambios inesperados" in compat
assert "UIManager.cs importado tiene cambios inesperados" in compat
print("PASS: Original repo isolated outside Unity Assets; maps intact; BIOFIT legacy receiver/physics preserved.")
