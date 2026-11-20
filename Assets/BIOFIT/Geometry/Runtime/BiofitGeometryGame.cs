using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Biofit.Geometry
{
    [RequireComponent(typeof(BiofitBLEMonitor))]
    public sealed class BiofitGeometryGame : MonoBehaviour
    {
        public enum PlayState { Menu, Playing, Paused, Dead, Won, Error }
        public PlayState State { get; private set; } = PlayState.Menu;
        public GeometryCube Player => world == null ? null : world.Player;
        public GeometryLevel CurrentLevel => selected < 0 ? null : levels[selected];
        private BiofitBLEMonitor monitor;
        private GeometryLevel[] levels;
        private GeometryWorld world;
        private Camera gameCamera;
        private Sprite square, triangle, circle;
        private PhysicsMaterial2D frictionless;
        private readonly GeometryJumpCommands commands = new GeometryJumpCommands();
        private int selected=-1, monitorEpoch, candidates, accepted, refused, attempt;
        private long nextCommand;
        private double acceptAfter;
        private float oldTimeScale, oldFixedDelta;
        private bool initialized, ownsClock, showSession, showHistory, showCredits;
        private int historyPage;
        private Vector2 creditsScroll;
        private string notice="Elegí un nivel. ESPACIO, toque en pista o botón SALTAR.", lastAction="Sin entradas";
        private GUIStyle title, label, small, button, big;
        private static double Now => Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        private static string F(double v) => v.ToString("F6",CultureInfo.InvariantCulture);
        private static readonly Color[] Themes = { new Color(.25f,.92f,.86f),new Color(.43f,.68f,1),new Color(.77f,.55f,1),new Color(1,.65f,.3f) };

        private void Awake()
        {
            monitor=GetComponent<BiofitBLEMonitor>();
            // The dedicated scene contains exactly one monitor. Fail closed if
            // someone loads it additively beside another active BLE service.
            foreach(var other in FindObjectsByType<BiofitBLEMonitor>(FindObjectsSortMode.None))
                if(other!=monitor && other.enabled) { monitor.enabled=false; Fail("Ya existe otro receptor BLE. Abrí esta escena sola.");return; }
            foreach(var dash in FindObjectsByType<BiofitDashGame>(FindObjectsSortMode.None)) dash.enabled=false;
            try
            {
                levels=new GeometryLevel[4];
                for(int i=0;i<4;i++)
                {
                    var asset=Resources.Load<TextAsset>("BiofitGeometry/Levels/"+GeometryLevel.ResourceNames[i]);
                    if(asset==null)throw new InvalidOperationException("Falta nivel "+GeometryLevel.ResourceNames[i]);
                    levels[i]=JsonUtility.FromJson<GeometryLevel>(asset.text);levels[i].Validate(i+1);
                }
                square=GeometryWorld.CreateSprite(0);triangle=GeometryWorld.CreateSprite(1);circle=GeometryWorld.CreateSprite(2);
                frictionless=new PhysicsMaterial2D("BIOFIT frictionless"){friction=0,bounciness=0};
                gameCamera=GetComponentInChildren<Camera>();
                if(gameCamera==null)
                {
                    var obj=new GameObject("BIOFIT Geometry Camera");obj.transform.SetParent(transform,false);gameCamera=obj.AddComponent<Camera>();
                }
                gameCamera.orthographic=true;gameCamera.orthographicSize=5.5f;
                gameCamera.clearFlags=CameraClearFlags.SolidColor;gameCamera.backgroundColor=new Color(.025f,.055f,.095f);
                gameCamera.transform.position=new Vector3(0,2.8f,-10);
                oldTimeScale=Time.timeScale;oldFixedDelta=Time.fixedDeltaTime;ownsClock=true;
                Time.timeScale=1;Time.fixedDeltaTime=GeometryPhysics.FixedStep;
#if UNITY_ANDROID && !UNITY_EDITOR
                Screen.orientation=ScreenOrientation.LandscapeLeft;
#endif
                initialized=true;
            }
            catch(Exception ex) { Fail(ex.Message); }
        }
        private void OnEnable()
        {
            if(!initialized)return;
            monitor.PhaseFastPairs=false;monitor.LiftCycleMode=false;monitor.ShowDiagnostics=false;
            monitor.LegacyBilateralCandidateTimed+=OnLegacyPair;
            monitor.DiagnosticReturnLabel="VOLVER A BIOFIT GAME — GEOMETRY";
            monitor.DiagnosticReturnRequested+=CloseDiagnostics;
            monitorEpoch=monitor.ControlEpoch;
            CancelInputs("ENABLE");
        }
        private void OnDisable()
        {
            if(monitor!=null)
            {
                monitor.LegacyBilateralCandidateTimed-=OnLegacyPair;
                monitor.DiagnosticReturnRequested-=CloseDiagnostics;
            }
            if(Player!=null)Player.SetRunning(false);
            commands.Cancel();
        }
        private void OnDestroy()
        {
            if(ownsClock) { Time.timeScale=oldTimeScale;Time.fixedDeltaTime=oldFixedDelta; }
            foreach(var sprite in new[]{square,triangle,circle}) if(sprite!=null){Destroy(sprite.texture);Destroy(sprite);}
            if(frictionless!=null)Destroy(frictionless);
        }
        private void OnApplicationPause(bool paused) { if(paused)Pause(); }
        private void Fail(string reason) { State=PlayState.Error;notice=reason;UnityEngine.Debug.LogError(reason); }
        private void Log(string detail) { if(monitor!=null)monitor.RecordControlDiagnostic("GEOMETRY level="+(selected+1)+" attempt="+attempt+" "+detail); }
        private void CancelInputs(string reason)
        {
            commands.Cancel();acceptAfter=Now;
            if(monitor!=null)monitor.JumpDetector.ClearPending();
            Log("INPUT_CANCEL reason="+reason+" epoch="+commands.Epoch);
        }
        private void CheckTransport()
        {
            if(monitorEpoch!=monitor.ControlEpoch)
            {
                monitorEpoch=monitor.ControlEpoch;CancelInputs("BLE_EPOCH");
            }
        }
        private void OnLegacyPair(float gapMs,double pairedAt)
        {
            CheckTransport();candidates++;
            Log("BLE_CANDIDATE gapMs="+F(gapMs)+" pairedS="+F(pairedAt)+" receivedS="+F(Now));
            Submit("BLE_LEGACY",pairedAt,monitor.BothTrackersConnected && monitor.ClockAlignmentReady && !monitor.LiftCycleMode && !monitor.PhaseFastPairs);
        }
        public JumpOutcome Submit(string source,double occurredAt,bool available=true)
        {
            double now=Now;long id=++nextCommand;
            Log("COMMAND_CREATED id="+id+" source="+source+" originS="+F(occurredAt));
            var result=commands.Submit(id,commands.Epoch,occurredAt,now,
                available && State==PlayState.Playing && !monitor.ShowDiagnostics && occurredAt>=acceptAfter,Player);
            if(result==JumpOutcome.Executed || result==JumpOutcome.ExecutedOrb)accepted++;else refused++;
            lastAction=source+" · "+result+" · #"+id;
            Log("COMMAND_RESULT id="+id+" source="+source+" result="+result+" ageMs="+F((now-occurredAt)*1000));
            return result;
        }
        public void LaunchLevel(int index)
        {
            if(!initialized || index<0 || index>=levels.Length)return;
            CancelInputs("LEVEL_START");
            if(world!=null && world.Root!=null){world.Root.SetActive(false);Destroy(world.Root);}
            selected=index;attempt++;candidates=accepted=refused=0;
            world=new GeometryWorld(square,triangle,circle,frictionless);world.Build(levels[index],transform,Themes[index]);
            Player.Died+=OnDeath;Player.AutomaticAction+=action=>Log("AUTO_"+action);
            State=PlayState.Playing;showSession=false;showHistory=false;showCredits=false;monitor.ShowDiagnostics=false;
            Player.SetRunning(true);acceptAfter=Now;
            Log("LEVEL_START title="+levels[index].title+" CONTROL=LEGACY_ONLY minUpG="+F(monitor.MinUpG)+" pairMs="+F(monitor.PairWindowMs)+" cooldownMs=900 timeScale=1");
            notice="Saltá en las marcas amarillas. Cada toque es una orden; no mantener pulsado.";
        }
        private void OnDeath() { State=PlayState.Dead;CancelInputs("DEATH");Log("DEATH x="+F(Player.Body.position.x)); }
        public void Pause()
        {
            if(State!=PlayState.Playing)return;
            State=PlayState.Paused;Player.SetRunning(false);CancelInputs("PAUSE");Log("PAUSE");
        }
        public void Resume()
        {
            if(State!=PlayState.Paused)return;
            CancelInputs("RESUME");State=PlayState.Playing;Player.SetRunning(true);Log("RESUME");
        }
        public void ShowMenu()
        {
            if(Player!=null)Player.SetRunning(false);
            CancelInputs("MENU");State=PlayState.Menu;showSession=false;Log("MENU");
            if(world!=null && world.Root!=null)world.Root.SetActive(false);
        }
        private void OpenDiagnostics() { Pause();CancelInputs("DIAGNOSTICS");monitor.ShowDiagnostics=true; }
        private void CloseDiagnostics() { monitor.ShowDiagnostics=false;CancelInputs("DIAGNOSTICS_RETURN"); }
        private void Update()
        {
            if(!initialized)return;
            CheckTransport();
            if(monitor.ShowDiagnostics || showSession || showCredits)return;
            var keys=Keyboard.current;
            if(keys!=null)
            {
                if(keys.escapeKey.wasPressedThisFrame || keys.pKey.wasPressedThisFrame) { if(State==PlayState.Paused)Resume();else Pause(); }
                if(keys.rKey.wasPressedThisFrame && selected>=0)LaunchLevel(selected);
                if(keys.spaceKey.wasPressedThisFrame && State==PlayState.Playing)Submit("TECLADO",Now);
            }
            if(State==PlayState.Playing)
            {
                bool touch=Touchscreen.current!=null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
                var mouse=Mouse.current;
                if(touch)HandleTap(Touchscreen.current.primaryTouch.position.ReadValue());
                else if(mouse!=null && mouse.leftButton.wasPressedThisFrame)HandleTap(mouse.position.ReadValue());
                if(Player.Body.position.x>=CurrentLevel.finishX)
                {
                    State=PlayState.Won;Player.SetRunning(false);CancelInputs("VICTORY");Log("VICTORY");
                }
            }
        }
        private void HandleTap(Vector2 point)
        {
            Rect safe=Screen.safeArea;float scale=Mathf.Min(safe.width/1280f,safe.height/720f);
            float x=(point.x-safe.x-(safe.width-1280*scale)/2)/scale;
            float y=(safe.y+safe.height-point.y-(safe.height-720*scale)/2)/scale;
            if(!showSession && x>=0 && x<=1280 && y>130 && y<590)Submit("TOQUE_PISTA",Now);
        }
        private void LateUpdate()
        {
            if(Player!=null && gameCamera!=null && State!=PlayState.Menu)
                gameCamera.transform.position=new Vector3(Player.transform.position.x+4,2.8f,-10);
        }
        private void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=38,fontStyle=FontStyle.Bold};
            label=new GUIStyle(GUI.skin.label){fontSize=23,wordWrap=true};
            small=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};
            big=new GUIStyle(title){fontSize=48,alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(GUI.skin.button){fontSize=22,wordWrap=true};
        }
        private static void Fill(Rect r,Color c) { Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old; }
        private bool Button(float x,float y,float w,float h,string text) => GUI.Button(new Rect(x,y,w,h),text,button);
        private void OnGUI()
        {
            if(monitor!=null && monitor.ShowDiagnostics)return;
            Styles();var previous=GUI.matrix;
            Rect safe=Screen.safeArea;float scale=Mathf.Min(safe.width/1280f,safe.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x+(safe.width-1280*scale)/2,Screen.height-safe.y-safe.height+(safe.height-720*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            try { DrawInterface(); } finally { GUI.matrix=previous; }
        }
        private void DrawInterface()
        {
            Fill(new Rect(0,0,1280,106),new Color(.035f,.07f,.12f,.98f));
            GUI.Label(new Rect(30,14,780,52),"BIOFIT GAME — GEOMETRY",title);
            int connected=monitor==null?0:monitor.ConnectedTrackerCount;
            GUI.Label(new Rect(31,65,1200,35),"BLE: BILATERAL LEGACY  ·  Sensores "+connected+"/2  ·  "+(monitor!=null && monitor.ClockAlignmentReady?"Relojes listos":"Esperando sensores / control manual disponible"),small);
            if(State==PlayState.Error){GUI.Label(new Rect(50,160,1180,170),notice,label);return;}
            // Modal panels must consume the view before underlying level or
            // gameplay buttons are drawn; clicks cannot fall through.
            if(showSession){DrawSession();return;}
            if(showCredits){DrawCredits();return;}
            if(State==PlayState.Menu)
            {
                GUI.Label(new Rect(34,123,1200,48),"CUATRO RECORRIDOS · elegí dónde empezar",label);
                for(int i=0;i<4;i++)
                {
                    float x=32+(i%2)*620,y=184+(i/2)*150;
                    Fill(new Rect(x,y,594,128),new Color(.07f,.13f,.21f));Fill(new Rect(x,y,6,128),Themes[i]);
                    GUI.Label(new Rect(x+22,y+13,550,43),(i+1)+"  "+levels[i].title,label);
                    GUI.Label(new Rect(x+22,y+59,340,49),levels[i].sections.Length+" tramos · cubo · adaptación BIOFIT",small);
                    if(Button(x+414,y+62,157,49,"JUGAR"))LaunchLevel(i);
                }
                GUI.Label(new Rect(34,495,1200,70),"ESPACIO, botón SALTAR o toque en la pista. Los sensores usan el mismo comando.\nGráficos propios; recorridos adaptados de GeomeTry. Sin control CICLO EXP.",small);
                if(Button(30,625,210,65,"CRÉDITOS"))showCredits=!showCredits;
            }
            else
            {
                float progress=Player==null?0:Mathf.Clamp01((Player.Body.position.x-GeometryPhysics.SpawnX)/(CurrentLevel.finishX-GeometryPhysics.SpawnX));
                Fill(new Rect(30,110,1220,6),new Color(.11f,.18f,.25f));Fill(new Rect(30,110,1220*progress,6),Themes[selected]);
                GUI.Label(new Rect(30,124,1200,36),CurrentLevel.title+"    "+(progress*100).ToString("F0")+" %    ·    "+lastAction,small);
                if(State==PlayState.Playing)
                {
                    if(Button(30,625,250,65,"SALTAR"))Submit("BOTON",Now);
                    if(Button(294,625,180,65,"PAUSA"))Pause();
                }
                else
                {
                    Fill(new Rect(300,230,680,272),new Color(.025f,.055f,.10f,.97f));
                    GUI.Label(new Rect(320,242,640,80),State==PlayState.Won?"¡RECORRIDO COMPLETO!":State==PlayState.Dead?"VOLVÉ A INTENTAR":"PAUSA",big);
                    GUI.Label(new Rect(340,332,600,62),"Pares BLE: "+candidates+"  ·  Órdenes ejecutadas: "+accepted+"\nRechazadas: "+refused+" (incluye controles manuales)",small);
                    if(State==PlayState.Paused && Button(330,420,190,60,"CONTINUAR"))Resume();
                    else if(State==PlayState.Won && selected<3 && Button(330,420,190,60,"SIGUIENTE"))LaunchLevel(selected+1);
                    if(Button(535,420,190,60,"REINTENTAR"))LaunchLevel(selected);
                    if(Button(740,420,190,60,"NIVELES"))ShowMenu();
                }
                if(Button(490,625,180,65,"REINICIAR"))LaunchLevel(selected);
                if(Button(686,625,180,65,"NIVELES"))ShowMenu();
            }
            if(Button(900,625,165,65,"REGISTRO")) { Pause();showSession=!showSession; }
            if(Button(1080,625,170,65,"BLE / DIAG"))OpenDiagnostics();
            GUI.Label(new Rect(32,578,1218,36),monitor.CaptureBanner,small);
        }
        private void DrawCredits()
        {
            Fill(new Rect(26,145,1228,420),new Color(.035f,.07f,.12f,.99f));
            var credits=Resources.Load<TextAsset>("BiofitGeometry/NOTICE");
            creditsScroll=GUI.BeginScrollView(new Rect(45,160,1180,320),creditsScroll,new Rect(0,0,1130,800),false,true);
            GUI.Label(new Rect(0,0,1110,800),credits==null?"GeomeTry: MIT. Ver ThirdParty/GeomeTry/LICENSE.":credits.text,small);
            GUI.EndScrollView();
            if(Button(970,495,250,52,"CERRAR CRÉDITOS"))showCredits=false;
        }
        private void DrawSession()
        {
            if(showHistory){DrawHistory();return;}
            Fill(new Rect(26,154,1228,414),new Color(.035f,.07f,.12f,.99f));
            GUI.Label(new Rect(45,165,1050,46),"REGISTRO Y EXPORTACIÓN",label);
            if(Button(1110,166,120,42,"CERRAR")){showSession=false;return;}
            if(Button(45,222,215,52,"1. INICIAR") && monitor.StartGameCapture())
                Log("SESSION_START GAME_VERSION=GEOMETRY_1 CONTROL=LEGACY_ONLY thresholds=0.25/0.25/0.12 pairMs=180 noLandingBuffer=1");
            if(Button(275,222,215,52,"2. FINALIZAR"))monitor.StopGameCapture();
            if(Button(505,222,215,52,"3. ARCHIVOS"))monitor.RequestExport(false);
            if(Button(735,222,215,52,monitor.RawButtonLabel))monitor.ToggleRawCapture();
            if(Button(965,222,265,52,monitor.SyncLabel))monitor.MarkVideoSync();
            GUI.Label(new Rect(45,288,1180,42),monitor.RawStatus+"  ·  Muestras I/D: "+monitor.RawCount,small);
            GUI.Label(new Rect(45,328,1180,60),monitor.CaptureBanner+"\n"+monitor.ExportStatus,small);
            if(Button(45,403,260,52,"SESIONES ANTERIORES")){monitor.ScanSessionHistory();showHistory=!showHistory;}
            if(Button(325,403,245,52,"GUARDAR CON SAF"))monitor.RequestExport(true);
            GUI.Label(new Rect(45,480,1180,70),"INICIAR → RAW opcional → cerrar panel → JUGAR/CONTINUAR. Al terminar: FINALIZAR, esperar cierre y ARCHIVOS.\nEl mismo receptor y la misma captura continúan entre niveles. Se exportan principal y RAW si existe.",small);
        }
        private void DrawHistory()
        {
            Fill(new Rect(55,180,1170,370),new Color(.05f,.10f,.16f));
            GUI.Label(new Rect(75,192,970,47),monitor.ArchiveStatus,small);
            if(Button(1080,190,120,42,"VOLVER")){showHistory=false;return;}
            var sessions=monitor.StoredSessions;
            int pages=Math.Max(1,(sessions.Length+3)/4);historyPage=Math.Min(historyPage,pages-1);
            for(int row=0;row<4;row++)
            {
                int index=historyPage*4+row;if(index>=sessions.Length)break;
                if(Button(75,247+row*54,1125,48,(monitor.SelectedStoredSession==index?"✓ ":"")+sessions[index].Label))monitor.SelectStoredSession(index);
            }
            if(Button(75,475,170,52,"ANTERIORES"))historyPage=Math.Max(0,historyPage-1);
            if(Button(260,475,170,52,"SIGUIENTES"))historyPage=Math.Min(pages-1,historyPage+1);
            if(Button(670,475,250,52,"EXPORTAR SELECCIÓN"))monitor.RequestExport(false);
            if(Button(935,475,265,52,"EXPORTAR CON SAF"))monitor.RequestExport(true);
        }
    }
}
