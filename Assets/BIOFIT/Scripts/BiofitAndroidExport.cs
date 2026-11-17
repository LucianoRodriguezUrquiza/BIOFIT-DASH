using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Exports a completed, SHA256-protected BIOFIT archive directly to Android
/// Downloads using the platform MediaStore API (Android 10/API 29+).
/// No picker, no clipboard, no custom Java class and no broad storage permission.
/// This does NOT measure or affect sensor input latency.
/// </summary>
public static class BiofitAndroidExport
{
    public static string LastError { get; private set; } = "";
    public static string LastStatus { get; private set; } = "";
    public static string LastFileName { get; private set; } = "";

    // Keep the old entry point for callers; it now performs a synchronous save.
    public static bool Open(string path)
    {
        return SaveToDownloads(path);
    }

    public static bool SaveToDownloads(string path)
    {
        LastError = "";
        LastStatus = "";
        LastFileName = "";
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("No hay un CSV privado para exportar");

            string name = Path.GetFileName(path);
            if (!name.StartsWith("BIOFIT_v0", StringComparison.Ordinal) ||
                !name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Nombre de archivo BIOFIT no valido");

            byte[] bytes = File.ReadAllBytes(path);
            string text = new UTF8Encoding(false,true).GetString(bytes);
            if (!BiofitCaptureArchive.Verify(text))
                throw new InvalidDataException("CSV incompleto o SHA256 incorrecto");

#if UNITY_ANDROID && !UNITY_EDITOR
            SaveAndroid(bytes, name);
            LastStatus = "GUARDADO en Descargas (" + bytes.Length + " bytes)";
#else
            // Editor and CI: real local copy, never described as an Android download.
            string directory = Path.Combine(Application.persistentDataPath, "BiofitEditorExports");
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, name);
            File.WriteAllBytes(destination, bytes);
            if (!BiofitCaptureArchive.Verify(File.ReadAllText(destination, Encoding.UTF8)))
                throw new IOException("Fallo al verificar exportacion local");
            LastStatus = "EDITOR: CSV en BiofitEditorExports (" + bytes.Length + " bytes)";
#endif
            LastFileName = name;
            return true;
        }
        catch (Exception ex)
        {
            LastError = "ERROR CSV Descargas: " + ex.Message;
            LastStatus = LastError;
            Debug.LogWarning("BIOFIT: " + LastError);
            return false;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void SaveAndroid(byte[] bytes, string name)
    {
        using (var sdk = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            if (sdk.GetStatic<int>("SDK_INT") < 29)
                throw new NotSupportedException(
                    "Guardado automatico en Descargas requiere Android 10 o posterior");
        }

        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if (activity == null)
                throw new InvalidOperationException("Actividad Android no disponible");
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var collection = new AndroidJavaClass(
                "android.provider.MediaStore$Downloads"))
            using (var downloadsUri = collection.GetStatic<AndroidJavaObject>(
                "EXTERNAL_CONTENT_URI"))
            using (var metadata = new AndroidJavaObject("android.content.ContentValues"))
            using (var environment = new AndroidJavaClass("android.os.Environment"))
            {
                // The three strings are the standard MediaStore MediaColumns keys.
                // No app-defined Activity or Java plugin class is involved.
                metadata.Call("put", "_display_name", name);
                metadata.Call("put", "mime_type", "text/csv");
                metadata.Call("put", "relative_path",
                    environment.GetStatic<string>("DIRECTORY_DOWNLOADS") + "/");

                using(var pending = new AndroidJavaObject("java.lang.Integer", 1))
                    metadata.Call("put", "is_pending", pending);
                AndroidJavaObject savedUri = null;
                try
                {
                    savedUri = resolver.Call<AndroidJavaObject>(
                        "insert", downloadsUri, metadata);
                    if (savedUri == null)
                        throw new IOException("Android no pudo crear el CSV en Descargas");

                    // MediaStore owns this file; writing directly to a filesystem path
                    // would not be reliable on Android 10+ scoped storage.
                    using (var output = resolver.Call<AndroidJavaObject>(
                        "openOutputStream", savedUri))
                    {
                        if (output == null)
                            throw new IOException("Android no abrio el archivo de destino");
                        try
                        {
                            output.Call("write", bytes);
                            output.Call("flush");
                        }
                        finally { output.Call("close"); }
                    }

                    // Check the published document's reported byte count when the
                    // provider exposes it; the footer is checked before export.
                    using (var descriptor = resolver.Call<AndroidJavaObject>(
                        "openFileDescriptor", savedUri, "r"))
                    {
                        if (descriptor == null)
                            throw new IOException("Archivo de Descargas no reabrible");
                        try
                        {
                            long written = descriptor.Call<long>("getStatSize");
                            if (written < 0 || written != bytes.LongLength)
                                throw new IOException("No se pudo verificar el tamaño completo en Descargas");
                        }
                        finally { descriptor.Call("close"); }
                    }

                    using(var input = resolver.Call<AndroidJavaObject>("openInputStream", savedUri))
                    {
                        if(input==null) throw new IOException("No se pudo verificar el contenido exportado");
                        try
                        {
                            using(var scanner = new AndroidJavaObject("java.util.Scanner", input, "UTF-8"))
                            {
                                try
                                {
                                    using(var ignored = scanner.Call<AndroidJavaObject>("useDelimiter", "\\A")) { }
                                    string copied = scanner.Call<bool>("hasNext") ? scanner.Call<string>("next") : "";
                                    if(copied != Encoding.UTF8.GetString(bytes) || !BiofitCaptureArchive.Verify(copied))
                                        throw new IOException("Contenido o SHA256 de Descargas incorrecto");
                                }
                                finally { scanner.Call("close"); }
                            }
                        }
                        finally { input.Call("close"); }
                    }
                    using(var published = new AndroidJavaObject("android.content.ContentValues"))
                    using(var ready = new AndroidJavaObject("java.lang.Integer", 0))
                    {
                        published.Call("put", "is_pending", ready);
                        if(resolver.Call<int>("update",savedUri,published,"",new string[0]) != 1)
                            throw new IOException("No se pudo publicar el CSV en Descargas");
                    }

                    Debug.Log("BIOFIT: CSV guardado en Descargas/" + name +
                              " (" + bytes.Length + " bytes)");
                }
                catch
                {
                    // Best-effort deletion of an incomplete entry: leave the private
                    // SHA256-verified original untouched for a later retry.
                    if (savedUri != null)
                    {
                        try
                        {
                            resolver.Call<int>("delete", savedUri,
                                "", new string[0]);
                        }
                        catch (Exception cleanupError)
                        {
                            Debug.LogWarning("BIOFIT: no se pudo limpiar CSV parcial: " +
                                             cleanupError.Message);
                        }
                    }
                    throw;
                }
                finally
                {
                    if (savedUri != null) savedUri.Dispose();
                }
            }
        }
    }
#endif

    // Backwards-compatible status for older UI/tests. The save is synchronous.
    public static string PollStatus() { return LastStatus; }

    public static bool IsFinalStatus(string status)
    {
        return !string.IsNullOrEmpty(status) &&
            (status.StartsWith("GUARDADO", StringComparison.Ordinal) ||
             status.StartsWith("EDITOR:", StringComparison.Ordinal) ||
             status.StartsWith("ERROR", StringComparison.Ordinal) ||
             status.StartsWith("CANCELADO:", StringComparison.Ordinal));
    }
}
