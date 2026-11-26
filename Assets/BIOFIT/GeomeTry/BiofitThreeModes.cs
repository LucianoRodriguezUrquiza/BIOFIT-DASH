using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biofit.GeomeTryOriginal
{
    public enum BiofitMode { Cube, Ball, Spider, Ship, InvertedCube, MiniCube, MiniInvertedCube }

    // Self-contained experimental modes. Nothing is modified inside the pinned GeomeTry submodule.
    // Maintains Character (Cube) on the original player for death, respawn, camera and progress.
    public sealed class BiofitThreeModesBootstrap : MonoBehaviour
    {
        private GameObject avatar;
        private GameObject roof;
        private readonly List<GameObject> shipCeilings = new List<GameObject>();
        private bool installed;
        public static bool Active =>
            Application.isEditor || Application.identifier == "com.biofitgame.threemodeslab";

        private void Awake()
        {
            if (!Active) { enabled = false; return; }
            Register("biofitBallPortal", BiofitMode.Ball);
            Register("biofitSpiderPortal", BiofitMode.Spider);
            Register("biofitCubePortal", BiofitMode.Cube);
            Register("biofitShipPortal", BiofitMode.Ship);
            RegisterCoin();
            RegisterDecoration("biofitDecoPulse");
            RegisterDecoration("biofitDecoCrystal");
            RegisterDecoration("biofitDecoStar");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static void Register(string key, BiofitMode mode)
        {
            if (Prefab.Prefabs.ContainsKey(key) && Prefab.Prefabs[key] != null) return;
            GameObject template = new GameObject("BIOFIT_TEMPLATE_" + mode);
            DontDestroyOnLoad(template);
            template.transform.position = new Vector3(-50000f, 0f, 0f);
            // Visible across floor and roof, while trigger stays on the root.
            GameObject visual = new GameObject("PortalVisual");
            visual.transform.SetParent(template.transform, false);
            visual.transform.localScale = new Vector3(1f, 3.1f, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = BiofitThreeModeArt.Make(mode, true);
            renderer.sortingOrder = 8;
            BoxCollider2D trigger = template.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.5f, 10f);
            template.AddComponent<BiofitModePortal>().Target = mode;
            Prefab.Prefabs[key] = template;
        }

        // Decorative sprites are instantiated by the ORIGINAL map loader through
        // Prefab.Prefabs, without a collider or any change to player physics.
        // Optional licensed artwork overrides are loaded from Resources.
        private static void RegisterCoin()
        {
            const string key = "biofitStereoCoin";
            if (Prefab.Prefabs.ContainsKey(key) && Prefab.Prefabs[key] != null) return;
            GameObject template = new GameObject("BIOFIT_TEMPLATE_STEREO_COIN");
            DontDestroyOnLoad(template);
            template.transform.position = new Vector3(-50000f, 0f, 0f);
            var sprite = template.AddComponent<SpriteRenderer>();
            sprite.sprite = BiofitThreeModeArt.MakeDecoration("biofitDecoStar");
            sprite.sortingOrder = 9;
            var collider = template.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = .6f;
            template.AddComponent<BiofitStereoCoinPickup>();
            Prefab.Prefabs[key] = template;
        }

        private static void RegisterDecoration(string key)
        {
            if (Prefab.Prefabs.ContainsKey(key) && Prefab.Prefabs[key] != null) return;
            GameObject template = new GameObject("BIOFIT_TEMPLATE_" + key);
            DontDestroyOnLoad(template);
            template.transform.position = new Vector3(-50000f, 0f, 0f);
            var sprite = template.AddComponent<SpriteRenderer>();
            sprite.sprite = BiofitThreeModeArt.MakeDecoration(key);
            sprite.sortingOrder = -2;
            template.transform.localScale = Vector3.one * 1.55f;
            Prefab.Prefabs[key] = template;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            avatar = null;
            roof = null;
            shipCeilings.Clear();
            installed = false;
            if (scene.name == "Level")
            {
                BiofitStereoCoinPickup.ResetRun();
                StartCoroutine(SetupAfterOriginalGame());
            }
        }

        private IEnumerator SetupAfterOriginalGame()
        {
            // Game.Start() creates the actual Cube and original floor after sceneLoaded.
            yield return null;
            Level level = GameManager.Instance == null ? null : GameManager.Instance.SelectedLevel;
            if (level == null) yield break;
            installed = true;
            GameObject ceilingPrefab = Resources.Load<GameObject>("Prefabs/Decors/CeilPrefab");
            if (ceilingPrefab == null) { Debug.LogError("BIOFIT Three Modes: falta CeilPrefab."); yield break; }
            if (BiofitStereoLocalRuntime.IsLocal(level)) yield break;
            if (level.Id == 1)
            {
                // Stereo Madness: ship-only roof spans, not a ceiling over the
                // entire Cube level. Leave the existing levels 2-5 unchanged.
                for (int i = 0; i < level.Map.Count; i++)
                {
                    if (level.Map[i].Type != "biofitShipPortal") continue;
                    float start = level.Map[i].X + 2f;
                    float end = level.getLastMapItem().X;
                    for (int j = i + 1; j < level.Map.Count; j++)
                        if (level.Map[j].Type == "biofitCubePortal")
                        { end = level.Map[j].X; break; }
                    if (end <= start + 1f) continue;
                    GameObject ceiling = Instantiate(ceilingPrefab,
                        new Vector3((start + end) * .5f, 8f, 0f), Quaternion.identity);
                    ceiling.name = "BIOFIT_STEREO_SHIP_ROOF";
                    ceiling.transform.localScale = new Vector3(end - start, 1f, 1f);
                    ceiling.layer = 3;
                    shipCeilings.Add(ceiling);
                }
                yield break;
            }
            float lastX = level.getLastMapItem().X;
            roof = Instantiate(ceilingPrefab,
                new Vector3((lastX) * .5f, 8f, 0f),
                Quaternion.identity);
            roof.name = "BIOFIT_TECHO_CONTINUO";
            roof.transform.localScale = new Vector3(lastX + 80f, 1f, 1f);
            roof.layer = 3; // Same Ground layer as original Floor/Ceil prefabs.
        }

        private void LateUpdate()
        {
            if (!installed || SceneManager.GetActiveScene().name != "Level") return;
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found == null || found == avatar) return;
            avatar = found;
            Cube cube = avatar.GetComponent<Cube>();
            if (cube == null) return;
            BiofitModeMotor motor = avatar.GetComponent<BiofitModeMotor>();
            if (motor == null) motor = avatar.AddComponent<BiofitModeMotor>();
            if (avatar.GetComponent<BiofitVrBoxController>() == null)
                avatar.AddComponent<BiofitVrBoxController>();
            int id = GameManager.Instance?.SelectedLevel?.Id ?? 1;
            if (BiofitStereoLocalRuntime.IsLocal(GameManager.Instance?.SelectedLevel)) { BiofitStereoCoinPickup.ResetRun(); motor.SetMode(BiofitMode.Cube); }
            else { if(id==1) BiofitStereoCoinPickup.ResetRun(); motor.SetMode(id == 2 ? BiofitMode.Ball : id == 3 ? BiofitMode.Spider : BiofitMode.Cube); }
        }
    }

    public sealed class BiofitStereoCoinPickup : MonoBehaviour
    {
        public static int Collected { get; private set; }
        public static void RestorePractice(BiofitStereoCoinPickup[] picked)
        {
            if(!BiofitStereoPractice.Enabled) return;
            ResetRun();
            foreach(var coin in picked)
                if(coin!=null && coin.gameObject.activeSelf) {coin.gameObject.SetActive(false);Collected++;}
        }
        public static void ResetRun()
        {
            Collected = 0;
            // The original GeomeTry death/respawn does not reload the scene:
            // re-arm coins when a NEW player appears, instead of allowing
            // progress to accumulate across multiple failed attempts.
            Scene active = SceneManager.GetActiveScene();
            foreach (BiofitStereoCoinPickup coin in
                UnityEngine.Object.FindObjectsByType<BiofitStereoCoinPickup>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (coin == null || coin.gameObject.scene != active) continue;
                coin.gameObject.SetActive(true);
            }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            Cube cube = other.GetComponent<Cube>();
            if (cube == null || !cube.isAlive ||
                GameManager.Instance == null ||
                GameManager.Instance.SelectedLevel == null ||
                !BiofitStereoLocalRuntime.IsLocal(GameManager.Instance.SelectedLevel) && GameManager.Instance.SelectedLevel.Id != 1) return;
            Collected++;
            Debug.Log("BIOFIT STEREO COIN " + Collected + "/3");
            gameObject.SetActive(false);
        }
    }

    public sealed class BiofitModePortal : MonoBehaviour
    {
        public BiofitMode Target;
        private void OnTriggerEnter2D(Collider2D other)
        {
            Cube cube = other.GetComponent<Cube>();
            if (cube == null || !cube.isAlive) return;
            BiofitModeMotor motor = cube.GetComponent<BiofitModeMotor>();
            if (motor == null || motor.Mode == Target) return;
            motor.SetMode(Target);
            if (BiofitStereoPractice.Available)
                Debug.Log("BIOFIT V3 portal: " + Target + " playerX=" + cube.transform.position.x.ToString("F3") + " playerY=" + cube.transform.position.y.ToString("F3"));
            StartCoroutine(PortalFlash(transform.position));
        }

        private IEnumerator PortalFlash(Vector3 position)
        {
            GameObject flash = new GameObject("BIOFIT_PORTAL_TRANSITION");
            SpriteRenderer ring = flash.AddComponent<SpriteRenderer>();
            ring.sprite = BiofitThreeModeArt.Make(Target, true);
            ring.sortingOrder = 24;
            flash.transform.position = position;
            for (float t = 0f; t < .24f; t += Time.unscaledDeltaTime)
            {
                float phase = Mathf.Clamp01(t / .24f);
                flash.transform.localScale = Vector3.one * (1.0f + phase * .45f);
                ring.color = new Color(1f, 1f, 1f, 0.7f * (1f - phase));
                yield return null;
            }
            Destroy(flash);
        }
    }

    // This reads the REAL Android controller observed as "VR BOX".
    // The user validated MODE CHANGE + C and KeyCodes 0, 1 and 13.
    // A/R1=0+13, B/R2=0, C=13, D=1. All are the SAME action by request.
    internal static class BiofitVrBoxInput
    {
        private static float refreshAt;
        private static bool connected;
        public static bool Connected
        {
            get
            {
                if (Time.unscaledTime >= refreshAt)
                {
                    refreshAt = Time.unscaledTime + .25f;
                    connected = false;
                    foreach (string name in Input.GetJoystickNames())
                        if (!string.IsNullOrEmpty(name) &&
                            name.IndexOf("VR BOX", StringComparison.OrdinalIgnoreCase) >= 0)
                        { connected = true; break; }
                }
                return connected;
            }
        }
        public static bool AnyHeld =>
            Connected && (Input.GetKey(KeyCode.JoystickButton0) ||
                          Input.GetKey(KeyCode.JoystickButton1) ||
                          Input.GetKey(KeyCode.JoystickButton13));
        public static bool AnyDown =>
            Connected && (Input.GetKeyDown(KeyCode.JoystickButton0) ||
                          Input.GetKeyDown(KeyCode.JoystickButton1) ||
                          Input.GetKeyDown(KeyCode.JoystickButton13));
    }

    // The original Cube.Update only polls mouse/Space. It has no joystick
    // binding. Maintain the validated Cube.onClick() physics AND hold repeat:
    // while any VR BOX action is held, call onClick every frame, so a new
    // grounded jump is possible without requiring a button release.
    public sealed class BiofitVrBoxController : MonoBehaviour
    {
        private static readonly FieldInfo KeyPressed =
            typeof(Character).GetField("keyPressed",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private Cube cube;
        private BiofitModeMotor motor;
        private void Awake()
        {
            cube = GetComponent<Cube>();
            motor = GetComponent<BiofitModeMotor>();
        }
        private void Update()
        {
            if (cube == null || KeyPressed == null || !cube.enabled ||
                !cube.isAlive || motor == null || motor.Mode != BiofitMode.Cube ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode ||
                !BiofitVrBoxInput.AnyHeld) return;
            bool prior = (bool)KeyPressed.GetValue(cube);
            try
            {
                KeyPressed.SetValue(cube, true);
                cube.onClick();
            }
            finally { KeyPressed.SetValue(cube, prior); }
        }
    }

    public sealed class BiofitModeMotor : MonoBehaviour
    {
        // Original Cube body, gravity and jump code are never changed.
        // Only when in Ball/Spider mode, Cube MonoBehaviour is disabled
        // and this adapter moves the same Rigidbody2D player horizontally.
        private const float OriginalSpeed = 6.5f;
        private const float BallSpiderGravity = 4f;
        // Original Christ0u/GeomeTry Ship.cs: gravity +/-2.5, |Vy|<=10,
        // rotation clamp +/-40 degrees. This motor keeps the Cube object.
        private const float ShipGravity = 2.5f;
        private const float ShipMaxVerticalSpeed = 10f;
        private const float ShipMaxAngle = 40f;
        // A BLE bilateral jump is a discrete thrust pulse, unlike a held key.
        // 400ms is a starting playable hold; user can tune after APK tests.
        private const float ShipBleThrustSeconds = .40f;
        private const float SurfaceRay = .92f;
        private const int GroundMask = 1 << 3;
        private int ActiveGroundMask => BiofitStereoPractice.Available ? GroundMask | (1 << 6) : GroundMask;
        public bool IsMini => Mode==BiofitMode.MiniCube || Mode==BiofitMode.MiniInvertedCube;
        public bool IsCubeMode => Mode==BiofitMode.Cube || Mode==BiofitMode.InvertedCube || IsMini;
        public bool IsBallSpiderMode => Mode==BiofitMode.Ball || Mode==BiofitMode.Spider;
        // External pads/orbs change gravity without changing vehicle or teleporting.
        public void SetBallSpiderGravity(bool inverted)
        {
            if(!initialized || !IsBallSpiderMode) return;
            flipped=inverted;
            body.gravityScale=flipped?-BallSpiderGravity:BallSpiderGravity;
            if(Mode==BiofitMode.Spider) icon.flipY=flipped;
        }
        public bool StereoInverted => Mode==BiofitMode.InvertedCube || Mode==BiofitMode.MiniInvertedCube || flipped;
        public const float MiniScale=.60f, MiniSpeed=7.8f, MiniJump=10.5f;
        private Vector3 normalScale;
        public void SetMini(bool mini)
        {
            Initialize();if(!initialized || !IsCubeMode)return;
            SetMode(mini?(StereoInverted?BiofitMode.MiniInvertedCube:BiofitMode.MiniCube):(StereoInverted?BiofitMode.InvertedCube:BiofitMode.Cube));
        }
        public void SetCubeGravity(bool invert)
        {
            if(!IsCubeMode)return;
            SetMode(IsMini?(invert?BiofitMode.MiniInvertedCube:BiofitMode.MiniCube):(invert?BiofitMode.InvertedCube:BiofitMode.Cube));
        }
        public void RestoreStereoGravity(bool inverted)
        {
            if (!BiofitStereoPractice.Available || !initialized || (Mode != BiofitMode.Ball && Mode != BiofitMode.Spider)) return;
            flipped = inverted;
            body.gravityScale = flipped ? -BallSpiderGravity : BallSpiderGravity;
            if (Mode == BiofitMode.Spider) icon.flipY = flipped;
        }
        private Cube cube;
        private Rigidbody2D body;
        private Collider2D playerCollider;
        private SpriteRenderer icon;
        private Sprite originalSprite;
        private Color originalTint;
        private bool initialized;
        private bool initialFacing;
        private bool flipped;
        private float lastManualAction;
        private float shipBleHeldUntil;
        public BiofitMode Mode { get; private set; } = BiofitMode.Cube;

        private void Initialize()
        {
            if (initialized) return;
            cube = GetComponent<Cube>();
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<Collider2D>();
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            // Resolve the same renderer Character.RotateSprite uses, not an arbitrary child.
            icon=typeof(Character).GetField("spriteRenderer",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(cube) as SpriteRenderer;
            if(icon==null) foreach (SpriteRenderer sr in sprites)
                if (sr.sprite != null) { icon = sr; break; }
            if (icon == null && sprites.Length > 0) icon = sprites[0];
            if (icon != null) { originalSprite = icon.sprite; originalTint = icon.color; }
            initialized = cube != null && body != null && icon != null;
            if(initialized) normalScale=transform.localScale;
            if(initialized && icon.transform!=transform && BiofitStereoLocalRuntime.IsLocal(GameManager.Instance?.SelectedLevel))
            { initialFacing=true; icon.transform.rotation=Quaternion.identity; }
        }

        public void SetMode(BiofitMode newMode)
        {
            Initialize();
            if (!initialized || !cube.isAlive || newMode == Mode) return;
            initialFacing=false;
            bool wasMini=IsMini;
            Mode = newMode;
            if(IsMini||wasMini)transform.localScale=IsMini?normalScale*MiniScale:normalScale;
            flipped = false;
            shipBleHeldUntil = 0f;
            // Keep the incoming jump momentum at the local mode portal; do not edit Cube physics.
            if (!(BiofitStereoPractice.Available && (newMode == BiofitMode.Ball || newMode == BiofitMode.Spider)))
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            body.gravityScale = newMode == BiofitMode.Ship ? ShipGravity : BallSpiderGravity;
            if (newMode == BiofitMode.Cube)
            {
                cube.enabled = true;
                icon.sprite = originalSprite;
                icon.color = originalTint;
                icon.flipY = false;
                icon.transform.localRotation = Quaternion.identity;
            }
            else if(IsMini)
            {
                cube.enabled=false;flipped=newMode==BiofitMode.MiniInvertedCube;
                body.gravityScale=flipped?-BallSpiderGravity:BallSpiderGravity;
                icon.sprite=originalSprite;icon.color=originalTint;icon.flipY=flipped;icon.transform.localRotation=Quaternion.identity;
            }
            else if(newMode==BiofitMode.InvertedCube)
            {
                cube.enabled=false; flipped=true; body.gravityScale=-BallSpiderGravity;
                icon.sprite=originalSprite; icon.color=originalTint; icon.flipY=true;
                icon.transform.localRotation=Quaternion.identity;
            }
            else
            {
                cube.enabled = false;
                icon.sprite = (newMode==BiofitMode.Ball||newMode==BiofitMode.Spider?BiofitV65Skins.Current(newMode,false):null) ?? BiofitThreeModeArt.Make(newMode, false);
                icon.color = Color.white;
                icon.flipY = false;
                icon.transform.localRotation = Quaternion.identity;
            }
            // Avoid a Cube-only ground particle stream while Ball rolls or Spider teleports.
            ParticleSystem particles = GetComponentInChildren<ParticleSystem>(true);
            if (particles != null)
            {
                if (newMode == BiofitMode.Cube) particles.Play();
                else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            BiofitBLEMonitor monitor = FindFirstObjectByType<BiofitBLEMonitor>();
            if (monitor != null && monitor.GameCaptureActive)
                monitor.RecordControlDiagnostic("GEOMETRY_MODE_CHANGE mode=" + newMode +
                    " x=" + transform.position.x.ToString("F2",
                        System.Globalization.CultureInfo.InvariantCulture));
        }

        private void LateUpdate()
        {
            if(initialized&&cube.isAlive&&(Mode==BiofitMode.Ball||Mode==BiofitMode.Spider))
            {
                var selected=BiofitV65Skins.Current(Mode,GameManager.Instance!=null&&GameManager.Instance.PlayMode);
                if(selected!=null&&icon.sprite!=selected)icon.sprite=selected;
            }
            // Correct initial falling/settling sprite only. Leave all subsequent jump rotations intact.
            if(!initialFacing || !initialized || Mode!=BiofitMode.Cube || !cube.isAlive) return;
            if(body.linearVelocity.y>.10f) {initialFacing=false;return;}
            icon.transform.rotation=Quaternion.identity;
            if(Mathf.Abs(body.linearVelocity.y)<.05f && SurfaceContact(Vector2.down)) initialFacing=false;
        }

        private void Update()
        {
            if (!initialized || Mode == BiofitMode.Cube || !cube.isAlive ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode) return;
            if (Mode == BiofitMode.Ship) return; // Ship responds to held input in FixedUpdate.
            if(IsMini || Mode==BiofitMode.InvertedCube) { if(Input.GetKey(KeyCode.Space) || (Input.GetMouseButton(0) && !BiofitStereoPracticeButton.BlocksGameplayPointer()) || BiofitVrBoxInput.AnyHeld) TryPulse(); return; }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) ||
                BiofitVrBoxInput.AnyDown) TryPulse();
        }

        private void FixedUpdate()
        {
            if (!initialized || Mode == BiofitMode.Cube || !cube.isAlive ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode) return;
            if(IsMini)
            {
                transform.Translate(new Vector2(MiniSpeed*Time.deltaTime,0),Space.World);
                if(!MiniSupport())icon.transform.Rotate(0,0,(StereoInverted?1:-1)*280f*Time.deltaTime);
                else icon.transform.localRotation=Quaternion.Euler(0,0,Mathf.Round(icon.transform.localEulerAngles.z/90f)*90f);
                return;
            }
            // Equivalent to the original Character.FixedUpdate horizontal motion.
            transform.Translate(new Vector2(OriginalSpeed * Time.deltaTime, 0f), Space.World);
            if (Mode == BiofitMode.Ship)
            {
                // No synthetic key repeat: one continuous held state controls
                // upward gravity just like the ORIGINAL Ship. Releasing falls.
                bool held = Input.GetKey(KeyCode.Space) ||
                            (Input.GetMouseButton(0) && !Biofit.GeomeTryOriginal.BiofitStereoPracticeButton.BlocksGameplayPointer()) ||
                            BiofitVrBoxInput.AnyHeld ||
                            Time.unscaledTime < shipBleHeldUntil;
                body.gravityScale = held ? -ShipGravity : ShipGravity;
                float vy = Mathf.Clamp(body.linearVelocity.y,
                    -ShipMaxVerticalSpeed, ShipMaxVerticalSpeed);
                if (vy != body.linearVelocity.y)
                    body.linearVelocity = new Vector2(body.linearVelocity.x, vy);
                float angle = Mathf.Clamp(
                    Mathf.Atan2(vy, 7f) * Mathf.Rad2Deg,
                    -ShipMaxAngle, ShipMaxAngle);
                icon.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            if(Mode==BiofitMode.InvertedCube)
            {
                if(!SurfaceContact(Vector2.up)) icon.transform.Rotate(0,0,280f*Time.deltaTime);
                else icon.transform.localRotation=Quaternion.Euler(0,0,Mathf.Round(icon.transform.localEulerAngles.z/90f)*90f);
            }
            if (Mode == BiofitMode.Ball)
            {
                icon.transform.Rotate(0, 0,
                    (flipped ? 1f : -1f) * OriginalSpeed * Time.deltaTime * 57.2958f);
            }
        }

        private bool MiniSupport()
        {
            if(playerCollider==null)return false;
            var b=playerCollider.bounds;Vector2 dir=StereoInverted?Vector2.up:Vector2.down;
            foreach(float side in new[]{-.85f,0f,.85f})
                if(Physics2D.Raycast(new Vector2(b.center.x+side*b.extents.x,b.center.y),dir,b.extents.y+.06f,ActiveGroundMask).collider!=null)return true;
            return false;
        }
        private bool SurfaceContact(Vector2 direction)
        {
            Vector2 center = body.position;
            foreach (float x in new[] { -.28f, 0f, .28f })
            {
                if (Physics2D.Raycast(center + new Vector2(x, 0f),
                    direction, SurfaceRay, ActiveGroundMask).collider != null) return true;
            }
            return false;
        }

        public bool TryPulse()
        {
            Initialize();
            if (!initialized || Mode == BiofitMode.Cube || !cube.isAlive ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode) return false;
            // Protect against the *same* simultaneous touch and BLE pulse, not a
            // replacement for the unchanged 900-ms bilateral receiver cooldown.
            if (Time.unscaledTime - lastManualAction < .12f) return false;
            if (Mode == BiofitMode.Ship)
            {
                shipBleHeldUntil = Mathf.Max(shipBleHeldUntil,
                    Time.unscaledTime + ShipBleThrustSeconds);
                lastManualAction = Time.unscaledTime;
                return true;
            }
            if(IsMini)
            {
                foreach(var hit in Physics2D.OverlapCircleAll(body.position,.5f))
                {var interaction=hit.GetComponent<BiofitV5Interaction>();if(interaction!=null && interaction.TryInvertedOrb(cube)){lastManualAction=Time.unscaledTime;return true;}}
                if(!MiniSupport())return false;
                body.linearVelocity=new Vector2(body.linearVelocity.x,(StereoInverted?-1:1)*MiniJump);
                lastManualAction=Time.unscaledTime;return true;
            }
            if(Mode==BiofitMode.InvertedCube)
            {
                foreach(var hit in Physics2D.OverlapCircleAll(body.position,.8f))
                {
                    var interaction=hit.GetComponent<BiofitV5Interaction>();
                    if(interaction!=null && interaction.TryInvertedOrb(cube)) { lastManualAction=Time.unscaledTime; return true; }
                }
                if(!SurfaceContact(Vector2.up)) return false;
                body.linearVelocity=new Vector2(body.linearVelocity.x,-13.5f);
                lastManualAction=Time.unscaledTime; return true;
            }
            // An accepted pulse may activate a contacted orb in the air.
            // Give it priority over the normal Ball flip/Spider teleport.
            if(IsBallSpiderMode && playerCollider!=null)
            {
                var bounds=playerCollider.bounds;
                foreach(var hit in Physics2D.OverlapBoxAll(bounds.center,bounds.size,0f))
                {
                    var interaction=hit.GetComponent<BiofitV5Interaction>();
                    if(interaction!=null && interaction.TryBallSpiderOrb(cube))
                    {
                        lastManualAction=Time.unscaledTime;
                        return true;
                    }
                }
            }
            Vector2 support = flipped ? Vector2.up : Vector2.down;
            if (!SurfaceContact(support)) return false;
            if (Mode == BiofitMode.Ball)
            {
                flipped = !flipped;
                body.gravityScale = flipped ? -BallSpiderGravity : BallSpiderGravity;
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
                lastManualAction = Time.unscaledTime;
                StartCoroutine(GravityFlipEffect(transform.position));
                return true;
            }

            Vector2 destinationDirection = -support;
            RaycastHit2D destinationHit = Physics2D.Raycast(
                body.position, destinationDirection, 16f, ActiveGroundMask);
            if (destinationHit.collider == null) return false;
            float halfHeight = playerCollider == null ? .5f :
                Mathf.Max(.2f, playerCollider.bounds.extents.y);
            Vector2 destination = new Vector2(body.position.x,
                destinationHit.point.y - destinationDirection.y * (halfHeight + .035f));
            if (Vector2.Distance(destination, body.position) < 1.0f) return false;

            // Avoid teleportation into hazards or blocks near the receiving surface.
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(destination,
                new Vector2(.72f, .72f), 0f);
            foreach (Collider2D collision in overlaps)
            {
                if (collision == playerCollider || collision == destinationHit.collider ||
                    collision.isTrigger) continue;
                return false;
            }

            Vector3 oldPosition = transform.position;
            body.position = destination;
            transform.position = new Vector3(destination.x, destination.y, transform.position.z);
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            flipped = !flipped;
            body.gravityScale = flipped ? -BallSpiderGravity : BallSpiderGravity;
            icon.flipY = flipped;
            Physics2D.SyncTransforms();
            lastManualAction = Time.unscaledTime;
            StartCoroutine(SpiderEffect(oldPosition, transform.position));
            return true;
        }

        private IEnumerator GravityFlipEffect(Vector3 position)
        {
            GameObject ring = new GameObject("BIOFIT_BALL_GRAVITY_FLIP");
            SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = BiofitThreeModeArt.Make(BiofitMode.Ball, false);
            sr.sortingOrder = 19;
            ring.transform.position = position;
            float t = 0f;
            while (t < .2f)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / .2f);
                ring.transform.localScale = Vector3.one * (1f + p * .8f);
                sr.color = new Color(1f, .5f, .5f, 1f - p);
                yield return null;
            }
            Destroy(ring);
        }

        private IEnumerator SpiderEffect(Vector3 from, Vector3 to)
        {
            GameObject trail = new GameObject("BIOFIT_SPIDER_TELEPORT_TRAIL");
            LineRenderer line = trail.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = .18f;
            line.endWidth = .18f;
            line.numCapVertices = 4;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
            line.sortingOrder = 20;
            GameObject flash = new GameObject("BIOFIT_SPIDER_ARRIVAL_FLASH");
            SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
            flashRenderer.sprite = BiofitThreeModeArt.Make(BiofitMode.Spider, false);
            flashRenderer.sortingOrder = 19;
            flash.transform.position = to;
            float timer = 0f;
            while (timer < .18f)
            {
                timer += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(1f - timer / .18f);
                line.startColor = new Color(.4f, 1f, 1f, alpha);
                line.endColor = new Color(.9f, .3f, 1f, alpha);
                line.startWidth = Mathf.Lerp(.18f, 0f, 1f - alpha);
                line.endWidth = line.startWidth;
                flash.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, .5f, 1f - alpha);
                flashRenderer.color = new Color(.7f, .25f, 1f, .75f * alpha);
                yield return null;
            }
            if (line.material != null) Destroy(line.material);
            Destroy(flash);
            Destroy(trail);
        }
    }

    internal static class BiofitThreeModeArt
    {
        private static Sprite ball, spider, ship, cubeIcon, cubePortal, ballPortal, spiderPortal, shipPortal;
        private static readonly Dictionary<string, Sprite> decorationCache = new Dictionary<string, Sprite>();
        public static Sprite Make(BiofitMode mode, bool portal)
        {
            // A licensed PNG sprite can be supplied later via Resources.
            // No unofficial RobTop sprite extraction is committed to the repo.
            string artName = (portal ? "Portal" : "Icon") + mode;
            Sprite provided = Resources.Load<Sprite>("BiofitThreeModes/Artwork/" + artName);
            if (provided != null) return provided;
            if (!portal)
            {
                if (mode == BiofitMode.Cube) return cubeIcon ?? (cubeIcon = Build(mode, false));
                if (mode == BiofitMode.Ball) return ball ?? (ball = Build(mode, false));
                if (mode == BiofitMode.Ship) return ship ?? (ship = Build(mode, false));
                return spider ?? (spider = Build(mode, false));
            }
            if (mode == BiofitMode.Cube) return cubePortal ?? (cubePortal = Build(mode, true));
            if (mode == BiofitMode.Ball) return ballPortal ?? (ballPortal = Build(mode, true));
            if (mode == BiofitMode.Ship) return shipPortal ?? (shipPortal = Build(mode, true));
            return spiderPortal ?? (spiderPortal = Build(mode, true));
        }
        private static Sprite Build(BiofitMode mode, bool portal)
        {
            const int n = 96;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color transparent = new Color(0, 0, 0, 0);
            Color accent = mode == BiofitMode.Ball ? new Color(1f, .36f, .25f) :
                mode == BiofitMode.Spider ? new Color(.7f, .23f, 1f) :
                mode == BiofitMode.Ship ? new Color(.17f, .95f, .9f) :
                new Color(.3f, 1f, .32f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x - n / 2f) / (n / 2f);
                float v = (y - n / 2f) / (n / 2f);
                float r = Mathf.Sqrt(u * u + v * v);
                Color c = transparent;
                if (portal)
                {
                    float ellipse = Mathf.Sqrt(u * u / .34f + v * v / .85f);
                    if (ellipse < 1f && ellipse > .76f) c = Color.black;
                    if (ellipse < .93f && ellipse > .81f) c = accent;
                    if (ellipse < .72f && ellipse > .65f) c = Color.white;
                    if (ellipse < .53f && ellipse > .46f) c = accent;
                    if (Mathf.Abs(u) < .09f && Mathf.Abs(v) < .15f) c = accent;
                }
                else if (mode == BiofitMode.Cube)
                {
                    float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                    if (ax < .92f && ay < .92f) c = Color.black;
                    if (ax < .78f && ay < .78f) c = accent;
                    if (ax < .56f && ay < .56f) c = Color.white;
                    if (ax < .42f && ay < .42f) c = accent;
                    if (ay < .16f && ax > .23f && ax < .37f) c = Color.black;
                }
                else if (mode == BiofitMode.Ball)
                {
                    if (r < .92f) c = Color.black;
                    if (r < .79f) c = accent;
                    if (r < .63f) c = Color.white;
                    if (r < .45f) c = accent;
                    float angle = Mathf.Atan2(v,u);
                    if (r > .46f && r < .65f && Mathf.Abs(Mathf.Sin(angle * 6f)) < .32f) c = Color.black;
                    if (r < .22f) c = Color.black;
                    if (r < .12f) c = Color.white;
                }
                else if (mode == BiofitMode.Ship)
                {
                    // Original procedural triangular ship silhouette: no third-
                    // party texture is embedded or redistributed with the repo.
                    float span = .88f - .64f * Mathf.Abs(v);
                    if (u > -.83f && u < span && Mathf.Abs(v) < .68f) c = Color.black;
                    if (u > -.69f && u < span - .13f &&
                        Mathf.Abs(v) < .55f) c = accent;
                    if (u > -.15f && u < .13f && Mathf.Abs(v) < .18f) c = Color.white;
                    if (u < -.45f && Mathf.Abs(v) < .25f) c = new Color(1f, .52f, .17f);
                }
                else
                {
                    float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                    bool torso = ax < .55f && ay < .45f;
                    bool legs = (ax >= .40f && ax < .89f && ay > .21f && ay < .34f) ||
                        (ay >= .40f && ay < .82f && ax > .25f && ax < .37f);
                    if (torso || legs) c = Color.black;
                    if (ax < .47f && ay < .37f) c = accent;
                    if (ax < .21f && ay < .14f) c = Color.white;
                    if (ax < .12f && ay < .075f) c = Color.black;
                }
                texture.SetPixel(x, y, c);
            }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, n, n),
                new Vector2(.5f, .5f), portal ? 36f : 96f);
        }
        // Generic ORIGINAL icons, not copied from the proprietary Geometry Dash pack.
        // The user may replace them locally with verified-license PNG files.
        public static Sprite MakeDecoration(string key)
        {
            string fileName = key == "biofitDecoPulse" ? "DecoPulse" :
                key == "biofitDecoCrystal" ? "DecoCrystal" : "DecoStar";
            Sprite supplied = Resources.Load<Sprite>("BiofitThreeModes/Artwork/" + fileName);
            if (supplied != null) return supplied;
            if (decorationCache.TryGetValue(key, out Sprite sprite)) return sprite;

            const int n = 64;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x - 31.5f) / 32f;
                float v = (y - 31.5f) / 32f;
                float r = Mathf.Sqrt(u*u + v*v);
                Color c = Color.clear;
                if (key == "biofitDecoPulse")
                {
                    if (r > .64f && r < .81f) c = Color.black;
                    if (r > .69f && r < .77f) c = new Color(.26f, 1f, .96f);
                    if (Mathf.Abs(u) < .095f && Mathf.Abs(v) < .39f ||
                        Mathf.Abs(v) < .095f && Mathf.Abs(u) < .39f)
                        c = new Color(.95f, 1f, .96f);
                }
                else if (key == "biofitDecoCrystal")
                {
                    float diamond = Mathf.Abs(u) + Mathf.Abs(v);
                    if (diamond < .86f) c = Color.black;
                    if (diamond < .75f) c = new Color(.5f, .35f, 1f);
                    if (diamond < .56f) c = new Color(.78f, .7f, 1f);
                    if (u < -.06f && v > .06f && diamond < .46f) c = Color.white;
                }
                else
                {
                    float angle = Mathf.Atan2(v, u);
                    float radius = .53f + .30f * Mathf.Cos(angle * 5f);
                    if (r < radius) c = Color.black;
                    if (r < radius - .12f) c = new Color(1f, .86f, .25f);
                    if (r < .20f) c = Color.white;
                }
                texture.SetPixel(x, y, c);
            }
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, n, n),
                new Vector2(.5f, .5f), 64f);
            decorationCache[key] = sprite;
            return sprite;
        }

    }

    // All optional GD textures are provided by the user's local importer.
    // No proprietary graphics or animation data are embedded in this script.
    public static class BiofitV65Skins
    {
        [Serializable] sealed class Manifest {public string[] balls,spiders;}
        static Manifest manifest;
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        const string Root="BiofitThreeModes/Artwork/";
        static Manifest Data
        {
            get
            {
                if(manifest==null){var t=Resources.Load<TextAsset>(Root+"V65Skins");manifest=t==null?new Manifest():JsonUtility.FromJson<Manifest>(t.text)??new Manifest();}
                return manifest;
            }
        }
        public static string[] Names(BiofitMode mode)=>mode==BiofitMode.Ball?(Data.balls??Array.Empty<string>()):(Data.spiders??Array.Empty<string>());
        static string Pref(BiofitMode mode)=>mode==BiofitMode.Ball?"BIOFIT_V65_BALL_SKIN":"BIOFIT_V65_SPIDER_SKIN";
        public static int Selected(BiofitMode mode)=>Mathf.Clamp(PlayerPrefs.GetInt(Pref(mode),0),0,Mathf.Max(0,Names(mode).Length-1));
        public static void Select(BiofitMode mode,int index)
        {
            if(index<0||index>=Names(mode).Length)return;PlayerPrefs.SetInt(Pref(mode),index);PlayerPrefs.Save();
        }
        public static Sprite Get(BiofitMode mode,int index,int frame=0)
        {
            string[] names=Names(mode);if(index<0||index>=names.Length)return null;
            string key=mode==BiofitMode.Ball?"V65Ball/"+names[index]:"V65Spider/"+names[index]+"_"+Mathf.Clamp(frame,0,6);
            if(cache.TryGetValue(key,out var result))return result;
            var t=Resources.Load<Texture2D>(Root+key);
            result=t==null?null:Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),96f,0,SpriteMeshType.FullRect);
            cache[key]=result;return result;
        }
        public static Sprite Current(BiofitMode mode,bool running)
        {
            int frame=mode==BiofitMode.Spider&&running?Mathf.FloorToInt(Time.time*14f)%7:0;
            return Get(mode,Selected(mode),frame);
        }
    }
}
