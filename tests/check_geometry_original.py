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
for path in ("ProjectSettings/Physics2DSettings.asset", "ProjectSettings/TimeManager.asset",
             "Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs"):
    baseline = subprocess.check_output(["git", "show", "5616fc4571193d4a2d076550a909089344144384:" + path], cwd=root)
    assert (root / path).read_bytes() == baseline, path + " was modified"

# The MOV/STATUS taps in BiofitBLEMonitor are the only permitted additive
# changes to the BLE receiver. Strip these exact additions and require a
# byte-for-byte match with v0.9.11: UUIDs, thresholds, event sequencing,
# 900ms bilateral detector and BLE data collection MUST remain untouched.
monitor_path = "Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs"
baseline = subprocess.check_output(["git", "show",
    "5616fc4571193d4a2d076550a909089344144384:" + monitor_path], cwd=root)
actual = (root / monitor_path).read_text()
allowed = [
    ("""
    // Motion telemetry is only published here. Individual game modes decide
    // how to interpret it; the BLE receiver has NO dependency on GeomeTry.
    // Callbacks run on Unity's main thread from ReadStatus/ReadEvent.
    public event Action<byte, byte, uint, double> MotionStatusReceived;
    public event Action<byte, bool, uint, double> MotionTransitionReceived;
    public event Action MotionEpochReset;
""", ""),
    ("""    private void InvalidateControl() {
        liftPairs.Clear(); jumpDetector.ClearPending(); ControlEpoch++;
        MotionEpochReset?.Invoke();
    }""",
     "    private void InvalidateControl() {liftPairs.Clear();jumpDetector.ClearPending();ControlEpoch++;}"),
    ("            MotionEpochReset?.Invoke();\n", ""),
    ("        MotionEpochReset?.Invoke();\n", ""),
    ("""        // STATUS bit0 is the firmware's existing MOV classifier (10 Hz);
        // no always-on RAW stream or CONFIG changes are required.
        MotionStatusReceived?.Invoke(packet.expectedId, b[2],
            sensor, callbackSeconds);
""", ""),
    ("""        // Only fresh MOV_ON/MOV_OFF events are supplied to consumers.
        // The clock and per-tracker sequence checks above are unchanged.
        if (fresh && (eventType == 1 || eventType == 2))
            MotionTransitionReceived?.Invoke(state.id, eventType == 1,
                millis, callbackSeconds);
        if (fresh && eventType <= 3)""",
     "        else if(eventType<=3)")
]
for added, original in allowed:
    assert added in actual, "Missing expected narrow monitor hook"
    actual = actual.replace(added, original)
assert actual.encode() == baseline, "Unexpected receiver BLE/protocol change"

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
print("PASS: Original repo isolated outside Unity Assets; maps intact; BIOFIT legacy receiver/physics preserved.")
