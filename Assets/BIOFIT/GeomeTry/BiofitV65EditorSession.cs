#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Biofit.GeomeTryOriginal
{
    // Exists only in the Editor player. Uses actual loaded level, colliders and camera.
    public sealed class BiofitV65EditorSession : MonoBehaviour
    {
        const string Prefix="BIOFIT_V65_";
        static bool Active=>SessionState.GetBool(Prefix+"ACTIVE",false);
        public static bool IsPreview=>Active && SessionState.GetBool(Prefix+"PREVIEW",false);
        static BiofitV65EditorSession current;
        UnityEngine.Camera view;
        float floor,end,x,y,originalSize,speed=1;
        bool auto,hud=true,ready,spawned;
        int avatarId;
        readonly Dictionary<Canvas,bool> canvases=new Dictionary<Canvas,bool>();
        public static void Configure(int slot,string draft,float x,float y,int mode,bool invert,bool restart,bool preview)
        {
            SessionState.SetString("BIOFIT_EDITOR_TEST_PATH",draft);
            SessionState.SetInt("BIOFIT_EDITOR_TEST_SLOT",slot);
            SessionState.SetBool(Prefix+"ACTIVE",true);
            SessionState.SetBool(Prefix+"LAUNCH",true);
            SessionState.SetBool(Prefix+"PREVIEW",preview);
            SessionState.SetFloat(Prefix+"X",x);SessionState.SetFloat(Prefix+"Y",y);
            SessionState.SetInt(Prefix+"MODE",mode);SessionState.SetBool(Prefix+"INVERT",invert);
            SessionState.SetBool(Prefix+"RESTART",restart);
        }
        public static bool TryTakeLaunch(out int slot)
        {
            slot=SessionState.GetInt("BIOFIT_EDITOR_TEST_SLOT",0);
            if(!Active||!SessionState.GetBool(Prefix+"LAUNCH",false)||slot<1||slot>5)return false;
            SessionState.SetBool(Prefix+"LAUNCH",false);return true;
        }
        public static void Clear()
        {
            bool wasActive=Active;
            SessionState.SetBool(Prefix+"ACTIVE",false);SessionState.SetBool(Prefix+"LAUNCH",false);
            SessionState.SetBool(Prefix+"PREVIEW",false);
            if(!wasActive)return;
            string previous=SessionState.GetString(Prefix+"PREV_START_SCENE","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.EraseString(Prefix+"PREV_START_SCENE");
        }
        public static void Tick(BiofitStereoLocalRuntime host,UnityEngine.Camera camera,float floor,float end)
        {
            if(!Active)return;
            if(current==null||current.gameObject!=host.gameObject)
            {
                current=host.gameObject.AddComponent<BiofitV65EditorSession>();
                current.view=camera;current.floor=floor;current.end=end;current.originalSize=camera.orthographicSize;
                current.x=Mathf.Clamp(Character.InitialPosition+SessionState.GetFloat(Prefix+"X",0)/30f,Character.InitialPosition,end);
                current.y=floor+SessionState.GetFloat(Prefix+"Y",15)/30f;
            }
            current.Step();
        }
        void Step()
        {
            if(view==null||GameManager.Instance==null)return;
            if(IsPreview)
            {
                if(!ready)
                {
                    // Do not simulate gameplay or BLE in free-camera inspection.
                    GameManager.Instance.PlayMode=false;
                    var p=GameObject.FindGameObjectWithTag("Player");
                    if(p!=null)p.SetActive(false);
                    // The upstream camera follows in FixedUpdate. Pausing the
                    // simulation stops that loop; no dependency on its script type.
                    Time.timeScale=0;
                    y=Mathf.Max(floor+originalSize*.65f,y);
                    Time.timeScale=0;AudioListener.pause=true;ready=true;
                }
                float dt=Time.unscaledDeltaTime;
                if(auto)x+=9.75f*speed*dt;
                if(Input.GetKey(KeyCode.LeftArrow))x-=12*dt;
                if(Input.GetKey(KeyCode.RightArrow))x+=12*dt;
                if(Input.GetKey(KeyCode.UpArrow))y+=8*dt;
                if(Input.GetKey(KeyCode.DownArrow))y-=8*dt;
                if(Mathf.Abs(Input.mouseScrollDelta.y)>.01f)view.orthographicSize=Mathf.Clamp(view.orthographicSize-Input.mouseScrollDelta.y*.5f,1,30);
                x=Mathf.Clamp(x,Character.InitialPosition,end);
                view.transform.position=new Vector3(x,y,view.transform.position.z);
                foreach(var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                {
                    if(!canvases.ContainsKey(c))canvases[c]=c.enabled;
                    c.enabled=hud&&canvases[c];
                }
                return;
            }
            var player=GameObject.FindGameObjectWithTag("Player");
            if(player==null)return;
            var cube=player.GetComponent<Cube>();var motor=player.GetComponent<BiofitModeMotor>();var rb=player.GetComponent<Rigidbody2D>();
            if(cube==null||motor==null||rb==null||!cube.isAlive)return;
            if(avatarId==player.GetInstanceID())return;
            avatarId=player.GetInstanceID();
            if(spawned&&!SessionState.GetBool(Prefix+"RESTART",true))return;
            int mode=SessionState.GetInt(Prefix+"MODE",0);bool invert=SessionState.GetBool(Prefix+"INVERT",false);
            motor.SetMode(mode==1?BiofitMode.Ball:mode==2?BiofitMode.Spider:invert?BiofitMode.InvertedCube:BiofitMode.Cube);
            if(mode==4){motor.SetMini(true);motor.SetCubeGravity(invert);}
            else if(mode!=0)motor.RestoreStereoGravity(invert);
            rb.position=new Vector2(x,y);player.transform.position=new Vector3(x,y,player.transform.position.z);rb.linearVelocity=Vector2.zero;
            Physics2D.SyncTransforms();
            view.transform.position=new Vector3(x+5,Mathf.Max(4,y),view.transform.position.z);
            GameManager.Instance.PlayMode=true;spawned=true;
            Debug.Log("BIOFIT V6.5 prueba: X="+x+" Y="+y+" modo="+motor.Mode+" (borrador, no Active)");
        }
        void OnGUI()
        {
            if(!Active)return;
            if(!IsPreview)
            {
                // Compact, editor-only controls; no panel covering the playfield.
                const float margin=8f,gap=5f,height=24f;
                float width=Mathf.Min(150f,(Screen.width-2*margin-gap)*.5f);
                if(width<=0)return;
                float right=Screen.width-margin, bottom=Screen.height-margin-height;
                var restart=new Rect(right-width,bottom,width,height);
                var back=new Rect(restart.x-gap-width,bottom,width,height);
                if(GUI.Button(back,"Volver al editor"))EditorApplication.isPlaying=false;
                if(GUI.Button(restart,"Reiniciar desde aquí"))SceneManager.LoadScene("Level");
                return;
            }
            GUILayout.BeginArea(new Rect(12,Screen.height-92,Mathf.Max(300,Screen.width-24),80),GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("VISTA REAL • sin física");
            if(GUILayout.Button("Volver al editor",GUILayout.Width(145)))EditorApplication.isPlaying=false;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            float pct=100*(x-Character.InitialPosition)/Mathf.Max(1,end-Character.InitialPosition);
            GUILayout.Label(pct.ToString("F1")+"%",GUILayout.Width(55));
            float next=GUILayout.HorizontalSlider(pct,0,100);
            if(!Mathf.Approximately(next,pct))x=Mathf.Lerp(Character.InitialPosition,end,next/100);
            auto=GUILayout.Toggle(auto,"Recorrer",GUILayout.Width(80));
            hud=GUILayout.Toggle(hud,"HUD",GUILayout.Width(55));
            if(GUILayout.Button(speed.ToString("0.##")+"×",GUILayout.Width(55)))speed=speed==.25f?.5f:speed==.5f?1:speed==1?2:.25f;
            if(GUILayout.Button("Cámara original",GUILayout.Width(120)))view.orthographicSize=originalSize;
            GUILayout.EndHorizontal();
            GUILayout.Label("Flechas: mover • rueda: zoom • vista del motor real, colores según X");
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            if(current==this)current=null;
            if(ready){Time.timeScale=1.5f;AudioListener.pause=false;foreach(var p in canvases)if(p.Key!=null)p.Key.enabled=p.Value;}
        }
    }
}
#endif
