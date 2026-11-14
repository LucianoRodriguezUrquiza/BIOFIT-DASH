using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Asistente de configuracion para la primera prueba BLE de BIOFIT GAME.
/// Crea la escena y la agrega al listado de compilacion de Android.
/// No modifica las Waveshare ni las preferencias de los sensores.
/// </summary>
public static class BiofitPrepararEscenaBLE
{
    private const string SceneFolder = "Assets/BIOFIT/Scenes";
    private const string ScenePath = SceneFolder + "/DiagnosticoBLE.unity";

    [MenuItem("BIOFIT/Preparar escena de diagnostico BLE")]
    public static void Preparar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets/BIOFIT", "Scenes");

        SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (existing == null)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject receiver = new GameObject("BIOFIT_BLE");
            receiver.AddComponent<BiofitBLEMonitor>();
            Selection.activeGameObject = receiver;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Hace que DiagnosticoBLE sea la primera escena de la APK.
        // Conserva las otras escenas existentes.
        var scenes = new List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (entry.path != ScenePath)
                scenes.Add(entry);
        }
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "BIOFIT GAME",
            "Escena de diagnostico BLE preparada.\n\n" +
            "En Play del Editor se mostrara la vista previa. " +
            "Para conectar los sensores BLE, hay que compilar y ejecutar en Android.",
            "Entendido");
    }
}
