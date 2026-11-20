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
             "Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs", "Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs"):
    baseline = subprocess.check_output(["git", "show", "5616fc4571193d4a2d076550a909089344144384:" + path], cwd=root)
    assert (root / path).read_bytes() == baseline, path + " was modified"
cfg = (root / "ProjectSettings/ProjectSettings.asset").read_text()
assert re.search(r'^  activeInputHandler: 2$', cfg, re.M)
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
    blob = subprocess.check_output(["git", "hash-object", str(path)], text=True).strip()
    assert blob == sha, "Original map changed: " + name
print("PASS: Original repo isolated outside Unity Assets; maps intact; BIOFIT legacy receiver/physics preserved.")
