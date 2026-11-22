// Host-only API shims, NOT Unity or the native UniBLE plugin.
// Used to exercise the actual receiver ReadStatus/ReadEvent/Update code.
using System;
using System.Threading.Tasks;
namespace UnityEngine {
 public static class Application { public static string unityVersion="HOST_SHIMS"; public static string persistentDataPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"biofit-host"); public static void OpenURL(string path){} }
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int order){}}
 public class HeaderAttribute:Attribute { public HeaderAttribute(string s){} }
 public class SerializeField:Attribute {}
 public static partial class Time { public static float timeScale=1.5f,deltaTime=.02f,unscaledTime; public static float realtimeSinceStartup=10; }
 public static class Debug { public static void Log(object o){} public static void LogError(object o){System.Console.WriteLine(o);} public static void LogWarning(object o){} public static void LogException(Exception e){throw e;} }
 public static partial class Screen { public static int width=1080,height=1920; }
 public static partial class Mathf { public static float Round(float x)=>(float)Math.Round(x); public static float LerpAngle(float a,float b,float t)=>a+(b-a)*t; public static float Max(float a,float b)=>Math.Max(a,b); }
 
 public struct Rect { public float x,y,width,height; public Rect(float a,float b,float c,float d){ x=a; y=b; width=c; height=d; } }
 public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v)=>new Matrix4x4(); }
 public struct Color { public static Color white,red; public Color(float a,float b,float c){} public Color(float a,float b,float c,float d){} }
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
 public interface IBleService { Task<IBleCharacteristic> GetCharacteristicAsync(string s, System.Threading.CancellationToken token=default); }
 public interface IBleDevice { event Action<BleConnectionState> OnConnectionStateChanged; Task ConnectAsync(System.Threading.CancellationToken token=default); Task DisconnectAsync(System.Threading.CancellationToken token=default); Task<IBleService> GetServiceAsync(string s, System.Threading.CancellationToken token=default); }
 public interface IBleAdapter { Task<bool> RequestPermissionAsync(); Task<bool> IsAvailableAsync(System.Threading.CancellationToken token=default); Task<IBleDevice> GetDeviceAsync(string s, System.Threading.CancellationToken token=default); }
 public static class BleManager { public static IBleAdapter Adapter; }
}
// Engine surface for compile + deterministic binding tests only. No Unity physics,
// scheduling, native destruction, SceneManager implementation or Android executes.
namespace UnityEngine {
 public class Object {
  static int next; readonly int id=++next; public bool destroyed, queued;
  public static readonly System.Collections.Generic.List<Object> All=new System.Collections.Generic.List<Object>();
  public Object(){All.Add(this);} public int GetInstanceID()=>id;
  public static bool operator ==(Object a,Object b){bool an=ReferenceEquals(a,null)||(!ReferenceEquals(a,null)&&a.destroyed),bn=ReferenceEquals(b,null)||(!ReferenceEquals(b,null)&&b.destroyed);return an||bn?an&&bn:ReferenceEquals(a,b);}
  public static bool operator !=(Object a,Object b)=>!(a==b);
  public override bool Equals(object b)=>ReferenceEquals(this,b);public override int GetHashCode()=>id;
  public static T FindFirstObjectByType<T>() where T:Object {foreach(var x in All)if(x is T t&&!t.destroyed&&!t.queued)return t;return null;}
  public static Func<GameObject,Vector3,Quaternion,Transform,GameObject> Factory;
  public static GameObject Instantiate(GameObject g,Vector3 p,Quaternion q,Transform parent)=>Factory(g,p,q,parent);
  public static void Destroy(Object o){if(o!=null)o.queued=true;}
  public static void FlushDestroyed(){foreach(var o in All.ToArray())if(o.queued&&!o.destroyed){if(o is GameObject g)foreach(var c in g.Components.Values){var method=c.GetType().GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);method?.Invoke(c,null);c.destroyed=true;}o.destroyed=true;}}
  public static void DontDestroyOnLoad(Object o){}
 }
 public class MonoBehaviour:Object {
  public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy&&!destroyed;
  public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;
  public T GetComponent<T>()=>gameObject.GetComponent<T>();
  public void Invoke(string n,float delay){} public void CancelInvoke(string n){}
 }
 public class GameObject:Object {
  public string name,tag;public bool activeInHierarchy=true;public Transform transform=new Transform();
  public readonly System.Collections.Generic.Dictionary<Type,MonoBehaviour> Components=new System.Collections.Generic.Dictionary<Type,MonoBehaviour>();
  public GameObject(){}public GameObject(string name){this.name=name;transform.name=name;}
  public T GetComponent<T>(){foreach(var x in Components.Values)if(x is T t)return t;return default(T);}
  public T AddComponent<T>(){var value=(T)Activator.CreateInstance(typeof(T));Attach((MonoBehaviour)(object)value);return value;}
  public void Attach(MonoBehaviour v){v.gameObject=this;Components[v.GetType()]=v;}
  public void SetActive(bool b){activeInHierarchy=b;}
 }
 public class Transform {public string name="";public Vector3 position,localPosition,eulerAngles;public Transform parent;public void Translate(Vector2 p){position+=(Vector3)p;}public void Rotate(float a,float b,float c){} public Quaternion rotation;}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 down=>new Vector2(0,-1);public static Vector2 zero=>new Vector2();public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 operator *(Vector2 a,float n)=>new Vector2(a.x*n,a.y*n);public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y,0);public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
 public struct Vector3Int {public int x,y,z;public Vector3Int(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
 public struct Quaternion {public static Quaternion identity=>new Quaternion();public static Quaternion Euler(float x,float y,float z)=>new Quaternion();}
 public class Rigidbody2D:MonoBehaviour {public Vector2 position,linearVelocity;public float linearVelocityX=>linearVelocity.x;public float gravityScale;public CollisionDetectionMode2D collisionDetectionMode;}
 public enum CollisionDetectionMode2D {Continuous}
 public struct LayerMask {}
 public class SpriteRenderer:MonoBehaviour {}
 public class ParticleSystem:MonoBehaviour {public void Play(){}public void Stop(bool x){}}
 public class Animator:MonoBehaviour {public void Play(string x){}}
 public class AudioSource:MonoBehaviour {public void Play(){}}
 public class Collider2D:MonoBehaviour {}
 public struct RaycastHit2D {public Collider2D collider;}
 public static class Physics2D {public static bool Grounded=true;static Collider2D ground=new Collider2D();public static RaycastHit2D Raycast(Vector2 p,Vector2 v,float d,LayerMask m)=>new RaycastHit2D{collider=Grounded?ground:null};}
 public static class Resources {public static T Load<T>(string n)=>default(T);}
 public static class Gizmos {public static Color color;public static void DrawLine(Vector3 a,Vector3 b){}}
 public enum KeyCode {Space}
 public static class Input {public static bool Space,Mouse;public static bool GetKey(KeyCode c)=>Space;public static bool GetMouseButton(int i)=>Mouse;}
 public class DisallowMultipleComponent:Attribute {}
 public enum RuntimeInitializeLoadType {AfterSceneLoad}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
}
namespace UnityEngine.Tilemaps {public class Tilemap:UnityEngine.MonoBehaviour {public UnityEngine.Vector3 cellSize=new UnityEngine.Vector3(1,1,1);public UnityEngine.Vector3 GetCellCenterWorld(UnityEngine.Vector3Int i)=>new UnityEngine.Vector3(i.x+.5f,i.y+.5f,0);}}
namespace UnityEngine.SceneManagement {
 public struct Scene {public string name;}
 public enum LoadSceneMode {Single}
 public static class SceneManager {public static string Name="Level";public static event Action<Scene,LoadSceneMode> sceneLoaded;public static Scene GetActiveScene()=>new Scene{name=Name};public static void Change(string name){Name=name;sceneLoaded?.Invoke(GetActiveScene(),LoadSceneMode.Single);}}
}
public class GameManager {public static GameManager Instance=new GameManager();public bool PlayMode=true;}
public class GeometryCamera:UnityEngine.MonoBehaviour {public UnityEngine.Transform player;}
