using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Biofit.Geometry.Editor
{
    public static class BiofitGeometryEditor
    {
        public const string ScenePath="Assets/BIOFIT/Geometry/Scenes/BiofitGeometry.unity";
        [MenuItem("BIOFIT/Geometry/1. Abrir juego (cuatro niveles LEGACY)")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("BIOFIT/Geometry/2. Validar niveles y fisica (en Play)")]
        public static void Validate()
        {
            if(!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Validacion Unity", "Abri la escena del juego y pulsa Play; luego ejecuta este menu. Se prueba el Rigidbody2D real en escenas fisicas aisladas.","Aceptar");return;
            }
            GeometryPlayValidation.Run();
        }

        [MenuItem("BIOFIT/Geometry/3. Compilar APK cuatro niveles LEGACY")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Detene Play antes de compilar.");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("Seleccionar Android", "File > Build Profiles > Android > Switch Platform. Requiere Android Build Support de Unity 6000.3.28f1.","Aceptar");return;
            }
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            for(int i=0;i<4;i++)
            {
                var asset=Resources.Load<TextAsset>("BiofitGeometry/Levels/"+GeometryLevel.ResourceNames[i]);
                if(asset==null)throw new InvalidOperationException("Falta mapa "+GeometryLevel.ResourceNames[i]);
                JsonUtility.FromJson<GeometryLevel>(asset.text).Validate(i+1);
            }
            string folder=Path.Combine(Directory.GetCurrentDirectory(),"Builds");Directory.CreateDirectory(folder);
            // A fresh filename protects any APK previously built by the user.
            string output=Path.Combine(folder,"BIOFIT_GEOMETRY_LEGACY_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".apk");
            bool oldBundle=EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle=false;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.Android,options=BuildOptions.None
                });
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("APK no generada: "+report.summary.result);
                Debug.Log("APK generada (no validada fisicamente): "+output);EditorUtility.RevealInFinder(output);
            }
            finally { EditorUserBuildSettings.buildAppBundle=oldBundle; }
            // Never overwrite EditorBuildSettings, package ID, signing, version
            // code, diagnostic scenes or the user's ProjectSettings here.
        }
    }
}
