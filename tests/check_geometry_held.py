"""Source contracts for movement-held GeomeTry input."""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
gate = (root / "Assets/BIOFIT/GeomeTry/BiofitGeometryHeldMotion.cs").read_text()
bridge = (root / "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs").read_text()
monitor = (root / "Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs").read_text()
ui = (root / "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs").read_text()

assert "Biofit.GeomeTryOriginal" not in monitor
for token in [
    "public event Action<byte, byte, uint, double> MotionStatusReceived;",
    "public event Action<byte, bool, uint, double> MotionTransitionReceived;",
    "public event Action MotionEpochReset;",
    "MotionStatusReceived?.Invoke(packet.expectedId, b[2]",
    "MotionTransitionReceived?.Invoke(state.id, eventType == 1",
    "if (fresh && (eventType == 1 || eventType == 2))",
]:
    assert token in monitor, token
for token in [
    "StatusTimeoutSeconds = 0.55", "BothRestGraceSeconds = 0.16",
    "f.Moving = f.Healthy && (flags & 0x01) != 0;",
    "if (left && right)", "if (left || right)",
    "if (now - restSince >= BothRestGraceSeconds)",
    "!FreshAndHealthy(0, now) || !FreshAndHealthy(1, now)",
    "IsOlder(sensorMs, f.LastSensorMs)"
]:
    assert token in gate, token
for forbidden in ("0.12f", "0.90f", "LIFT_ONSET", "PhaseFast"):
    assert forbidden not in gate, forbidden
for token in [
    "monitor.MotionStatusReceived += OnMotionStatus;",
    "monitor.MotionTransitionReceived += OnMotionTransition;",
    "monitor.MotionEpochReset += OnMotionReset;",
    "monitor.MotionStatusReceived -= OnMotionStatus;",
    "monitor.MotionTransitionReceived -= OnMotionTransition;",
    "monitor.MotionEpochReset -= OnMotionReset;",
    "new BiofitGeometryHeldMotion();",
    "SceneManager.GetActiveScene().name == LevelScene",
    "GameManager.Instance.PlayMode",
    "if (cube == null || !cube.isAlive) return;",
    "private void LateUpdate()",
    "KeyPressed.SetValue(cube, true);",
    "cube.onClick();",
    "KeyPressed.SetValue(cube, previous);",
    "public static bool TryConsumeOrbPulse()",
    "b.heldInput && b.ReadyForGameplay()",
    "monitor.PhaseFastPairs = false;",
    "monitor.LiftCycleMode = false;",
    "BilateralJumpCandidate += OnBilateralLegacyCandidate;",
]:
    assert token in bridge, token
handler = bridge.split("private void OnBilateralLegacyCandidate(", 1)[1]
assert "cube.onClick();" not in handler, "Pair cooldown cannot command jumps"
for forbidden in ["Time.fixedDeltaTime =", "Time.timeScale =", "body.linearVelocity = ",
                  "jumpForce = ", "DefaultSpeed =", "BiofitJumpBuffer"]:
    assert forbidden not in bridge, forbidden
assert "bridge.MovementHeld" in ui
assert '"PARES LEGACY: "' in ui and '"   |   SALTOS: "' in ui
assert "Application.targetFrameRate = 60;" in ui
print("PASS: motion state only, no experimental mode selector, no changed physics.")
