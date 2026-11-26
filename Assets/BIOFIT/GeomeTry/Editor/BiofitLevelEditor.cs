#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Item = Biofit.GeomeTryOriginal.BiofitStereoLocalRuntime.Item;
using Data = Biofit.GeomeTryOriginal.BiofitStereoLocalRuntime.Data;

namespace Biofit.LevelEditing
{
    // Editor-only IMGUI. No preview objects in scenes, no runtime editor assemblies.
    public sealed class BiofitLevelEditor : EditorWindow
    {
        static readonly string[] Names={"Stereo Madness","Back on Track","Polargeist","Can't Let Go","Jumper"};
        static readonly string[] Kinds={"solid","hazard","decoration","cubePortal","ballPortal","spiderPortal","miniPortal","normalSizePortal","gravityUp","gravityDown","pad","pinkPad","redPad","gravityPad","orb","coin","backgroundColor","groundColor","enterEffect"};
        static readonly string[] Collisions={"Original (según tipo)","Decoración / sin contacto","Plataforma sin muerte lateral","Letal al tocar (caja)","Plataforma + muerte lateral"};
        static readonly string[] Mask={"Cube / invertido","Ball","Spider","Ship"};
        const string SpriteDir="Assets/BIOFIT/Resources/BiofitStereoLocal/Sprites/";
        int slot, requestedSlot, paletteIndex;
        Data data;
        string baseText, activeHash, status="Cargá un nivel.";
        readonly Dictionary<int,string> baseRaw=new Dictionary<int,string>();
        readonly Dictionary<int,string> baseTyped=new Dictionary<int,string>();
        readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        readonly HashSet<int> selection=new HashSet<int>();
        readonly List<Item> catalog=new List<Item>();
        readonly Stack<string> undo=new Stack<string>(), redo=new Stack<string>();
        List<Item> clipboard=new List<Item>();
        Vector2 pan=new Vector2(0,120), inspectorScroll, paletteScroll, press, panStart;
        Dictionary<int,Vector2> dragStart;
        float zoom=1.2f, grid=30, dragGrid;
        bool snap=true, showGrid=true, showColliders=true, showTriggers=true, placing, dirty, dragging, panning, selecting;
        string query="",spriteName="";
        int goIndex;
        string lastDelta="";
        float maxLength;
        [Serializable] sealed class ArtEntry {public string name,kind;public float widthGD,heightGD;}
        [Serializable] sealed class ArtIndex {public ArtEntry[] items;}
        int catalogPage; string priorQuery="";
        readonly Dictionary<string,Texture2D> externalTextures=new Dictionary<string,Texture2D>();
        string ArtDir=>Path.Combine(Root,"Builds","BiofitV65Art");
        float testX=300,testY=15;int testMode=0;bool testInvert,testRestart=true,testPick;
        Rect canvas, marquee;
        Vector2 lastWorld=new Vector2(300,15);
        string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string MapDir=>Path.Combine(Application.dataPath,"BIOFIT/Resources/BiofitStereoLocal/CampaignV5",(slot+1).ToString());
        string Active=>Path.Combine(MapDir,"Active.json");
        string Work=>Path.Combine(Root,"Builds","BiofitLevelEditor",(slot+1).ToString());
        string Draft=>Path.Combine(Work,"Draft.json");
        string Baseline=>Path.Combine(Work,"Base.json");
        string KnownActive=>Path.Combine(Work,"Active.sha256");
        static string Hash(string s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
        static Item Clone(Item i)=>JsonUtility.FromJson<Item>(JsonUtility.ToJson(i));
        static bool IsInteraction(Item i)=>i.kind.EndsWith("Portal")||i.kind=="gravityUp"||i.kind=="gravityDown"||i.kind=="pad"||i.kind=="pinkPad"||i.kind=="redPad"||i.kind=="gravityPad"||i.kind=="orb"||i.kind=="coin";
        static bool IsTrigger(Item i)=>i.kind=="backgroundColor"||i.kind=="groundColor"||i.kind=="enterEffect";
        [MenuItem("BIOFIT/Level Editor/Abrir editor")]
        static void Open(){var w=GetWindow<BiofitLevelEditor>("BIOFIT Level Editor");w.minSize=new Vector2(870,510);w.Show();}
        void OnEnable(){wantsMouseMove=true;Undo.undoRedoPerformed+=Repaint;}
        void OnDisable(){Undo.undoRedoPerformed-=Repaint; if(data!=null&&dirty){try{SaveDraft();}catch(Exception ex){Debug.LogError(ex);}}foreach(var image in externalTextures.Values)if(image!=null)DestroyImmediate(image);externalTextures.Clear();}
        void Guard(Action a){try{a();}catch(Exception ex){status="ERROR: "+ex.Message;Debug.LogException(ex);}Repaint();}
        void Load(int n,bool resume)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            if(data!=null&&dirty)SaveDraft();
            string nextDir=Path.Combine(Application.dataPath,"BIOFIT/Resources/BiofitStereoLocal/CampaignV5",(n+1).ToString());
            string nextActive=Path.Combine(nextDir,"Active.json");
            string nextWork=Path.Combine(Root,"Builds","BiofitLevelEditor",(n+1).ToString());
            if(!File.Exists(nextActive))throw new Exception("Falta "+nextActive);
            // Preflight before switching the current document: a rejected load cannot redirect saves.
            string candidate=File.ReadAllText(nextActive);Parse(candidate);Parse(File.ReadAllText(Path.Combine(nextDir,"Original.json")));
            string candidateBase=Path.Combine(nextWork,"Base.json"),candidateDraft=Path.Combine(nextWork,"Draft.json"),candidateKnown=Path.Combine(nextWork,"Active.sha256");
            if(resume&&File.Exists(candidateBase)&&File.Exists(candidateDraft))
            {
                string btext=File.ReadAllText(candidateBase),dtext=File.ReadAllText(candidateDraft);Parse(btext);Parse(dtext);
                string h=Hash(candidate);if(Hash(btext)!=h&&Hash(dtext)!=h&&(!File.Exists(candidateKnown)||File.ReadAllText(candidateKnown).Trim()!=h))throw new Exception("El mapa activo cambió; el borrador se conserva sin abrir.");
            }
            slot=n;requestedSlot=n;
            Directory.CreateDirectory(Work);
            string active=File.ReadAllText(Active);
            activeHash=Hash(active);
            baseText=active;
            string loaded=active;
            if(resume&&File.Exists(Draft)&&File.Exists(Baseline))
            {
                baseText=File.ReadAllText(Baseline);
                loaded=File.ReadAllText(Draft);
                if(Hash(baseText)!=activeHash && Hash(loaded)!=activeHash && (!File.Exists(KnownActive)||File.ReadAllText(KnownActive).Trim()!=activeHash))throw new Exception("El mapa activo cambió desde el borrador. Exportá/resguardá el borrador antes de cargar sin él.");
            }
            else {File.WriteAllText(Baseline,baseText,new UTF8Encoding(false));File.WriteAllText(KnownActive,activeHash); }
            data=Parse(loaded);
            baseRaw.Clear();baseTyped.Clear();
            var b=Parse(baseText);
            maxLength=Parse(File.ReadAllText(Path.Combine(MapDir,"Original.json"))).endGD;
            var raw=RawJson.Objects(RawJson.Fields(baseText)["objects"]);
            for(int i=0;i<b.objects.Length;i++){baseRaw.Add(b.objects[i].index,raw[i]);baseTyped.Add(b.objects[i].index,JsonUtility.ToJson(b.objects[i]));}
            undo.Clear();redo.Clear();selection.Clear();dirty=loaded!=active;
            pan=new Vector2(0,180);zoom=1.2f;BuildCatalog();
            status=Names[slot]+" | "+data.objects.Length+" registros"+(dirty?" | borrador recuperado":"");
        }
        static Data Parse(string s){var d=JsonUtility.FromJson<Data>(s);if(d==null||d.objects==null||d.unitsPerBlock!=30)throw new Exception("JSON de nivel no reconocido.");return d;}
        void BuildCatalog()
        {
            catalog.Clear();catalogPage=0;
            var seen=new HashSet<string>();
            void Template(int id,string kind,float w,float h,string[] frames,int layer=5,int order=2)
            {
                string key=id+"|"+kind+"|"+string.Join(",",frames);
                if(frames.All(f=>Tex(f)!=null)&&seen.Add(key))catalog.Add(new Item{id=id,kind=kind,widthGD=w,heightGD=h,frames=frames,layer=layer,order=order,xGD=15,yGD=h/2});
            }
            // Standard functional objects stay in the palette even when all copies are removed from the campaign.
            for(int i=1;i<=7;i++)Template(i,i==5?"decoration":"solid",30,30,new[]{"square_"+i.ToString("D2")},i==5?1:5,i==5?-7:2);
            Template(8,"hazard",30,30,new[]{"spike_01"});Template(9,"hazard",30.25f,26,new[]{"pit_01"});Template(39,"hazard",30,13.5f,new[]{"spike_02"});Template(103,"hazard",20,19,new[]{"spike_03"});
            Template(40,"solid",30,13.5f,new[]{"plank_01"});Template(62,"solid",30,16,new[]{"square_b_01"});Template(68,"solid",30,16,new[]{"square_b_06"});
            Template(35,"pad",25,4,new[]{"bump_01"},3,12);Template(140,"pinkPad",25,5,new[]{"bump_03"},3,12);Template(900001,"redPad",25,4,new[]{"bump_03"},3,12);Template(67,"gravityPad",25,6,new[]{"gravbump_01"},3,12);Template(36,"orb",30,30,new[]{"ring_01"},3,12);
            Template(142,"coin",40,40,new[]{"secretCoin_01"},5,9);
            Template(10,"gravityDown",34.5f,77,new[]{"portal_01_back","portal_01_front"},5,10);Template(11,"gravityUp",34.5f,77,new[]{"portal_02_back","portal_02_front"},5,10);Template(12,"cubePortal",44.5f,85.5f,new[]{"portal_03_back","portal_03_front"},5,10);Template(47,"ballPortal",44.5f,85.5f,new[]{"portal_07_back","portal_07_front"},5,10);Template(1331,"spiderPortal",44.5f,84.5f,new[]{"portal_17_back","portal_17_front"},5,10);
            Template(99,"miniPortal",44,89.5f,new[]{"gd_portal_09_back","gd_portal_09_front"},5,10);Template(101,"normalSizePortal",44,89.5f,new[]{"gd_portal_08_back","gd_portal_08_front"},5,10);
            Template(29,"backgroundColor",20.5f,28,Array.Empty<string>());Template(30,"groundColor",20.5f,28,Array.Empty<string>());
            for(int k=1;k<=5;k++)
            {
                string f=Path.Combine(Application.dataPath,"BIOFIT/Resources/BiofitStereoLocal/CampaignV5",k.ToString(),"Active.json");
                if(!File.Exists(f))continue;
                foreach(var o in Parse(File.ReadAllText(f)).objects)
                {
                    if(o.kind=="shipPortal")continue;
                    if(seen.Add(o.id+"|"+o.kind+"|"+string.Join(",",o.frames??Array.Empty<string>())))catalog.Add(Clone(o));
                }
            }
            // Real local textures only; arbitrary decorative sprites can be selected.
            string dir=Path.Combine(Root,SpriteDir);
            if(Directory.Exists(dir))
            foreach(string f in Directory.GetFiles(dir,"*.png"))
            {
                string name=Path.GetFileNameWithoutExtension(f);
                if(name=="fondo"||name=="suelo"||catalog.Any(x=>x.frames!=null&&x.frames.Contains(name)))continue;
                var t=Tex(name);if(t==null)continue;
                catalog.Add(new Item{id=0,kind="decoration",frames=new[]{name},widthGD=t.width*.5f,heightGD=t.height*.5f,layer=3,order=0});
            }
            string index=Path.Combine(ArtDir,"Catalog.json");
            if(File.Exists(index))
            {
                var source=JsonUtility.FromJson<ArtIndex>(File.ReadAllText(index));
                if(source!=null&&source.items!=null)foreach(var e in source.items)
                {
                    if(e==null||string.IsNullOrEmpty(e.name)||!e.name.StartsWith("gd_")||e.widthGD<=0||e.heightGD<=0||catalog.Any(x=>x.frames!=null&&x.frames.Contains(e.name)))continue;
                    catalog.Add(new Item{id=0,kind=e.kind=="solid"?"solid":"decoration",frames=new[]{e.name},widthGD=e.widthGD,heightGD=e.heightGD,layer=e.kind=="solid"?5:3,order=0,yGD=e.heightGD/2});
                }
            }
            catalog.Sort((a,b)=>{int x=Array.IndexOf(Kinds,a.kind).CompareTo(Array.IndexOf(Kinds,b.kind));return x!=0?x:a.id.CompareTo(b.id);});
        }
        Texture2D Tex(string name)
        {
            if(string.IsNullOrEmpty(name))return null;
            if(!textures.TryGetValue(name,out var t)){t=AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteDir+name+".png");textures[name]=t;}
            if(t!=null)return t;
            if(!name.StartsWith("gd_")||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)return null;
            if(externalTextures.TryGetValue(name,out t))return t;
            string f=Path.Combine(ArtDir,"Catalog",name+".png");
            if(!File.Exists(f))return null;
            if(externalTextures.Count>=100){foreach(var image in externalTextures.Values)if(image!=null)DestroyImmediate(image);externalTextures.Clear();}
            t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!t.LoadImage(File.ReadAllBytes(f))){DestroyImmediate(t);return null;}
            t.hideFlags=HideFlags.HideAndDontSave;externalTextures[name]=t;return t;
        }
        void EnsureFrame(string name)
        {
            if(string.IsNullOrEmpty(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new Exception("Nombre de sprite inválido.");
            string relative=SpriteDir+name+".png",dst=Path.Combine(Root,relative);
            if(File.Exists(dst))return;
            string source=Path.Combine(ArtDir,"Catalog",name+".png");
            if(!name.StartsWith("gd_")||!File.Exists(source))throw new Exception("Falta sprite: "+name);
            var info=new System.Diagnostics.ProcessStartInfo("git","check-ignore -q -- \""+relative+"\""){WorkingDirectory=Root,UseShellExecute=false,CreateNoWindow=true};
            using(var proc=System.Diagnostics.Process.Start(info)){proc.WaitForExit();if(proc.ExitCode!=0)throw new Exception("Sprite no excluido por Git: "+relative);}
            File.Copy(source,dst);
            AssetDatabase.ImportAsset(relative,ImportAssetOptions.ForceSynchronousImport);textures.Remove(name);

        }
        void Push(){undo.Push(JsonUtility.ToJson(data));if(undo.Count>60){var kept=undo.Take(60).Reverse().ToArray();undo.Clear();foreach(var x in kept)undo.Push(x);}redo.Clear();dirty=true;}
        void History(bool back)
        {
            var from=back?undo:redo;var to=back?redo:undo;if(from.Count==0)return;
            to.Push(JsonUtility.ToJson(data));data=Parse(from.Pop());selection.RemoveWhere(i=>!data.objects.Any(o=>o.index==i));dirty=true;
        }
        Item[] Selected()=>data.objects.Where(o=>selection.Contains(o.index)).ToArray();
        float Snap(float v,float origin=0)=>snap?origin+Mathf.Round((v-origin)/grid)*grid:v;
        Vector2 Screen(Vector2 w)=>new Vector2(canvas.x+(w.x-pan.x)*zoom,canvas.y+(pan.y-w.y)*zoom);
        Vector2 World(Vector2 p)=>new Vector2(pan.x+(p.x-canvas.x)/zoom,pan.y-(p.y-canvas.y)/zoom);
        Rect ObjectRect(Item o)
        {
            var p=Screen(new Vector2(o.xGD,o.yGD));float w=o.widthGD,h=o.heightGD;
            var t=o.frames!=null&&o.frames.Length>0?Tex(o.frames[0]):null;
            if(t!=null){w=Mathf.Max(w,t.width*.5f*(o.editorScaleX>0?o.editorScaleX:1));h=Mathf.Max(h,t.height*.5f*(o.editorScaleY>0?o.editorScaleY:1));}
            return new Rect(p.x-w*zoom/2,p.y-h*zoom/2,w*zoom,h*zoom);
        }
        bool Hit(Item o,Vector2 pos)
        {
            Vector2 p=Screen(new Vector2(o.xGD,o.yGD));Vector2 delta=pos-p;
            float a=-o.rotationGD*Mathf.Deg2Rad; // screen rotation is +GD
            Vector2 q=new Vector2(delta.x*Mathf.Cos(a)-delta.y*Mathf.Sin(a),delta.x*Mathf.Sin(a)+delta.y*Mathf.Cos(a));
            Rect r=ObjectRect(o);return Mathf.Abs(q.x)<=Mathf.Max(5,r.width/2)&&Mathf.Abs(q.y)<=Mathf.Max(5,r.height/2);
        }
        int NextId()=>Math.Max(100000,Math.Max(data.objects.Select(o=>o.index).DefaultIfEmpty(0).Max(),baseRaw.Keys.DefaultIfEmpty(0).Max())+1);
        void Add(Item template,Vector2 pos)
        {
            foreach(string f in template.frames??Array.Empty<string>())EnsureFrame(f);
            Push();var o=Clone(template);o.index=NextId();o.xGD=Snap(pos.x,template.xGD%grid);o.yGD=Snap(pos.y,template.yGD%grid);
            o.portalMinGD=0;o.portalMaxGD=0; // new portal has local hitbox, not inherited corridor
            data.objects=data.objects.Concat(new[]{o}).ToArray();selection.Clear();selection.Add(o.index);
        }
        void Shift(Item o,float dx,float dy)
        {
            o.xGD+=dx;o.yGD+=dy;
            if(o.portalMaxGD>o.portalMinGD){o.portalMinGD+=dy;o.portalMaxGD+=dy;}
        }
        void Delete(){if(selection.Count==0)return;Push();data.objects=data.objects.Where(o=>!selection.Contains(o.index)).ToArray();selection.Clear();}
        void Copy(){clipboard=Selected().Select(Clone).ToList();}
        void Paste(Vector2 delta)
        {
            if(clipboard.Count==0)return;Push();int id=NextId();selection.Clear();var added=new List<Item>();
            foreach(var source in clipboard){var o=Clone(source);o.index=id++;Shift(o,delta.x,delta.y);selection.Add(o.index);added.Add(o);}
            data.objects=data.objects.Concat(added).ToArray();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("BIOFIT LEVEL EDITOR • V6.5 desarrollo",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                requestedSlot=EditorGUILayout.Popup(requestedSlot,Names,GUILayout.Width(155));
                if(GUILayout.Button("Cargar / borrador",EditorStyles.toolbarButton))Guard(()=>Load(requestedSlot,true));
                if(GUILayout.Button("Cargar activo",EditorStyles.toolbarButton))Guard(()=>{if(EditorUtility.DisplayDialog("Cargar activo","Ignorar borrador guardado y trabajar desde el activo?","Sí","Cancelar")){Load(requestedSlot,false);if(File.Exists(Draft))File.Delete(Draft);}});
                if(data!=null)
                {
                    if(GUILayout.Button("Guardar borrador",EditorStyles.toolbarButton))Guard(SaveDraft);
                    if(GUILayout.Button("Validar",EditorStyles.toolbarButton))Guard(()=>{Validate();status="Validación estructural OK. Jugabilidad pendiente.";});
                    if(GUILayout.Button("Probar borrador",EditorStyles.toolbarButton))Guard(PrepareTest);
                    if(GUILayout.Button("Vista real",EditorStyles.toolbarButton))Guard(()=>LaunchV65(true));
                    if(GUILayout.Button("Jugar desde aquí",EditorStyles.toolbarButton))Guard(()=>LaunchV65(false));
                    if(GUILayout.Button("Aplicar al juego",EditorStyles.toolbarButton))Guard(Apply);
                    if(GUILayout.Button("Exportar cambios",EditorStyles.toolbarButton))Guard(Export);
                    if(GUILayout.Button("FLM",EditorStyles.toolbarButton))Guard(()=>BiofitEditorFlm.Export(data,Names[slot],Tex));
                }
            }
            if(data==null){EditorGUILayout.HelpBox(status,MessageType.Info);return;}
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if(GUILayout.Button("Deshacer",EditorStyles.toolbarButton))History(true);
                    if(GUILayout.Button("Rehacer",EditorStyles.toolbarButton))History(false);
                    if(GUILayout.Button("Copiar",EditorStyles.toolbarButton))Copy();
                    if(GUILayout.Button("Pegar +1",EditorStyles.toolbarButton))Paste(new Vector2(grid,0));
                    if(GUILayout.Button("Duplicar",EditorStyles.toolbarButton)){Copy();Paste(new Vector2(grid,0));}
                    if(GUILayout.Button("Eliminar",EditorStyles.toolbarButton))Delete();
                    if(GUILayout.Button("Vaciar borrador",EditorStyles.toolbarButton))Guard(()=>{if(EditorUtility.DisplayDialog("Crear desde cero","Vaciar todos los registros del borrador. Se conserva la ranura, música y longitud.","Vaciar","Cancelar")){Push();data.objects=Array.Empty<Item>();selection.Clear();}});
                    placing=GUILayout.Toggle(placing,"Colocar",EditorStyles.toolbarButton);
                    snap=GUILayout.Toggle(snap,"Snap",EditorStyles.toolbarButton);
                    grid=EditorGUILayout.Popup(grid==15?0:grid==30?1:2,new[]{"15 GD","30 GD","60 GD"},GUILayout.Width(65)) switch {0=>15f,1=>30f,_=>60f};
                    showGrid=GUILayout.Toggle(showGrid,"Grilla",EditorStyles.toolbarButton);
                    showColliders=GUILayout.Toggle(showColliders,"Colisión",EditorStyles.toolbarButton);
                    showTriggers=GUILayout.Toggle(showTriggers,"Eventos",EditorStyles.toolbarButton);
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Zoom",GUILayout.Width(35));zoom=GUILayout.HorizontalSlider(zoom,.01f,4f,GUILayout.Width(140));
                    if(GUILayout.Button("Ver todo",GUILayout.Width(75))){zoom=Mathf.Clamp((canvas.width>0?canvas.width:position.width-500)/Mathf.Max(30,data.endGD),.01f,4);pan=new Vector2(0,400);}
                    GUILayout.Label("X",GUILayout.Width(15));pan.x=EditorGUILayout.FloatField(pan.x,GUILayout.Width(85));
                    GUILayout.Label("Y",GUILayout.Width(15));pan.y=EditorGUILayout.FloatField(pan.y,GUILayout.Width(70));
                    GUILayout.Label("Ir a %",GUILayout.Width(45));float pct=EditorGUILayout.Slider(Mathf.Clamp01(pan.x/data.endGD)*100,0,100);if(GUI.changed&&Mathf.Abs(pct-Mathf.Clamp01(pan.x/data.endGD)*100)>.001f)pan.x=pct*data.endGD/100;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(data.objects.Length+" objetos | "+selection.Count+" seleccionados");
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    using(new EditorGUILayout.VerticalScope(GUILayout.Width(310)))DrawCatalog();
                    using(new EditorGUILayout.VerticalScope())
                    {
                        canvas=GUILayoutUtility.GetRect(300,300,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
                        DrawCanvas();HandleCanvas();
                    }
                    using(new EditorGUILayout.VerticalScope(GUILayout.Width(270)))DrawInspector();
                }
                EditorGUILayout.LabelField("Mouse: arrastrar objeto • Shift selección múltiple • arrastrar vacío: caja • botón medio/derecho: desplazar • rueda: zoom • Supr • Ctrl+Z/Y/C/V/D",EditorStyles.miniLabel);
                EditorGUILayout.LabelField(status+(dirty?" | borrador modificado":""),EditorStyles.wordWrappedMiniLabel);
            }
        }
        static string Friendly(string kind)
        {
            switch(kind){case "solid":return "Bloque / plataforma";case "hazard":return "Pincho / peligro";case "decoration":return "Decoración";case "cubePortal":return "Portal Cube";case "ballPortal":return "Portal Ball";case "spiderPortal":return "Portal Spider";case "miniPortal":return "Portal mini (rosa)";case "normalSizePortal":return "Portal tamaño normal (verde)";case "pad":return "Impulso amarillo";case "pinkPad":return "Impulso rosa";case "redPad":return "Impulso rojo (9,5)";case "gravityPad":return "Impulso gravedad";case "orb":return "Orbe amarillo";case "gravityUp":return "Gravedad invertida";case "gravityDown":return "Gravedad normal";case "coin":return "Moneda secreta";case "backgroundColor":return "Color fondo";case "groundColor":return "Color suelo";default:return kind;}
        }
        void DrawCatalog()
        {
            GUILayout.Label("CATÁLOGO LOCAL",EditorStyles.boldLabel);
            query=EditorGUILayout.TextField("Buscar",query);
            if(query!=priorQuery){priorQuery=query;catalogPage=0;paletteScroll=Vector2.zero;}
            string Label(Item o)=>Friendly(o.kind)+" / "+o.kind+" #"+o.id+" "+string.Join(" ",o.frames??Array.Empty<string>());
            var matched=Enumerable.Range(0,catalog.Count).Where(i=>Label(catalog[i]).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            int pages=Mathf.Max(1,Mathf.CeilToInt(matched.Length/60f));catalogPage=Mathf.Clamp(catalogPage,0,pages-1);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("<",GUILayout.Width(30))){catalogPage=Mathf.Max(0,catalogPage-1);paletteScroll=Vector2.zero;}
                GUILayout.Label((catalogPage+1)+"/"+pages+" · "+matched.Length+" objetos");
                if(GUILayout.Button(">",GUILayout.Width(30))){catalogPage=Mathf.Min(pages-1,catalogPage+1);paletteScroll=Vector2.zero;}
            }
            paletteScroll=EditorGUILayout.BeginScrollView(paletteScroll);
            foreach(int i in matched.Skip(catalogPage*60).Take(60))
            {
                var o=catalog[i];string label=Label(o);
                using(new EditorGUILayout.HorizontalScope())
                {
                    var t=o.frames!=null&&o.frames.Length>0?Tex(o.frames[0]):null;
                    Color oldColor=GUI.color;if(o.kind=="redPad")GUI.color=new Color(1f,.2f,.2f);
                    GUILayout.Label(t,GUILayout.Width(30),GUILayout.Height(30));GUI.color=oldColor;
                    var style=new GUIStyle(EditorStyles.miniButton){alignment=TextAnchor.MiddleLeft,wordWrap=true,clipping=TextClipping.Clip};
                    if(GUILayout.Toggle(paletteIndex==i,new GUIContent(label,label),style,GUILayout.Width(250),GUILayout.Height(42))&&paletteIndex!=i){paletteIndex=i;placing=true;}
                }
            }
            EditorGUILayout.EndScrollView();
            if(GUILayout.Button("Colocar seleccionado"))placing=true;
        }
        void DrawCanvas()
        {
            EditorGUI.DrawRect(canvas,new Color(.075f,.13f,.21f));
            GUI.BeginGroup(canvas);
            Rect old=canvas;canvas=new Rect(0,0,old.width,old.height);
            if(showGrid)
            {
                float step=grid;while(step*zoom<12)step*=2;
                for(float x=Mathf.Floor(pan.x/step)*step;x<pan.x+canvas.width/zoom;x+=step){float p=Screen(new Vector2(x,0)).x;EditorGUI.DrawRect(new Rect(p,0,1,canvas.height),new Color(1,1,1,.08f));}
                for(float y=Mathf.Floor((pan.y-canvas.height/zoom)/step)*step;y<pan.y;y+=step){float p=Screen(new Vector2(0,y)).y;EditorGUI.DrawRect(new Rect(0,p,canvas.width,1),new Color(1,1,1,.08f));}
            }
            float fy=Screen(Vector2.zero).y;EditorGUI.DrawRect(new Rect(0,fy,canvas.width,2),Color.cyan);
            float end=Screen(new Vector2(data.endGD,0)).x;EditorGUI.DrawRect(new Rect(end,0,2,canvas.height),Color.green);
            foreach(var o in data.objects.OrderBy(x=>x.layer*100+x.order))
            {
                if(IsTrigger(o)&&!showTriggers)continue;
                Rect r=ObjectRect(o);float margin=Mathf.Max(r.width,r.height);
                if(!new Rect(-margin,-margin,canvas.width+2*margin,canvas.height+2*margin).Contains(r.center))continue;
                Matrix4x4 prior=GUI.matrix;GUIUtility.RotateAroundPivot(o.rotationGD,r.center);
                bool painted=false;
                foreach(string f in o.frames??Array.Empty<string>())
                {
                    var t=Tex(f);if(t==null)continue;
                    float sw=t.width*zoom*.5f*(o.editorScaleX>0?o.editorScaleX:1),sh=t.height*zoom*.5f*(o.editorScaleY>0?o.editorScaleY:1);
                    Rect sr=new Rect(r.center.x-sw*.5f,r.center.y-sh*.5f,sw,sh);
                    Color before=GUI.color;if(o.kind=="redPad")GUI.color=new Color(1f,.2f,.2f);
                    GUI.DrawTextureWithTexCoords(sr,t,new Rect(o.flipX?1:0,o.flipY?1:0,o.flipX?-1:1,o.flipY?-1:1));GUI.color=before;painted=true;
                }
                if(!painted){EditorGUI.DrawRect(r,new Color(.7f,.45f,.1f,.65f));GUI.Label(r,o.kind,EditorStyles.miniLabel);}
                if(showColliders||selection.Contains(o.index))
                {
                    Color c=selection.Contains(o.index)?Color.yellow:o.collisionMode==1||o.kind=="decoration"?new Color(.7f,.7f,.7f,.45f):o.collisionMode==3||o.kind=="hazard"?Color.red:Color.green;
                    float w=o.widthGD*zoom,h=o.heightGD*zoom;
                    Outline(new Rect(r.center.x-w/2,r.center.y-h/2,w,h),c);
                }
                GUI.matrix=prior;
            }
            var marker=Screen(new Vector2(testX,testY));Outline(new Rect(marker.x-7,marker.y-7,14,14),Color.cyan);
            if(selecting)Outline(new Rect(marquee.x-old.x,marquee.y-old.y,marquee.width,marquee.height),Color.yellow);
            GUI.Label(new Rect(5,2,250,20),"GD "+lastWorld.x.ToString("F0")+", "+lastWorld.y.ToString("F0"));
            canvas=old;GUI.EndGroup();
        }
        static void Outline(Rect r,Color c){EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,1),c);EditorGUI.DrawRect(new Rect(r.x,r.yMax-1,r.width,1),c);EditorGUI.DrawRect(new Rect(r.x,r.y,1,r.height),c);EditorGUI.DrawRect(new Rect(r.xMax-1,r.y,1,r.height),c);}
        void HandleCanvas()
        {
            Event e=Event.current;
            bool inside=canvas.Contains(e.mousePosition);
            if(inside)lastWorld=World(e.mousePosition);
            if(e.type==EventType.ScrollWheel&&inside){Vector2 anchor=World(e.mousePosition);zoom=Mathf.Clamp(zoom*Mathf.Pow(1.12f,-e.delta.y),.01f,4);Vector2 after=World(e.mousePosition);pan+=anchor-after;e.Use();Repaint();return;}
            if(e.type==EventType.MouseDown&&inside)
            {
                GUI.FocusControl(null);press=e.mousePosition;panStart=pan;
                if(e.button==1||e.button==2){panning=true;e.Use();return;}
                if(e.button!=0)return;
                if(testPick){testX=Snap(lastWorld.x);testY=Snap(lastWorld.y);testPick=false;e.Use();Repaint();return;}
                if(placing&&catalog.Count>0){Guard(()=>Add(catalog[Mathf.Clamp(paletteIndex,0,catalog.Count-1)],lastWorld));e.Use();Repaint();return;}
                var hits=data.objects.Where(o=>(showTriggers||!IsTrigger(o))&&Hit(o,e.mousePosition)).OrderByDescending(o=>o.layer*100+o.order).ThenByDescending(o=>o.index).ToArray();
                var hit=hits.FirstOrDefault();
                if(e.alt&&hits.Length>1){int prev=Array.FindIndex(hits,o=>selection.Contains(o.index));hit=hits[(prev+1)%hits.Length];}
                if(hit==null){if(!e.shift)selection.Clear();selecting=true;marquee=new Rect(press,Vector2.zero);}
                else
                {
                    if(e.shift){if(!selection.Add(hit.index)){selection.Remove(hit.index);e.Use();Repaint();return;}}
                    else if(!selection.Contains(hit.index)){selection.Clear();selection.Add(hit.index);}
                    dragStart=Selected().ToDictionary(o=>o.index,o=>new Vector2(o.xGD,o.yGD));
                    dragGrid=grid;dragging=false;
                }
                e.Use();Repaint();
            }
            else if(e.type==EventType.MouseDrag)
            {
                Vector2 diff=e.mousePosition-press;
                if(panning){pan=panStart+new Vector2(-diff.x/zoom,diff.y/zoom);e.Use();Repaint();}
                else if(selecting){marquee=Rect.MinMaxRect(Mathf.Min(press.x,e.mousePosition.x),Mathf.Min(press.y,e.mousePosition.y),Mathf.Max(press.x,e.mousePosition.x),Mathf.Max(press.y,e.mousePosition.y));e.Use();Repaint();}
                else if(dragStart!=null)
                {
                    if(!dragging){Push();dragging=true;}
                    float dx=diff.x/zoom,dy=-diff.y/zoom;
                    if(snap){dx=Mathf.Round(dx/dragGrid)*dragGrid;dy=Mathf.Round(dy/dragGrid)*dragGrid;}
                    foreach(var o in Selected())if(dragStart.TryGetValue(o.index,out var old))Shift(o,old.x+dx-o.xGD,old.y+dy-o.yGD);
                    e.Use();Repaint();
                }
            }
            else if(e.type==EventType.MouseUp&&(dragStart!=null||panning||selecting))
            {
                if(selecting)foreach(var o in data.objects)if((showTriggers||!IsTrigger(o))&&marquee.Contains(Screen(new Vector2(o.xGD,o.yGD))))selection.Add(o.index);
                dragStart=null;dragging=false;panning=false;selecting=false;e.Use();Repaint();
            }
            else if(e.type==EventType.KeyDown&&inside&&!EditorGUIUtility.editingTextField)
            {
                bool used=true;
                if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace)Delete();
                else if(e.keyCode==KeyCode.Escape){placing=false;selection.Clear();}
                else if(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.DownArrow)
                {if(selection.Count>0){Push();float step=e.shift?1:grid;foreach(var o in Selected())Shift(o,e.keyCode==KeyCode.LeftArrow?-step:e.keyCode==KeyCode.RightArrow?step:0,e.keyCode==KeyCode.DownArrow?-step:e.keyCode==KeyCode.UpArrow?step:0);}}
                else if(e.control||e.command)
                {
                    switch(e.keyCode){case KeyCode.Z:History(!e.shift);break;case KeyCode.Y:History(false);break;case KeyCode.C:Copy();break;case KeyCode.V:Paste(new Vector2(grid,0));break;case KeyCode.D:Copy();Paste(new Vector2(grid,0));break;case KeyCode.A:foreach(var o in data.objects)selection.Add(o.index);break;case KeyCode.S:Guard(SaveDraft);break;default:used=false;break;}
                }
                else used=false;
                if(used){e.Use();Repaint();}
            }
        }
        void DrawInspector()
        {
            GUILayout.Label("PROPIEDADES",EditorStyles.boldLabel);
            inspectorScroll=EditorGUILayout.BeginScrollView(inspectorScroll);
            goIndex=EditorGUILayout.IntField("Buscar índice",goIndex);
            if(GUILayout.Button("Seleccionar / centrar índice"))Guard(()=>{var found=data.objects.FirstOrDefault(o=>o.index==goIndex);if(found==null)throw new Exception("Índice inexistente.");selection.Clear();selection.Add(found.index);pan=new Vector2(found.xGD-canvas.width/(2*zoom),found.yGD+canvas.height/(2*zoom));});
            var selected=Selected();
            if(selected.Length==1)
            {
                Item o=selected[0];GUILayout.Label("#"+o.index+" · ID "+o.id);
                var n=Clone(o);
                n.id=EditorGUILayout.IntField("ID gráfico",n.id);
                int k=Array.IndexOf(Kinds,n.kind);n.kind=Kinds[EditorGUILayout.Popup("Función",Mathf.Max(0,k),Kinds)];
                n.xGD=EditorGUILayout.FloatField("X (GD)",n.xGD);n.yGD=EditorGUILayout.FloatField("Y (GD)",n.yGD);
                n.rotationGD=EditorGUILayout.FloatField("Rotación GD",n.rotationGD);
                n.flipX=EditorGUILayout.Toggle("Espejo X",n.flipX);n.flipY=EditorGUILayout.Toggle("Espejo Y",n.flipY);
                n.widthGD=EditorGUILayout.FloatField("Ancho hitbox GD",n.widthGD);n.heightGD=EditorGUILayout.FloatField("Alto hitbox GD",n.heightGD);
                n.layer=EditorGUILayout.IntField("Capa",n.layer);n.order=EditorGUILayout.IntField("Orden",n.order);
                float vx=EditorGUILayout.DelayedFloatField("Escala visual X (1=original)",n.editorScaleX>0?n.editorScaleX:1),vy=EditorGUILayout.DelayedFloatField("Escala visual Y (1=original)",n.editorScaleY>0?n.editorScaleY:1);
                if(vx!=(n.editorScaleX>0?n.editorScaleX:1))n.editorScaleX=Mathf.Clamp(vx,.05f,20);
                if(vy!=(n.editorScaleY>0?n.editorScaleY:1))n.editorScaleY=Mathf.Clamp(vy,.05f,20);
                if(!IsInteraction(n)&&!IsTrigger(n))
                {
                    int beforeCollision=n.collisionMode;
                    n.collisionMode=EditorGUILayout.Popup("Colisión",Mathf.Clamp(n.collisionMode,0,4),Collisions);
                    if(n.collisionMode!=beforeCollision && (n.collisionMode==3||n.collisionMode==4) && n.deathMask==0)n.deathMask=15;
                    if(n.collisionMode==3||n.collisionMode==4)n.deathMask=EditorGUILayout.MaskField("Mortal para",n.deathMask,Mask)&15;
                }
                else {n.collisionMode=0;EditorGUILayout.HelpBox("La función utiliza el trigger del motor existente.",MessageType.None);}
                if(n.kind.EndsWith("Portal")||n.kind=="gravityUp"||n.kind=="gravityDown")
                {
                    n.portalMinGD=EditorGUILayout.FloatField("Trigger mínimo Y",n.portalMinGD);
                    n.portalMaxGD=EditorGUILayout.FloatField("Trigger máximo Y",n.portalMaxGD);
                }
                if(n.kind=="backgroundColor"||n.kind=="groundColor")
                {
                    var c=EditorGUILayout.ColorField("Color",new Color32((byte)n.r,(byte)n.g,(byte)n.b,255));Color32 b=c;n.r=b.r;n.g=b.g;n.b=b.b;
                    n.duration=EditorGUILayout.FloatField("Transición s",n.duration);
                }
                string frames=EditorGUILayout.TextField("Sprites (coma)",string.Join(",",n.frames??Array.Empty<string>()));
                if(frames!=string.Join(",",n.frames??Array.Empty<string>()))n.frames=frames.Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).ToArray();
                if(JsonUtility.ToJson(n)!=JsonUtility.ToJson(o))
                {
                    Push();
                    if(n.yGD!=o.yGD&&n.portalMinGD==o.portalMinGD&&n.portalMaxGD==o.portalMaxGD&&o.portalMaxGD>o.portalMinGD){n.portalMinGD+=n.yGD-o.yGD;n.portalMaxGD+=n.yGD-o.yGD;}
                    data.objects[Array.IndexOf(data.objects,o)]=n;
                }
                spriteName=EditorGUILayout.TextField("Sprite local",spriteName);
                if(GUILayout.Button("Seleccionar todos del mismo ID")){int id=o.id;foreach(var v in data.objects)if(v.id==id)selection.Add(v.index);}
                if(GUILayout.Button("Añadir sprite al objeto"))Guard(()=>{if(Tex(spriteName)==null)throw new Exception("Sprite inexistente.");Push();var v=Selected()[0];v.frames=(v.frames??Array.Empty<string>()).Concat(new[]{spriteName}).ToArray();});
            }
            else if(selected.Length>1)
            {
                EditorGUILayout.LabelField("Selección múltiple: "+selected.Length);
                if(GUILayout.Button("Mover +1 bloque X")){Push();foreach(var o in selected)Shift(o,30,0);}
                if(GUILayout.Button("Mover -1 bloque X")){Push();foreach(var o in selected)Shift(o,-30,0);}
                if(GUILayout.Button("Mover +1 bloque Y")){Push();foreach(var o in selected)Shift(o,0,30);}
                if(GUILayout.Button("Mover -1 bloque Y")){Push();foreach(var o in selected)Shift(o,0,-30);}
                if(GUILayout.Button("Rotar 90° individual")){Push();foreach(var o in selected)o.rotationGD+=90;}
            }
            else EditorGUILayout.HelpBox("Seleccioná objetos o elegí una plantilla del catálogo y activá Colocar.",MessageType.Info);
            GUILayout.Space(8);GUILayout.Label("PRUEBA V6.5",EditorStyles.boldLabel);
            testX=EditorGUILayout.FloatField("Inicio X GD",testX);testY=EditorGUILayout.FloatField("Centro Y GD",testY);
            testMode=EditorGUILayout.Popup("Vehículo",testMode,new[]{"Cube","Ball","Spider","Auto por portales","Mini Cube"});
            testInvert=EditorGUILayout.Toggle("Gravedad invertida",testInvert);testRestart=EditorGUILayout.Toggle("Reiniciar aquí",testRestart);
            if(GUILayout.Button("Elegir punto en mapa")){testPick=true;placing=false;}
            if(GUILayout.Button("Usar centro de vista")){var v=World(canvas.center);testX=v.x;testY=v.y;}
            GUILayout.Label("Progreso: "+(100f*testX/data.endGD).ToString("F1")+" %");
            GUILayout.Space(8);GUILayout.Label("NIVEL",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Monedas",data.objects.Count(o=>o.kind=="coin")+" / 3");
            EditorGUILayout.LabelField("Portales",data.objects.Count(o=>o.kind.EndsWith("Portal")).ToString());
            float length=EditorGUILayout.DelayedFloatField("Longitud GD (máx. original)",data.endGD);
            if(length!=data.endGD){Push();data.endGD=Mathf.Clamp(length,300,maxLength);}
            if(GUILayout.Button("Restaurar último Active"))Guard(RestorePrevious);
            if(GUILayout.Button("Importar PNG propio local"))Guard(ImportPng);
            if(GUILayout.Button("Cancelar prueba preparada")){SessionState.EraseString("BIOFIT_EDITOR_TEST_PATH");SessionState.EraseInt("BIOFIT_EDITOR_TEST_SLOT");status="Prueba desactivada; Play utiliza Active.";}
            if(GUILayout.Button("Publicar delta experimental"))Guard(PublishDelta);
            if(GUILayout.Button("Abrir carpeta de trabajo"))EditorUtility.RevealInFinder(Work);
            EditorGUILayout.HelpBox("Probar borrador no cambia Active. Aplicar modifica solo Active.json. Original.json y Level.json permanecen intactos. Exportar genera diferencias en tools/level_editor/changes; publicar con el script documentado.",MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void Validate()
        {
            if(data==null)throw new Exception("Sin nivel.");
            if(data.objects.Count(x=>x.kind=="coin")>3)throw new Exception("Máximo 3 monedas por HUD actual.");
            float previousLength=Parse(baseText).endGD;
            if(float.IsNaN(data.endGD)||data.endGD<300||data.endGD>maxLength)throw new Exception("Longitud fuera del intervalo permitido.");
            var ids=new HashSet<int>();
            foreach(var o in data.objects)
            {
                if(!ids.Add(o.index)||!Kinds.Contains(o.kind)||o.frames==null||o.widthGD<=0||o.heightGD<=0||o.collisionMode<0||o.collisionMode>4||o.deathMask<0||o.deathMask>15)throw new Exception("Registro inválido #"+o.index);
                foreach(float f in new[]{o.xGD,o.yGD,o.rotationGD,o.widthGD,o.heightGD,o.portalMinGD,o.portalMaxGD,o.duration,o.editorScaleX,o.editorScaleY})if(float.IsNaN(f)||float.IsInfinity(f))throw new Exception("Número inválido #"+o.index);
                if((!baseTyped.TryGetValue(o.index,out var existing)||existing!=JsonUtility.ToJson(o)||data.endGD<previousLength) && (o.xGD<0||o.xGD>data.endGD))throw new Exception("Fuera del área del nivel #"+o.index);
                foreach(string f in o.frames){EnsureFrame(f);if(Tex(f)==null)throw new Exception("Sprite local no encontrado: "+f);}
                if(!IsTrigger(o)&&o.frames.Length==0)throw new Exception("Objeto visible sin sprite #"+o.index);
            }
        }
        string Serialize()
        {
            var header=RawJson.Fields(baseText);
            header["editorRevision"]="1";
            if(data.endGD!=Parse(baseText).endGD)header["endGD"]=data.endGD.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            var records=new List<string>();
            foreach(var o in data.objects)
            {
                string t=JsonUtility.ToJson(o);
                if(baseRaw.TryGetValue(o.index,out var old))
                {
                    if(baseTyped[o.index]==t){records.Add(old);continue;}
                    var f=RawJson.Fields(old);var before=RawJson.Fields(baseTyped[o.index]);
                    foreach(var p in RawJson.Fields(t))if(!before.TryGetValue(p.Key,out var b)||p.Value!=b)f[p.Key]=p.Value;
                    records.Add(RawJson.Join(f));
                }
                else records.Add(t);
            }
            header["objects"]="["+string.Join(",",records)+"]";return RawJson.Join(header);
        }
        void SaveDraft()
        {
            if(data==null)return;
            Directory.CreateDirectory(Work);Atomic(Draft,Serialize());
            dirty=false;status="Borrador guardado: "+Draft;
        }
        static void Atomic(string path,string text)
        {
            string temp=path+".tmp";File.WriteAllText(temp,text,new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        void LaunchV65(bool preview)
        {
            Validate();if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            if(!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/GeomeTryOriginal/Assets/Scenes/Main Menu.unity");
            if(scene==null)throw new Exception("Falta Main Menu original.");
            if(float.IsNaN(testX)||float.IsInfinity(testX)||float.IsNaN(testY)||float.IsInfinity(testY))throw new Exception("Coordenadas inválidas.");
            SaveDraft();int mode=testMode;bool invert=testInvert;
            if(mode==3){mode=0;invert=false;foreach(var o in data.objects.Where(o=>o.xGD<=testX).OrderBy(o=>o.xGD)){switch(o.kind){case "cubePortal":mode=0;invert=false;break;case "ballPortal":mode=1;invert=false;break;case "spiderPortal":mode=2;invert=false;break;case "gravityUp":if(mode==0||mode==4)invert=true;break;case "gravityDown":if(mode==0||mode==4)invert=false;break;case "miniPortal":if(mode==0)mode=4;break;case "normalSizePortal":if(mode==4)mode=0;break;}}}
            Biofit.GeomeTryOriginal.BiofitV65EditorSession.Configure(slot+1,Draft,Mathf.Clamp(testX,0,data.endGD),testY,mode,invert,testRestart,preview);
            SessionState.SetString("BIOFIT_V65_PREV_START_SCENE",UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene==null?"":AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene));
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=scene;
            EditorApplication.isPlaying=true;
        }
        void PrepareTest()
        {
            Validate();if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            SaveDraft();UnityEditor.SessionState.SetString("BIOFIT_EDITOR_TEST_PATH",Draft);UnityEditor.SessionState.SetInt("BIOFIT_EDITOR_TEST_SLOT",slot+1);
            status="Prueba preparada: "+Names[slot]+". Abrí Main Menu original, Play y seleccioná ese nivel. Active.json intacto.";
            EditorUtility.DisplayDialog("Prueba de borrador",status,"Entendido");
        }
        [InitializeOnLoadMethod] static void ClearTestAfterPlay()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                bool test=SessionState.GetBool("BIOFIT_V65_ACTIVE",false);
                if(state==PlayModeStateChange.EnteredPlayMode&&test)
                    EditorApplication.delayCall+=()=>EditorApplication.ExecuteMenuItem("Window/General/Game");
                if(state!=PlayModeStateChange.EnteredEditMode)return;
                int returnSlot=SessionState.GetInt("BIOFIT_EDITOR_TEST_SLOT",1)-1;
                float x=SessionState.GetFloat("BIOFIT_V65_X",300),y=SessionState.GetFloat("BIOFIT_V65_Y",15);
                int mode=SessionState.GetInt("BIOFIT_V65_MODE",0);
                bool invert=SessionState.GetBool("BIOFIT_V65_INVERT",false);
                SessionState.EraseString("BIOFIT_EDITOR_TEST_PATH");SessionState.EraseInt("BIOFIT_EDITOR_TEST_SLOT");
                Biofit.GeomeTryOriginal.BiofitV65EditorSession.Clear();
                if(test)EditorApplication.delayCall+=()=>
                {
                    var w=GetWindow<BiofitLevelEditor>("BIOFIT Level Editor");
                    w.Show();w.Focus();
                    w.Guard(()=>{w.Load(Mathf.Clamp(returnSlot,0,4),true);w.testX=x;w.testY=y;w.testMode=mode;w.testInvert=invert;w.pan=new Vector2(Mathf.Max(0,x-300),y+200);});
                };
            };
        }
        void RestorePrevious()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            string p=Path.Combine(Work,"PreviousActive.json");if(!File.Exists(p))throw new Exception("No hay versión anterior guardada.");
            if(Hash(File.ReadAllText(Active))!=activeHash)throw new Exception("Active cambió externamente.");
            if(!EditorUtility.DisplayDialog("Restaurar","Restaurar únicamente el último Active anterior? El borrador permanece.","Restaurar","Cancelar"))return;
            Atomic(Active,File.ReadAllText(p));activeHash=Hash(File.ReadAllText(Active));File.WriteAllText(KnownActive,activeHash);AssetDatabase.Refresh();status="Active anterior restaurado. Borrador conservado.";
        }
        void ImportPng()
        {
            string src=EditorUtility.OpenFilePanel("PNG propio o autorizado","","png");if(string.IsNullOrEmpty(src))return;
            if(!EditorUtility.DisplayDialog("Importar local","Importar solo un PNG propio o con permiso. No se publicará automáticamente.","Importar","Cancelar"))return;
            string name="editor_"+Path.GetFileNameWithoutExtension(src).Replace(" ","_");
            if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new Exception("Nombre inválido.");
            string dst=Path.Combine(Root,SpriteDir,name+".png");if(File.Exists(dst))throw new Exception("Ya existe ese sprite. No se sobrescribe.");
            File.Copy(src,dst);AssetDatabase.ImportAsset(SpriteDir+name+".png",ImportAssetOptions.ForceSynchronousImport);textures.Remove(name);BuildCatalog();spriteName=name;status="Sprite importado localmente: "+name;
        }
        void Apply()
        {
            Validate();
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            if(Hash(File.ReadAllText(Active))!=activeHash)throw new Exception("Active.json cambió externamente; no se sobrescribe.");
            if(!EditorUtility.DisplayDialog("Aplicar al juego","Se reemplaza Active.json de "+Names[slot]+". Se conserva una copia puntual del anterior. No se publica a GitHub.","Aplicar","Cancelar"))return;
            SaveDraft();SessionState.EraseString("BIOFIT_EDITOR_TEST_PATH");SessionState.EraseInt("BIOFIT_EDITOR_TEST_SLOT");string s=File.ReadAllText(Draft);
            // Keep the publication baseline (Base.json) until explicitly exporting/reloading.
            File.WriteAllText(Path.Combine(Work,"PreviousActive.json"),File.ReadAllText(Active),new UTF8Encoding(false));
            Atomic(Active,s);activeHash=Hash(s);File.WriteAllText(KnownActive,activeHash);
            AssetDatabase.Refresh();status="Mapa aplicado. Abrí el menú original, Play, elegí "+Names[slot]+". Exportá diferencias antes de recargar.";
        }
        void PublishDelta()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            string dir=Path.Combine(Root,"tools","level_editor","changes");Directory.CreateDirectory(dir);
            string f=File.Exists(lastDelta)?lastDelta:EditorUtility.OpenFilePanel("Elegir delta exportado",dir,"json");if(string.IsNullOrEmpty(f))return;
            if(!EditorUtility.DisplayDialog("Publicar delta en la rama actual","Se verificará el delta y se publicará únicamente su JSON e índice documental en la rama autorizada actual (v6.5-desarrollo o main, según rama actual). Los recursos originales no se publican. ¿Continuar?","Publicar","Cancelar"))return;
            string Quote(string v)=>"\x27"+v.Replace("\x27","\x27\x27")+"\x27";
            string cmd="& py -3 "+Quote(Path.Combine(Root,"tools","level_editor","level_delta.py"))+" --proyecto "+Quote(Root)+" --delta "+Quote(f)+" --publicar";
            var start=new System.Diagnostics.ProcessStartInfo("powershell.exe","-NoExit -Command \""+cmd.Replace("\"","\\\"")+"\""){UseShellExecute=true};
            System.Diagnostics.Process.Start(start);status="Publicación iniciada en PowerShell. Confirmá OK DELTA PUBLICADO; no se da por publicada hasta entonces.";
        }
        void Export()
        {
            Validate();SaveDraft();
            var current=RawJson.Objects(RawJson.Fields(File.ReadAllText(Draft))["objects"]);
            var up=new List<string>();var edits=new List<string>();var removed=new List<int>();var now=new HashSet<int>();
            for(int i=0;i<data.objects.Length;i++)
            {
                int id=data.objects[i].index;now.Add(id);
                if(!baseRaw.TryGetValue(id,out var old)){up.Add(current[i]);continue;}
                if(old==current[i])continue;
                var oldFields=RawJson.Fields(old);var changes=new Dictionary<string,string>();
                foreach(var p in RawJson.Fields(current[i]))if(p.Key!="raw"&&(!oldFields.TryGetValue(p.Key,out var b)||b!=p.Value))changes[p.Key]=p.Value;
                if(changes.Count>0)edits.Add("{\"index\":"+id+",\"fields\":"+RawJson.Join(changes)+"}");
            }
            foreach(int id in baseRaw.Keys)if(!now.Contains(id))removed.Add(id);
            var header=new Dictionary<string,string>{{"schema","1"},{"slot",(slot+1).ToString()},{"baseSha256","\""+Hash(baseText)+"\""},{"targetSha256","\""+Hash(File.ReadAllText(Draft))+"\""},{"editorRevision","1"},{"endGD",RawJson.Fields(File.ReadAllText(Draft))["endGD"]},{"removed","["+string.Join(",",removed)+"]"},{"added","["+string.Join(",",up)+"]"},{"edits","["+string.Join(",",edits)+"]"},{"order","["+string.Join(",",data.objects.Select(x=>x.index))+"]"}};
            string exportDir=Path.Combine(Root,"tools","level_editor","changes");Directory.CreateDirectory(exportDir);
            string f=EditorUtility.SaveFilePanel("Exportar cambios (solo diferencia)",exportDir,"BIOFIT_level_"+(slot+1)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"),"json");
            if(string.IsNullOrEmpty(f))return;
            Atomic(f,RawJson.Join(header));lastDelta=f;status="Cambios exportados: "+f+" | "+(up.Count+edits.Count)+" registros nuevos/editados, "+removed.Count+" eliminados.";
        }
    }
    // Minimal raw JSON reader: keeps unknown source fields (including 'raw')
    // byte-for-byte at value level instead of losing them through JsonUtility.
    internal static class RawJson
    {
        static void WS(string s,ref int i){while(i<s.Length&&char.IsWhiteSpace(s[i]))i++;}
        static void StringEnd(string s,ref int i)
        {
            if(s[i++]!='"')throw new FormatException();
            while(i<s.Length){char c=s[i++];if(c=='\\'){i++;continue;}if(c=='"')return;}throw new FormatException("String sin cerrar");
        }
        static void ValueEnd(string s,ref int i)
        {
            WS(s,ref i);if(i>=s.Length)throw new FormatException();
            if(s[i]=='"'){StringEnd(s,ref i);return;}
            if(s[i]=='{'||s[i]=='[')
            {
                char end=s[i++]=='{'?'}':']';WS(s,ref i);
                while(i<s.Length&&s[i]!=end){if(s[i]==','||s[i]==':'){i++;continue;}ValueEnd(s,ref i);WS(s,ref i);}
                if(i>=s.Length)throw new FormatException();i++;return;
            }
            int start=i;while(i<s.Length&&",]} \r\n\t".IndexOf(s[i])<0)i++;if(i==start)throw new FormatException();
        }
        public static Dictionary<string,string> Fields(string s)
        {
            int i=0;WS(s,ref i);if(s[i++]!='{')throw new FormatException();
            var d=new Dictionary<string,string>();WS(s,ref i);
            while(s[i]!='}')
            {
                int a=i;StringEnd(s,ref i);string key=s.Substring(a+1,i-a-2);WS(s,ref i);if(s[i++]!=':')throw new FormatException();WS(s,ref i);
                a=i;ValueEnd(s,ref i);d.Add(key,s.Substring(a,i-a));WS(s,ref i);
                if(s[i]==','){i++;WS(s,ref i);}else if(s[i]!='}')throw new FormatException();
            }
            return d;
        }
        public static List<string> Objects(string s)
        {
            int i=0;WS(s,ref i);if(s[i++]!='[')throw new FormatException();var a=new List<string>();WS(s,ref i);
            while(s[i]!=']'){int start=i;ValueEnd(s,ref i);a.Add(s.Substring(start,i-start));WS(s,ref i);if(s[i]==','){i++;WS(s,ref i);}else if(s[i]!=']')throw new FormatException();}
            return a;
        }
        public static string Join(Dictionary<string,string> d)=>"{"+string.Join(",",d.Select(p=>"\""+p.Key+"\":"+p.Value))+"}";
    }
}
#endif
