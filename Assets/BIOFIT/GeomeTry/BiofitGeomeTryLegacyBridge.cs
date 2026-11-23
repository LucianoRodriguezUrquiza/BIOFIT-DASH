using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// Adapter, never a replacement for Christ0u/GeomeTry physics or maps.
    /// The original Cube.onClick -> Cube.Jump -> Rigidbody2D pipeline is executed
    /// unchanged. Ship and Wave retain their upstream held-key/touch semantics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiofitGeomeTryLegacyBridge : MonoBehaviour
    {
        private BiofitBLEMonitor monitor;
        private static readonly FieldInfo KeyPressed =
            typeof(Character).GetField("keyPressed", BindingFlags.Instance | BindingFlags.NonPublic);
        private const string LevelScene = "Level";
        private const string MenuScene = "Main Menu";
        private const string SelectionScene = "Level Selection";
        private int accepted, executed, ignored;
        private int receiverEpoch;
        private bool showRecording;
        // One accepted bilateral pair may also operate a jump-orb in air.
        // This is input only: the original JumpOrb applies its original force.
        // The pulse is consumable only on the NEXT physics step, never held
        // as a virtual key and never accumulated in a landing buffer.
        private static long orbPulseId;
        private static long consumedOrbPulseId;
        private static float orbPulseDeadline;

        public static bool TryConsumeOrbPulse()
        {
            if (orbPulseId == consumedOrbPulseId) return false;
            if (Time.fixedTime > orbPulseDeadline)
            {
                consumedOrbPulseId = orbPulseId;
                return false;
            }
            consumedOrbPulseId = orbPulseId;
            return true;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene != MenuScene && scene != SelectionScene && scene != LevelScene)
                return; // Never initialize an extra BLE receiver in BIOFIT DASH scenes.
            if (FindFirstObjectByType<BiofitGeomeTryLegacyBridge>() != null)
                return;
            if (FindFirstObjectByType<BiofitBLEMonitor>() != null)
            {
                UnityEngine.Debug.LogError("BIOFIT GeomeTry: ya hay un receptor BLE. No se crea otro.");
                return;
            }

            var root = new GameObject("BIOFIT | GeomeTry original | BLE LEGACY");
            DontDestroyOnLoad(root);
            root.AddComponent<BiofitBLEMonitor>();
            root.AddComponent<BiofitGeomeTryLegacyBridge>();
            // Optional three-mode experiment; never enables in stable BLE builds.
            if (BiofitThreeModesBootstrap.Active)
                root.AddComponent<BiofitThreeModesBootstrap>();
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            // Preserve original GeomeTry's GetMouseButton(0) held-touch input.
            Input.simulateMouseWithTouches = true;
#endif
        }

        // Fail visibly in Editor when an imported original scene is absent
        // from Unity's enabled Build Scene List. GameManager.SceneExists()
        // otherwise discards the click without reporting any error.
        private static void VerifyMenuNavigationInEditor()
        {
#if UNITY_EDITOR
            foreach (string target in new[] { "Main Menu", "Level Selection", "Level" })
            {
                bool found = false;
                for (int i = 0;
                     i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                     i++)
                {
                    string path = UnityEngine.SceneManagement.SceneUtility
                        .GetScenePathByBuildIndex(i);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == target)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    UnityEngine.Debug.LogError(
                        "BIOFIT GeomeTry: la escena '" + target +
                        "' no esta habilitada en Build Settings. Los botones " +
                        "pueden animarse sin navegar. Ejecuta la verificacion " +
                        "BIOFIT > GeomeTry ORIGINAL > 1. Verificar fuente y mapas.");
            }
#endif
        }

        private void OnEnable()
        {
            monitor = GetComponent<BiofitBLEMonitor>();
            if (monitor == null) { enabled = false; return; }
            VerifyMenuNavigationInEditor();
            monitor.ShowDiagnostics = false;
            monitor.PhaseFastPairs = false;
            monitor.LiftCycleMode = false;
            receiverEpoch = monitor.ControlEpoch;
            monitor.BilateralJumpCandidate += OnBilateralLegacyCandidate;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (monitor != null)
                monitor.BilateralJumpCandidate -= OnBilateralLegacyCandidate;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (monitor == null) return;
            // Old unmatched one-foot impulses cannot form a jump in another scene.
            monitor.JumpDetector.ClearPending();
            consumedOrbPulseId = orbPulseId;
            receiverEpoch = monitor.ControlEpoch;
            monitor.ShowDiagnostics = false;
        }

        private void Update()
        {
            if (monitor == null) return;
            if (receiverEpoch != monitor.ControlEpoch)
            {
                receiverEpoch = monitor.ControlEpoch;
                monitor.JumpDetector.ClearPending();
            }
            if (monitor.PhaseFastPairs) monitor.PhaseFastPairs = false;
            if (monitor.LiftCycleMode) monitor.LiftCycleMode = false;
        }

        private void OnBilateralLegacyCandidate(float pairGapMs)
        {
            accepted++;
            // The receiver already enforced freshness, clock alignment, UP_IMPULSE,
            // thresholds, 180 ms pairing and the 900 ms legacy cooldown.
            // Do NOT reinterpret this event as a second physiological detector.
            if (monitor == null || monitor.PhaseFastPairs || monitor.LiftCycleMode ||
                !monitor.BothTrackersConnected || !monitor.ClockAlignmentReady ||
                SceneManager.GetActiveScene().name != LevelScene ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode ||
                KeyPressed == null)
            {
                ignored++;
                return;
            }

            GameObject avatar = GameObject.FindGameObjectWithTag("Player");
            BiofitModeMotor motor = avatar == null ? null : avatar.GetComponent<BiofitModeMotor>();
            if (motor != null && motor.Mode != BiofitMode.Cube)
            {
                // Accepted BLE pair -> one action in Ball or Spider.
                // Preserve the upstream Cube.onClick path for Cube unchanged below.
                bool applied = motor.TryPulse();
                if (applied) executed++; else ignored++;
                if (monitor.GameCaptureActive)
                    monitor.RecordControlDiagnostic(
                        "GEOMETRY_THREE_MODES BLE_LEGACY_" + motor.Mode +
                        " gapMs=" + pairGapMs.ToString("F2",
                            System.Globalization.CultureInfo.InvariantCulture) +
                        " outcome=" + (applied ? "APPLIED" : "NO_ACTION"));
                return;
            }

            Cube cube = avatar == null ? null : avatar.GetComponent<Cube>();
            if (cube == null || !cube.isAlive)
            {
                // Ship/Wave continue to work as in original GeomeTry with held touch.
                ignored++;
                return;
            }

            // BLE input for existing JumpOrbs: one-use on the next physics
            // step only. The imported original JumpOrb code still controls
            // collider, contact and exact vertical velocity.
            orbPulseId++;
            orbPulseDeadline = Time.fixedTime + Time.fixedDeltaTime * 1.5f;

            // One BLE pair = one momentary original Cube.onClick invocation.
            // No held virtual key, queue, landing buffer or rewritten Jump() physics.
            Rigidbody2D body = cube.GetComponent<Rigidbody2D>();
            float beforeY = body == null ? 0 : body.linearVelocity.y;
            bool old = (bool)KeyPressed.GetValue(cube);
            try
            {
                KeyPressed.SetValue(cube, true);
                cube.onClick();
            }
            finally
            {
                KeyPressed.SetValue(cube, old);
            }
            bool jumped = body != null && body.linearVelocity.y > beforeY + .05f;
            if (jumped) executed++; else ignored++;
            if (monitor.GameCaptureActive)
                monitor.RecordControlDiagnostic(
                    "GEOMETRY_ORIGINAL BLE_LEGACY_CUBE gapMs=" +
                    pairGapMs.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    " outcome=" + (jumped ? "JUMP_APPLIED" : "NO_JUMP") +
                    " scene=Level");
        }

        // Read-only counters for the new Configuration screen. The BLE
        // detector, legacy thresholds, firmware and cube input path are untouched.
        public int AcceptedPairs => accepted;
        public int ExecutedJumps => executed;

        // The old OnGUI panel (which obscured the menu and level cards) has
        // moved to BiofitGeometryUX -> Settings. No BLE logic was changed.
    }
}
