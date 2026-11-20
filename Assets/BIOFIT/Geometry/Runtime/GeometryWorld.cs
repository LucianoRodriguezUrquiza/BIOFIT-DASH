using UnityEngine;

namespace Biofit.Geometry
{
    // Original visual assets built from geometric primitives. No music,
    // commercial sprites, prefabs or materials are redistributed.
    public sealed class GeometryWorld
    {
        public GameObject Root { get; private set; }
        public GeometryCube Player { get; private set; }
        private readonly Sprite square, triangle, circle;
        private readonly PhysicsMaterial2D material;
        public GeometryWorld(Sprite square, Sprite triangle, Sprite circle, PhysicsMaterial2D material)
        { this.square=square; this.triangle=triangle; this.circle=circle; this.material=material; }

        public void Build(GeometryLevel level, Transform parent, Color accent)
        {
            Root = new GameObject("Level_"+level.id); Root.transform.SetParent(parent,false);
            float length = level.finishX + 45;
            var ground = Shape("GROUND",square,new Vector2(level.finishX/2, -.55f),new Vector2(length,1.1f),new Color(.08f,.14f,.22f));
            Attach(ground,"ground",0,true);
            Shape("Horizon",square,new Vector2(level.finishX/2,.01f),new Vector2(length,.035f),accent);
            for (float x=-15;x<level.finishX+25;x+=4)
            {
                Shape("Grid",square,new Vector2(x,4),new Vector2(.015f,8),new Color(.10f,.17f,.26f,.5f));
                Shape("FloorTick",square,new Vector2(x,-.22f),new Vector2(.8f,.025f),new Color(.16f,.25f,.34f));
            }
            foreach (var o in level.obstacles)
            {
                Sprite sprite = o.kind=="spike"?triangle:o.kind=="orb"?circle:square;
                Color tint = o.kind=="spike"?new Color(1,.36f,.43f):o.kind=="pad"?new Color(1,.77f,.25f):accent;
                var obj=Shape(o.kind+"_"+o.id,sprite,new Vector2(o.x,o.y),new Vector2(o.width,o.height),tint);
                Attach(obj,o.kind,o.id,o.kind=="block");
                if(o.kind=="block")
                    Shape("BlockInset",square,new Vector2(o.x,o.y),new Vector2(o.width-.1f,o.height-.1f),new Color(.10f,.23f,.30f)).GetComponent<SpriteRenderer>().sortingOrder=1;
            }
            foreach (var s in level.sections)
            {
                // Visible timing strip is guidance, not an automatic jump.
                Shape("JumpCue",square,new Vector2(s.jumpX,-.06f),new Vector2(.6f,.08f),new Color(1,.85f,.4f));
            }
            Shape("Finish",square,new Vector2(level.finishX,3),new Vector2(.12f,6),new Color(.5f,1,.75f));
            var player = new GameObject("BIOFIT_Cube");player.transform.SetParent(Root.transform,false);
            player.transform.position = new Vector3(GeometryPhysics.SpawnX, .47f,0);
            player.AddComponent<Rigidbody2D>();var box=player.AddComponent<BoxCollider2D>();box.sharedMaterial=material;
            Player=player.AddComponent<GeometryCube>();
            var view=Shape("CubeArt",square,new Vector2(0,0),Vector2.one*GeometryPhysics.CubeSize,accent);
            view.transform.SetParent(player.transform,false);view.transform.localPosition=Vector3.zero;
            view.GetComponent<SpriteRenderer>().sortingOrder=5;
            for(int i=0;i<2;i++)
            {
                var eye=Shape("Eye",square,Vector2.zero,new Vector2(.13f,.17f),new Color(.025f,.07f,.12f));
                eye.transform.SetParent(view.transform,false);eye.transform.localPosition=new Vector3(i==0?-.22f:.22f,.12f,-.01f);
                eye.GetComponent<SpriteRenderer>().sortingOrder=6;
            }
            Player.Initialize(view.transform);
        }
        private GameObject Shape(string name,Sprite sprite,Vector2 pos,Vector2 size,Color color)
        {
            var obj=new GameObject(name);obj.transform.SetParent(Root.transform,false);
            obj.transform.localPosition=new Vector3(pos.x,pos.y,0);obj.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=obj.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=color;
            return obj;
        }
        private void Attach(GameObject obj,string kind,int id,bool solid)
        {
            var item=obj.AddComponent<GeometryWorldItem>();item.Kind=kind;item.Id=id;
            var box=obj.AddComponent<BoxCollider2D>();box.size=Vector2.one;box.isTrigger=!solid;box.sharedMaterial=material;
        }
        public static Sprite CreateSprite(int shape)
        {
            const int n=32;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                bool visible=shape==0 || (shape==1 ? Mathf.Abs(x-15.5f)<(n-y)*.5f : (x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f)<240);
                pixels[y*n+x]=visible?Color.white:Color.clear;
            }
            tex.SetPixels(pixels);tex.Apply();
            return Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);
        }
    }
}
