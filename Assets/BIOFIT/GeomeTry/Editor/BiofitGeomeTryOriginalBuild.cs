using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Biofit.GeomeTryOriginal.Editor
{
    /// <summary>
    /// Builds the ORIGINAL GeomeTry scenes. Does not generate replacement levels,
    /// change the source submodule, or overwrite EditorBuildSettings.
    /// </summary>
    public static class BiofitGeomeTryOriginalBuild
    {
        private const string Prefix = "Assets/GeomeTryOriginal/Assets/";
        private static readonly string[] SceneNames = {
            "Main Menu", "Level", "Level Editor", "Customisation",
            "Level Selection", "Success", "Settings", "Stats", "Profile", "Credits"
        };
        private static readonly string[] SourceMaps = {
            "StereoMadness", "BackOnTrack", "Polargeist", "JumpyNightmare"
        };
        // Git blob SHA-1 at Christ0u/GeomeTry@d922cadd... (raw bytes, no adaptation).
        private static readonly string[] MapBlobs = {
            "0af72f78ccc80ad982fcb7aabc0c39387bb2350c",
            "47e10ff7f5b48a8045e3ddcfdd20e1dce2c0deda",
            "fe4af269c83b2984c643d6f3282799152ea84dc",
            "7c0b86d610daf6b79654b3edeacb5e412dcc2c80"
        };

        private static string Scene(string name) => Prefix + "Scenes/" + name + ".unity";

        [MenuItem("BIOFIT/GeomeTry ORIGINAL/1. Verificar fuente y mapas")]
        public static void Verify()
        {
            if (!File.Exists(Prefix + "Scripts/GameMechanics/CharacterType/Cube.cs"))
                throw new InvalidOperationException(
                    "Falta el submodulo original. Cierra Unity y ejecuta: git submodule update --init --recursive");
            foreach (string name in SceneNames)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene(name)) == null)
                    throw new InvalidOperationException("Falta escena ORIGINAL: " + Scene(name));
            for (int i = 0; i < SourceMaps.Length; i++)
            {
                string path = Prefix + "Resources/maps/BASE/" + SourceMaps[i] + ".json";
                if (!File.Exists(path)) throw new InvalidOperationException("Falta mapa: " + path);
                byte[] raw = File.ReadAllBytes(path);
                byte[] header = Encoding.ASCII.GetBytes("blob " + raw.Length + "\0");
                byte[] combined = new byte[header.Length + raw.Length];
                Buffer.BlockCopy(header, 0, combined, 0, header.Length);
                Buffer.BlockCopy(raw, 0, combined, header.Length, raw.Length);
                using (var sha = SHA1.Create())
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(combined)).Replace("-", "").ToLowerInvariant();
                    if (actual != MapBlobs[i])
                        throw new InvalidOperationException("Mapa original modificado: " + path);
                }
            }
            foreach (string name in new[] {"CubePrefab", "ShipPrefab", "WavePrefab"})
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefix + "Resources/Prefabs/Player/" + name + ".prefab") == null)
                    throw new InvalidOperationException("Falta prefab original: " + name);
            UnityEngine.Debug.Log("BIOFIT GeomeTry original: escenas, personajes y los cuatro mapas RAW verificados sin modificar.");
        }

        [MenuItem("BIOFIT/GeomeTry ORIGINAL/2. Abrir Main Menu original")]
        public static void Open()
        {
            Verify();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(Scene("Main Menu"));
        }

        [MenuItem("BIOFIT/GeomeTry ORIGINAL/3. Compilar APK Android original + BLE LEGACY")]
        public static void BuildAndroid()
        {
            Verify();
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detene Play antes del build.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Selecciona Android en File > Build Profiles > Switch Platform.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            RenderPipelineAsset urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                Prefix + "Settings/UniversalRP.asset");
            if (urp == null) throw new InvalidOperationException("Falta URP original. Comproba el paquete Universal RP y el submodulo.");
            RenderPipelineAsset previousPipeline = GraphicsSettings.defaultRenderPipeline;
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder,
                "BIOFIT_GeomeTry_ORIGINAL_LEGACY_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".apk");
            try
            {
                // Only for this build, then restore. Original URP asset is untouched.
                GraphicsSettings.defaultRenderPipeline = urp;
                EditorUserBuildSettings.buildAppBundle = false;
                string[] scenes = Array.ConvertAll(SceneNames, Scene);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes, locationPathName = path,
                    target = BuildTarget.Android, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Unity no generó APK: " + report.summary.result);
                UnityEngine.Debug.Log("APK generada sin prueba en dispositivo: " + path);
                EditorUtility.RevealInFinder(path);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                GraphicsSettings.defaultRenderPipeline = previousPipeline;
            }
        }
    }
}
