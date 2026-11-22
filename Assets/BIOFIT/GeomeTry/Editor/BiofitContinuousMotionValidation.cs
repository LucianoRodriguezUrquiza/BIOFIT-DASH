#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Biofit.GeomeTryOriginal.Editor
{
    // Native Unity validation, deliberately NOT part of host CI. Runs the
    // actual Cube prefab, Rigidbody2D, raycasts, Die(), delayed Invoke(Respawn),
    // Instantiate and Destroy. Only sensor input is simulated. No map is edited.
    public static class BiofitContinuousMotionValidation
    {
        private static IEnumerator run;
        private static Scene priorScene, testScene;
        private static readonly List<GameObject> hidden = new List<GameObject>();
        private static BiofitGeomeTryLegacyBridge bridge;
        private static bool priorPlay, virtualMode, feedEnabled;
        private static float priorScale, nextStatus, phaseStart;
        private static int lastFrame;
        private static uint sampleMs;
        private static int passed;
        [MenuItem("BIOFIT/GeomeTry ORIGINAL/6. Validar MOV HOLD V2 (en Play)")]
        public static void Start()
        {
            if (!EditorApplication.isPlaying || run != null)
                throw new InvalidOperationException("Abrir Main Menu, entrar en Play y ejecutar una sola validacion.");
            bridge = UnityEngine.Object.FindFirstObjectByType<BiofitGeomeTryLegacyBridge>();
            var monitor = UnityEngine.Object.FindFirstObjectByType<BiofitBLEMonitor>();
            if (bridge == null || monitor == null || GameManager.Instance == null || monitor.GameCaptureActive)
                throw new InvalidOperationException("Se necesita Main Menu y su puente BLE, sin una captura activa.");
            BiofitGeomeTryOriginalBuild.Verify();
            priorScene = SceneManager.GetActiveScene(); priorPlay = GameManager.Instance.PlayMode; priorScale = Time.timeScale;
            passed = 0; sampleMs = 1000; lastFrame = -1; feedEnabled = true;
            run = Validate(); EditorApplication.update += Step;
            UnityEngine.Debug.Log("MOV V2: validacion NATIVA iniciada (~2 minutos); no tocar teclado/mouse. Sensor simulado, no BLE real.");
        }
        private static void Step()
        {
            if (!EditorApplication.isPlaying) { Cleanup(); return; }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (bridge != null && feedEnabled)
                {
                    if (virtualMode) bridge.EditorSetVirtualButton(true);
                    else
                    {
                        // Same nominal 10 Hz STATUS, with repeated 200 ms calm
                        // phases, not a permanent MOV=true convenience fixture.
                        float now = Time.realtimeSinceStartup;
                        if (now >= nextStatus)
                        {
                            nextStatus = now + .1f; sampleMs += 100;
                            bool move = ((int)((now - phaseStart) * 10) % 8) < 6;
                            bridge.EditorFeedMotion(move ? (byte)0x8d : (byte)0x8c, sampleMs);
                        }
                    }
                }
                if (!run.MoveNext()) Cleanup();
            }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); Cleanup(); }
        }
        private static void Require(bool okay, string reason)
        { if (!okay) throw new InvalidOperationException("MOV V2 NATIVE FAIL: " + reason); passed++; }
        private static IEnumerator Validate()
        {
            // Disable only original scene roots temporarily. Persistent BLE/UI/
            // GameManager remain in place; the fixture is a separate scene.
            foreach (GameObject root in priorScene.GetRootGameObjects())
                if (root.activeSelf) { hidden.Add(root); root.SetActive(false); }
            testScene = SceneManager.CreateScene("Level"); SceneManager.SetActiveScene(testScene);
            var gridRoot = new GameObject("MOV V2 TEST GRID"); gridRoot.AddComponent<Grid>();
            var tileObject = new GameObject("MOV V2 TEST TILEMAP"); tileObject.transform.SetParent(gridRoot.transform);
            var tile = tileObject.AddComponent<Tilemap>();
            var prefab = Resources.Load<GameObject>("Prefabs/Player/CubePrefab");
            Require(prefab != null, "original cube prefab missing");
            var mask = (LayerMask)typeof(Character).GetField("groundLayer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(prefab.GetComponent<Cube>());
            int layer = 0; while (layer < 31 && (mask.value & (1 << layer)) == 0) layer++;
            Require((mask.value & (1 << layer)) != 0, "original ground mask empty");
            var ground = new GameObject("MOV V2 TEST GROUND"); ground.layer = layer;
            ground.transform.position = new Vector3(0, 1, 0);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(10000, 1);
            GameManager.Instance.PlayMode = true;
            UnityEngine.Object.Instantiate(prefab, new Vector3(-9, 2, 0), Quaternion.identity, tile.transform);
            virtualMode = true; phaseStart = Time.realtimeSinceStartup; yield return null;
            int first = bridge.ExecutedJumps;
            float end = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < end) yield return null;
            Require(bridge.ExecutedJumps - first >= 3, "held virtual button did not yield >=3 grounded jumps");
            bridge.EditorEndValidation(); virtualMode = false; nextStatus = 0; phaseStart = Time.realtimeSinceStartup;
            first = bridge.ExecutedJumps; end = Time.realtimeSinceStartup + 5;
            while (Time.realtimeSinceStartup < end) yield return null;
            Require(bridge.ExecutedJumps - first >= 3, "MOV envelope did not yield >=3 grounded jumps");
            for (int cycle = 1; cycle <= 100; cycle++)
            {
                Cube old = UnityEngine.Object.FindFirstObjectByType<Cube>();
                Require(old != null && old.isAlive, "live cube absent before death " + cycle);
                int oldId = old.GetInstanceID(); int before = bridge.ExecutedJumps;
                old.Die(); // actual delayed Invoke; do not shortcut Respawn()
                Require(!BiofitGeomeTryLegacyBridge.ReadHeldInput(old), "dead object received input");
                end = Time.realtimeSinceStartup + 3;
                while ((bridge.CurrentActorId == oldId || bridge.CurrentActorId == 0 || bridge.ExecutedJumps == before) && Time.realtimeSinceStartup < end)
                    yield return null;
                Cube current = UnityEngine.Object.FindFirstObjectByType<Cube>();
                Require(old == null, "old native Unity object not destroyed");
                Require(current != null && current.isAlive && current.GetInstanceID() != oldId, "original respawn failed");
                Require(current.GetInstanceID() == bridge.CurrentActorId && bridge.ExecutedJumps > before,
                    "new native cube did not execute input " + cycle);
                Require(UnityEngine.Object.FindObjectsByType<Cube>(FindObjectsSortMode.None).Length == 1, "cube accumulation " + cycle);
                if (cycle == 10 || cycle == 20 || cycle == 50 || cycle == 100)
                    UnityEngine.Debug.Log("MOV V2 NATIVE PASS checkpoint: " + cycle + " actual death/respawn cycles");
            }
            feedEnabled = false; bridge.EditorEndValidation(); yield return null;
            Require(!bridge.MovementHeld, "validation input remained pressed");
            UnityEngine.Debug.Log("MOV V2 NATIVE PASS: " + passed + " checks; consecutive native jumps and 100 actual respawns. NOT Android or physical sensor validation.");
        }
        private static void Cleanup()
        {
            EditorApplication.update -= Step; run = null; feedEnabled = false;
            if (bridge != null) bridge.EditorEndValidation();
            if (priorScene.IsValid() && priorScene.isLoaded) SceneManager.SetActiveScene(priorScene);
            if (testScene.IsValid() && testScene.isLoaded) SceneManager.UnloadSceneAsync(testScene);
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            hidden.Clear();
            if (GameManager.Instance != null) GameManager.Instance.PlayMode = priorPlay;
            Time.timeScale = priorScale;
        }
    }
}
#endif
