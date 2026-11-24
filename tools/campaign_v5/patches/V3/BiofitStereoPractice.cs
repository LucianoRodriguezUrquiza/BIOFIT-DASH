using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biofit.GeomeTryOriginal
{
    [DefaultExecutionOrder(1300)]
    public sealed class BiofitStereoPractice : MonoBehaviour
    {
        private sealed class Checkpoint
        {
            public Vector2 position, velocity;
            public BiofitMode mode;
            public GameObject marker;
            public BiofitStereoCoinPickup[] picked;
        }
        private static BiofitStereoPractice current;
        private static bool requested;
        private readonly List<Checkpoint> checkpoints=new List<Checkpoint>();
        private Cube avatar;
        private int avatarFrame;
        private bool restorePending, sawDeath;
        private float lastAuto;
        private Sprite markerSprite;
        private AudioSource practiceMusic;
        public static bool Available => GameManager.Instance!=null && GameManager.Instance.SelectedLevel!=null &&
            GameManager.Instance.SelectedLevel.Id==1 && GameManager.Instance.SelectedLevel.Map.Count==1 &&
            GameManager.Instance.SelectedLevel.Map[0].Type==BiofitStereoLocalRuntime.Marker;
        public static bool Enabled => requested && Available;
        public static bool Auto {get;private set;}=true;
        public static int Count => current==null?0:current.checkpoints.Count;
        public static string Feedback => current!=null && Time.unscaledTime<current.feedbackUntil?current.feedback:"";
        private string feedback="";
        private float feedbackUntil;
        private void Report(string message) {feedback=message;feedbackUntil=Time.unscaledTime+1.5f;Debug.Log("BIOFIT V3 practica: "+message);}
        public static void ToggleMode() { if(Available) requested=!requested; }
        public static void ToggleAuto() { Auto=!Auto; }
        public static void Place() { if(current!=null) current.Save(false); }
        public static void Remove() { if(current!=null) current.RemoveLast(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            requested=false; current=null; Auto=true;
            SceneManager.sceneLoaded-=Loaded; SceneManager.sceneLoaded+=Loaded;
        }
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if(scene.name!="Level" || !Available) { requested=false; return; }
            if(!requested) return;
            new GameObject("BIOFIT_PRACTICE_V3").AddComponent<BiofitStereoPractice>();
        }
        private void Awake() { current=this; lastAuto=Time.unscaledTime; }
        private void Start()
        {
            var clip=Resources.Load<AudioClip>("BiofitStereoV2/PracticeMusic");
            if(clip==null) return;
            var game=FindFirstObjectByType<Game>();
            var original=game==null?null:game.GetComponent<AudioSource>();
            if(original!=null) original.mute=true;
            practiceMusic=gameObject.AddComponent<AudioSource>();practiceMusic.clip=clip;
            practiceMusic.loop=true;practiceMusic.spatialBlend=0;practiceMusic.Play();
        }
        private void OnDestroy()
        {
            if(current==this) current=null;
            if(markerSprite!=null) Destroy(markerSprite);
        }
        private void LateUpdate()
        {
            if(!Enabled) return;
            if(practiceMusic!=null && GameManager.Instance!=null && !GameManager.Instance.PlayMode && Time.timeScale>0f) practiceMusic.Stop();
            if(avatar!=null && !avatar.isAlive) sawDeath=true;
            var found=GameObject.FindGameObjectWithTag("Player");
            var next=found==null?null:found.GetComponent<Cube>();
            if(next!=null && next!=avatar)
            {
                restorePending=sawDeath && checkpoints.Count>0;
                sawDeath=false; avatar=next; avatarFrame=Time.frameCount;
            }
            if(restorePending && avatar!=null && avatar.isAlive && Time.frameCount>avatarFrame)
            {
                restorePending=false; Restore(checkpoints[checkpoints.Count-1]);
            }
            if(GameManager.Instance==null || !GameManager.Instance.PlayMode || Time.timeScale==0f) return;
            if(Input.GetKeyDown(KeyCode.Z)) Save(false);
            if(Input.GetKeyDown(KeyCode.X)) RemoveLast();
            if(Auto && Time.unscaledTime-lastAuto>3f) Save(true);
        }
        private void Save(bool automatic)
        {
            if(!Enabled || avatar==null || !avatar.isAlive || restorePending || Time.timeScale==0f ||
                GameManager.Instance==null || !GameManager.Instance.PlayMode) {if(!automatic && Enabled) Report("ESPERA AL PERSONAJE");return;}
            var rb=avatar.GetComponent<Rigidbody2D>(); var motor=avatar.GetComponent<BiofitModeMotor>();
            var col=avatar.GetComponent<Collider2D>();
            if(rb==null || motor==null || col==null) return;
            // Only AUTO requires a stable grounded Cube. A manual press may save
            // mid-jump or during Ship flight, with position and velocity restored.
            if(motor.Mode!=BiofitMode.Cube && motor.Mode!=BiofitMode.Ship) {if(!automatic) Report("MODO NO VALIDO");return;}
            if(automatic && (motor.Mode!=BiofitMode.Cube || Mathf.Abs(rb.linearVelocity.y)>.4f ||
                !Physics2D.Raycast(rb.position,Vector2.down,.60f,(1<<3)|(1<<6)))) return;
            if(rb.position.x<Character.InitialPosition+(automatic?1f:0f)) {if(!automatic) Report("ESPERA EL INICIO");return;}
            foreach(var h in Physics2D.OverlapBoxAll(col.bounds.center,col.bounds.size*.92f,0f))
                if(h!=col && (h.GetComponent<DeathZone>()!=null || h.GetComponent<BiofitModePortal>()!=null))
                {if(!automatic) Report("ZONA NO SEGURA");return;}
            if(checkpoints.Count>0 && Vector2.Distance(checkpoints[checkpoints.Count-1].position,rb.position)<(automatic?1f:.15f))
            {if(!automatic) Report("CHECKPOINT YA COLOCADO");return;}
            var picked=new List<BiofitStereoCoinPickup>();
            foreach(var coin in FindObjectsByType<BiofitStereoCoinPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(coin.gameObject.scene==SceneManager.GetActiveScene() && !coin.gameObject.activeSelf) picked.Add(coin);
            var cp=new Checkpoint{position=rb.position,velocity=rb.linearVelocity,mode=motor.Mode,picked=picked.ToArray()};
            var tex=Resources.Load<Texture2D>("BiofitStereoV2/UI/checkpoint");
            if(tex!=null)
            {
                if(markerSprite==null) markerSprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),60f);
                cp.marker=new GameObject("Practice checkpoint"); cp.marker.transform.SetParent(transform,false);
                cp.marker.transform.position=new Vector3(cp.position.x,cp.position.y-.3f,0);
                var sr=cp.marker.AddComponent<SpriteRenderer>();sr.sprite=markerSprite;sr.sortingOrder=-10;
            }
            checkpoints.Add(cp); lastAuto=Time.unscaledTime;
            Debug.Log("BIOFIT V3 practica: checkpoint "+checkpoints.Count+" x="+cp.position.x+" manual="+(!automatic));
            if(!automatic) Report("CHECKPOINT COLOCADO");
        }
        private void RemoveLast()
        {
            if(!Enabled || Time.timeScale<=0f || GameManager.Instance==null || !GameManager.Instance.PlayMode) return;
            if(checkpoints.Count==0) {Report("SIN CHECKPOINTS");return;}
            var cp=checkpoints[checkpoints.Count-1]; checkpoints.RemoveAt(checkpoints.Count-1);
            if(cp.marker!=null) Destroy(cp.marker); lastAuto=Time.unscaledTime;Report("CHECKPOINT ELIMINADO");
        }
        private void Restore(Checkpoint cp)
        {
            var rb=avatar.GetComponent<Rigidbody2D>(); var motor=avatar.GetComponent<BiofitModeMotor>();
            if(rb==null || motor==null) return;
            motor.SetMode(cp.mode); rb.position=cp.position; avatar.transform.position=new Vector3(cp.position.x,cp.position.y,avatar.transform.position.z);
            rb.linearVelocity=cp.velocity; Physics2D.SyncTransforms();
            BiofitStereoCoinPickup.RestorePractice(cp.picked);
            var camera=UnityEngine.Camera.main;
            if(camera!=null) camera.transform.position=new Vector3(cp.position.x+5,Mathf.Max(4,cp.position.y),camera.transform.position.z);
            lastAuto=Time.unscaledTime;
            Debug.Log("BIOFIT practica: restaurado checkpoint x="+cp.position.x+" modo="+cp.mode);
        }
    }
}
