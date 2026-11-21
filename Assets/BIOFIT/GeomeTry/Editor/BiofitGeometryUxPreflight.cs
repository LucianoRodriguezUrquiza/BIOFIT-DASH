using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Biofit.GeomeTryOriginal.Editor
{
    /// <summary>
    /// Preflight for the requested BIOFIT UX, with an idempotent fix for
    /// GeomeTry's original InputSystemUIInputModule on Android/old InputManager.
    /// Only the imported working-copy EventSystem prefab is touched.
    /// </summary>
    [InitializeOnLoad]
    internal static class BiofitGeometryUxPreflight
    {
        private const string PrefabPath =
            "Assets/GeomeTryOriginal/Assets/Resources/Prefabs/EventSystem.prefab";
        private const string ImagePath = "Assets/BIOFIT/Resources/BiofitUi/";
        private static readonly string[] Scenes = {
            "Main Menu", "Level", "Level Editor", "Customisation",
            "Level Selection", "Success", "Settings", "Stats", "Profile", "Credits"
        };

        static BiofitGeometryUxPreflight()
        {
            // After the user has copied the pinned Assets submodule, Unity can
            // import the old-input UI prefab automatically. No scene edits.
            EditorApplication.delayCall += AutoRepairInput;
        }

        private static void AutoRepairInput()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                return;
            try
            {
                RepairEventSystemInput(false);
                PrepareVisualImporters();
            }
            catch (Exception ex)
            {
                Debug.LogError("BIOFIT UX: fallo de preconfiguracion del proyecto " +
                    "de trabajo: " + ex);
            }
        }

        private static void PrepareVisualImporters()
        {
            // Sprite.Create may need CPU-readable source pixels on Android.
            // Set it once, never at runtime; disable unused UI mipmaps.
            foreach (string art in new[] { "Skins", "LevelSelect", "Back", "Pause" })
            {
                string asset = ImagePath + art + ".png";
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                if (importer == null) continue;
                bool changed = importer.mipmapEnabled;
                if (importer.mipmapEnabled) importer.mipmapEnabled = false;
                if (art == "Skins" && !importer.isReadable)
                {
                    importer.isReadable = true;
                    changed = true;
                }
                if (art == "Skins" && importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        [MenuItem("BIOFIT/GeomeTry UX/1. Configurar modulo tactil Android")]
        public static void SetupInput()
        {
            RepairEventSystemInput(true);
            PrepareVisualImporters();
        }

        private static void RepairEventSystemInput(bool report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Falta el prefab EventSystem original.");
            var originalModules = prefab.GetComponents<BaseInputModule>();
            if (originalModules.Length == 1 &&
                originalModules[0] is StandaloneInputModule)
            {
                if (report)
                    Debug.Log("BIOFIT UX: EventSystem ya usa StandaloneInputModule.");
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (BaseInputModule module in root.GetComponents<BaseInputModule>())
                    UnityEngine.Object.DestroyImmediate(module, true);
                root.AddComponent<StandaloneInputModule>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("BIOFIT UX: EventSystem original ahora usa entrada tactil " +
                "Input Manager (Old), sin modificar niveles ni fisica.");
        }

        [MenuItem("BIOFIT/GeomeTry UX/2. Verificar UX y recursos")]
        public static void Verify()
        {
            string[] registered = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToArray();
            string[] expected = Scenes.Select(s =>
                "Assets/GeomeTryOriginal/Assets/Scenes/" + s + ".unity").ToArray();
            if (!registered.SequenceEqual(expected))
                throw new InvalidOperationException(
                    "Scene List debe tener las 10 escenas originales en orden.");
            foreach (string name in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/GeomeTryOriginal/Assets/Scenes/" + name + ".unity") == null)
                    throw new InvalidOperationException("Falta escena: " + name);
            foreach (string level in new[] {
                "BackOnTrack", "JumpyNightmare", "Polargeist",
                "StereoMadness", "WaveVibe"
            })
                if (Resources.Load<TextAsset>("maps/BASE/" + level) == null)
                    throw new InvalidOperationException("Falta JSON original: " + level);
            foreach (string art in new[] { "Skins", "LevelSelect", "Back", "Pause" })
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath + art + ".png") == null)
                    throw new InvalidOperationException(
                        "Falta imagen del pack: " + art +
                        ". Ejecuta tools/install_geometry_ui_assets.ps1 con Unity cerrado.");
            Texture2D skin = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath + "Skins.png");
            if (skin.width != 1280 || skin.height != 887)
                throw new InvalidOperationException(
                    "El atlas de 70 cubos no coincide con la referencia del usuario.");
            if (!skin.isReadable)
                throw new InvalidOperationException(
                    "Atlas sin lectura CPU. Ejecuta Configurar modulo tactil Android " +
                    "para preparar tambien el importador de skins.");
            GameObject system = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (system == null || system.GetComponent<StandaloneInputModule>() == null)
                throw new InvalidOperationException(
                    "Ejecuta BIOFIT > GeomeTry UX > Configurar modulo tactil Android.");
            string settings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            if (!settings.Contains("activeInputHandler: 0"))
                throw new InvalidOperationException(
                    "Player > Other Settings > Active Input Handling debe estar " +
                    "en Input Manager (Old) para Android.");
            Debug.Log("BIOFIT UX: OK 10 escenas, 5 mapas originales, " +
                "4 imagenes, 70 skins, EventSystem tactil e input Android antiguo. " +
                "FPS reales y BLE requieren prueba fisica.");
        }
    }
}
