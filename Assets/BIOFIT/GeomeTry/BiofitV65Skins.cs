using System;
using System.Collections.Generic;
using UnityEngine;

namespace Biofit.GeomeTryOriginal
{
    // All optional GD textures are provided by the user's local importer.
    // No proprietary graphics or animation data are embedded in this script.
    public static class BiofitV65Skins
    {
        [Serializable] sealed class Manifest {public string[] balls,spiders;}
        static Manifest manifest;
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        const string Root="BiofitThreeModes/Artwork/";
        static Manifest Data
        {
            get
            {
                if(manifest==null){var t=Resources.Load<TextAsset>(Root+"V65Skins");manifest=t==null?new Manifest():JsonUtility.FromJson<Manifest>(t.text)??new Manifest();}
                return manifest;
            }
        }
        public static string[] Names(BiofitMode mode)=>mode==BiofitMode.Ball?(Data.balls??Array.Empty<string>()):(Data.spiders??Array.Empty<string>());
        static string Pref(BiofitMode mode)=>mode==BiofitMode.Ball?"BIOFIT_V65_BALL_SKIN":"BIOFIT_V65_SPIDER_SKIN";
        public static int Selected(BiofitMode mode)=>Mathf.Clamp(PlayerPrefs.GetInt(Pref(mode),0),0,Mathf.Max(0,Names(mode).Length-1));
        public static void Select(BiofitMode mode,int index)
        {
            if(index<0||index>=Names(mode).Length)return;PlayerPrefs.SetInt(Pref(mode),index);PlayerPrefs.Save();
        }
        public static Sprite Get(BiofitMode mode,int index,int frame=0)
        {
            string[] names=Names(mode);if(index<0||index>=names.Length)return null;
            string key=mode==BiofitMode.Ball?"V65Ball/"+names[index]:"V65Spider/"+names[index]+"_"+Mathf.Clamp(frame,0,6);
            if(cache.TryGetValue(key,out var result))return result;
            var t=Resources.Load<Texture2D>(Root+key);
            result=t==null?null:Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),96f,0,SpriteMeshType.FullRect);
            cache[key]=result;return result;
        }
        public static Sprite Current(BiofitMode mode,bool running)
        {
            int frame=mode==BiofitMode.Spider&&running?Mathf.FloorToInt(Time.time*14f)%7:0;
            return Get(mode,Selected(mode),frame);
        }
    }
}
