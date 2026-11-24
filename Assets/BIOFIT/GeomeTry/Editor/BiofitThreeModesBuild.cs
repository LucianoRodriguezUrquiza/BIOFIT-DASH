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
            "biofitSpiderPortal", "biofitCubePortal", "biofitDecoPulse",
            "biofitDecoCrystal", "biofitDecoStar", "tile", "smallTile",
            "biofitShipPortal", "biofitStereoCoin"
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
                if (map.Id != i + 1 || map.Name != "Nivel " + (i + 1) ||
                    map.getLastMapItem().X != (i == 0 ? 825 : 575) || map.Music == null)
                    throw new InvalidOperationException("Id/longitud/musica invalidos: " + Names[i]);
                if (i == 0)
                {
                    // Video-guided Stereo Madness is a FULL 85-second map,
                    // not the former 575-unit placeholder. 2 ship transitions,
                    // 2 returns to Cube and 3 collectible coins.
                    int ships = 0, cubes = 0, coins = 0, spikes1 = 0;
                    int blocks = 0, oldX = int.MinValue;
                    foreach (MapItem item in map.Map)
                    {
                        if (Array.IndexOf(Supported, item.Type) < 0 ||
                            item.X < oldX)
                            throw new InvalidOperationException(
                                "Stereo: objeto sin prefab o coordenadas desordenadas");
                        oldX = item.X;
                        if (item.Type == "biofitShipPortal")
                        {
                            if (item.X != (ships == 0 ? 245 : 724) || ships >= 2)
                                throw new InvalidOperationException("Stereo: portal ship incorrecto");
                            ships++;
                        }
                        if (item.Type == "biofitCubePortal")
                        {
                            if (item.X != (cubes == 0 ? 381 : 786) || cubes >= 2)
                                throw new InvalidOperationException("Stereo: portal cube incorrecto");
                            cubes++;
                        }
                        if (item.Type == "biofitStereoCoin") coins++;
                        if (item.Type == "spike") spikes1++;
                        if (item.Type == "tile" || item.Type == "smallTile") blocks++;
                    }
                    if (ships != 2 || cubes != 2 || coins != 3 ||
                        spikes1 < 100 || blocks < 280)
                        throw new InvalidOperationException(
                            "Stereo: falta una fase, coleccionable o geometria");
                    continue;
                }
                int portals = 0, spikes = 0, previousSpikeX = -100;
                int[] expectedPortalX = i == 3 ? new[] {185, 385, 515} :
                    i == 4 ? new[] {117, 275, 405, 515} : new int[0];
                int[] actualPortals = new int[expectedPortalX.Length];
                bool roof = false, marker = false;
                foreach (MapItem item in map.Map)
                {
                    if (Array.IndexOf(Supported, item.Type) < 0)
                        throw new InvalidOperationException("Objeto no soportado: " + item.Type);
                    if (item.Type == "spike")
                    {
                        if (item.X - previousSpikeX < 15 || item.X < 25)
                            throw new InvalidOperationException("Pinchos demasiado cercanos: " + Names[i]);
                        previousSpikeX = item.X;
                        spikes++;
                    }
                    if (item.Type.EndsWith("Portal", StringComparison.Ordinal))
                    {
                        if (portals >= expectedPortalX.Length ||
                            item.X != expectedPortalX[portals])
                            throw new InvalidOperationException("Portal fuera de secuencia: " + Names[i]);
                        actualPortals[portals++] = item.X;
                    }
                    if (item.Type == "biofitRoof") roof = true;
                    if (item.Type == "ground" && item.X == 575) marker = true;
                }
                if (!marker || roof != (i != 0) || portals != expectedPortals[i] || spikes < 8)
                    throw new InvalidOperationException("Estructura inesperada: " + Names[i]);
            }
            Debug.Log("BIOFIT NIVEL 1 STEREO: video-map 825u, Ship+Cube+3 monedas " +
                "y Niveles 2-5 legacy de 575u verificados por estructura. " +
                "Solo validacion estatica: NO equivale a geometria original exacta " +
                "ni a gameplay verificado en Unity/Android.");
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
