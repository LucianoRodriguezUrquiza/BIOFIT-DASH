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
        public AndroidJavaObject(string name, params object[] args) { kind = name;
            if(name=="java.lang.Integer") values["integer"]=args[0].ToString();
            if(name=="java.util.Scanner") entry=((AndroidJavaObject)args[0]).entry;
        }
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
                values[(string)args[0]] = args[1] is AndroidJavaObject a ? a.values["integer"] : (string)args[1];
                return;
            }
            if (kind == "stream" && method == "write")
            {
                if (FakeStore.FailWrite) throw new IOException("disk full");
                entry.Bytes = (byte[])((byte[])args[0]).Clone();
                if(FakeStore.Corrupt && entry.Bytes.Length>10) entry.Bytes[10]^=1;
                return;
            }
            if (kind == "stream" && method == "flush") return;
            if ((kind=="input" || kind=="java.util.Scanner") && method=="close") return;
            if (method == "close" && (kind == "stream" || kind == "descriptor"))
            {
                if (kind == "stream") { FakeStore.StreamClosed = true; if(FakeStore.FailClose) throw new IOException("close failed"); }
                else FakeStore.DescriptorClosed = true;
                return;
            }
            throw new InvalidOperationException(kind + "." + method);
        }

        public virtual T Call<T>(string method, params object[] args)
        {
            object value;
            if(kind=="java.util.Scanner" && method=="useDelimiter") value=this;
            else if(kind=="java.util.Scanner" && method=="hasNext") value=entry.Bytes.Length>0;
            else if(kind=="java.util.Scanner" && method=="next") value=System.Text.Encoding.UTF8.GetString(entry.Bytes);
            else if(kind=="resolver" && method=="update") { ((AndroidJavaObject)args[0]).entry.Published=true; value=1; }
            else if(kind=="resolver" && method=="openInputStream") value=new AndroidJavaObject("input") {entry=((AndroidJavaObject)args[0]).entry};
            else if (kind == "activity" && method == "getContentResolver")
                value = new AndroidJavaObject("resolver");
            else if (kind == "resolver" && method == "insert")
            {
                var metadata = (AndroidJavaObject)args[1];
                if (metadata.values["mime_type"] != "text/csv" ||
                    metadata.values["relative_path"] != "Download/")
                    throw new InvalidOperationException("unexpected destination");
                if(metadata.values["is_pending"]!="1") throw new Exception("must insert pending");
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
                if (!FakeStore.StreamClosed) throw new IOException("output not closed");
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
    public sealed class Entry { public string Name; public byte[] Bytes; public bool Published; }
    public static readonly List<Entry> Entries = new List<Entry>();
    public static int Sdk = 32;
    public static bool FailWrite;
    public static bool WrongSize;
    public static bool StreamClosed, DescriptorClosed, FailClose, Corrupt;
    public static void Reset()
    {
        Entries.Clear();
        Sdk = 32;
        FailWrite = false;
        WrongSize = false;
        StreamClosed = DescriptorClosed = FailClose = Corrupt = false;
    }
}
