// Compilation surface only. No simulation, rendering, Android or device claims.
using System;
namespace UnityEngine
{
    public enum FindObjectsSortMode { None }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class RequireComponent : Attribute { public RequireComponent(Type t){} }
    public class Transform : Component
    {
        public Vector3 position,localPosition,localScale,eulerAngles;
        public Quaternion rotation;public void SetParent(Transform t,bool b){} public void Rotate(float x,float y,float z){}
    }
    public struct Vector2
    {
        public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 zero,one,down;public static Vector2 operator*(Vector2 v,float f)=>v;
    }
    public struct Vector3 { public float x,y,z;public static Vector3 zero;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Quaternion { public static Quaternion identity;public static Quaternion Euler(float x,float y,float z)=>default; }
    public enum RigidbodyConstraints2D { FreezeRotation }
    public enum CollisionDetectionMode2D { Continuous }
    public enum RigidbodyInterpolation2D { Interpolate }
    public class Rigidbody2D : Component
    {
        public float gravityScale;public RigidbodyConstraints2D constraints;public CollisionDetectionMode2D collisionDetectionMode;
        public RigidbodyInterpolation2D interpolation;public bool simulated;public Vector2 linearVelocity,position;
        public int Cast(Vector2 dir,RaycastHit2D[] hits,float distance)=>0;
    }
    public class Collider2D : Component { public bool isTrigger;public PhysicsMaterial2D sharedMaterial; }
    public class BoxCollider2D : Collider2D { public Vector2 size; }
    public struct RaycastHit2D { public Collider2D collider;public Vector2 normal; }
    public struct ContactPoint2D { public Vector2 normal; }
    public class Collision2D { public Collider2D collider;public int contactCount;public ContactPoint2D GetContact(int n)=>default; }
    public class PhysicsMaterial2D : Object { public float friction,bounciness;public PhysicsMaterial2D(string n){} }
    public class SpriteRenderer : Component { public Sprite sprite;public Color color;public int sortingOrder; }
    public class Sprite : Object { public Texture2D texture;public static Sprite Create(Texture2D t,Rect r,Vector2 v,float f)=>new Sprite(); }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point }
    public class Texture2D : Object
    {
        public FilterMode filterMode;public static Texture2D whiteTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);
        public Texture2D(int w,int h,TextureFormat t,bool m){} public void SetPixels(Color[] p){} public void Apply(){}
    }
    public enum CameraClearFlags { SolidColor }
    public class Camera : Component { public bool orthographic;public float orthographicSize;public CameraClearFlags clearFlags;public Color backgroundColor; }
    public class TextAsset : Object { public string text; }
    public static class Resources { public static T Load<T>(string path)=>default; }
    public static class JsonUtility { public static T FromJson<T>(string json)=>default; }
    public static partial class Mathf
    {
        public static float Min(float a,float b)=>Math.Min(a,b);public static float Abs(float v)=>Math.Abs(v);
        public static float Round(float v)=>(float)Math.Round(v);public static float Clamp01(float v)=>Math.Clamp(v,0,1);
        public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
    }
    public static partial class Time { public static float deltaTime,timeScale,fixedDeltaTime; }
    public enum ScreenOrientation { LandscapeLeft }
    public static partial class Screen { public static Rect safeArea;public static ScreenOrientation orientation; }
    public static partial class GUI { public static Color color;public static void DrawTexture(Rect r,Texture2D t){} }
}
namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool wasPressedThisFrame; }
    public class PositionControl { public UnityEngine.Vector2 ReadValue()=>default; }
    public class TouchControl { public ButtonControl press;public PositionControl position; }
    public class Keyboard { public static Keyboard current;public ButtonControl spaceKey,escapeKey,pKey,rKey; }
    public class Mouse { public static Mouse current;public ButtonControl leftButton;public PositionControl position; }
    public class Touchscreen { public static Touchscreen current;public TouchControl primaryTouch; }
}
