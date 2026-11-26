#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Data=Biofit.GeomeTryOriginal.BiofitStereoLocalRuntime.Data;
using Item=Biofit.GeomeTryOriginal.BiofitStereoLocalRuntime.Item;

namespace Biofit.LevelEditing
{
    internal static class BiofitEditorFlm
    {
        sealed class Pixels {public int w,h;public Color32[] colors;}
        static Pixels Read(Texture2D t)
        {
            var prev=RenderTexture.active;var rt=RenderTexture.GetTemporary(t.width,t.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Texture2D temp=null;
            try
            {
                Graphics.Blit(t,rt);RenderTexture.active=rt;temp=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false,true);
                temp.ReadPixels(new Rect(0,0,t.width,t.height),0,0);temp.Apply();
                return new Pixels{w=t.width,h=t.height,colors=temp.GetPixels32()};
            }
            finally {RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);if(temp!=null)UnityEngine.Object.DestroyImmediate(temp);}
        }
        public static void Export(Data d,string name,Func<string,Texture2D> tex)
        {
            string folder=EditorUtility.OpenFolderPanel("Exportar FLM y tramos","","");
            if(string.IsNullOrEmpty(folder))return;
            string root=Path.Combine(folder,"FLM_"+name.Replace(" ","_")+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            var cache=new Dictionary<string,Pixels>();
            var ordered=d.objects.Where(x=>x.frames!=null).OrderBy(x=>x.layer*100+x.order).ToArray();
            float bottom=Mathf.Min(-60,ordered.Select(x=>x.yGD-Mathf.Max(x.widthGD,x.heightGD)).DefaultIfEmpty(-60).Min());
            float top=Mathf.Max(540,ordered.Select(x=>x.yGD+Mathf.Max(x.widthGD,x.heightGD)).DefaultIfEmpty(540).Max()+30);
            // Keep images within common GPU/PNG dimensions; high-resolution tiles have 1 px/GD.
            float scale=Mathf.Min(1,Mathf.Min(8192f/d.endGD,4096f/(top-bottom)));
            float tileScale=Mathf.Min(1,4096f/(top-bottom));
            try
            {
                Render(Path.Combine(root,"FULL.png"),0,d.endGD,scale,bottom,top,d,ordered,tex,cache);
                int count=Mathf.CeilToInt(d.endGD/(4096f/tileScale));
                for(int i=0;i<count;i++)
                {
                    if(EditorUtility.DisplayCancelableProgressBar("BIOFIT FLM","Tramo "+(i+1)+"/"+count,(float)i/count))break;
                    float a=i*4096f/tileScale,b=Mathf.Min(d.endGD,(i+1)*4096f/tileScale);
                    Render(Path.Combine(root,string.Format("{0:D2}_{1:F1}-{2:F1}pct.png",i+1,100*a/d.endGD,100*b/d.endGD)),a,b,tileScale,bottom,top,d,ordered,tex,cache);
                }
                File.WriteAllText(Path.Combine(root,"LEEME.txt"),"Render estático desde el borrador JSON. FULL escalado; tramos a "+tileScale+" px/GD. Y inferior="+bottom+", Y superior="+top+". Sin partículas, contornos especiales V6.3 ni animación; colores iniciales. No representa colliders exactos ni prueba de jugabilidad.");
                EditorUtility.RevealInFinder(root);
            }
            finally {EditorUtility.ClearProgressBar();}
        }
        static Color32 RGB(int[] c)=>c!=null&&c.Length>=3?new Color32((byte)c[0],(byte)c[1],(byte)c[2],255):new Color32(30,53,83,255);
        static void Render(string file,float x0,float x1,float scale,float bottom,float top,Data data,Item[] objects,Func<string,Texture2D> tex,Dictionary<string,Pixels> cache)
        {
            int w=Mathf.Max(1,Mathf.CeilToInt((x1-x0)*scale)),h=Mathf.Max(1,Mathf.CeilToInt((top-bottom)*scale));
            var dst=new Color32[w*h];var bg=RGB(data.initialBackground);var ground=RGB(data.initialGround);
            for(int y=0;y<h;y++){var c=bottom+(y+.5f)/scale<0?ground:bg;for(int x=0;x<w;x++)dst[y*w+x]=c;}
            foreach(var o in objects)
            {
                if(o.kind=="backgroundColor"||o.kind=="groundColor"||o.kind=="enterEffect")continue;
                float sx=o.editorScaleX>0?o.editorScaleX:1,sy=o.editorScaleY>0?o.editorScaleY:1;
                foreach(string f in o.frames)
                {
                    if(!cache.TryGetValue(f,out var p)){var t=tex(f);if(t==null)continue;p=Read(t);cache.Add(f,p);}
                    float hw=p.w*.25f*sx,hh=p.h*.25f*sy;
                    float radius=Mathf.Sqrt(hw*hw+hh*hh);
                    if(o.xGD+radius<x0||o.xGD-radius>x1)continue;
                    int xa=Mathf.Max(0,Mathf.FloorToInt((o.xGD-radius-x0)*scale)),xb=Mathf.Min(w-1,Mathf.CeilToInt((o.xGD+radius-x0)*scale));
                    int ya=Mathf.Max(0,Mathf.FloorToInt((o.yGD-radius-bottom)*scale)),yb=Mathf.Min(h-1,Mathf.CeilToInt((o.yGD+radius-bottom)*scale));
                    float angle=o.rotationGD*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                    for(int y=ya;y<=yb;y++)for(int x=xa;x<=xb;x++)
                    {
                        float dx=x0+(x+.5f)/scale-o.xGD,dy=bottom+(y+.5f)/scale-o.yGD;
                        float u=(dx*c-dy*s)/sx,v=(dx*s+dy*c)/sy;
                        if(o.flipX)u=-u;if(o.flipY)v=-v;
                        int ix=Mathf.FloorToInt(u*2+p.w*.5f),iy=Mathf.FloorToInt(v*2+p.h*.5f);
                        if(ix<0||ix>=p.w||iy<0||iy>=p.h)continue;
                        Color32 a=p.colors[iy*p.w+ix];if(a.a==0)continue;
                        if(o.id==9){a.r=0;a.g=0;a.b=0;}
                        else if(o.id>=18&&o.id<=21){a.g=(byte)(a.g*200/255);a.b=(byte)(a.b*75/255);}
                        int idx=y*w+x;Color32 b=dst[idx];int alpha=a.a,inv=255-alpha;
                        dst[idx]=new Color32((byte)((a.r*alpha+b.r*inv)/255),(byte)((a.g*alpha+b.g*inv)/255),(byte)((a.b*alpha+b.b*inv)/255),255);
                    }
                }
            }
            var output=new Texture2D(w,h,TextureFormat.RGBA32,false);
            try{output.SetPixels32(dst);output.Apply();File.WriteAllBytes(file,output.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(output);}
        }
    }
}
#endif
