using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Editor-only. Never changes firmware, NVS, package ID, signing or stored sessions.
public static class BiofitBuildAndroidPhaseExp
{
    [MenuItem("BIOFIT/Compilar APK Android v0.9.11 CICLO EXP")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUtility.DisplayDialog("BIOFIT: selecciona Android",
                "Abri File > Build Profiles y cambia primero la plataforma a Android. " +
                "Comproba que Android Build Support esta instalado. Volve a ejecutar este menu.",
                "Aceptar");
            return;
        }
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No hay escenas activas en Build Profiles.");
        string folder = Path.Combine(Directory.GetCurrentDirectory(),"Builds");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder,"BIOFIT_v0911_CICLO_EXP.apk");
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            EditorUserBuildSettings.buildAppBundle = false;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("La APK no se genero: " +
                    report.summary.result + ". Revisa la Console y Editor.log.");
            Debug.Log("BIOFIT APK experimental generada en: " + output);
            EditorUtility.RevealInFinder(output);
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = oldBundle;
        }
    }
}
