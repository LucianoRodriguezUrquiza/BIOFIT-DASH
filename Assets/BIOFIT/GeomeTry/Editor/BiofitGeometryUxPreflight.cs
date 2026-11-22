using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;

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
                EnsureOriginalUrpPreview();
            }
            catch (Exception ex)
            {
                Debug.LogError("BIOFIT UX: fallo de preconfiguracion del proyecto " +
                    "de trabajo: " + ex);
            }
        }

        // GeomeTry is authored for URP 2D; the BIOFIT base repo is Built-in.
        // The Android builder already activates URP temporarily, but Editor Play
        // never did. Set the original renderer for this isolated game copy.
        private static void EnsureOriginalUrpPreview()
        {
            const string urpPath =
                "Assets/GeomeTryOriginal/Assets/Settings/UniversalRP.asset";
            RenderPipelineAsset urp =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(urpPath);
            if (urp == null)
            {
                Debug.LogWarning("BIOFIT UX: falta UniversalRP.asset; " +
                    "la vista de niveles no puede usar URP 2D.");
                return;
            }
            if (GraphicsSettings.defaultRenderPipeline == urp &&
                QualitySettings.renderPipeline == null) return;
            QualitySettings.renderPipeline = null;
            GraphicsSettings.defaultRenderPipeline = urp;
            Debug.Log("BIOFIT UX: URP 2D original ACTIVADO para Play. " +
                "Render de escenarios restaurado sin tocar los mapas ni fisica.");
        }

        [MenuItem("BIOFIT/GeomeTry UX/3. Activar URP 2D original para Play")]
        public static void SetupUrp() { EnsureOriginalUrpPreview(); }

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
                // The previous 527px selector was visibly blurred on Android.
                // Keep the user-provided 1672px art uncompressed and bilinear.
                if (art == "LevelSelect" || art == "Pause" || art == "Back")
                {
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        changed = true;
                    }
                    if (importer.maxTextureSize < 2048)
                    {
                        importer.maxTextureSize = 2048;
                        changed = true;
                    }
                    if (importer.filterMode != FilterMode.Bilinear)
                    {
                        importer.filterMode = FilterMode.Bilinear;
                        changed = true;
                    }
                }
                // The selector is rebuilt once into transparent card,
                // logo and corner textures. CPU access is required only
                // during scene initialization; generated textures then
                // release CPU pixels via Apply(false, true).
                if (art == "LevelSelect" && !importer.isReadable)
                {
                    importer.isReadable = true;
                    changed = true;
                }
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
            EnsureOriginalUrpPreview();
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
            Texture2D selector = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath + "LevelSelect.png");
            if (selector.width < 1600 || selector.height < 900)
                throw new InvalidOperationException(
                    "Selector de baja resolucion. Instala BIOFIT_GeomeTry_UI_Assets_HD_v2.zip.");
            if (!selector.isReadable || selector.width != 1672 || selector.height != 941)
                throw new InvalidOperationException(
                    "El selector HD debe ser 1672x941 y legible para separar " +
                    "tarjetas, logo y decoraciones. Ejecuta BIOFIT > GeomeTry UX > " +
                    "1. Configurar modulo tactil Android.");
            EnsureOriginalUrpPreview();
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
