// Host-only stand-ins for compilation of the real Dash UI script.
// This validates C# API references and pure geometry, NOT Android rendering.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireComponent : Attribute
    {
        public RequireComponent(Type t) {}
    }
    public enum ScreenOrientation { LandscapeLeft, Portrait }
    public static partial class Time { public static float deltaTime = .016f; }
    public static partial class Screen
    {
        public static ScreenOrientation orientation;
    }
    public static partial class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi,v));
        public static float Abs(float v) => Math.Abs(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
    }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public enum ScaleMode { StretchToFill }
    public class Texture2D
    {
        public static Texture2D whiteTexture = new Texture2D(1,1,TextureFormat.RGBA32,false);
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Texture2D(int w,int h,TextureFormat fmt,bool mipmaps){}
        public void SetPixels(Color[] pixels){}
        public void Apply(){}
    }
    public static partial class GUI
    {
        public static readonly List<string> ButtonTexts = new List<string>();
        public static void DrawTexture(Rect r,Texture2D t){}
        public static void DrawTexture(Rect r,Texture2D t,ScaleMode s,bool alpha){}
        public static void BeginGroup(Rect r){}
        public static void EndGroup(){}
    }
}
namespace UnityEngine.InputSystem
{
    public class KeyControl { public bool wasPressedThisFrame; }
    public sealed class Keyboard
    {
        public static Keyboard current;
        public KeyControl spaceKey = new KeyControl();
        public KeyControl upArrowKey = new KeyControl();
        public KeyControl rKey = new KeyControl();
        public KeyControl pKey = new KeyControl();
    }
}
namespace UnityEngine.SceneManagement {}
