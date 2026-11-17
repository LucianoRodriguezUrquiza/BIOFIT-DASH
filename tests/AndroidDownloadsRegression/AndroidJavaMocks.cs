using System;
using System.IO;
using System.Collections.Generic;

namespace UnityEngine
{
    // Simulates public Android API contracts at the JNI boundary.
    // These shims compile the actual UNITY_ANDROID branch in CI.
    public class AndroidJavaObject : IDisposable
    {
        protected readonly string kind;
        internal readonly Dictionary<string, string> values =
            new Dictionary<string, string>();
        internal FakeStore.Entry entry;
        public AndroidJavaObject(string name) { kind = name; }
        public virtual void Dispose() { }

        public virtual T GetStatic<T>(string key)
        {
            object value;
            if (kind == "android.os.Build$VERSION" && key == "SDK_INT")
                value = FakeStore.Sdk;
            else if (kind == "android.os.Environment" && key == "DIRECTORY_DOWNLOADS")
                value = "Download";
            else if (kind == "com.unity3d.player.UnityPlayer" && key == "currentActivity")
                value = new AndroidJavaObject("activity");
            else if (kind == "android.provider.MediaStore$Downloads" &&
                     key == "EXTERNAL_CONTENT_URI")
                value = new AndroidJavaObject("collection");
            else throw new InvalidOperationException(kind + "." + key);
            return (T)value;
        }

        public virtual void Call(string method, params object[] args)
        {
            if (kind == "android.content.ContentValues" && method == "put")
            {
                values[(string)args[0]] = (string)args[1];
                return;
            }
            if (kind == "stream" && method == "write")
            {
                if (FakeStore.FailWrite) throw new IOException("disk full");
                entry.Bytes = (byte[])((byte[])args[0]).Clone();
                return;
            }
            if (kind == "stream" && method == "flush") return;
            throw new InvalidOperationException(kind + "." + method);
        }

        public virtual T Call<T>(string method, params object[] args)
        {
            object value;
            if (kind == "activity" && method == "getContentResolver")
                value = new AndroidJavaObject("resolver");
            else if (kind == "resolver" && method == "insert")
            {
                var metadata = (AndroidJavaObject)args[1];
                if (metadata.values["mime_type"] != "text/csv" ||
                    metadata.values["relative_path"] != "Download/")
                    throw new InvalidOperationException("unexpected destination");
                var e = new FakeStore.Entry {
                    Name = metadata.values["_display_name"],
                    Bytes = new byte[0] };
                FakeStore.Entries.Add(e);
                value = new AndroidJavaObject("uri") { entry = e };
            }
            else if (kind == "resolver" && method == "openOutputStream")
            {
                value = new AndroidJavaObject("stream") {
                    entry = ((AndroidJavaObject)args[0]).entry };
            }
            else if (kind == "resolver" && method == "openFileDescriptor")
            {
                value = new AndroidJavaObject("descriptor") {
                    entry = ((AndroidJavaObject)args[0]).entry };
            }
            else if (kind == "descriptor" && method == "getStatSize")
                value = FakeStore.WrongSize ? entry.Bytes.LongLength + 5L : entry.Bytes.LongLength;
            else if (kind == "resolver" && method == "delete")
            {
                FakeStore.Entries.Remove(((AndroidJavaObject)args[0]).entry);
                value = 1;
            }
            else throw new InvalidOperationException(kind + "." + method);
            return (T)value;
        }
    }
    public sealed class AndroidJavaClass : AndroidJavaObject
    {
        public AndroidJavaClass(string name) : base(name) { }
    }
}
public static class FakeStore
{
    public sealed class Entry { public string Name; public byte[] Bytes; }
    public static readonly List<Entry> Entries = new List<Entry>();
    public static int Sdk = 32;
    public static bool FailWrite;
    public static bool WrongSize;
    public static void Reset()
    {
        Entries.Clear();
        Sdk = 32;
        FailWrite = false;
        WrongSize = false;
    }
}
