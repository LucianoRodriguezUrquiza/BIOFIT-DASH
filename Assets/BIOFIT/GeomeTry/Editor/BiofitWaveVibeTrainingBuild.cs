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
    // Separate experimental Android package. The stable GeomeTry build remains unchanged.
    public static class BiofitWaveVibeTrainingBuild
    {
        private const string Prefix = "Assets/GeomeTryOriginal/Assets/";
        private static readonly string[] SceneNames = {
            "Main Menu", "Level", "Level Editor", "Customisation",
            "Level Selection", "Success", "Settings", "Stats", "Profile", "Credits"
        };
        private static readonly int[] SpikeX = {25, 65, 105, 145, 185, 225, 265};

        private static void VerifyTrainingMap()
        {
            TextAsset source = Resources.Load<TextAsset>("maps/BASE/WaveVibe");
            if (source == null)
                throw new InvalidOperationException("Falta WaveVibe. Ejecuta tools/install_wavevibe_training.ps1.");
            Level level = new Level(source);
            if (level.Id != 4 || level.Name != "Wave Vibe" || level.Difficulty != 1 ||
                level.Music == null || level.Map.Count != 8)
                throw new InvalidOperationException("WaveVibe no coincide con el escenario de prueba esperado.");
            int found = 0;
            int ground = 0;
            foreach (MapItem item in level.Map)
            {
                if (item.Type == "spike")
                {
                    if (found >= SpikeX.Length || item.X != SpikeX[found] || item.Y != 0)
                        throw new InvalidOperationException("Pinchos no coinciden con el recorrido de prueba.");
                    found++;
                }
                else if (item.Type == "ground")
                {
                    if (item.X != 285 || item.Y != -2)
                        throw new InvalidOperationException("Marcador final incorrecto.");
                    ground++;
                }
                else
                {
                    throw new InvalidOperationException("El nivel de prueba contiene un objeto inesperado: " + item.Type);
                }
            }
            if (found != 7 || ground != 1 || level.getLastMapItem().X != 285)
                throw new InvalidOperationException("No coinciden los siete pinchos y la longitud del nivel.");
            Debug.Log("BIOFIT SPIKE LAB: mapa verificado, 7 pinchos, 40 unidades de separacion; ~30 s nominales.");
        }

        [MenuItem("BIOFIT/GeomeTry ORIGINAL/7. Compilar BIOFIT SPIKE LAB (BLE LEGACY)")]
        public static void BuildAndroid()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Detene Play antes de compilar.");
            BiofitGeomeTryOriginalBuild.Verify();
            VerifyTrainingMap();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Activa Android en File > Build Profiles > Switch Platform.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var urp = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                Prefix + "Settings/UniversalRP.asset");
            if (urp == null)
                throw new InvalidOperationException("Falta el URP original de GeomeTry.");

            var oldPipeline = GraphicsSettings.defaultRenderPipeline;
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string oldName = PlayerSettings.productName;
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder,
                "BIOFIT_SPIKE_LAB_LEGACY_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".apk");
            try
            {
                // Never replace the user's installed stable LEGACY or MOV HOLD packages.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.biofitgame.wavevibelab");
                PlayerSettings.productName = "BIOFIT SPIKE LAB";
                GraphicsSettings.defaultRenderPipeline = urp;
                EditorUserBuildSettings.buildAppBundle = false;

                string[] scenes = Array.ConvertAll(SceneNames,
                    name => Prefix + "Scenes/" + name + ".unity");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("No se genero APK: " + report.summary.result);
                Debug.Log("BIOFIT SPIKE LAB APK creada: " + output +
                    " (BILATERAL LEGACY; sin cambios de firmware ni fisica).");
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
