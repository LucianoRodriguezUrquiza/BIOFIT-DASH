// Host-only API shims, NOT Unity or the native UniBLE plugin.
// Used to exercise the actual receiver ReadStatus/ReadEvent/Update code.
using System;
using System.Threading.Tasks;
namespace UnityEngine {
 public static class Application { public static string persistentDataPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"biofit-host"); public static void OpenURL(string path){} }
 public class Object { public string name; public static void Destroy(Object o){} public static T[] FindObjectsByType<T>(FindObjectsSortMode mode)=>new T[0]; }
 public class Component : Object { public bool enabled=true; public GameObject gameObject; public Transform transform; public T GetComponent<T>()=>default; public T GetComponentInChildren<T>()=>default; }
 public class MonoBehaviour : Component {}
 public class GameObject : Object { public Transform transform; public GameObject(string s=""){} public T AddComponent<T>()=>default(T); public T GetComponent<T>()=>default(T); public void SetActive(bool v){} }
 public class HeaderAttribute:Attribute { public HeaderAttribute(string s){} }
 public class SerializeField:Attribute {}
 public static partial class Time { public static float realtimeSinceStartup=10; }
 public static class Debug { public static void Log(object o){} public static void LogError(object o){} public static void LogWarning(object o){} public static void LogException(Exception e){throw e;} }
 public static partial class Screen { public static int width=1080,height=1920; }
 public static partial class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); }

 public struct Rect { public float x,y,width,height; public Rect(float a,float b,float c,float d){ x=a; y=b; width=c; height=d; } }
 public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v)=>new Matrix4x4(); public static Matrix4x4 TRS(Vector3 v,Quaternion q,Vector3 s)=>new Matrix4x4(); }
 public struct Color { public static Color white,clear,cyan; public Color(float a,float b,float c){} public Color(float a,float b,float c,float d){} }
 public enum FontStyle { Bold }
 public enum TextAnchor { MiddleLeft, MiddleCenter }
 public class GUIStyleState { public Color textColor; }
 public class GUIStyle { public int fontSize; public FontStyle fontStyle; public bool wordWrap; public TextAnchor alignment; public GUIStyleState normal=new GUIStyleState(); public GUIStyle(object o){} }
 public class GUISkin { public object label,button; }
 public static partial class GUI { public static readonly System.Collections.Generic.List<string> ButtonTexts=new System.Collections.Generic.List<string>(); public static bool enabled=true; public static Matrix4x4 matrix; public static GUISkin skin=new GUISkin(); public static void Box(Rect r,string s){} public static void Label(Rect r,string s,GUIStyle st){} public static string ClickNext; public static bool Button(Rect r,string s,GUIStyle st){ButtonTexts.Add(s);if(!enabled || ClickNext!=s)return false;ClickNext=null;return true;} public static Vector2 BeginScrollView(Rect r,Vector2 v,Rect c,bool h,bool b)=>v; public static void EndScrollView(){} }
 public static class GUIUtility { public static string systemCopyBuffer; }
}
#if !DASH_UI_TESTS
public class BiofitDashGame : UnityEngine.MonoBehaviour {}
#endif
namespace UniBLE {
 // Surface checked against UniBLE v0.1.1 dba3f296; this does not execute BLE.
 public enum BleConnectionState { Disconnected,Connecting,Connected,Disconnecting }
 public interface IBleCharacteristic { Task SubscribeAsync(Action<byte[]> a, System.Threading.CancellationToken cancellationToken=default); Task UnsubscribeAsync(System.Threading.CancellationToken cancellationToken=default); Task WriteAsync(byte[] data,bool withResponse=true,System.Threading.CancellationToken cancellationToken=default); }
 public interface IBleService { Task<IBleCharacteristic> GetCharacteristicAsync(string s); }
 public interface IBleDevice { event Action<BleConnectionState> OnConnectionStateChanged; Task ConnectAsync(); Task<IBleService> GetServiceAsync(string s); }
 public interface IBleAdapter { Task<bool> RequestPermissionAsync(); Task<bool> IsAvailableAsync(); Task<IBleDevice> GetDeviceAsync(string s); }
 public static class BleManager { public static IBleAdapter Adapter; }
}
