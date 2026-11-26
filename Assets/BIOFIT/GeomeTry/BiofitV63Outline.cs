using System.Collections.Generic;
using UnityEngine;
namespace Biofit.GeomeTryOriginal
{
    // Visual union of exposed edges only, in the three requested Cube areas.
    // Colliders, transforms, hazards and sorting of other objects are unchanged.
    public sealed class BiofitV63Outline
    {
        private readonly HashSet<Vector2Int> all=new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> selected=new HashSet<Vector2Int>();
        private readonly Transform parent;
        private readonly float floor;
        private readonly Material mat;
        public Sprite FillSprite{get;private set;}
        private static bool Full(BiofitStereoLocalRuntime.Item o)=>o.kind=="solid" && o.collisionMode==0 && o.editorScaleX==0 && o.editorScaleY==0 && o.frames!=null && o.frames.Length==1 && o.frames[0].StartsWith("square_0") && Mathf.Abs(o.widthGD-30)<.1f && Mathf.Abs(o.heightGD-30)<.1f;
        private static Vector2Int Key(BiofitStereoLocalRuntime.Item o)=>new Vector2Int(Mathf.RoundToInt(o.xGD),Mathf.RoundToInt(o.yGD));
        private static bool Region(BiofitStereoLocalRuntime.Item o)=>(o.xGD>=12495 && o.xGD<=12585 && o.yGD>=225)||(o.xGD>=12915 && o.xGD<=14505 && o.yGD<=270);
        public BiofitV63Outline(BiofitStereoLocalRuntime.Data data,Transform root,float y,Material m,Sprite fill)
        {
            parent=root;floor=y;mat=m;FillSprite=fill;
            foreach(var o in data.objects)if(Full(o)){all.Add(Key(o));if(Region(o))selected.Add(Key(o));}
            // The decorative fill BELOW a walkable top is part of its visual silhouette,
            // not another collider. This avoids a horizontal white seam across platforms.
            bool changed;
            do
            {
                changed=false;
                foreach(var o in data.objects)
                {
                    if(o.id!=5 || o.kind!="decoration" || o.xGD<12915 || o.xGD>14505 || o.yGD<15 || o.yGD>255)continue;
                    var k=Key(o);
                    if(selected.Contains(k+new Vector2Int(0,30)) && selected.Add(k)){all.Add(k);changed=true;}
                }
            }while(changed);
        }
        public bool Contains(BiofitStereoLocalRuntime.Item o)=>Full(o)&&selected.Contains(Key(o));
        public void Draw()
        {
            var host=new GameObject("BIOFIT V63 connected outlines");host.transform.SetParent(parent,false);
            var verts=new List<Vector3>();
            foreach(var k in selected)
            {
                float x=Character.InitialPosition+k.x/30f,y=floor+k.y/30f;
                if(!all.Contains(k+new Vector2Int(-30,0)))Quad(verts,x-.5f,y,.045f,1.045f);
                if(!all.Contains(k+new Vector2Int(30,0)))Quad(verts,x+.5f,y,.045f,1.045f);
                if(k.y!=15 && !all.Contains(k+new Vector2Int(0,-30)))Quad(verts,x,y-.5f,1.045f,.045f);
                if(!all.Contains(k+new Vector2Int(0,30)))Quad(verts,x,y+.5f,1.045f,.045f);
            }
            // One small white texture shared by all outline segments.
            var tex=new Texture2D(1,1,TextureFormat.RGBA32,false);
            tex.SetPixel(0,0,Color.white);tex.Apply();tex.filterMode=FilterMode.Point;
            var edgeSprite=Sprite.Create(tex,new Rect(0,0,1,1),new Vector2(.5f,.5f),1f,0,SpriteMeshType.FullRect);
            var lifetime=host.AddComponent<BiofitV63OutlineLifetime>();lifetime.sprite=edgeSprite;lifetime.texture=tex;
            for(int i=0;i<verts.Count;i+=4)
            {
                var a=verts[i];var b=verts[i+2];var go=new GameObject("edge");go.transform.SetParent(host.transform,false);
                go.transform.position=(a+b)*.5f;go.transform.localScale=new Vector3(b.x-a.x,b.y-a.y,1);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=edgeSprite;sr.color=Color.white;sr.sortingOrder=-130;if(mat!=null)sr.sharedMaterial=mat;
            }
        }
        private static void Quad(List<Vector3> v,float x,float y,float w,float h)
        {v.Add(new Vector3(x-w/2,y-h/2,0));v.Add(new Vector3(x-w/2,y+h/2,0));v.Add(new Vector3(x+w/2,y+h/2,0));v.Add(new Vector3(x+w/2,y-h/2,0));}
    }
    public sealed class BiofitV63OutlineLifetime:MonoBehaviour
    { public Sprite sprite; public Texture2D texture; private void OnDestroy(){if(sprite!=null)Destroy(sprite);if(texture!=null)Destroy(texture);} }
}
