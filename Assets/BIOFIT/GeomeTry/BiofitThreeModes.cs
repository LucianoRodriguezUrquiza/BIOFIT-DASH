using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biofit.GeomeTryOriginal
{
    public enum BiofitMode { Cube, Ball, Spider }

    // Self-contained experimental modes. Nothing is modified inside the pinned GeomeTry submodule.
    // Maintains Character (Cube) on the original player for death, respawn, camera and progress.
    public sealed class BiofitThreeModesBootstrap : MonoBehaviour
    {
        private GameObject avatar;
        private GameObject roof;
        private bool installed;
        public static bool Active =>
            Application.isEditor || Application.identifier == "com.biofitgame.threemodeslab";

        private void Awake()
        {
            if (!Active) { enabled = false; return; }
            Register("biofitBallPortal", BiofitMode.Ball);
            Register("biofitSpiderPortal", BiofitMode.Spider);
            Register("biofitCubePortal", BiofitMode.Cube);
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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            avatar = null;
            roof = null;
            installed = false;
            if (scene.name == "Level") StartCoroutine(SetupAfterOriginalGame());
        }

        private IEnumerator SetupAfterOriginalGame()
        {
            // Game.Start() creates the actual Cube and original floor after sceneLoaded.
            yield return null;
            Level level = GameManager.Instance == null ? null : GameManager.Instance.SelectedLevel;
            if (level == null) yield break;
            installed = true;
            if (level.Id == 1) yield break;
            GameObject ceilingPrefab = Resources.Load<GameObject>("Prefabs/Decors/CeilPrefab");
            if (ceilingPrefab == null) { Debug.LogError("BIOFIT Three Modes: falta CeilPrefab."); yield break; }
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
            int id = GameManager.Instance?.SelectedLevel?.Id ?? 1;
            motor.SetMode(id == 2 ? BiofitMode.Ball : id == 3 ? BiofitMode.Spider : BiofitMode.Cube);
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
            if (motor != null) motor.SetMode(Target);
        }
    }

    public sealed class BiofitModeMotor : MonoBehaviour
    {
        // Original Cube body, gravity and jump code are never changed.
        // Only when in Ball/Spider mode, Cube MonoBehaviour is disabled
        // and this adapter moves the same Rigidbody2D player horizontally.
        private const float OriginalSpeed = 6.5f;
        private const float BallSpiderGravity = 4f;
        private const float SurfaceRay = .92f;
        private const int GroundMask = 1 << 3;
        private Cube cube;
        private Rigidbody2D body;
        private Collider2D playerCollider;
        private SpriteRenderer icon;
        private Sprite originalSprite;
        private Color originalTint;
        private bool initialized;
        private bool flipped;
        private float lastManualAction;
        public BiofitMode Mode { get; private set; } = BiofitMode.Cube;

        private void Initialize()
        {
            if (initialized) return;
            cube = GetComponent<Cube>();
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<Collider2D>();
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in sprites)
                if (sr.sprite != null) { icon = sr; break; }
            if (icon == null && sprites.Length > 0) icon = sprites[0];
            if (icon != null) { originalSprite = icon.sprite; originalTint = icon.color; }
            initialized = cube != null && body != null && icon != null;
        }

        public void SetMode(BiofitMode newMode)
        {
            Initialize();
            if (!initialized || !cube.isAlive || newMode == Mode) return;
            Mode = newMode;
            flipped = false;
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            body.gravityScale = BallSpiderGravity;
            if (newMode == BiofitMode.Cube)
            {
                cube.enabled = true;
                icon.sprite = originalSprite;
                icon.color = originalTint;
                icon.flipY = false;
                icon.transform.localRotation = Quaternion.identity;
            }
            else
            {
                cube.enabled = false;
                icon.sprite = BiofitThreeModeArt.Make(newMode, false);
                icon.color = Color.white;
                icon.flipY = false;
                icon.transform.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (!initialized || Mode == BiofitMode.Cube || !cube.isAlive ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) TryPulse();
        }

        private void FixedUpdate()
        {
            if (!initialized || Mode == BiofitMode.Cube || !cube.isAlive ||
                GameManager.Instance == null || !GameManager.Instance.PlayMode) return;
            // Equivalent to the original Character.FixedUpdate horizontal motion.
            transform.Translate(new Vector2(OriginalSpeed * Time.deltaTime, 0f), Space.World);
            if (Mode == BiofitMode.Ball)
            {
                icon.transform.Rotate(0, 0,
                    (flipped ? 1f : -1f) * OriginalSpeed * Time.deltaTime * 57.2958f);
            }
        }

        private bool SurfaceContact(Vector2 direction)
        {
            Vector2 center = body.position;
            foreach (float x in new[] { -.28f, 0f, .28f })
            {
                if (Physics2D.Raycast(center + new Vector2(x, 0f),
                    direction, SurfaceRay, GroundMask).collider != null) return true;
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
                body.position, destinationDirection, 16f, GroundMask);
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
        private static Sprite ball, spider, cubePortal, ballPortal, spiderPortal;
        public static Sprite Make(BiofitMode mode, bool portal)
        {
            if (!portal)
            {
                if (mode == BiofitMode.Ball) return ball ?? (ball = Build(mode, false));
                return spider ?? (spider = Build(mode, false));
            }
            if (mode == BiofitMode.Cube) return cubePortal ?? (cubePortal = Build(mode, true));
            if (mode == BiofitMode.Ball) return ballPortal ?? (ballPortal = Build(mode, true));
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
    }
}
