using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Biofit.GeomeTryOriginal
{
    // Local reference import, selected only via the explicit level-one manifest.
    // Original Game, Cube, BLE, controller and other levels are not patched.
    [DefaultExecutionOrder(1000)]
    public sealed class BiofitStereoLocalRuntime : MonoBehaviour
    {
        public const string ResourceRoot = "BiofitStereoLocal/";
        public const string Marker = "biofitStereoLocalAnchor";
        private const int ExpectedObjects = 2291;
        private const float OriginX = Character.InitialPosition;
        private const float Unit = 30f;
        [Serializable] public sealed class Item
        {
            public int index, id, layer, order;
            public string kind;
            public float xGD, yGD, rotationGD, widthGD, heightGD;
            public bool flipX, flipY;
            public string[] frames;
            public int r, g, b;
            public float duration;
        }
        [Serializable] public sealed class Data
        {
            public string sourceSha256;
            public int unitsPerBlock;
            public Item[] objects;
            public int[] initialBackground, initialGround;
        }
        private sealed class TintTrack
        {
            public readonly List<Item> items = new List<Item>();
            public Color initial;
            public Color Evaluate(float x)
            {
                Color from = initial, to = initial;
                float start = 0f, span = 0f;
                foreach (Item item in items)
                {
                    float p = WorldX(item.xGD);
                    if (x < p) break;
                    from = span <= 0f ? to : Color.Lerp(from, to, (p-start)/span);
                    to = new Color32((byte)item.r, (byte)item.g, (byte)item.b, 255);
                    start = p;
                    // Duration in real seconds at the existing 6.5*1.5 speed.
                    span = item.duration * 9.75f;
                }
                return span <= 0f ? to : Color.Lerp(from, to, (x-start)/span);
            }
        }
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private TintTrack background, ground;
        private SpriteRenderer backgroundVisual, groundVisual;
        private UnityEngine.Camera cameraView;
        private Transform player;
        private float floorY, finalX;
        private float lastPlayerX = OriginX;
        private Material unlit;
        private bool ready;
        private static float WorldX(float gd) => OriginX + gd / Unit;
        private static Color RGB(int[] v) => new Color32((byte)v[0], (byte)v[1], (byte)v[2], 255);
        private static bool IsLocal(Level level) => level != null && level.Id == 1 && level.Map.Count == 1 && level.Map[0].Type == Marker;
        public static TextAsset SelectMap(int index, TextAsset fallback)
        {
            if (index != 0 || !BiofitThreeModesBootstrap.Active) return fallback;
            TextAsset local = Resources.Load<TextAsset>(ResourceRoot + "Level");
            if (local == null) return fallback;
            ValidateInstalled();
            return local;
        }
        private static Data ReadData()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceRoot + "Objects");
            if (asset == null) throw new InvalidOperationException("Faltan los datos Stereo locales.");
            Data d = JsonUtility.FromJson<Data>(asset.text);
            if (d == null || d.unitsPerBlock != 30 || d.objects == null || d.objects.Length != ExpectedObjects || d.initialBackground == null || d.initialBackground.Length != 3 || d.initialGround == null || d.initialGround.Length != 3)
                throw new InvalidOperationException("Datos Stereo locales invalidos.");
            return d;
        }
        // Invoked by the existing editor verification menu for Level 1 only.
        public static bool ValidateInstalled()
        {
            TextAsset map = Resources.Load<TextAsset>(ResourceRoot + "Level");
            if (map == null) return false;
            Level level = new Level(map);
            if (!IsLocal(level) || level.Music == null || level.getLastMapItem().X != 876)
                throw new InvalidOperationException("Manifest Stereo o musica invalidos.");
            Data d = ReadData();
            var ids = new HashSet<int>(); var indexes = new HashSet<int>(); var frames = new HashSet<string>();
            int ships=0,cubes=0,coins=0,solid=0,hazard=0,deco=0,bg=0,gr=0,fx=0;
            foreach(Item o in d.objects)
            {
                if (o == null || o.index < 0 || o.index >= ExpectedObjects || !indexes.Add(o.index) || o.frames == null || o.widthGD <= 0 || o.heightGD <= 0 || float.IsNaN(o.xGD) || float.IsNaN(o.yGD) || float.IsInfinity(o.xGD) || float.IsInfinity(o.yGD))
                    throw new InvalidOperationException("Registro Stereo invalido.");
                ids.Add(o.id);
                switch(o.kind)
                {
                    case "shipPortal": ships++; break; case "cubePortal": cubes++; break;
                    case "coin": coins++; break; case "solid": solid++; break;
                    case "hazard": hazard++; break; case "decoration": deco++; break;
                    case "backgroundColor": bg++; break; case "groundColor": gr++; break;
                    case "enterEffect": fx++; break; default: throw new InvalidOperationException("Tipo Stereo desconocido: " + o.kind);
                }
                foreach(string f in o.frames) frames.Add(f);
            }
            if (ids.Count!=31 || ships!=3 || cubes!=1 || coins!=3 || solid!=786 || hazard!=610 || deco!=869 || bg!=7 || gr!=7 || fx!=5 || frames.Count!=28)
                throw new InvalidOperationException("Conteos Stereo locales inesperados.");
            frames.Add("fondo"); frames.Add("suelo");
            foreach(string frame in frames)
                if(Resources.Load<Texture2D>(ResourceRoot + "Sprites/" + frame)==null)
                    throw new InvalidOperationException("Falta textura Stereo local: " + frame);
            Debug.Log("BIOFIT Stereo local: 2291 registros, 31 IDs, 30 texturas, 3 monedas. Validacion estructural, no gameplay.");
            return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterScene()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Level" || GameManager.Instance == null || !IsLocal(GameManager.Instance.SelectedLevel)) return;
            var host = new GameObject("BIOFIT_STEREO_LOCAL_RUNTIME");
            host.AddComponent<BiofitStereoLocalRuntime>();
        }
        private void Start()
        {
            try { Build(); ready = true; }
            catch(Exception ex)
            {
                if(GameManager.Instance != null) GameManager.Instance.PlayMode = false;
                Debug.LogError("BIOFIT Stereo: importacion interrumpida: " + ex);
                enabled = false;
            }
        }
        private Sprite GetSprite(string frame)
        {
            if(sprites.TryGetValue(frame,out Sprite sprite)) return sprite;
            Texture2D tex=Resources.Load<Texture2D>(ResourceRoot + "Sprites/" + frame);
            if(tex==null) throw new InvalidOperationException("Falta textura " + frame);
            sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),60f,0,SpriteMeshType.FullRect);
            sprites.Add(frame,sprite); return sprite;
        }
        private SpriteRenderer Visual(Transform parent,string frame,int sorting)
        {
            var child=new GameObject(frame); child.transform.SetParent(parent,false);
            var sr=child.AddComponent<SpriteRenderer>(); sr.sprite=GetSprite(frame);
            sr.sortingOrder=sorting; if(unlit!=null) sr.sharedMaterial=unlit;
            return sr;
        }
        private void Build()
        {
            if(!ValidateInstalled()) throw new InvalidOperationException("Falta manifest Stereo");
            Data d=ReadData();
            Tilemap tm=FindFirstObjectByType<Tilemap>();
            if(tm==null || Mathf.Abs(tm.cellSize.x-1f)>.001f || Mathf.Abs(tm.cellSize.y-1f)>.001f)
                throw new InvalidOperationException("Tilemap original no disponible o escala inesperada.");
            GameObject originalGround=GameObject.Find("GroundPrefab(Clone)");
            var groundCollider=originalGround==null?null:originalGround.GetComponent<BoxCollider2D>();
            if(groundCollider==null) throw new InvalidOperationException("No se encontro el suelo original.");
            Physics2D.SyncTransforms();
            floorY=groundCollider.bounds.max.y;
            cameraView=UnityEngine.Camera.main;
            if(cameraView==null || !cameraView.orthographic) throw new InvalidOperationException("Camara ortografica no disponible.");
            Shader sh=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if(sh==null) sh=Shader.Find("Sprites/Default");
            if(sh!=null) unlit=new Material(sh);
            GameObject floorRenderer=originalGround;
            var originalRenderer=floorRenderer.GetComponent<Renderer>();
            if(originalRenderer!=null) originalRenderer.enabled=false;
            background=new TintTrack{initial=RGB(d.initialBackground)};
            ground=new TintTrack{initial=RGB(d.initialGround)};
            backgroundVisual=Visual(transform,"fondo",-1000);
            groundVisual=Visual(transform,"suelo",-900);
            backgroundVisual.drawMode=SpriteDrawMode.Tiled;
            groundVisual.drawMode=SpriteDrawMode.Tiled;
            GameObject tile=Resources.Load<GameObject>("Prefabs/Decors/TilePrefab");
            GameObject spike=Resources.Load<GameObject>("Prefabs/Decors/SpikePrefab");
            if(tile==null || spike==null) throw new InvalidOperationException("Faltan prefabs de colision originales.");
            float maxGD=0f;
            foreach(Item o in d.objects)
            {
                maxGD=Mathf.Max(maxGD,o.xGD);
                if(o.kind=="backgroundColor") {background.items.Add(o);continue;}
                if(o.kind=="groundColor") {ground.items.Add(o);continue;}
                if(o.kind=="enterEffect") continue; // preserved in data, not implemented visually
                var root=new GameObject("GD_"+o.id+"_"+o.index);
                root.transform.SetParent(transform,false);
                root.transform.position=new Vector3(WorldX(o.xGD),floorY+o.yGD/Unit,0f);
                root.transform.rotation=Quaternion.Euler(0,0,-o.rotationGD);
                root.transform.localScale=new Vector3(o.flipX?-1f:1f,o.flipY?-1f:1f,1f);
                for(int k=0;k<o.frames.Length;k++)
                {
                    int sort=o.layer*100+o.order;
                    if(o.kind=="shipPortal"||o.kind=="cubePortal") sort=k==0?-50:900;
                    // Object layers remain ordered relative to each other; player remains above normal geometry.
                    else sort-=650;
                    var sr=Visual(root.transform,o.frames[k],sort);
                    if(o.id==9) sr.color=Color.black;
                    else if(o.id>=18&&o.id<=21) sr.color=new Color32(255,200,75,255);
                }
                Vector2 size=new Vector2(o.widthGD/Unit,o.heightGD/Unit);
                if(o.kind=="solid" || (o.kind=="hazard"&&o.id!=9))
                {
                    GameObject shape=Instantiate(o.kind=="solid"?tile:spike,root.transform,false);
                    shape.name="OriginalCollision";
                    shape.transform.localPosition=Vector3.zero;
                    shape.transform.localRotation=Quaternion.identity;
                    shape.transform.localScale=new Vector3(size.x,size.y,1);
                    foreach(Renderer r in shape.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
                }
                else if(o.kind=="hazard") // narrow low pit teeth, provisional hitbox
                {
                    var hit=root.AddComponent<BoxCollider2D>(); hit.isTrigger=true;
                    hit.size=new Vector2(size.x*.82f,.12f);hit.offset=new Vector2(0,.11f);
                    root.AddComponent<DeathZone>();
                }
                else if(o.kind=="shipPortal"||o.kind=="cubePortal")
                {
                    var hit=root.AddComponent<BoxCollider2D>(); hit.isTrigger=true;
                    hit.size=new Vector2(size.x*.75f,size.y);
                    root.AddComponent<BiofitModePortal>().Target=o.kind=="shipPortal"?BiofitMode.Ship:BiofitMode.Cube;
                }
                else if(o.kind=="coin")
                {
                    var hit=root.AddComponent<CircleCollider2D>();hit.isTrigger=true;hit.radius=.55f;
                    root.AddComponent<BiofitStereoCoinPickup>();
                }
            }
            background.items.Sort((a,b)=>a.xGD.CompareTo(b.xGD));ground.items.Sort((a,b)=>a.xGD.CompareTo(b.xGD));
            // Provisional ship bounds, deliberately do not close the secret upper route.
            AddRoof(7995,12555,300);
            AddRoof(22935,23700,300);
            AddRoof(23700,maxGD+180,450);
            finalX=WorldX(maxGD)+4f;
            GameObject end=GameObject.FindWithTag("EndOfMap");
            if(end!=null) end.transform.position=new Vector3(finalX,end.transform.position.y,end.transform.position.z);
            GameObject wall=GameObject.Find("EndWallPrefab(Clone)");
            if(wall==null) throw new InvalidOperationException("Falta meta original.");
            wall.transform.position=new Vector3(finalX,wall.transform.position.y,wall.transform.position.z);
            Physics2D.SyncTransforms();
            Debug.Log("BIOFIT Stereo local generado: 2272 objetos visibles, 19 triggers/efectos; suelo Y="+floorY+", meta X="+finalX);
        }
        private void AddRoof(float start,float end,float yGD)
        {
            var obj=new GameObject("StereoShipRoof");obj.transform.SetParent(transform,false);obj.layer=3;
            obj.transform.position=new Vector3((WorldX(start)+WorldX(end))*.5f,floorY+yGD/Unit+.5f,0);
            var col=obj.AddComponent<BoxCollider2D>();col.size=new Vector2((end-start)/Unit,1f);
        }
        private void LateUpdate()
        {
            if(!ready || cameraView==null) return;
            if(player==null) {var p=GameObject.FindGameObjectWithTag("Player");if(p!=null)player=p.transform;}
            if(player!=null)lastPlayerX=player.position.x;
            float x=Mathf.Max(OriginX,lastPlayerX);
            Color bg=background.Evaluate(x),gr=ground.Evaluate(x);
            cameraView.backgroundColor=bg;
            float halfH=cameraView.orthographicSize,halfW=halfH*cameraView.aspect;
            Vector3 c=cameraView.transform.position;
            float tileW=backgroundVisual.sprite.bounds.size.x, tileH=backgroundVisual.sprite.bounds.size.y;
            // Tile phase follows the camera slowly for a simple parallax effect.
            float bgX=Mathf.Floor((c.x*.75f)/tileW)*tileW+c.x*.25f;
            float bgY=Mathf.Floor(c.y/tileH)*tileH;
            backgroundVisual.transform.position=new Vector3(bgX,bgY,1f);
            backgroundVisual.size=new Vector2(2f*(halfW+tileW),2f*(halfH+tileH));
            backgroundVisual.color=bg;
            float gw=groundVisual.sprite.bounds.size.x;
            float depth=Mathf.Max(1f,2f*halfH+Mathf.Abs(c.y-floorY));
            groundVisual.transform.position=new Vector3(Mathf.Floor(c.x/gw)*gw,floorY-depth*.5f,0f);
            groundVisual.size=new Vector2(2f*(halfW+gw),depth);groundVisual.color=gr;
        }
        private void OnDestroy()
        {
            foreach(Sprite s in sprites.Values) if(s!=null) Destroy(s);
            if(unlit!=null) Destroy(unlit);
        }
    }
}
