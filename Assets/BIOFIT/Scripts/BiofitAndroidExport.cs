using System;
using UnityEngine;

public static class BiofitAndroidExport
{
    // True means the destination picker was requested, not that the user saved.
    public static bool Open(string path)
    {
        try {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var exporter=new AndroidJavaClass("com.biofit.export.CsvExportActivity"))
                exporter.CallStatic("start",activity,path);
#else
            Application.OpenURL(new Uri(path).AbsoluteUri);
#endif
            return true;
        } catch(Exception ex) { Debug.LogWarning("BIOFIT export: "+ex.Message);return false; }
    }
}
