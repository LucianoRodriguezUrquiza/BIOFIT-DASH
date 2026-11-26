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
        static readonly string[] Kinds={"solid","hazard","decoration","cubePortal","ballPortal","spiderPortal","gravityUp","gravityDown","pad","pinkPad","gravityPad","orb","coin","backgroundColor","groundColor","enterEffect"};
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
        float maxLength;
        Rect canvas, marquee;
        Vector2 lastWorld=new Vector2(300,15);
        string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string MapDir=>Path.Combine(Application.dataPath,"BIOFIT/Resources/BiofitStereoLocal/CampaignV5",(slot+1).ToString());
        string Active=>Path.Combine(MapDir,"Active.json");
        string Work=>Path.Combine(Root,"Builds","BiofitLevelEditor",(slot+1).ToString());
        string Draft=>Path.Combine(Work,"Draft.json");
        string Baseline=>Path.Combine(Work,"Base.json");
        static string Hash(string s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
        static Item Clone(Item i)=>JsonUtility.FromJson<Item>(JsonUtility.ToJson(i));
        static bool IsInteraction(Item i)=>i.kind.EndsWith("Portal")||i.kind=="gravityUp"||i.kind=="gravityDown"||i.kind=="pad"||i.kind=="pinkPad"||i.kind=="gravityPad"||i.kind=="orb"||i.kind=="coin";
        static bool IsTrigger(Item i)=>i.kind=="backgroundColor"||i.kind=="groundColor"||i.kind=="enterEffect";
        [MenuItem("BIOFIT/Level Editor/Abrir editor")]
        static void Open(){var w=GetWindow<BiofitLevelEditor>("BIOFIT Level Editor");w.minSize=new Vector2(870,510);w.Show();}
        void OnEnable(){wantsMouseMove=true;Undo.undoRedoPerformed+=Repaint;}
        void OnDisable(){Undo.undoRedoPerformed-=Repaint; if(data!=null&&dirty){try{SaveDraft();}catch(Exception ex){Debug.LogError(ex);}}}
        void Guard(Action a){try{a();}catch(Exception ex){status="ERROR: "+ex.Message;Debug.LogException(ex);}Repaint();}
        void Load(int n,bool resume)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            if(data!=null&&dirty)SaveDraft();
            slot=n;requestedSlot=n;
            if(!File.Exists(Active))throw new Exception("Falta "+Active);
            Directory.CreateDirectory(Work);
            string active=File.ReadAllText(Active);
            activeHash=Hash(active);
            baseText=active;
            string loaded=active;
            if(resume&&File.Exists(Draft)&&File.Exists(Baseline))
            {
                baseText=File.ReadAllText(Baseline);
                loaded=File.ReadAllText(Draft);
                if(Hash(baseText)!=activeHash && Hash(loaded)!=activeHash)throw new Exception("El mapa activo cambió desde el borrador. Exportá/resguardá el borrador antes de cargar sin él.");
            }
            else {File.WriteAllText(Baseline,baseText,new UTF8Encoding(false));}
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
            catalog.Clear();
            var seen=new HashSet<string>();
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
            catalog.Sort((a,b)=>{int x=Array.IndexOf(Kinds,a.kind).CompareTo(Array.IndexOf(Kinds,b.kind));return x!=0?x:a.id.CompareTo(b.id);});
        }
        Texture2D Tex(string name)
        {
            if(string.IsNullOrEmpty(name))return null;
            if(!textures.TryGetValue(name,out var t)){t=AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteDir+name+".png");textures[name]=t;}
            return t;
        }
        void Push(){undo.Push(JsonUtility.ToJson(data));redo.Clear();dirty=true;}
        void History(bool back)
        {
            var from=back?undo:redo;var to=back?redo:undo;if(from.Count==0)return;
            to.Push(JsonUtility.ToJson(data));data=Parse(from.Pop());selection.RemoveWhere(i=>!data.objects.Any(o=>o.index==i));dirty=true;
        }
        Item[] Selected()=>data.objects.Where(o=>selection.Contains(o.index)).ToArray();
        float Snap(float v)=>snap?Mathf.Round(v/grid)*grid:v;
        Vector2 Screen(Vector2 w)=>new Vector2(canvas.x+(w.x-pan.x)*zoom,canvas.y+(pan.y-w.y)*zoom);
        Vector2 World(Vector2 p)=>new Vector2(pan.x+(p.x-canvas.x)/zoom,pan.y-(p.y-canvas.y)/zoom);
        Rect ObjectRect(Item o)
        {
            var p=Screen(new Vector2(o.xGD,o.yGD));float w=o.widthGD,h=o.heightGD;
            var t=o.frames!=null&&o.frames.Length>0?Tex(o.frames[0]):null;
            if(t!=null){w=Mathf.Max(w,t.width*.5f);h=Mathf.Max(h,t.height*.5f);}
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
            Push();var o=Clone(template);o.index=NextId();o.xGD=Snap(pos.x);o.yGD=Snap(pos.y);
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
            EditorGUILayout.LabelField("BIOFIT LEVEL EDITOR • V6.4 experimental",EditorStyles.boldLabel);
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
                    GUILayout.Label("Zoom",GUILayout.Width(35));zoom=GUILayout.HorizontalSlider(zoom,.035f,4f,GUILayout.Width(140));
                    if(GUILayout.Button("Ver todo",GUILayout.Width(75))){zoom=Mathf.Clamp((position.width-300)/Mathf.Max(30,data.endGD),.035f,4);pan=new Vector2(0,400);}
                    GUILayout.Label("X",GUILayout.Width(15));pan.x=EditorGUILayout.FloatField(pan.x,GUILayout.Width(85));
                    GUILayout.Label("Y",GUILayout.Width(15));pan.y=EditorGUILayout.FloatField(pan.y,GUILayout.Width(70));
                    GUILayout.Label("Ir a %",GUILayout.Width(45));float pct=EditorGUILayout.Slider(Mathf.Clamp01(pan.x/data.endGD)*100,0,100);if(GUI.changed&&Mathf.Abs(pct-Mathf.Clamp01(pan.x/data.endGD)*100)>.001f)pan.x=pct*data.endGD/100;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(data.objects.Length+" objetos | "+selection.Count+" seleccionados");
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    using(new EditorGUILayout.VerticalScope(GUILayout.Width(225)))DrawCatalog();
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
        void DrawCatalog()
        {
            GUILayout.Label("CATÁLOGO LOCAL",EditorStyles.boldLabel);
            query=EditorGUILayout.TextField("Buscar",query);
            paletteScroll=EditorGUILayout.BeginScrollView(paletteScroll);
            for(int i=0;i<catalog.Count;i++)
            {
                var o=catalog[i];string label=o.kind+" #"+o.id+" "+string.Join(" ",o.frames??Array.Empty<string>());
                if(!label.ToLowerInvariant().Contains(query.ToLowerInvariant()))continue;
                using(new EditorGUILayout.HorizontalScope())
                {
                    var t=o.frames!=null&&o.frames.Length>0?Tex(o.frames[0]):null;
                    GUILayout.Label(t,GUILayout.Width(30),GUILayout.Height(30));
                    if(GUILayout.Toggle(paletteIndex==i,label,EditorStyles.miniButton,GUILayout.Height(30))&&paletteIndex!=i){paletteIndex=i;placing=true;}
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
                    Rect sr=new Rect(r.center.x-t.width*zoom*.25f,r.center.y-t.height*zoom*.25f,t.width*zoom*.5f,t.height*zoom*.5f);
                    GUI.DrawTextureWithTexCoords(sr,t,new Rect(o.flipX?1:0,o.flipY?1:0,o.flipX?-1:1,o.flipY?-1:1));painted=true;
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
            if(e.type==EventType.ScrollWheel&&inside){Vector2 anchor=World(e.mousePosition);zoom=Mathf.Clamp(zoom*Mathf.Pow(1.12f,-e.delta.y),.035f,4);Vector2 after=World(e.mousePosition);pan+=anchor-after;e.Use();Repaint();return;}
            if(e.type==EventType.MouseDown&&inside)
            {
                GUI.FocusControl(null);press=e.mousePosition;panStart=pan;
                if(e.button==1||e.button==2){panning=true;e.Use();return;}
                if(e.button!=0)return;
                if(placing&&catalog.Count>0){Add(catalog[Mathf.Clamp(paletteIndex,0,catalog.Count-1)],lastWorld);e.Use();Repaint();return;}
                var hit=data.objects.Where(o=>(showTriggers||!IsTrigger(o))&&Hit(o,e.mousePosition)).OrderByDescending(o=>o.layer*100+o.order).ThenByDescending(o=>o.index).FirstOrDefault();
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
            else if(e.type==EventType.KeyDown&&inside&&GUI.GetNameOfFocusedControl()=="")
            {
                bool used=true;
                if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace)Delete();
                else if(e.keyCode==KeyCode.Escape){placing=false;selection.Clear();}
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
            GUILayout.Space(8);GUILayout.Label("NIVEL",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Monedas",data.objects.Count(o=>o.kind=="coin")+" / 3");
            EditorGUILayout.LabelField("Portales",data.objects.Count(o=>o.kind.EndsWith("Portal")).ToString());
            float length=EditorGUILayout.DelayedFloatField("Longitud GD (máx. original)",data.endGD);
            if(length!=data.endGD){Push();data.endGD=Mathf.Clamp(length,300,maxLength);}
            if(GUILayout.Button("Restaurar último Active"))Guard(RestorePrevious);
            if(GUILayout.Button("Importar PNG propio local"))Guard(ImportPng);
            if(GUILayout.Button("Abrir carpeta de trabajo"))EditorUtility.RevealInFinder(Work);
            EditorGUILayout.HelpBox("Aplicar modifica solo Active.json. Para jugar, usá el menú normal y elegí la ranura. Original.json y Level.json permanecen intactos. Exportar genera diferencias, no publica automáticamente.",MessageType.Info);
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
                foreach(float f in new[]{o.xGD,o.yGD,o.rotationGD,o.widthGD,o.heightGD,o.portalMinGD,o.portalMaxGD,o.duration})if(float.IsNaN(f)||float.IsInfinity(f))throw new Exception("Número inválido #"+o.index);
                if((!baseTyped.TryGetValue(o.index,out var existing)||existing!=JsonUtility.ToJson(o)||data.endGD<previousLength) && (o.xGD<0||o.xGD>data.endGD))throw new Exception("Fuera del área del nivel #"+o.index);
                foreach(string f in o.frames)if(Tex(f)==null)throw new Exception("Sprite local no encontrado: "+f);
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
        void PrepareTest()
        {
            Validate();if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            SaveDraft();UnityEditor.SessionState.SetString("BIOFIT_EDITOR_TEST_PATH",Draft);UnityEditor.SessionState.SetInt("BIOFIT_EDITOR_TEST_SLOT",slot+1);
            status="Prueba preparada: "+Names[slot]+". Abrí Main Menu original, Play y seleccioná ese nivel. Active.json intacto.";
            EditorUtility.DisplayDialog("Prueba de borrador",status,"Entendido");
        }
        [InitializeOnLoadMethod] static void ClearTestAfterPlay()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode){SessionState.EraseString("BIOFIT_EDITOR_TEST_PATH");SessionState.EraseInt("BIOFIT_EDITOR_TEST_SLOT");}};
        }
        void RestorePrevious()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Detené Play Mode.");
            string p=Path.Combine(Work,"PreviousActive.json");if(!File.Exists(p))throw new Exception("No hay versión anterior guardada.");
            if(Hash(File.ReadAllText(Active))!=activeHash)throw new Exception("Active cambió externamente.");
            if(!EditorUtility.DisplayDialog("Restaurar","Restaurar únicamente el último Active anterior? El borrador permanece.","Restaurar","Cancelar"))return;
            Atomic(Active,File.ReadAllText(p));activeHash=Hash(File.ReadAllText(Active));AssetDatabase.Refresh();status="Active anterior restaurado. Borrador conservado.";
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
            SaveDraft();string s=File.ReadAllText(Draft);
            // Keep the publication baseline (Base.json) until explicitly exporting/reloading.
            File.WriteAllText(Path.Combine(Work,"PreviousActive.json"),File.ReadAllText(Active),new UTF8Encoding(false));
            Atomic(Active,s);activeHash=Hash(s);
            AssetDatabase.Refresh();status="Mapa aplicado. Abrí el menú original, Play, elegí "+Names[slot]+". Exportá diferencias antes de recargar.";
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
            string f=EditorUtility.SaveFilePanel("Exportar cambios (solo diferencia) ",Path.Combine(Root,"tools"),"BIOFIT_level_"+(slot+1)+"_changes","json");
            if(string.IsNullOrEmpty(f))return;
            Atomic(f,RawJson.Join(header));status="Cambios exportados: "+f+" | "+(up.Count+edits.Count)+" registros nuevos/editados, "+removed.Count+" eliminados.";
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
