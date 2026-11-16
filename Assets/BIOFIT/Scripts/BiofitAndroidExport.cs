using System;
using UnityEngine;

/// <summary>
/// SAF Android export. Open() only requests a picker; PollStatus() reports the
/// later user confirmation, cancellation or failure. No storage permissions.
/// </summary>
public static class BiofitAndroidExport
{
    public static string LastError { get; private set; } = "";

    public static bool Open(string path)
    {
        LastError = "";
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var exporter = new AndroidJavaClass("com.biofit.export.CsvExportActivity"))
            {
                if (!exporter.CallStatic<bool>("start", activity, path))
                {
                    LastError = exporter.GetStatic<string>("status");
                    Debug.LogWarning("BIOFIT export: " + LastError);
                    return false;
                }
            }
#else
            Application.OpenURL(new Uri(path).AbsoluteUri);
#endif
            return true;
        }
        catch (Exception ex)
        {
            LastError = "ERROR al abrir ARCHIVO: " + ex.Message;
            Debug.LogWarning("BIOFIT export: " + LastError);
            return false;
        }
    }

    // Polled only while a user-initiated Android export is pending.
    public static string PollStatus()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var exporter = new AndroidJavaClass("com.biofit.export.CsvExportActivity"))
                return exporter.GetStatic<string>("status");
        }
        catch (Exception ex)
        {
            return "ERROR al consultar exportacion: " + ex.Message;
        }
#else
        return "EDITOR: selector Android no disponible";
#endif
    }

    public static bool IsFinalStatus(string status)
    {
        return !string.IsNullOrEmpty(status) &&
            (status.StartsWith("GUARDADO:", StringComparison.Ordinal) ||
             status.StartsWith("CANCELADO:", StringComparison.Ordinal) ||
             status.StartsWith("ERROR", StringComparison.Ordinal) ||
             status.StartsWith("EDITOR:", StringComparison.Ordinal));
    }
}
