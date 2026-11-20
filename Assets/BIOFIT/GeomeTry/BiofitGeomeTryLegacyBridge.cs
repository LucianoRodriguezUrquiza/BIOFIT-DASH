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
#endif
        }

        private void OnEnable()
        {
            monitor = GetComponent<BiofitBLEMonitor>();
            if (monitor == null) { enabled = false; return; }
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
            Cube cube = avatar == null ? null : avatar.GetComponent<Cube>();
            if (cube == null || !cube.isAlive)
            {
                // Ship/Wave continue to work as in original GeomeTry with held touch.
                ignored++;
                return;
            }

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

        private void OnGUI()
        {
            if (monitor == null) return;
            string scene = SceneManager.GetActiveScene().name;
            // Do not cover or modify the original game-level view.
            if (scene != MenuScene && scene != SelectionScene) return;
            int oldDepth = GUI.depth;
            GUI.depth = -1500;
            try
            {
                const float width = 380;
                var rect = new Rect(Mathf.Max(4, Screen.width - width - 15), 15, width, showRecording ? 258 : 155);
                GUILayout.BeginArea(rect, GUI.skin.box);
                GUILayout.Label("BIOFIT GAME | Bluetooth BILATERAL LEGACY");
                GUILayout.Label("Waveshare: " + (monitor.BothTrackersConnected ? "2/2 conectados" : "Esperando ambos trackers"));
                GUILayout.Label("Relojes: " + (monitor.ClockAlignmentReady ? "listos" : "esperando STATUS"));
                GUILayout.Label("Pares recibidos: " + accepted + " | saltos cubo: " + executed);
                GUILayout.Label("La conexión BLE se inicia automáticamente.");
                if (GUILayout.Button(showRecording ? "OCULTAR REGISTRO" : "REGISTRO / ARCHIVOS"))
                    showRecording = !showRecording;
                if (showRecording)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("INICIAR") && monitor.StartGameCapture())
                        monitor.RecordControlDiagnostic("GAME=GEOMETRY_ORIGINAL INPUT=BILATERAL_LEGACY");
                    if (GUILayout.Button("FINALIZAR")) monitor.StopGameCapture();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(monitor.RawButtonLabel)) monitor.ToggleRawCapture();
                    if (GUILayout.Button("SYNC")) monitor.MarkVideoSync();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("DESCARGAS")) monitor.RequestExport(false);
                    if (GUILayout.Button("GUARDAR SAF")) monitor.RequestExport(true);
                    GUILayout.EndHorizontal();
                    GUILayout.Label(monitor.CaptureBanner);
                }
                GUILayout.EndArea();
            }
            finally { GUI.depth = oldDepth; }
        }
    }
}
