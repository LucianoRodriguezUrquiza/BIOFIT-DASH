using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// A virtual held button read IN Character.Update, together with mouse/space.
    /// No reflection, avatar search, second onClick, velocity write or jump queue.
    /// Sensor intention survives a respawn; output is bound to the live caller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiofitGeomeTryLegacyBridge : MonoBehaviour
    {
        private const string LevelScene = "Level", MenuScene = "Main Menu", SelectionScene = "Level Selection";
        private BiofitBLEMonitor monitor;
        private readonly BiofitGeometryHeldMotion heldMotion = new BiofitGeometryHeldMotion();
        private static BiofitGeomeTryLegacyBridge activeBridge;
        private Character actor;
        private int actorId, previousActorId, deadActorId, receiverEpoch;
        private int accepted, executed, physicsWrites, deaths, spawns, destructions, orbs, rebindings;
        private long inputReads, virtualDeliveries, groundChecks;
        private bool heldInput, appPaused, lastGrounded, groundKnown, groundEpisodeApplied;
        private bool manualHeld, virtualToActor, duplicateBridge;
        private double lastDeliveryAt, nextSnapshotAt, traceWindowAt;
        private int traceLines, suppressed;
        private BiofitGeometryHeldMotion.State lastMotionState;
        private bool captureWasActive;
        private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

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
            if (activeBridge != null && activeBridge != this)
            {
                duplicateBridge = true; enabled = false;
                UnityEngine.Debug.LogError("BIOFIT: puente duplicado deshabilitado; conservar un receptor.");
                return;
            }
            activeBridge = this;
            VerifyMenuNavigationInEditor();
            monitor.ShowDiagnostics = false;
            monitor.PhaseFastPairs = false; monitor.LiftCycleMode = false;
            monitor.MotionLinkRecoveryEnabled = true;
            receiverEpoch = monitor.ControlEpoch;
            monitor.BilateralJumpCandidate += OnBilateralLegacyCandidate;
            monitor.MotionStatusReceived += OnMotionStatus;
            monitor.MotionTransitionReceived += OnMotionTransition;
            monitor.MotionEpochReset += OnMotionReset;
            SceneManager.sceneLoaded += OnSceneLoaded;
            heldMotion.Reset("bridge_enabled");
        }
        private void OnDisable()
        {
            if (duplicateBridge) return;
            if (monitor != null)
            {
                monitor.BilateralJumpCandidate -= OnBilateralLegacyCandidate;
                monitor.MotionStatusReceived -= OnMotionStatus;
                monitor.MotionTransitionReceived -= OnMotionTransition;
                monitor.MotionEpochReset -= OnMotionReset;
                monitor.MotionLinkRecoveryEnabled = false;
            }
            heldMotion.Reset("bridge_disabled"); heldInput = virtualToActor = false;
            actor = null; actorId = 0;
            if (activeBridge == this) activeBridge = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        private void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (paused) { heldMotion.Reset("app_pause"); heldInput = virtualToActor = false; }
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (monitor == null) return;
            monitor.JumpDetector.ClearPending();
            heldMotion.Reset("scene_" + scene.name);
            heldInput = virtualToActor = false;
            previousActorId = actorId; actor = null; actorId = deadActorId = 0;
            groundKnown = groundEpisodeApplied = false;
            receiverEpoch = monitor.ControlEpoch;
            monitor.ShowDiagnostics = false;
            Trace("SCENE", "name=" + scene.name);
        }
        private void OnMotionStatus(byte foot, byte flags, uint sensorMs, double callbackSeconds) =>
            heldMotion.ObserveStatus(foot, flags, sensorMs, callbackSeconds);
        private void OnMotionTransition(byte foot, bool moving, uint sensorMs, double callbackSeconds) =>
            heldMotion.ObserveEvent(foot, moving, sensorMs, callbackSeconds);
        private void OnMotionReset()
        {
            receiverEpoch = monitor.ControlEpoch; // do not discard the next STATUS again in Update
            // Legacy pair-clock bookkeeping can change because an unrelated UP
            // was unmappable. It is not a sensor reboot. Tick still requires
            // healthy clocks/statuses; a real clock outage releases immediately.
            if (monitor.MotionResetReason != "event_clock_source")
            {
                heldMotion.Reset(monitor.MotionResetReason);
                heldInput = virtualToActor = false;
            }
            Trace("RECEIVER_RESET", "reason=" + monitor.MotionResetReason + " epoch=" + receiverEpoch);
        }
        private bool Playing() => !appPaused && monitor != null &&
            SceneManager.GetActiveScene().name == LevelScene && GameManager.Instance != null &&
            GameManager.Instance.PlayMode && Time.timeScale > 0 && !monitor.PhaseFastPairs && !monitor.LiftCycleMode;
        private void RefreshHeldState()
        {
            if (monitor == null) { heldInput = false; return; }
            double now = Now;
            bool old = heldInput;
            bool connected = monitor.BothTrackersConnected, clocks = monitor.ClockAlignmentReady;
#if UNITY_EDITOR
            if (editorMotionUntil > now) { connected = true; clocks = true; }
#endif
            heldInput = heldMotion.Tick(now, connected, clocks, Playing());
#if UNITY_EDITOR
            if (editorTestUntil > now && Playing()) heldInput = editorTestHeld;
#endif
            if (!heldInput) virtualToActor = false;
            if (old != heldInput || lastMotionState != heldMotion.CurrentState)
                Trace("INTENT", "held=" + heldInput + " state=" + heldMotion.CurrentState +
                    " MOV=" + heldMotion.LeftMoving + "/" + heldMotion.RightMoving);
            lastMotionState = heldMotion.CurrentState;
        }
        private void Update()
        {
            if (monitor == null) return;
            if (receiverEpoch != monitor.ControlEpoch) OnMotionReset();
            monitor.PhaseFastPairs = false; monitor.LiftCycleMode = false;
            RefreshHeldState();
        }
        private void LateUpdate()
        {
            RefreshHeldState(); // expire even when there is no character to read input
            if (monitor == null) return;
            double now = Now;
            if (monitor.GameCaptureActive && !captureWasActive)
                Trace("SESSION", "control=CONTINUOUS_MOTION_V2 holdWindowMs=250 statusTimeoutMs=550 unity=" + Application.unityVersion);
            captureWasActive = monitor.GameCaptureActive;
            if (now < nextSnapshotAt) return;
            nextSnapshotAt = now + 1;
            if (!monitor.GameCaptureActive) return;
            Trace("HEALTH", Snapshot(now));
            Trace("SENSOR", monitor.GetMotionTelemetry(1, now).ToString());
            Trace("SENSOR", monitor.GetMotionTelemetry(2, now).ToString());
        }
        // Explicit hooks in the imported upstream Character (not the submodule).
        // No callback is attached to a cube, so destroyed cubes cannot leak listeners.
        public static void CharacterStarted(Character value)
        {
            var b = activeBridge;
            if (b == null || value == null) return;
            b.previousActorId = b.actorId;
            b.actor = value; b.actorId = value.GetInstanceID(); b.deadActorId = 0;
            b.groundKnown = b.groundEpisodeApplied = false;
            b.virtualToActor = b.manualHeld = false; b.spawns++;
            b.Trace("SPAWN", "id=" + b.actorId + " previous=" + b.previousActorId + " n=" + b.spawns + " type=" + value.GetType().Name);
        }
        public static void CharacterDied(Character value)
        {
            var b = activeBridge;
            if (b == null || value == null) return;
            b.deaths++;
            if (value.GetInstanceID() == b.actorId)
            { b.deadActorId = b.actorId; b.virtualToActor = false; }
            b.Trace("DEATH", "id=" + value.GetInstanceID() + " n=" + b.deaths + " intent=" + b.heldInput);
        }
        public static void CharacterDestroyed(Character value)
        {
            var b = activeBridge;
            if (b == null || value == null) return;
            int id = value.GetInstanceID(); b.destructions++;
            // OnDestroy(old) may run AFTER Start(new). Never clear the new binding.
            if (id == b.actorId)
            { b.previousActorId = id; b.actor = null; b.actorId = 0; b.virtualToActor = false; }
            b.Trace("DESTROY", "id=" + id + " current=" + b.actorId + " n=" + b.destructions);
        }
        public static bool ReadHeldInput(Character value, bool physicalHeld = false)
        {
            var b = activeBridge;
            if (b == null) return false;
            b.RefreshHeldState();
            bool eligible = value != null && value is Cube && value.isAlive && value.isActiveAndEnabled &&
                value.gameObject.activeInHierarchy && value.GetInstanceID() != b.deadActorId;
            // A bridge can be re-enabled while the SAME character is still alive.
            // Rebind from that live caller, without a search or a synthetic respawn.
            if (eligible && b.actor == null)
            {
                b.previousActorId = b.actorId; b.actor = value; b.actorId = value.GetInstanceID();
                b.rebindings++; b.groundKnown = b.groundEpisodeApplied = false;
                b.Trace("REBIND", "id=" + b.actorId + " n=" + b.rebindings);
            }
            bool valid = eligible && value.GetInstanceID() == b.actorId;
            b.inputReads++;
            if (!valid) return false;
            b.manualHeld = physicalHeld;
            b.virtualToActor = b.heldInput && b.Playing();
            if (b.virtualToActor) { b.virtualDeliveries++; b.lastDeliveryAt = Now; }
            return b.virtualToActor;
        }
        // Old imported assets compile, but preflight refuses building without V2 overlays.
        public static bool TryConsumeOrbPulse() => false;
        public static bool TryConsumeOrbPulse(Cube cube) => ReadHeldInput(cube);
        public static void GroundObserved(Character value, bool grounded)
        {
            var b = activeBridge;
            if (b == null || value == null || value.GetInstanceID() != b.actorId) return;
            b.groundChecks++; b.groundKnown = true; b.lastGrounded = grounded;
            if (!grounded) b.groundEpisodeApplied = false;
        }
        public static void JumpApplied(Cube cube)
        {
            var b = activeBridge;
            if (b == null || cube == null || cube.GetInstanceID() != b.actorId) return;
            b.physicsWrites++;
            if (b.groundEpisodeApplied) return; // upstream may assign velocity on consecutive grounded frames
            b.groundEpisodeApplied = true; b.executed++;
            b.Trace("JUMP_APPLIED", "id=" + b.actorId + " n=" + b.executed + " writes=" + b.physicsWrites +
                " source=" + (b.virtualToActor ? (b.manualHeld ? "BLE_AND_MANUAL" : "BLE") : "MANUAL"));
        }
        public static void OrbApplied(Character value)
        {
            var b = activeBridge;
            if (b != null && value != null && value.GetInstanceID() == b.actorId) b.orbs++;
        }
        private void OnBilateralLegacyCandidate(float gapMs)
        {
            accepted++;
            Trace("PAIR_DIAGNOSTIC_ONLY", "gapMs=" + gapMs.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }
        private string Snapshot(double now) =>
            "scene=" + SceneManager.GetActiveScene().name + " playing=" + Playing() +
            " clock=" + monitor.ClockAlignmentReady + " epoch=" + monitor.ControlEpoch +
            " resets=" + monitor.MotionResetCount + " resetWhy=" + monitor.MotionResetReason +
            " held=" + heldInput + " state=" + heldMotion.CurrentState + " heldS=" + F(heldMotion.HoldDuration(now)) +
            " activityAge=" + F(heldMotion.ActivityAge(1, now)) + "/" + F(heldMotion.ActivityAge(2, now)) +
            " actor=" + actorId + " previous=" + previousActorId + " alive=" + (actor != null && actor.isAlive) +
            " grounded=" + (groundKnown ? lastGrounded.ToString() : "unknown") +
            " reads=" + inputReads + " delivered=" + virtualDeliveries + " deliveryAge=" + F(now - lastDeliveryAt) +
            " physical=" + manualHeld + " virtual=" + virtualToActor + " groundChecks=" + groundChecks +
            " jumps=" + executed + " writes=" + physicsWrites + " orbWrites=" + orbs +
            " deaths=" + deaths + " spawns=" + spawns + " destroyed=" + destructions + " rebindings=" + rebindings +
            " suppressed=" + suppressed + " " + monitor.MotionConsumerSummary + " " + monitor.TransportSummary;
        private static string F(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "unknown" :
            value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        private void Trace(string kind, string detail)
        {
            if (monitor == null || !monitor.GameCaptureActive) return;
            double now = Now;
            if (now - traceWindowAt >= 1) { traceWindowAt = now; traceLines = 0; }
            if (++traceLines > 24) { suppressed++; return; }
            monitor.RecordControlDiagnostic("MOTION_V2 " + kind + " mono=" + F(now) + " " + detail);
        }
        public int AcceptedPairs => accepted;
        public int ExecutedJumps => executed;
        public bool MovementHeld => heldInput;
        public int MovementPresses => heldMotion.PressCount;
        public int MovementReleases => heldMotion.ReleaseCount;
        public string ControlHealth => heldMotion.CurrentState.ToString();
        public int CurrentActorId => actorId;
        public int DeathCount => deaths;
        public int SpawnCount => spawns;
#if UNITY_EDITOR
        private double editorTestUntil, editorMotionUntil;
        private bool editorTestHeld;
        // Only an Editor validation fixture can inject this short-lived input.
        // Not compiled into Android; no production mode/selector is added.
        public void EditorSetVirtualButton(bool pressed)
        { editorTestHeld = pressed; editorTestUntil = Now + .2; }
        public void EditorFeedMotion(byte flags, uint sampleMs)
        {
            double at = Now; editorMotionUntil = at + .2;
            heldMotion.ObserveStatus(1, flags, sampleMs, at);
            heldMotion.ObserveStatus(2, flags, sampleMs, at);
        }
        public void EditorEndValidation()
        {
            editorTestUntil = editorMotionUntil = 0;
            heldMotion.Reset("editor_validation_end"); heldInput = false;
        }
#endif
    }
}
