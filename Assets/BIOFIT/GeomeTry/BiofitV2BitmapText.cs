using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Biofit.GeomeTryOriginal
{
    // BMFont rendering in a separate non-interactive UI Graphic. Existing TMP
    // components retain their text, layout, references and button interactions.
    public sealed class BiofitV2BitmapText : MaskableGraphic
    {
        [Serializable] private sealed class Glyph { public int id,x,y,width,height,xoffset,yoffset,xadvance; }
        [Serializable] private sealed class FontData { public int size,lineHeight; public Glyph[] glyphs; public Pair[] kernings; }
        [Serializable] private sealed class Pair { public int first,second,amount; }
        private sealed class Face
        {
            public Texture2D texture; public FontData data;
            public readonly Dictionary<int,Glyph> glyphs=new Dictionary<int,Glyph>();
            public readonly Dictionary<long,int> kerning=new Dictionary<long,int>();
        }
        private static Face main,secondary,gold;
        private TMP_Text source;
        private Face face;
        private string last;
        private float lastSize;
        private Color lastColor;
        private TextAlignmentOptions lastAlign;
        private Vector2 lastRect;
        private bool usable,lastEnabled,hidden,fitInk;
        public static void ConstrainToRect(TMP_Text t)
        {
            if(t==null)return;
            var bitmap=t.GetComponentInChildren<BiofitV2BitmapText>(true);
            if(bitmap!=null){bitmap.fitInk=true;bitmap.SetVerticesDirty();}
        }
        public override Texture mainTexture => face!=null?face.texture:Texture2D.whiteTexture;
        public static Texture2D Art(string key) => Resources.Load<Texture2D>("BiofitStereoV2/UI/"+key);
        private static Face Load(string key)
        {
            var tex=Resources.Load<Texture2D>("BiofitStereoV2/Fonts/"+key);
            var text=Resources.Load<TextAsset>("BiofitStereoV2/Fonts/"+key+"_metrics");
            if(tex==null || text==null) return null;
            var d=JsonUtility.FromJson<FontData>(text.text);
            if(d==null || d.glyphs==null || d.size<=0 || d.lineHeight<=0) return null;
            var f=new Face{texture=tex,data=d};
            foreach(var g in d.glyphs) f.glyphs[g.id]=g;
            if(d.kernings!=null) foreach(var k in d.kernings) f.kerning[Key(k.first,k.second)]=k.amount;
            return f;
        }
        private static long Key(int a,int b) => ((long)a<<32)|(uint)b;
        public static void Attach(TMP_Text t, string variant="main")
        {
            if(t==null || t.GetComponentInChildren<BiofitV2BitmapText>(true)!=null) return;
            if(main==null) main=Load("bigFont");
            if(secondary==null) secondary=Load("chatFont");
            if(gold==null) gold=Load("goldFont");
            Face f=variant=="gold"?gold:variant=="secondary"?secondary:main;
            if(f==null) return;
            var go=new GameObject("BIOFIT Bitmap Font",typeof(RectTransform),typeof(CanvasRenderer));
            var rt=go.GetComponent<RectTransform>();rt.SetParent(t.transform,false);
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
            var g=go.AddComponent<BiofitV2BitmapText>();g.source=t;g.face=f;g.raycastTarget=false;
            g.SetMaterialDirty(); g.Refresh();
        }
        private void LateUpdate() { Refresh(); }
        private void Refresh()
        {
            if(source==null || face==null) return;
            string value=source.text??""; var bounds=rectTransform.rect.size;
            bool changed=value!=last || source.fontSize!=lastSize || source.color!=lastColor || source.alignment!=lastAlign || bounds!=lastRect || source.enabled!=lastEnabled;
            if(changed)
            {
                lastEnabled=source.enabled;last=value;lastSize=source.fontSize;lastColor=source.color;lastAlign=source.alignment;lastRect=bounds;
                usable=source.enabled && value.IndexOf('<')<0;
                foreach(char c in value) if(c!='\n' && c!='\r' && !face.glyphs.ContainsKey(c)) {usable=false;break;}
                SetVerticesDirty();
            }
            // Avoid modifying TMP text/color; fallback preserves accents and rich text.
            if(usable && source.enabled) {source.canvasRenderer.SetAlpha(0f);hidden=true;}
            else if(hidden) {source.canvasRenderer.SetAlpha(1f);hidden=false;}
            canvasRenderer.SetAlpha(usable && source.enabled?1f:0f);
        }
        protected override void OnDisable()
        {
            if(source!=null && hidden) {source.canvasRenderer.SetAlpha(1f);hidden=false;}
            base.OnDisable();
        }
        private int Kern(int a,int b) => face.kerning.TryGetValue(Key(a,b),out int k)?k:0;
        private float Measure(string s)
        {
            float width=0;int prev=-1;
            foreach(char c in s) {if(!face.glyphs.TryGetValue(c,out Glyph g)) continue; width+=Kern(prev,c)+g.xadvance;prev=c;}
            return width;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!usable || face==null || string.IsNullOrEmpty(last)) return;
            Rect r=rectTransform.rect; if(r.width<=0 || r.height<=0) return;
            float scale=lastSize/face.data.size;
            var wrapped=new List<string>();
            foreach(string paragraph in last.Replace("\r","").Split('\n'))
            {
                if(!source.enableWordWrapping || Measure(paragraph)*scale<=r.width) {wrapped.Add(paragraph);continue;}
                string line="";
                foreach(string word in paragraph.Split(' '))
                {
                    string next=line.Length==0?word:line+" "+word;
                    if(line.Length>0 && Measure(next)*scale>r.width) {wrapped.Add(line);line=word;}
                    else line=next;
                }
                wrapped.Add(line);
            }
            string[] lines=wrapped.ToArray();
            float max=1;foreach(string l in lines) max=Mathf.Max(max,Measure(l));
            scale=Mathf.Min(scale,r.width/max,r.height/(lines.Length*face.data.lineHeight));
            int align=(int)lastAlign;float height=lines.Length*face.data.lineHeight*scale;
            float top=(align&0x100)!=0?r.yMax:(align&0x400)!=0?r.yMin+height:r.center.y+height*.5f;
            Color32 col=lastColor;
            for(int row=0;row<lines.Length;row++)
            {
                string l=lines[row];float w=Measure(l)*scale;
                float x=(align&1)!=0?r.xMin:(align&4)!=0?r.xMax-w:r.center.x-w*.5f;
                float y=top-row*face.data.lineHeight*scale;int prev=-1;
                foreach(char c in l)
                {
                    if(!face.glyphs.TryGetValue(c,out Glyph g)) continue;
                    x+=Kern(prev,c)*scale;prev=c;
                    if(g.width>0 && g.height>0)
                    {
                        float x0=x+g.xoffset*scale,y1=y-g.yoffset*scale,x1=x0+g.width*scale,y0=y1-g.height*scale;
                        float u0=(float)g.x/face.texture.width,u1=(float)(g.x+g.width)/face.texture.width;
                        float v1=1f-(float)g.y/face.texture.height,v0=1f-(float)(g.y+g.height)/face.texture.height;
                        int i=vh.currentVertCount;
                        vh.AddVert(new Vector3(x0,y0),col,new Vector2(u0,v0));vh.AddVert(new Vector3(x0,y1),col,new Vector2(u0,v1));
                        vh.AddVert(new Vector3(x1,y1),col,new Vector2(u1,v1));vh.AddVert(new Vector3(x1,y0),col,new Vector2(u1,v0));
                        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
                    }
                    x+=g.xadvance*scale;
                }
            }
            if(fitInk)FitVisibleInk(vh,r);
        }
        // BMFont advances are not ink bounds: offsets and overhangs can extend
        // beyond them. Fit actual quad vertices uniformly, retaining alignment.
        private static void FitVisibleInk(VertexHelper vh,Rect r)
        {
            if(vh.currentVertCount==0)return;
            UIVertex vertex=default;
            Vector2 lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            Vector2 hi=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<vh.currentVertCount;i++){
                vh.PopulateUIVertex(ref vertex,i);
                var point=new Vector2(vertex.position.x,vertex.position.y);
                lo=Vector2.Min(lo,point);hi=Vector2.Max(hi,point);
            }
            // Leave a subpixel safety inset; the calling menu reserves padding.
            float w=Mathf.Max(.01f,hi.x-lo.x),h=Mathf.Max(.01f,hi.y-lo.y);
            float scale=Mathf.Min(1f,Mathf.Max(.01f,r.width-2f)/w,Mathf.Max(.01f,r.height-2f)/h);
            Vector2 center=r.center;
            Vector2 min=center+(lo-center)*scale,max=center+(hi-center)*scale;
            float dx=Mathf.Clamp(0f,r.xMin+1f-min.x,r.xMax-1f-max.x);
            float dy=Mathf.Clamp(0f,r.yMin+1f-min.y,r.yMax-1f-max.y);
            for(int i=0;i<vh.currentVertCount;i++){
                vh.PopulateUIVertex(ref vertex,i);
                Vector3 v=vertex.position;
                vertex.position=new Vector3(center.x+(v.x-center.x)*scale+dx,center.y+(v.y-center.y)*scale+dy,v.z);
                vh.SetUIVertex(vertex,i);
            }
        }
    }
}
