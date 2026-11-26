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
        public static int TotalCoins { get; private set; } = 3;
        [Serializable] public sealed class Item
        {
            public int index, id, layer, order;
            // 0 preserves the exact imported behaviour. Nonzero is an explicit editor override.
            public int collisionMode, deathMask;
            public float editorScaleX, editorScaleY;
            public string kind;
            public float xGD, yGD, rotationGD, widthGD, heightGD;
            public bool flipX, flipY;
            public string[] frames;
            public int r, g, b;
            public float duration, portalMinGD, portalMaxGD;
        }
        [Serializable] public sealed class Data
        {
            public string sourceSha256;
            public int unitsPerBlock;
            public int editorRevision;
            public float endGD;
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
        private float floorY, finalX, maxY;
        private float lastPlayerX = OriginX;
        private Material unlit;
        private bool ready, oldBackgroundHidden;
        private static float WorldX(float gd) => OriginX + gd / Unit;
        private static Color RGB(int[] v) => new Color32((byte)v[0], (byte)v[1], (byte)v[2], 255);
        public static bool IsLocal(Level level) => level != null && level.Id >= 1 && level.Id <= 5 && level.Map.Count == 1 && level.Map[0].Type == Marker;
        private static string Campaign(int slot) => ResourceRoot + "CampaignV5/" + slot + "/";
        public static TextAsset SelectMap(int index, TextAsset fallback)
        {
            if (index < 0 || index >= 5 || !BiofitThreeModesBootstrap.Active) return fallback;
            ValidateInstalled(index);
            return Resources.Load<TextAsset>(Campaign(index+1)+"Level");
        }
        private static Data ReadData(int slot, string file)
        {
            string text=null;
#if UNITY_EDITOR
            if(file=="Active" && UnityEditor.SessionState.GetInt("BIOFIT_EDITOR_TEST_SLOT",-1)==slot)
            {
                string path=UnityEditor.SessionState.GetString("BIOFIT_EDITOR_TEST_PATH","");
                if(!string.IsNullOrEmpty(path))
                {
                    if(!System.IO.File.Exists(path))throw new InvalidOperationException("Falta borrador de prueba.");
                    text=System.IO.File.ReadAllText(path);
                }
            }
#endif
            if(text==null)
            {
                TextAsset asset=Resources.Load<TextAsset>(Campaign(slot)+file);
                if(asset==null) throw new InvalidOperationException("Falta nivel local V5: "+slot+"/"+file);
                text=asset.text;
            }
            Data d=JsonUtility.FromJson<Data>(text);
            if(d==null || d.unitsPerBlock!=30 || d.objects==null || d.initialBackground==null || d.initialBackground.Length!=3 || d.initialGround==null || d.initialGround.Length!=3 || d.endGD<=0) throw new InvalidOperationException("Datos V5 invalidos");
            return d;
        }
        public static bool ValidateInstalled(int index=0)
        {
            int slot=index+1;
            var map=Resources.Load<TextAsset>(Campaign(slot)+"Level");
            if(map==null) throw new InvalidOperationException("Falta manifest V5: "+slot);
            Level level=new Level(map); Data original=ReadData(slot,"Original"), active=ReadData(slot,"Active");
            int[] counts={2291,1545,1531,1704,2813}; int[] sections={2,1,1,1,2};
            if(!IsLocal(level) || level.Id!=slot || level.Music==null || original.objects.Length!=counts[index] || original.sourceSha256!=active.sourceSha256 || (active.editorRevision==0 && original.endGD!=active.endGD) || active.endGD>original.endGD) throw new InvalidOperationException("Manifest V5 invalido: "+slot);
            var indexes=new HashSet<int>(); var frames=new HashSet<string>();int coins=0,portals=0;
            foreach(Item o in active.objects)
            {
                if(o==null || !indexes.Add(o.index) || o.frames==null || o.widthGD<=0 || o.heightGD<=0 || float.IsNaN(o.xGD)||float.IsNaN(o.yGD)||float.IsInfinity(o.xGD)||float.IsInfinity(o.yGD)||o.kind=="shipPortal") throw new InvalidOperationException("Registro V5 invalido");
                if(o.collisionMode<0 || o.collisionMode>4 || o.deathMask<0 || o.deathMask>15) throw new InvalidOperationException("Colision de editor invalida");
                if(o.kind=="coin") coins++;
                if(o.kind=="ballPortal"||o.kind=="spiderPortal") portals++;
                foreach(string f in o.frames) frames.Add(f);
            }
            if(active.editorRevision==0 && (coins!=3 || portals!=sections[index])) throw new InvalidOperationException("Portales o monedas V5 invalidos");
            if(active.editorRevision!=0 && (active.editorRevision!=1 || coins>3)) throw new InvalidOperationException("Mapa editado: revisión desconocida o más de tres monedas.");
            frames.Add("fondo");frames.Add("suelo");
            foreach(string f in frames) if(Resources.Load<Texture2D>(ResourceRoot+"Sprites/"+f)==null) throw new InvalidOperationException("Falta textura V5: "+f);
            Debug.Log("BIOFIT V5: "+level.Name+" fuente="+original.objects.Length+" activos="+active.objects.Length+" portales="+portals+"; validacion estructural, no gameplay.");
            return true;
        }
        private static bool IsPortal(Item o) => o.kind=="cubePortal" || o.kind=="ballPortal" || o.kind=="spiderPortal" || o.kind=="gravityUp" || o.kind=="gravityDown";
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
            int slot=GameManager.Instance.SelectedLevel.Id;
            ValidateInstalled(slot-1);
            Data original=ReadData(slot,"Original");
            Data d=ReadData(slot,"Active");
            TotalCoins=0; foreach(Item item in d.objects) if(item.kind=="coin") TotalCoins++;
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
            var outlines = slot==1 ? new BiofitV63Outline(d, transform, floorY, unlit, GetSprite("square_05")) : null;
            float maxGD=0f;
            foreach(Item o in d.objects)
            {
                maxGD=Mathf.Max(maxGD,o.xGD);
                maxY=Mathf.Max(maxY,floorY+o.yGD/Unit);
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
                    if(IsPortal(o)) sort=k==0?-50:900;
                    // Object layers remain ordered relative to each other; player remains above normal geometry.
                    else sort-=650;
                    var sr=Visual(root.transform,o.frames[k],sort);
                    if(o.editorScaleX>0 || o.editorScaleY>0) sr.transform.localScale=new Vector3(o.editorScaleX>0?o.editorScaleX:1,o.editorScaleY>0?o.editorScaleY:1,1);
                    if(outlines!=null && outlines.Contains(o)) sr.sprite=outlines.FillSprite;
                    if(o.id==9) sr.color=Color.black;
                    else if(o.id>=18&&o.id<=21) sr.color=new Color32(255,200,75,255);
                }
                Vector2 size=new Vector2(o.widthGD/Unit,o.heightGD/Unit);
                if(o.collisionMode!=0 && (o.kind=="solid" || o.kind=="hazard" || o.kind=="decoration"))
                {
                    if(o.collisionMode==2 || o.collisionMode==4)
                    {
                        root.layer=6;
                        var support=root.AddComponent<BoxCollider2D>(); support.size=size;
                        if(o.collisionMode==4) root.AddComponent<BiofitStereoSolidContact>().Configure(false,o.deathMask);
                    }
                    else if(o.collisionMode==3)
                    {
                        var lethal=root.AddComponent<BoxCollider2D>(); lethal.size=size; lethal.isTrigger=true;
                        root.AddComponent<BiofitEditedDeathZone>().deathMask=o.deathMask;
                    }
                    // 1: visual only. Existing imported objects follow their original path below.
                }
                else if(o.kind=="solid")
                {
                    // Keep geometry and orientation; NEVER inherit the rotated TileDeathZone.
                    root.layer=6; // original Tile layer, included in Cube.groundLayer (72).
                    var box=root.AddComponent<BoxCollider2D>(); box.size=size;
                    root.AddComponent<BiofitStereoSolidContact>().Configure(false);
                }
                else if(o.kind=="hazard"&&o.id!=9)
                {
                    GameObject shape=Instantiate(spike,root.transform,false);
                    shape.name="OriginalCollision";
                    shape.transform.localPosition=Vector3.zero;
                    shape.transform.localRotation=Quaternion.identity;
                    shape.transform.localScale=new Vector3(size.x,size.y,1);
                    foreach(Renderer r in shape.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
                    foreach(Collider2D c in shape.GetComponentsInChildren<Collider2D>(true))
                        if(c.isTrigger) {var trace=c.gameObject.AddComponent<BiofitStereoHazardTrace>();trace.id=o.id;trace.index=o.index;}
                }
                else if(o.kind=="hazard") // narrow low pit teeth, provisional hitbox
                {
                    var hit=root.AddComponent<BoxCollider2D>(); hit.isTrigger=true;
                    hit.size=new Vector2(size.x*.82f,.12f);hit.offset=new Vector2(0,.11f);
                    root.AddComponent<DeathZone>();
                    var trace=root.AddComponent<BiofitStereoHazardTrace>();trace.id=o.id;trace.index=o.index;
                }
                else if(IsPortal(o))
                {
                    var hit=root.AddComponent<BoxCollider2D>(); hit.isTrigger=true;
                    hit.size=new Vector2(size.x*.75f,size.y);
                    if(o.portalMaxGD>o.portalMinGD)
                    {
                        hit.size=new Vector2(size.x*.75f,(o.portalMaxGD-o.portalMinGD)/Unit);
                        hit.offset=new Vector2(0,((o.portalMinGD+o.portalMaxGD)*.5f-o.yGD)/Unit);
                    }
                    if(o.kind=="gravityUp"||o.kind=="gravityDown")
                        root.AddComponent<BiofitV5Interaction>().kind=o.kind;
                    else root.AddComponent<BiofitModePortal>().Target=o.kind=="ballPortal"?BiofitMode.Ball:o.kind=="spiderPortal"?BiofitMode.Spider:BiofitMode.Cube;
                }
                else if(o.kind=="pad"||o.kind=="pinkPad"||o.kind=="gravityPad"||o.kind=="orb")
                {
                    var hit=root.AddComponent<BoxCollider2D>();hit.isTrigger=true;
                    hit.size=o.kind=="orb"?new Vector2(1.05f,1.05f):new Vector2(size.x,.30f);
                    root.AddComponent<BiofitV5Interaction>().kind=o.kind;
                }
                else if(o.kind=="coin")
                {
                    var hit=root.AddComponent<CircleCollider2D>();hit.isTrigger=true;hit.radius=.55f;
                    root.AddComponent<BiofitStereoCoinPickup>();
                }
            }
            if(outlines!=null) outlines.Draw();
            background.items.Sort((a,b)=>a.xGD.CompareTo(b.xGD));ground.items.Sort((a,b)=>a.xGD.CompareTo(b.xGD));
            // Both corridors have explicit continuous support surfaces. Keep the original finish coordinate.
            maxGD=0f;
            foreach(Item o in original.objects) maxGD=Mathf.Max(maxGD,o.xGD);
            finalX=WorldX(d.endGD);
            GameObject end=GameObject.FindWithTag("EndOfMap");
            if(end!=null) end.transform.position=new Vector3(finalX,end.transform.position.y,end.transform.position.z);
            GameObject wall=GameObject.Find("EndWallPrefab(Clone)");
            if(wall==null) throw new InvalidOperationException("Falta meta original.");
            wall.transform.position=new Vector3(finalX,wall.transform.position.y,wall.transform.position.z);
            BiofitV63Finish.Install(wall);
            Physics2D.SyncTransforms();
            Debug.Log("BIOFIT V6.3: "+GameManager.Instance.SelectedLevel.Name+", "+d.objects.Length+" registros activos; suelo Y="+floorY+", meta X="+finalX);
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
            if(player!=null) {lastPlayerX=player.position.x; var cube=player.GetComponent<Cube>(); if(cube!=null && cube.isAlive && (player.position.y<floorY-5f || player.position.y>maxY+8f)) cube.Die();}
            // The upstream UIManager may create a second background over the world.
            if(!oldBackgroundHidden)
            {
                var oldBackground=GameObject.Find("Background");
                if(oldBackground!=null && oldBackground.GetComponentInParent<Canvas>()!=null)
                {oldBackground.SetActive(false);oldBackgroundHidden=true;}
            }
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
