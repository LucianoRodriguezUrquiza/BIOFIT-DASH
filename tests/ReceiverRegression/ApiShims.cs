// Host-only API shims, NOT Unity or the native UniBLE plugin.
// Used to exercise the actual receiver ReadStatus/ReadEvent/Update code.
using System;
using System.Threading.Tasks;
namespace UnityEngine {
 public static class Application { public static string persistentDataPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"biofit-host"); public static void OpenURL(string path){} }
 public class MonoBehaviour { public T GetComponent<T>()=>(T)Activator.CreateInstance(typeof(T)); public GameObject gameObject=new GameObject(); protected void Destroy(object o){} }
 public class GameObject { public T AddComponent<T>()=>default(T); }
 public class HeaderAttribute:Attribute { public HeaderAttribute(string s){} }
 public class SerializeField:Attribute {}
 public static partial class Time { public static float realtimeSinceStartup=10; }
 public static class PlayerPrefs {
  private static readonly System.Collections.Generic.Dictionary<string,int> ints =
      new System.Collections.Generic.Dictionary<string,int>();
  public static int GetInt(string key,int fallback=0) =>
      ints.TryGetValue(key,out int value) ? value : fallback;
  public static void SetInt(string key,int value) { ints[key]=value; }
  public static void Save() {}
 }
 public static class Debug { public static void Log(object o){} public static void LogWarning(object o){} public static void LogException(Exception e){throw e;} }
 public static partial class Screen { public static int width=1080,height=1920; }
 public static partial class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); }
 public struct Vector2 {} public struct Vector3 { public Vector3(float a,float b,float c){} }
 public struct Rect { public float x,y,width,height; public Rect(float a,float b,float c,float d){ x=a; y=b; width=c; height=d; } }
 public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v)=>new Matrix4x4(); }
 public struct Color { public static Color white; public Color(float a,float b,float c){} public Color(float a,float b,float c,float d){} }
 public enum FontStyle { Bold }
 public enum TextAnchor { MiddleLeft, MiddleCenter }
 public class GUIStyleState { public Color textColor; }
 public class GUIStyle { public int fontSize; public FontStyle fontStyle; public bool wordWrap; public TextAnchor alignment; public GUIStyleState normal=new GUIStyleState(); public GUIStyle(object o){} }
 public class GUISkin { public object label,button; }
 public static partial class GUI { public static bool enabled=true; public static Matrix4x4 matrix; public static GUISkin skin=new GUISkin(); public static void Box(Rect r,string s){} public static void Label(Rect r,string s,GUIStyle st){} public static string ClickNext; public static bool Button(Rect r,string s,GUIStyle st){if(!enabled || ClickNext!=s)return false;ClickNext=null;return true;} public static Vector2 BeginScrollView(Rect r,Vector2 v,Rect c,bool h,bool b)=>v; public static void EndScrollView(){} }
 public static class GUIUtility { public static string systemCopyBuffer; }
}
#if !DASH_UI_TESTS
public class BiofitDashGame {}
#endif
namespace UniBLE {
 // Surface checked against UniBLE v0.1.1 dba3f296; this does not execute BLE.
 public enum BleConnectionState { Disconnected,Connecting,Connected,Disconnecting }
 public interface IBleCharacteristic { Task SubscribeAsync(Action<byte[]> a, System.Threading.CancellationToken cancellationToken=default); Task UnsubscribeAsync(System.Threading.CancellationToken cancellationToken=default); Task WriteAsync(byte[] data,bool withResponse=true,System.Threading.CancellationToken cancellationToken=default); }
 public interface IBleService { Task<IBleCharacteristic> GetCharacteristicAsync(string s,System.Threading.CancellationToken token=default); }
 public interface IBleDevice { BleConnectionState ConnectionState {get;} event Action<BleConnectionState> OnConnectionStateChanged; Task ConnectAsync(System.Threading.CancellationToken token=default); Task DisconnectAsync(System.Threading.CancellationToken token=default); Task<IBleService> GetServiceAsync(string s,System.Threading.CancellationToken token=default); }
 public interface IBleAdapter { Task<bool> RequestPermissionAsync(System.Threading.CancellationToken token=default); Task<bool> IsAvailableAsync(System.Threading.CancellationToken token=default); Task<IBleDevice> GetDeviceAsync(string s,System.Threading.CancellationToken token=default); }
 public static class BleManager { public static IBleAdapter Adapter; }
}
