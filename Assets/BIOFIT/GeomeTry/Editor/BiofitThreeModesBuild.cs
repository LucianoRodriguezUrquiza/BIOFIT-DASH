#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Biofit.GeomeTryOriginal.Editor
{
    public static class BiofitThreeModesBuild
    {
        private const string Prefix = "Assets/GeomeTryOriginal/Assets/";
        private static readonly string[] Names = {
            "BackOnTrack", "JumpyNightmare", "Polargeist", "StereoMadness", "WaveVibe"
        };
        private static readonly string[] Scenes = {
            "Main Menu", "Level", "Level Editor", "Customisation",
            "Level Selection", "Success", "Settings", "Stats", "Profile", "Credits"
        };
        private static readonly string[] Supported = {
            "spike", "ground", "biofitRoof", "biofitBallPortal",
            "biofitSpiderPortal", "biofitCubePortal"
        };

        [MenuItem("BIOFIT/GeomeTry ORIGINAL/8. Verificar niveles 3 MODOS (60 s)")]
        public static void VerifyMaps()
        {
            int[] expectedPortals = {0, 0, 0, 3, 4};
            for (int i = 0; i < Names.Length; ++i)
            {
                TextAsset asset = Resources.Load<TextAsset>("BiofitThreeModes/" + Names[i]);
                if (asset == null)
                    throw new InvalidOperationException("Falta mapa BIOFIT: " + Names[i]);
                Level map = new Level(asset);
                if (map.Id != i + 1 || map.getLastMapItem().X != 575 ||
                    map.Music == null)
                    throw new InvalidOperationException("Id/longitud/musica invalidos: " + Names[i]);
                int portals = 0, spikes = 0, previousSpikeX = -100;
                bool roof = false, marker = false;
                foreach (MapItem item in map.Map)
                {
                    if (Array.IndexOf(Supported, item.Type) < 0)
                        throw new InvalidOperationException("Objeto no soportado: " + item.Type);
                    if (item.Type == "spike")
                    {
                        if (item.X - previousSpikeX < 30 || item.X < 25)
                            throw new InvalidOperationException("Pinchos demasiado cercanos: " + Names[i]);
                        previousSpikeX = item.X;
                        spikes++;
                    }
                    if (item.Type.EndsWith("Portal", StringComparison.Ordinal)) portals++;
                    if (item.Type == "biofitRoof") roof = true;
                    if (item.Type == "ground" && item.X == 575) marker = true;
                }
                if (!marker || roof != (i != 0) || portals != expectedPortals[i] || spikes < 8)
                    throw new InvalidOperationException("Estructura inesperada: " + Names[i]);
            }
            Debug.Log("BIOFIT 3 MODOS: 5 mapas ~60 s, sin doble salto obligatorio y portales verificados estaticamente.");
        }

        [MenuItem("BIOFIT/GeomeTry ORIGINAL/9. Compilar BIOFIT 3 MODOS (BLE LEGACY)")]
        public static void BuildAndroid()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Salí de Play antes de compilar.");
            BiofitGeomeTryOriginalBuild.Verify(); // Originals remain byte-identical.
            VerifyMaps();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Activa Android en File > Build Profiles.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                Prefix + "Settings/UniversalRP.asset");
            if (urp == null) throw new InvalidOperationException("Falta URP original.");

            var oldPipeline = GraphicsSettings.defaultRenderPipeline;
            string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string oldName = PlayerSettings.productName;
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            string builds = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            Directory.CreateDirectory(builds);
            string output = Path.Combine(builds, "BIOFIT_TRES_MODOS_BILATERAL_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".apk");
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                    "com.biofitgame.threemodeslab");
                PlayerSettings.productName = "BIOFIT TRES MODOS";
                EditorUserBuildSettings.buildAppBundle = false;
                GraphicsSettings.defaultRenderPipeline = urp;
                string[] scenePaths = Array.ConvertAll(Scenes,
                    name => Prefix + "Scenes/" + name + ".unity");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Falló el build: " + report.summary.result);
                Debug.Log("BIOFIT 3 MODOS: APK experimental generada en " + output);
                EditorUtility.RevealInFinder(output);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                GraphicsSettings.defaultRenderPipeline = oldPipeline;
                PlayerSettings.productName = oldName;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            }
        }
    }
}
#endif
