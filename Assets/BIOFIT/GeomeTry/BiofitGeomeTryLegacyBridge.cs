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
        private readonly BiofitGeometryHeldMotion heldMotion =
            new BiofitGeometryHeldMotion();
        private static readonly FieldInfo KeyPressed =
            typeof(Character).GetField("keyPressed", BindingFlags.Instance | BindingFlags.NonPublic);
        private const string LevelScene = "Level";
        private const string MenuScene = "Main Menu";
        private const string SelectionScene = "Level Selection";
        private int accepted, executed, ignored, holdPresses, holdReleases;
        private int receiverEpoch;
        private bool heldInput;
        private static BiofitGeomeTryLegacyBridge activeBridge;

        // The imported JumpOrb compatibility patch already calls this method.
        // A held input must operate JumpOrbs just like holding the mouse.
        // It is evaluated each physics trigger step; no expiring orb pulse or
        // repeated synthetic tap is necessary.
        public static bool TryConsumeOrbPulse()
        {
            BiofitGeomeTryLegacyBridge b = activeBridge;
            return b != null && b.heldInput && b.ReadyForGameplay();
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
            activeBridge = this;
            VerifyMenuNavigationInEditor();
            monitor.ShowDiagnostics = false;
            // The BLE receiver stays in traditional legacy mode. The 900 ms
            // legacy pair detector is retained for DIAGNOSTICS ONLY: movement
            // hold comes from firmware MOV_ON/OFF and STATUS MOV, not from pairs.
            monitor.PhaseFastPairs = false;
            monitor.LiftCycleMode = false;
            receiverEpoch = monitor.ControlEpoch;
            monitor.BilateralJumpCandidate += OnBilateralLegacyCandidate;
            monitor.MotionStatusReceived += OnMotionStatus;
            monitor.MotionTransitionReceived += OnMotionTransition;
            monitor.MotionEpochReset += OnMotionReset;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (monitor != null)
            {
                monitor.BilateralJumpCandidate -= OnBilateralLegacyCandidate;
                monitor.MotionStatusReceived -= OnMotionStatus;
                monitor.MotionTransitionReceived -= OnMotionTransition;
                monitor.MotionEpochReset -= OnMotionReset;
                heldMotion.Reset();
            }
            heldInput = false;
            if (activeBridge == this) activeBridge = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause) return;
            heldInput = false;
            heldMotion.Reset();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (monitor == null) return;
            // Never carry an active hold from another scene/level or a
            // previous player life into the next scene. Fresh STATUS will
            // reestablish the movement state automatically.
            monitor.JumpDetector.ClearPending();
            heldMotion.Reset();
            heldInput = false;
            receiverEpoch = monitor.ControlEpoch;
            monitor.ShowDiagnostics = false;
        }

        private void OnMotionStatus(byte foot, byte flags, uint sensorMs,
            double callbackSeconds)
        {
            heldMotion.ObserveStatus(foot, flags, sensorMs, callbackSeconds);
        }

        private void OnMotionTransition(byte foot, bool moving,
            uint sensorMs, double callbackSeconds)
        {
            heldMotion.ObserveEvent(foot, moving, sensorMs, callbackSeconds);
        }

        private void OnMotionReset()
        {
            heldMotion.Reset();
            heldInput = false;
        }

        private bool ReadyForGameplay()
        {
            return monitor != null && monitor.BothTrackersConnected &&
                monitor.ClockAlignmentReady &&
                SceneManager.GetActiveScene().name == LevelScene &&
                GameManager.Instance != null && GameManager.Instance.PlayMode &&
                KeyPressed != null && !monitor.PhaseFastPairs &&
                !monitor.LiftCycleMode;
        }

        private void Update()
        {
            if (monitor == null) return;
            if (receiverEpoch != monitor.ControlEpoch)
            {
                receiverEpoch = monitor.ControlEpoch;
                monitor.JumpDetector.ClearPending();
                heldMotion.Reset();
                heldInput = false;
            }
            if (monitor.PhaseFastPairs) monitor.PhaseFastPairs = false;
            if (monitor.LiftCycleMode) monitor.LiftCycleMode = false;

            bool old = heldInput;
            // Stopwatch is the same monotonic timeline used by native BLE
            // callbacks. Game timeScale, frame rate, jump physics and pause
            // cannot alter the motion heartbeat watchdog.
            double now = System.Diagnostics.Stopwatch.GetTimestamp() /
                (double)System.Diagnostics.Stopwatch.Frequency;
            bool ready = ReadyForGameplay();
            heldInput = heldMotion.Tick(now,
                monitor.BothTrackersConnected, monitor.ClockAlignmentReady,
                ready);
            if (old == heldInput) return;
            if (heldInput) holdPresses++; else holdReleases++;
            if (monitor.GameCaptureActive)
                monitor.RecordControlDiagnostic(
                    "GEOMETRY_HOLD_" + (heldInput ? "ON" : "OFF") +
                    " feet=" + (heldMotion.LeftMoving ? "1" : "0") +
                    (heldMotion.RightMoving ? "1" : "0") +
                    " clockReady=" + monitor.ClockAlignmentReady +
                    " connected=" + monitor.BothTrackersConnected);
        }

        private void LateUpdate()
        {
            if (!heldInput || !ReadyForGameplay()) return;
            GameObject avatar = GameObject.FindGameObjectWithTag("Player");
            Cube cube = avatar == null ? null : avatar.GetComponent<Cube>();
            if (cube == null || !cube.isAlive) return;
            Rigidbody2D body = cube.GetComponent<Rigidbody2D>();
            float beforeY = body == null ? 0f : body.linearVelocity.y;

            // EXACT same original action used by Space or mouse hold:
            // Cube.onClick() each frame while held. Original Cube.Jump()
            // checks CheckGrounded() and only jumps on contact with ground.
            // Reflection is temporary and does not overwrite manual input.
            bool previous = (bool)KeyPressed.GetValue(cube);
            try
            {
                KeyPressed.SetValue(cube, true);
                cube.onClick();
            }
            finally { KeyPressed.SetValue(cube, previous); }

            if (body != null && body.linearVelocity.y > beforeY + .05f)
            {
                executed++;
                if (monitor.GameCaptureActive)
                    monitor.RecordControlDiagnostic(
                        "GEOMETRY_HELD_JUMP_APPLIED count=" + executed);
            }
        }

        private void OnBilateralLegacyCandidate(float pairGapMs)
        {
            // Preserve the old bilateral acceptance count only for diagnostics.
            // Do NOT command a second jump here: a MOVE-held button ignores
            // the original 900ms pair cooldown on purpose.
            accepted++;
            if (monitor != null && monitor.GameCaptureActive)
                monitor.RecordControlDiagnostic(
                    "GEOMETRY_LEGACY_PAIR_DIAG_ONLY gapMs=" +
                    pairGapMs.ToString("F2",
                        System.Globalization.CultureInfo.InvariantCulture));
        }

        // Read-only metrics for Settings; no new BLE mode selector.
        public int AcceptedPairs => accepted;
        public int ExecutedJumps => executed;
        public bool MovementHeld => heldInput;
        public int MovementPresses => holdPresses;
        public int MovementReleases => holdReleases;
    }
}
