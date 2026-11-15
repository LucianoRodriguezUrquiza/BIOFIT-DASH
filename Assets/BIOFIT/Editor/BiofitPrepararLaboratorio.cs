using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Agrega el laboratorio a la escena BLE ya existente. No crea otra
/// conexion Bluetooth ni reemplaza el receptor que ya funciono en Android.
/// </summary>
public static class BiofitPrepararLaboratorio
{
    private const string ScenePath = "Assets/BIOFIT/Scenes/DiagnosticoBLE.unity";

    [MenuItem("BIOFIT/Agregar laboratorio 2D a DiagnosticoBLE")]
    public static void Agregar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null)
        {
            EditorUtility.DisplayDialog("BIOFIT",
                "No existe DiagnosticoBLE. Primero ejecuta: " +
                "BIOFIT > Preparar escena de diagnostico BLE.", "Aceptar");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var receiver = GameObject.Find("BIOFIT_BLE");

        if (receiver == null || receiver.GetComponent<BiofitBLEMonitor>() == null)
        {
            EditorUtility.DisplayDialog("BIOFIT",
                "No encontre el receptor BIOFIT_BLE. Verifica la escena de diagnostico.",
                "Aceptar");
            return;
        }

        if (receiver.GetComponent<BiofitMovementLab>() == null)
            receiver.AddComponent<BiofitMovementLab>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = receiver;

        EditorUtility.DisplayDialog("BIOFIT",
            "Laboratorio 2D incorporado a DiagnosticoBLE.\n\n" +
            "Para probar SIN sensores: presiona Play en Unity, " +
            "entra a ABRIR LABORATORIO 2D y pulsa A y D alternadamente. " +
            "ESPACIO simula un salto, pero NO detecta saltos corporales.\n\n" +
            "Todavia NO hace falta generar otra APK.", "Entendido");
    }
}
