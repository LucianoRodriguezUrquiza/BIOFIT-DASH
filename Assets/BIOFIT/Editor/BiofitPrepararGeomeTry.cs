using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit opt-in scene builder. Never overwrites the user's existing
/// diagnostic/Dash scenes or ProjectSettings. Keeps current scenes until
/// Unity's native save/discard/cancel dialog is answered.
/// </summary>
public static class BiofitPrepararGeomeTry
{
    private const string Folder = "Assets/BIOFIT/Scenes";
    private const string ScenePath = Folder + "/BiofitGeometryLegacy.unity";

    [MenuItem("BIOFIT/Crear o abrir GEOMETRY (BLE LEGACY)")]
    public static void Preparar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/BIOFIT", "Scenes");

        SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (existing != null)
        {
            // A local existing scene is never recreated or reset.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.065f, 0.085f, 0.16f);
            cameraObject.transform.position = new Vector3(3.4f, 2.8f, -10f);

            GameObject gameObject = new GameObject("BIOFIT_GEOMETRY");
            gameObject.AddComponent<BiofitBLEMonitor>();
            gameObject.AddComponent<BiofitGeomeTryGame>();
            Selection.activeGameObject = gameObject;
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                EditorUtility.DisplayDialog("BIOFIT GEOMETRY",
                    "Error guardando la escena. No se cambiaron las escenas existentes.",
                    "Aceptar");
                return;
            }
        }

        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        foreach (EditorBuildSettingsScene scene in scenes)
            if (scene.path == ScenePath) found = true;
        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        EditorUtility.DisplayDialog("BIOFIT GEOMETRY",
            "Escena independiente lista: BiofitGeometryLegacy.\n\n" +
            "PLAY: ESPACIO/ARRIBA para saltar; R reinicia; P pausa.\n" +
            "En Android: SALTAR, BLE LEGACY, PAUSA, REINICIAR y DIAG BLE.\n\n" +
            "El modo legacy esta seleccionado y requiere ambos trackers conectados.\n" +
            "No se tocan firmware, umbrales, capturas o escenas previas.\n\n" +
            "Antes de compilar una APK, comprobar Build Profiles, package ID y firma.",
            "Entendido");
    }
}
