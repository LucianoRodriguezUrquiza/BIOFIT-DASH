using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// BIOFIT presentation/navigation layer for the untouched GeomeTry levels.
    /// Never changes Character/Cube movement, physics, gravity, collisions,
    /// original input processing, BLE thresholds, or firmware.
    /// Resources/BiofitUi graphics are imported from the user's asset pack.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiofitGeometryUX : MonoBehaviour
    {
        private const string Menu = "Main Menu";
        private const string Select = "Level Selection";
        private const string LevelScene = "Level";
        private const string Custom = "Customisation";
        private const string Settings = "Settings";
        private const string PrefSkin = "BIOFIT_GEOMETRY_SKIN_V1";
        private static readonly string[] MapKeys = {
            "BackOnTrack", "JumpyNightmare", "Polargeist",
            "StereoMadness", "WaveVibe"
        };
        private static readonly string[] MapTitles = {
            "NIVEL 1 · CUBO", "NIVEL 2 · BOLA", "NIVEL 3 · SPIDER",
            "NIVEL 4 · TRES MODOS", "NIVEL 5 · TRES MODOS +"
        };
        private static BiofitGeometryUX instance;
        private static readonly FieldInfo CharacterSpriteField =
            typeof(Character).GetField("spriteRenderer",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<int, Sprite> cachedSkins = new Dictionary<int, Sprite>();
        private Canvas canvas;
        private RectTransform canvasRoot;
        private RectTransform safeRoot;
        private RectTransform designStage;
        private Rect lastSafeRect;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private BiofitUiArtwork artwork;
        private TMP_Text connectionLabel;
        private TMP_Text eventLabel;
        private TMP_Text captureLabel;
        private TMP_Text performanceLabel;
        private TMP_Text statusLabel;
        private TMP_Text commandFeedbackLabel;
        private float commandFeedbackUntil;
        private TMP_Text skinLabel;
        private RectTransform skinSelectionOutline;
        private TMP_FontAsset fallbackFont;
        private Texture2D atlas;
        private Texture2D levelBackdrop;
        private Texture2D backIcon;
        private Texture2D pauseIcon;
        private SpriteRenderer cubeRenderer;
        private Cube activeCube;
        private Sprite baseCubeSprite;
        private Color baseCubeTint;
        private float findCubeAt;
        private float refreshStatusAt;
        private float sampledFps;
        private float fpsElapsed;
        private int fpsFrames;
        private int fpsSamples;
        private int skinIndex;
        private int focusLevel;
        private readonly Button[] selectorHotspots = new Button[5];
        private RectTransform selectorContentRoot;
        private RectTransform selectorFocusCard;
        private RectTransform[] separateCards = new RectTransform[5];
        private RawImage selectorFocusArt;
        private TMP_Text focusLabel;
        private float focusSince;
        private readonly Image[] selectorDotFills = new Image[5];
        private Sprite dotSprite;
        private Texture2D dotTexture;
        private bool paused;
        private bool priorPlayMode;
        private float priorTimeScale;
        private string currentScene = "";
        private const int UiSortingOrder = 500;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            string name = SceneManager.GetActiveScene().name;
            if (!IsGeomeTryScene(name)) return;
            var go = new GameObject("BIOFIT | Geometry UI / settings / skins");
            DontDestroyOnLoad(go);
            go.AddComponent<BiofitGeometryUX>();
        }

        private static bool IsGeomeTryScene(string scene)
        {
            return scene == Menu || scene == Select || scene == LevelScene ||
                scene == Custom || scene == Settings || scene == "Credits" ||
                scene == "Level Editor" || scene == "Success" ||
                scene == "Stats" || scene == "Profile";
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            skinIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefSkin, 0), 0, 69);
            atlas = Resources.Load<Texture2D>("BiofitUi/Skins");
            levelBackdrop = Resources.Load<Texture2D>("BiofitUi/LevelSelect");
            backIcon = Resources.Load<Texture2D>("BiofitUi/Back");
            pauseIcon = Resources.Load<Texture2D>("BiofitUi/Pause");
            fallbackFont = TMP_Settings.defaultFontAsset;
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_ANDROID && !UNITY_EDITOR
            // 60 FPS display request only. Do not alter Time.fixedDeltaTime or
            // the original GeomeTry timeScale (1.5), Cube speed or jump forces.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
#endif
        }

        private void Start() { BuildScreen(SceneManager.GetActiveScene().name); }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (artwork != null) { artwork.Dispose(); artwork = null; }
            if (dotSprite != null) Destroy(dotSprite);
            if (dotTexture != null) Destroy(dotTexture);
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (paused) RestorePause();
            BuildScreen(scene.name);
        }

        private void Update()
        {
            if (!IsGeomeTryScene(currentScene)) return;
            float frameDuration = Time.unscaledDeltaTime;
            if (frameDuration > 0f && frameDuration < 1f)
            {
                fpsElapsed += frameDuration;
                fpsFrames++;
                if (fpsElapsed >= .75f)
                {
                    sampledFps = fpsFrames / fpsElapsed;
                    fpsSamples++;
                    fpsElapsed = 0f;
                    fpsFrames = 0;
                }
            }
            // Game View, device rotation and Android cutouts can change the
            // available safe area without invoking another sceneLoaded event.
            UpdateViewport();

            if (currentScene == Select && selectorFocusCard != null)
            {
                // Tiny idle bounce, no yellow overlay or altered game input.
                float t = Time.unscaledTime - focusSince;
                float bounce = Mathf.Sin(t * 5.5f) * 2.5f;
                // Vertical lift ONLY: there is no scaling across neighboring
                // original cards, and the old baked card has been removed.
                selectorFocusCard.anchoredPosition =
                    new Vector2(0f, 7f + bounce);
                selectorFocusCard.localScale = Vector3.one;
            }

            if (currentScene == Settings && commandFeedbackLabel != null &&
                commandFeedbackLabel.text.Length > 0 &&
                Time.unscaledTime >= commandFeedbackUntil)
                commandFeedbackLabel.text = "";

            if (currentScene == Settings && statusLabel != null &&
                Time.unscaledTime >= refreshStatusAt)
            {
                refreshStatusAt = Time.unscaledTime + 0.4f;
                RefreshBleStatus();
            }
            if (currentScene == LevelScene && Time.unscaledTime >= findCubeAt)
            {
                findCubeAt = Time.unscaledTime + 0.2f;
                GameObject avatar = GameObject.FindGameObjectWithTag("Player");
                Cube cube = avatar == null ? null : avatar.GetComponent<Cube>();
                if (cube != activeCube)
                {
                    activeCube = cube;
                    cubeRenderer = cube == null ? null :
                        CharacterSpriteField != null ?
                        CharacterSpriteField.GetValue(cube) as SpriteRenderer : null;
                    baseCubeSprite = cubeRenderer == null ? null : cubeRenderer.sprite;
                    baseCubeTint = cubeRenderer == null ? Color.white : cubeRenderer.color;
                }
            }
        }

        private void LateUpdate()
        {
            // Sprite-only skin. Neither Rigidbody2D nor Character fields change.
            if (currentScene != LevelScene || cubeRenderer == null || activeCube == null ||
                atlas == null || !activeCube.isAlive) return;
            BiofitModeMotor currentMotor = activeCube.GetComponent<BiofitModeMotor>();
            if (currentMotor != null && currentMotor.Mode != BiofitMode.Cube) return;
            Sprite selected = GetSkin(skinIndex);
            if (selected != null && cubeRenderer.sprite != selected)
            {
                cubeRenderer.sprite = selected;
                cubeRenderer.color = Color.white;
            }
        }

        private Sprite GetSkin(int index)
        {
            if (atlas == null || index < 0 || index >= 70) return null;
            if (cachedSkins.TryGetValue(index, out Sprite cached)) return cached;
            int col = index % 10, row = index / 10;
            // The collage is not an exact 128x126 grid. These cuts follow the
            // actual transparent gutters of the user's 1280x887 source art,
            // so a playable skin never includes half of its neighbor.
            int[] xCuts = { 0, 134, 259, 384, 510, 635, 762, 888,
                1012, 1137, 1280 };
            int[] yCutsTopDown = { 0, 139, 262, 385, 509, 636, 755, 887 };
            int x0, x1, y0, y1;
            if (atlas.width == 1280 && atlas.height == 887)
            {
                x0 = xCuts[col];
                x1 = xCuts[col + 1];
                y0 = 887 - yCutsTopDown[row + 1];
                y1 = 887 - yCutsTopDown[row];
            }
            else
            {
                x0 = Mathf.RoundToInt(col * atlas.width / 10f);
                x1 = Mathf.RoundToInt((col + 1) * atlas.width / 10f);
                y0 = Mathf.RoundToInt((6 - row) * atlas.height / 7f);
                y1 = Mathf.RoundToInt((7 - row) * atlas.height / 7f);
            }
            if (y1 <= y0 || x1 <= x0) return null;
            // Match the footprint of the ORIGINAL cuboid sprite in world units.
            float originalWidth = baseCubeSprite == null ? 1f :
                Mathf.Max(0.1f, baseCubeSprite.bounds.size.x);
            float ppu = (x1 - x0) / originalWidth;
            Sprite value = Sprite.Create(atlas,
                new Rect(x0, y0, x1 - x0, y1 - y0),
                new Vector2(0.5f, 0.5f), ppu, 0,
                SpriteMeshType.FullRect);
            cachedSkins[index] = value;
            return value;
        }

        private void SetSkin(int index)
        {
            if (atlas == null) return;
            skinIndex = Mathf.Clamp(index, 0, 69);
            PlayerPrefs.SetInt(PrefSkin, skinIndex);
            PlayerPrefs.Save();
            // Recreate the sprite only after knowing the original cuboid width.
            foreach (Sprite s in cachedSkins.Values) if (s != null) Destroy(s);
            cachedSkins.Clear();
            if (skinLabel != null)
                skinLabel.text = "PERSONAJE " + (skinIndex + 1) + " DE 70  ·  GUARDADO";
            UpdateSkinSelectionOutline();
        }

        private void BuildScreen(string sceneName)
        {
            currentScene = sceneName;
            statusLabel = null;
            commandFeedbackLabel = null;
            commandFeedbackUntil = 0f;
            connectionLabel = null;
            eventLabel = null;
            captureLabel = null;
            performanceLabel = null;
            skinLabel = null;
            skinSelectionOutline = null;
            selectorContentRoot = null;
            selectorFocusCard = null;
            selectorFocusArt = null;
            focusLabel = null;
            for (int i = 0; i < selectorHotspots.Length; i++)
            {
                selectorHotspots[i] = null;
                selectorDotFills[i] = null;
                separateCards[i] = null;
            }
            activeCube = null;
            cubeRenderer = null;
            cachedSkins.Clear();
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
            canvasRoot = null;
            designStage = null;
            safeRoot = null;
            lastScreenWidth = -1;
            if (!IsGeomeTryScene(sceneName)) return;
            if (sceneName == Menu) { ConfigureOriginalMenu(); return; }
            EnsureUiEventSystem();

            GameObject host = new GameObject("BIOFIT overlay canvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.transform.SetParent(transform, false);
            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiSortingOrder;
            canvas.pixelPerfect = false;
            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Scale from HEIGHT, then fit a canonical 1600x900 stage inside the
            // safe area. Fixed UI coordinates are never allowed to overflow.
            scaler.matchWidthOrHeight = 1f;
            RectTransform hostRect = host.GetComponent<RectTransform>();
            // Letterbox gutters on ultrawide phones retain a matching blue
            // background. Never add this background while playing a level:
            // it could cover the original GeomeTry world behind the HUD.
            if (sceneName != LevelScene)
                Fill("Fondo exterior BIOFIT", hostRect, Blue);
            safeRoot = Rect("BIOFIT Android safe area", hostRect,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            designStage = Rect("BIOFIT 16:9 responsive design", safeRoot,
                new Vector2(.5f, .5f), new Vector2(1600f, 900f),
                Vector2.zero);
            canvasRoot = designStage;
            UpdateViewport(true);

            if (sceneName == Select) BuildLevelSelection();
            else if (sceneName == Custom) BuildSkinSelection();
            else if (sceneName == Settings) BuildSettings();
            else if (sceneName == LevelScene) BuildLevelHud();
            else BuildOtherScene(sceneName);
            if (sceneName != Menu) BuildBackButton(sceneName);
        }


        private void UpdateViewport(bool force = false)
        {
            if (safeRoot == null || designStage == null) return;
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Rect area = Screen.safeArea;
            if (!force && lastScreenWidth == width &&
                lastScreenHeight == height && area == lastSafeRect) return;
            lastScreenWidth = width;
            lastScreenHeight = height;
            lastSafeRect = area;
            safeRoot.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
            safeRoot.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
            safeRoot.offsetMin = Vector2.zero;
            safeRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float sx = safeRoot.rect.width / 1600f;
            float sy = safeRoot.rect.height / 900f;
            float scale = Mathf.Clamp(Mathf.Min(sx, sy), .1f, 4f);
            designStage.localScale = new Vector3(scale, scale, 1f);
            designStage.anchoredPosition = Vector2.zero;
        }

        // Some original game scenes do not contain an EventSystem. Creating
        // one only when absent keeps pause/back/settings clickable on Android.
        // It is scene-local (not DontDestroyOnLoad) to avoid duplicates.
        private static void EnsureUiEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("BIOFIT UI EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.Log("BIOFIT UI: EventSystem creado para " +
                SceneManager.GetActiveScene().name);
        }

        private void ConfigureOriginalMenu()
        {
            // Keep the user's original three central main-menu buttons.
            // Hide ONLY the unused bottom controls and credits as requested.
            foreach (string name in new[] {
                "Bottom Buttons Container", "Credits Button", "YouTube Button"
            })
            {
                GameObject item = GameObject.Find(name);
                if (item != null) item.SetActive(false);
            }
            // Crossed-tools central icon = Editor Button. The lower gear
            // Settings Button is part of the deliberately hidden footer.
            // The original scene's serialized button listener targets a
            // UIManager component on a duplicate GameManager prefab. After
            // returning to Main Menu the duplicate is destroyed because the
            // actual GameManager survives via DontDestroyOnLoad. Replace those
            // stale listeners with scene-independent routes.
            BindOriginalMenuButton("Play Button", Select);
            BindOriginalMenuButton("Editor Button", Settings);
            BindOriginalMenuButton("Custom Button", Custom);
        }

        private void BindOriginalMenuButton(string objectName, string targetScene)
        {
            var all = FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button b in all)
            {
                if (b.name != objectName) continue;
                // Remove persistent serialized UnityEvent listeners as well
                // as runtime listeners. RemoveAllListeners alone preserves
                // persistent listeners pointing at destroyed scene objects.
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => Navigate(targetScene));
                b.interactable = true;
                return;
            }
            Debug.LogWarning("BIOFIT UI: botón original no encontrado: " + objectName);
        }

        private static readonly Color Navy = new Color(0.015f, 0.06f, 0.22f, 1f);
        private static readonly Color Blue = new Color(0.03f, 0.21f, 0.58f, 1f);
        private static readonly Color Lime = new Color(0.46f, 0.94f, 0.05f, 1f);
        private static readonly Color Ice = new Color(0.91f, 0.98f, 1f, 1f);

        private RectTransform Rect(string name, Transform parent,
            Vector2 anchor, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform Fill(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private TMP_Text Text(string name, Transform parent, string label,
            Vector2 anchor, Vector2 size, Vector2 offset, int fontSize,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = Rect(name, parent, anchor, size, offset);
            TMP_Text t = rect.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.font = fallbackFont;
            t.fontSize = fontSize;
            t.fontStyle = FontStyles.Bold;
            t.alignment = alignment;
            t.color = color;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            // Arcade lettering: opaque black glyph outline, not plain Unity text.
            var styled = t as TextMeshProUGUI;
            if (styled != null)
            {
                styled.outlineColor = new Color32(5, 9, 18, 255);
                styled.outlineWidth = 0.25f;
            }
            return t;
        }

        private Button Button(string name, Transform parent, string label,
            Vector2 anchor, Vector2 size, Vector2 offset,
            Action click, Color background, int fontSize = 27)
        {
            RectTransform frame = Rect(name, parent, anchor, size, offset);
            Image outer = frame.gameObject.AddComponent<Image>();
            outer.color = background;
            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = outer;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.94f, .94f, .94f, 1f);
            button.colors = colors;
            if (click != null) button.onClick.AddListener(() => click());
            // In Settings the visible frame (including children) physically
            // shrinks on press and gently springs back on release. An invisible
            // overlay/color tint alone cannot animate the opaque nested artwork.
            if (currentScene == Settings && background.a > 0.05f)
                frame.gameObject.AddComponent<BiofitSettingsButtonFeedback>();

            // Arcade beveled button. Transparent click zones remain invisible.
            if (background.a > 0.05f)
            {
                var shadow = frame.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .9f);
                shadow.effectDistance = new Vector2(7f, -9f);
                RectTransform whiteEdge = Fill(name + " white edge", frame, Color.white);
                whiteEdge.offsetMin = new Vector2(4f, 4f);
                whiteEdge.offsetMax = new Vector2(-4f, -4f);
                RectTransform blackEdge = Fill(name + " black edge", whiteEdge,
                    new Color(.02f, .04f, .05f, 1f));
                blackEdge.offsetMin = new Vector2(5f, 5f);
                blackEdge.offsetMax = new Vector2(-5f, -5f);
                RectTransform inside = Fill(name + " lime inset", blackEdge, background);
                inside.offsetMin = new Vector2(5f, 5f);
                inside.offsetMax = new Vector2(-5f, -5f);
                // Top half is brighter, like the original Geometry arcade art.
                RectTransform shine = Fill(name + " shine", inside,
                    new Color(1f, 1f, 1f, .17f));
                shine.anchorMin = new Vector2(0f, .53f);
                shine.anchorMax = Vector2.one;
            }
            if (!string.IsNullOrEmpty(label))
                Text(name + " label", frame, label, new Vector2(.5f, .5f),
                    size - new Vector2(22f, 15f), Vector2.zero, fontSize,
                    Navy);
            return button;
        }

        // Hit region drawn over an already-painted original card or arrow.
        // Never tint it yellow; the separate real-art focus animation handles it.
        private Button Hotspot(string name, Transform parent, Vector2 anchor,
            Vector2 size, Action click)
        {
            RectTransform rect = Rect(name, parent, anchor, size, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, .001f);
            Button b = rect.gameObject.AddComponent<Button>();
            b.targetGraphic = image;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => click());
            return b;
        }

        private RectTransform ArcadePanel(string name, Transform parent,
            Vector2 anchor, Vector2 size, Vector2 offset)
        {
            RectTransform outer = Rect(name, parent, anchor, size, offset);
            var main = outer.gameObject.AddComponent<Image>();
            main.color = new Color(.012f, .025f, .08f, 1f);
            var shadow = outer.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(12, -12);
            RectTransform edge = Fill(name + " bright edge", outer, Color.white);
            edge.offsetMin = new Vector2(5f, 5f);
            edge.offsetMax = new Vector2(-5f, -5f);
            RectTransform black = Fill(name + " dark rim", edge, Navy);
            black.offsetMin = new Vector2(6f, 6f);
            black.offsetMax = new Vector2(-6f, -6f);
            RectTransform inner = Fill(name + " blue center", black, Blue);
            inner.offsetMin = new Vector2(7f, 7f);
            inner.offsetMax = new Vector2(-7f, -7f);
            return inner;
        }

        private Button ImageButton(string name, Transform parent, Texture image,
            string fallback, Vector2 anchor, Vector2 size, Vector2 offset, Action click)
        {
            Button b = Button(name, parent, "", anchor, size, offset,
                click, image == null ? Lime : new Color(1f, 1f, 1f, 0.001f));
            if (image != null)
            {
                var raw = Rect(name + " image", b.transform, new Vector2(.5f, .5f),
                    size, Vector2.zero).gameObject.AddComponent<RawImage>();
                raw.texture = image;
                raw.raycastTarget = false;
            }
            else Text(name + " icon", b.transform, fallback,
                new Vector2(.5f, .5f), size, Vector2.zero, 45, Navy);
            return b;
        }

        private void BuildBackButton(string scene)
        {
            string target = scene == LevelScene ? Select : Menu;
            // Level selector already includes its OWN back-arrow artwork.
            // Add only a touch hit region, never a second image over the first.
            if (scene == Select && levelBackdrop != null)
            {
                Hotspot("VOLVER sobre flecha original", canvasRoot,
                    new Vector2(.051f, .913f), new Vector2(144f, 135f),
                    () => Navigate(target));
                return;
            }
            ImageButton("VOLVER", canvasRoot, backIcon, "<",
                new Vector2(.080f, .902f), new Vector2(88f, 94f),
                Vector2.zero, () => Navigate(target));
        }

        private void Navigate(string scene)
        {
            if (string.IsNullOrEmpty(scene) ||
                !Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("BIOFIT UI: escena no registrada: " + scene);
                return;
            }
            if (paused) RestorePause();
            if (GameManager.Instance != null && currentScene == LevelScene)
                GameManager.Instance.PlayMode = false;
            SceneManager.LoadScene(scene);
        }

        private void StartLevel(int i)
        {
            if (i < 0 || i >= MapKeys.Length) return;
            TextAsset map = BiofitThreeModesBootstrap.Active
                ? Resources.Load<TextAsset>("BiofitThreeModes/" + MapKeys[i])
                : Resources.Load<TextAsset>("maps/BASE/" + MapKeys[i]);
            if (map == null || GameManager.Instance == null)
            {
                Debug.LogError("BIOFIT UI: mapa/GameManager faltante: " + MapKeys[i]);
                return;
            }
            if (paused) RestorePause();
            // Keep the original LoadLevel coroutine and physics unchanged.
            // Clear stale play-state only at the UI boundary on re-entry.
            GameManager.Instance.PlayMode = false;
            GameManager.Instance.LoadLevel(map);
        }

        private Sprite GetDotSprite()
        {
            if (dotSprite != null) return dotSprite;
            dotTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            dotTexture.name = "BIOFIT round indicator mask";
            dotTexture.wrapMode = TextureWrapMode.Clamp;
            dotTexture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y),
                        new Vector2(31.5f, 31.5f));
                    byte a = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(31.5f - d) * 255f);
                    pixels[y * 64 + x] = new Color32(255, 255, 255, a);
                }
            dotTexture.SetPixels32(pixels);
            dotTexture.Apply(false, false);
            dotSprite = Sprite.Create(dotTexture, new Rect(0, 0, 64, 64),
                new Vector2(.5f, .5f), 64f);
            return dotSprite;
        }

        private void BuildArcadeDots(Transform board)
        {
            Sprite sprite = GetDotSprite();
            RectTransform dots = Rect("Marcadores interactivos", board,
                new Vector2(.5f, .143f), new Vector2(425f, 65f), Vector2.zero);
            for (int i = 0; i < selectorDotFills.Length; i++)
            {
                float x = (i - 2) * 75f;
                RectTransform rim = Rect("Borde punto " + i, dots,
                    new Vector2(.5f, .5f), new Vector2(57f, 57f),
                    new Vector2(x, 0f));
                Image black = rim.gameObject.AddComponent<Image>();
                black.sprite = sprite;
                black.color = Color.black;
                black.raycastTarget = false;

                RectTransform inner = Rect("Punto " + i, rim,
                    new Vector2(.5f, .5f), new Vector2(47f, 47f), Vector2.zero);
                Image color = inner.gameObject.AddComponent<Image>();
                color.sprite = sprite;
                color.raycastTarget = false;
                selectorDotFills[i] = color;
            }
        }

        private void ArcadeCorners(Transform parent)
        {
            EnsureArtwork();
            if (artwork == null) return;
            // Four individually cropped, blue-keyed sections from the user's
            // ORIGINAL UI. No mirroring of broken geometry or offscreen halves.
            Vector2[] anchors = {
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f)
            };
            for (int i = 0; i < 4; i++)
            {
                bool right = (i & 1) != 0;
                bool bottom = i >= 2;
                RectTransform corner = Rect("Esquina BIOFIT completa " + i,
                    parent, anchors[i], new Vector2(266f, 264f), Vector2.zero);
                corner.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
                RawImage raw = corner.gameObject.AddComponent<RawImage>();
                raw.texture = artwork.corners[i];
                if (i == 0) raw.uvRect = new Rect(1f, 0f, -1f, 1f);
                raw.raycastTarget = false;
            }
        }

        private bool artworkFailed;
        private void EnsureArtwork()
        {
            if (artwork != null || artworkFailed) return;
            if (!BiofitUiArtwork.Supports(levelBackdrop))
            {
                artworkFailed = true;
                Debug.LogWarning("BIOFIT UX: imagen HD no legible, " +
                    "se usara el fondo original sin animacion individual.");
                return;
            }
            try { artwork = new BiofitUiArtwork(levelBackdrop); }
            catch (Exception exception)
            {
                artworkFailed = true;
                Debug.LogError("BIOFIT UX: fallo al preparar arte independiente: " +
                    exception.Message);
            }
        }

        private void BuildLevelSelection()
        {
            GameObject oldContainer = GameObject.Find("Level Container");
            if (oldContainer != null) oldContainer.SetActive(false);
            Fill("BIOFIT exterior azul", canvasRoot, Blue);
            RectTransform board = Rect("Selector arte 1672x941", canvasRoot,
                new Vector2(.5f, .5f), new Vector2(1600f, 900f), Vector2.zero);
            selectorContentRoot = board;

            EnsureArtwork();

            Texture2D backdrop = artwork != null ?
                artwork.background : levelBackdrop;
            if (backdrop != null)
            {
                RectTransform bg = Rect("BIOFIT fondo limpio sin duplicados", board,
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                bg.anchorMin = Vector2.zero;
                bg.anchorMax = Vector2.one;
                bg.offsetMin = bg.offsetMax = Vector2.zero;
                RawImage art = bg.gameObject.AddComponent<RawImage>();
                art.texture = backdrop;
                art.raycastTarget = false;
            }
            else
            {
                Fill("fondo emergencia", board, Navy);
                Text("titulo", board, "BIOFIT DASH",
                    new Vector2(.5f, .84f), new Vector2(940, 85),
                    Vector2.zero, 64, Lime);
            }

            // A movable card is not a cropped DUPLICATE above a baked card:
            // all five original cards were removed from the backdrop first.
            // Every card has its own transparent graphic and independent touch
            // target. Vertical-only focus lift stays inside the 4px gutters.
            for (int i = 0; i < MapKeys.Length; i++)
            {
                int index = i;
                float cx = (BiofitUiArtwork.CardLeft[i] +
                    BiofitUiArtwork.CardWidths[i] * .5f) /
                    BiofitUiArtwork.ReferenceWidth;
                float cy = 1f - (BiofitUiArtwork.CardTop +
                    (BiofitUiArtwork.CardBottom - BiofitUiArtwork.CardTop) * .5f) /
                    BiofitUiArtwork.ReferenceHeight;
                Vector2 center = new Vector2(cx, cy);
                float width = BiofitUiArtwork.CardWidths[i] * 1600f /
                    BiofitUiArtwork.ReferenceWidth;
                float height = (BiofitUiArtwork.CardBottom - BiofitUiArtwork.CardTop) *
                    900f / BiofitUiArtwork.ReferenceHeight;
                if (artwork != null)
                {
                    RectTransform card = Rect("Tarjeta independiente " + MapKeys[i],
                        board, center, new Vector2(width, height), Vector2.zero);
                    RawImage image = card.gameObject.AddComponent<RawImage>();
                    image.texture = artwork.cards[i];
                    image.raycastTarget = false;
                    separateCards[i] = card;
                    selectorHotspots[i] = Hotspot("Abrir nivel " + MapKeys[i],
                        card, new Vector2(.5f, .5f),
                        new Vector2(width, height), () => StartLevel(index));
                }
                else if (levelBackdrop == null)
                    selectorHotspots[i] = Button("Nivel " + MapKeys[i], board,
                        MapTitles[i], center, new Vector2(width, height),
                        Vector2.zero, () => StartLevel(index), Lime, 29);
                else
                    selectorHotspots[i] = Hotspot("Abrir nivel " + MapKeys[i],
                        board, center, new Vector2(width, height),
                        () => StartLevel(index));
            }

            // These are the ONLY live progress indicators. The old dots were
            // removed from the clean background, not covered by duplicated dots.
            if (artwork != null)
            {
                focusLabel = Text("NIVEL ACTUAL", board, "",
                    new Vector2(.5f, .226f), new Vector2(1110, 73),
                    Vector2.zero, 43, Color.white);
                BuildArcadeDots(board);
            }
            else if (levelBackdrop != null)
                focusLabel = Text("Nivel activo", board, "",
                    new Vector2(.5f, .218f), new Vector2(1020, 67),
                    Vector2.zero, 37, Color.white);

            // Arrow art belongs to the user's original reference screenshot.
            // Do not paint another arrow or copy of the artwork on top of it.
            if (levelBackdrop != null)
            {
                Hotspot("Nivel anterior", board, new Vector2(.041f, .476f),
                    new Vector2(123f, 200f), () => CycleFocus(-1));
                Hotspot("Nivel siguiente", board, new Vector2(.959f, .476f),
                    new Vector2(123f, 200f), () => CycleFocus(1));
            }
            UpdateFocus();
        }

        private void CycleFocus(int offset)
        {
            focusLevel = (focusLevel + offset + MapKeys.Length) % MapKeys.Length;
            UpdateFocus();
        }

        private void UpdateFocus()
        {
            focusSince = Time.unscaledTime;
            if (focusLabel != null)
                focusLabel.text = "NIVEL ACTUAL: " + MapTitles[focusLevel];
            for (int i = 0; i < selectorDotFills.Length; i++)
            {
                if (selectorDotFills[i] != null)
                    selectorDotFills[i].color = i == focusLevel ?
                        new Color(1f, .82f, .02f, 1f) :
                        new Color(.16f, .18f, .22f, 1f);
                if (separateCards[i] != null)
                    separateCards[i].anchoredPosition = Vector2.zero;
            }
            selectorFocusCard = separateCards[focusLevel];
            selectorFocusArt = selectorFocusCard == null ? null :
                selectorFocusCard.GetComponent<RawImage>();
        }

        private void ArcadeLogo(Transform parent)
        {
            EnsureArtwork();
            if (artwork == null) return;
            // Real alpha texture: the blue source background was keyed out,
            // rather than displayed as a different rectangular blue strip.
            RectTransform holder = Rect("BIOFIT DASH logo transparente", parent,
                new Vector2(.5f, .915f), new Vector2(805f, 137f), Vector2.zero);
            RawImage image = holder.gameObject.AddComponent<RawImage>();
            image.texture = artwork.logo;
            image.raycastTarget = false;
        }

        private void BuildSkinSelection()
        {
            Fill("BIOFIT personalizacion azul", canvasRoot, Blue);
            ArcadeCorners(canvasRoot);
            ArcadeLogo(canvasRoot);

            RectTransform title = ArcadePanel("Titulo personalizacion",
                canvasRoot, new Vector2(.5f, .794f),
                new Vector2(710f, 63f), Vector2.zero);
            Text("titulo skin", title, "PERSONALIZAR CUBO",
                new Vector2(.5f, .5f), new Vector2(665f, 53f),
                Vector2.zero, 36, Color.white);

            skinLabel = Text("skin actual", canvasRoot,
                "PERSONAJE " + (skinIndex + 1) + " DE 70",
                new Vector2(.5f, .707f), new Vector2(760f, 40f),
                Vector2.zero, 28, Ice);
            if (atlas == null)
            {
                Text("missing atlas", canvasRoot,
                    "NO SE ENCONTRO SKINS.PNG",
                    new Vector2(.5f, .39f), new Vector2(950f, 160f),
                    Vector2.zero, 32, Ice);
                return;
            }

            // Display the COMPLETE original 1280x887 collage instead of
            // cropping 70 independent RawImages. The source image is not a
            // uniform grid: several outlines spill across calculated cell
            // borders. Displaying one RawImage prevents visible cutoffs.
            // Keep visible margins under the label AND above the bottom edge.
            // Width 790 creates >50px bottom clearance at 1600x900.
            float width = 790f;
            float height = width * atlas.height / atlas.width;
            RectTransform sheet = Rect("70 SKINS SIN RECORTE",
                canvasRoot, new Vector2(.5f, .364f),
                new Vector2(width, height), Vector2.zero);
            RawImage wholeAtlas = sheet.gameObject.AddComponent<RawImage>();
            wholeAtlas.texture = atlas;
            wholeAtlas.raycastTarget = false;
            wholeAtlas.color = Color.white;

            // Each picture is a functional touch zone over the intact atlas.
            for (int i = 0; i < 70; i++)
            {
                int index = i;
                int col = i % 10;
                int row = i / 10;
                Hotspot("Elegir skin " + (i + 1), sheet,
                    new Vector2((col + .5f) / 10f,
                                1f - (row + .5f) / 7f),
                    new Vector2(width / 10f, height / 7f),
                    () => SetSkin(index));
            }

            skinSelectionOutline = Rect("Borde de skin seleccionada",
                sheet, new Vector2(.5f, .5f),
                new Vector2(width / 10f, height / 7f), Vector2.zero);
            float cellW = width / 10f, cellH = height / 7f;
            AddSelectionStroke(skinSelectionOutline, "Superior",
                new Vector2(.5f, 1f), new Vector2(cellW - 6f, 5f),
                new Vector2(0f, -3f));
            AddSelectionStroke(skinSelectionOutline, "Inferior",
                new Vector2(.5f, 0f), new Vector2(cellW - 6f, 5f),
                new Vector2(0f, 3f));
            AddSelectionStroke(skinSelectionOutline, "Izquierdo",
                new Vector2(0f, .5f), new Vector2(5f, cellH - 6f),
                new Vector2(3f, 0f));
            AddSelectionStroke(skinSelectionOutline, "Derecho",
                new Vector2(1f, .5f), new Vector2(5f, cellH - 6f),
                new Vector2(-3f, 0f));
            UpdateSkinSelectionOutline();
        }

        private void AddSelectionStroke(Transform root, string name,
            Vector2 anchor, Vector2 size, Vector2 offset)
        {
            RectTransform stroke = Rect(name, root, anchor, size, offset);
            Image image = stroke.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void UpdateSkinSelectionOutline()
        {
            if (skinSelectionOutline == null) return;
            int col = skinIndex % 10;
            int row = skinIndex / 10;
            skinSelectionOutline.anchorMin = skinSelectionOutline.anchorMax =
                new Vector2((col + .5f) / 10f, 1f - (row + .5f) / 7f);
            skinSelectionOutline.anchoredPosition = Vector2.zero;
        }

        private void BuildSettings()
        {
            Fill("BIOFIT configuracion fondo", canvasRoot, Blue);
            ArcadeCorners(canvasRoot);
            ArcadeLogo(canvasRoot);

            RectTransform heading = ArcadePanel("Titulo ajustes",
                canvasRoot, new Vector2(.5f, .793f),
                new Vector2(1080f, 66f), Vector2.zero);
            Text("Titulo de ajustes", heading, "CONFIGURACIÓN  ·  BILATERAL LEGACY",
                new Vector2(.5f, .5f), new Vector2(1030f, 55f),
                Vector2.zero, 33, Color.white);

            // Four independent, non-overlapping regions, rather than a
            // seven-line dense status dump over one tiny panel.
            RectTransform trackers = ArcadePanel("Conexion BLE",
                canvasRoot, new Vector2(.310f, .614f),
                new Vector2(596f, 164f), Vector2.zero);
            Text("Seccion trackers", trackers, "CONEXIÓN WAVESHARE",
                new Vector2(.5f, .80f), new Vector2(528f, 44f),
                Vector2.zero, 29, Lime);
            connectionLabel = Text("Estado trackers", trackers, "",
                new Vector2(.5f, .38f), new Vector2(536f, 100f),
                Vector2.zero, 25, Color.white);

            RectTransform movements = ArcadePanel("Eventos BLE",
                canvasRoot, new Vector2(.690f, .614f),
                new Vector2(596f, 164f), Vector2.zero);
            Text("Seccion movimientos", movements, "DETECCIÓN DE MOVIMIENTOS",
                new Vector2(.5f, .80f), new Vector2(528f, 44f),
                Vector2.zero, 26, Lime);
            eventLabel = Text("Pares y saltos", movements, "",
                new Vector2(.5f, .38f), new Vector2(526f, 100f),
                Vector2.zero, 26, Color.white);

            RectTransform captures = ArcadePanel("Captura y archivos",
                canvasRoot, new Vector2(.310f, .411f),
                new Vector2(596f, 139f), Vector2.zero);
            Text("Seccion captura", captures, "REGISTRO Y EXPORTACIÓN",
                new Vector2(.5f, .83f), new Vector2(530f, 39f),
                Vector2.zero, 27, Lime);
            captureLabel = Text("Estado capturas", captures, "",
                new Vector2(.5f, .54f), new Vector2(530f, 44f),
                Vector2.zero, 22, Color.white);
            statusLabel = Text("Aviso captura", captures, "",
                new Vector2(.5f, .22f), new Vector2(530f, 48f),
                Vector2.zero, 18, Ice);

            // Space between the heading and status cards: brief acknowledgement,
            // distinct from CaptureBanner which reports actual file/BLE state.
            commandFeedbackLabel = Text("Confirmacion boton", canvasRoot, "",
                new Vector2(.5f, .731f), new Vector2(1140f, 32f),
                Vector2.zero, 20, Ice);

            RectTransform performance = ArcadePanel("FPS medicion",
                canvasRoot, new Vector2(.690f, .411f),
                new Vector2(596f, 139f), Vector2.zero);
            Text("Seccion rendimiento", performance, "RENDIMIENTO",
                new Vector2(.5f, .83f), new Vector2(530f, 39f),
                Vector2.zero, 27, Lime);
            performanceLabel = Text("FPS y entorno", performance, "",
                new Vector2(.5f, .42f), new Vector2(538f, 86f),
                Vector2.zero, 21, Color.white);

            Button("Start capture", canvasRoot, "INICIAR REGISTRO",
                new Vector2(.310f, .269f), new Vector2(430f, 66f),
                Vector2.zero, StartCapture, Lime, 27);
            Button("Stop capture", canvasRoot, "FINALIZAR",
                new Vector2(.690f, .269f), new Vector2(430f, 66f),
                Vector2.zero, StopCapture, Lime, 27);
            Button("RAW", canvasRoot, "RAW",
                new Vector2(.205f, .167f), new Vector2(305f, 64f),
                Vector2.zero, ToggleRawCapture, Lime, 28);
            Button("SYNC", canvasRoot, "MARCAR SYNC",
                new Vector2(.500f, .167f), new Vector2(305f, 64f),
                Vector2.zero, MarkSync, Lime, 27);
            Button("Downloads", canvasRoot, "DESCARGAS",
                new Vector2(.795f, .167f), new Vector2(305f, 64f),
                Vector2.zero, () => ExportCapture(false), Lime, 27);
            Button("Export SAF", canvasRoot, "GUARDAR ARCHIVOS (SAF)",
                new Vector2(.500f, .078f), new Vector2(620f, 62f),
                Vector2.zero, () => ExportCapture(true), Lime, 27);
            RefreshBleStatus();
        }

        private static BiofitBLEMonitor GetMonitor()
        {
            return FindFirstObjectByType<BiofitBLEMonitor>();
        }

        private void ShowCommandFeedback(string message)
        {
            // Visual feedback confirms only that the UI received the command.
            // The persistent CaptureBanner confirms saving/export completion.
            if (commandFeedbackLabel != null)
            {
                commandFeedbackLabel.text = message;
                commandFeedbackUntil = Time.unscaledTime + 2.2f;
            }
            RefreshBleStatus();
        }

        private void StartCapture()
        {
            BiofitBLEMonitor monitor = GetMonitor();
            if (monitor == null)
            {
                ShowCommandFeedback("REGISTRO NO INICIADO: RECEPTOR BLE AUSENTE");
                return;
            }
            bool started = monitor.StartGameCapture();
            if (started)
                monitor.RecordControlDiagnostic("BIOFIT UI GAME=GEOMETRY INPUT=BILATERAL_LEGACY");
            ShowCommandFeedback(started ? "REGISTRO INICIADO · CAPTURA ACTIVA" :
                "REGISTRO NO INICIADO · REVISÁ ESTADO");
        }

        private void StopCapture()
        {
            BiofitBLEMonitor monitor = GetMonitor();
            bool stopped = monitor != null && monitor.StopGameCapture();
            ShowCommandFeedback(stopped ? "FINALIZANDO CSV · ESPERÁ ARCHIVO GUARDADO" :
                "NO SE FINALIZÓ NINGUNA CAPTURA");
        }

        private void ToggleRawCapture()
        {
            BiofitBLEMonitor monitor = GetMonitor();
            if (monitor == null)
            {
                ShowCommandFeedback("RAW: RECEPTOR BLE AUSENTE");
                return;
            }
            monitor.ToggleRawCapture();
            ShowCommandFeedback(monitor.RawStatus);
        }

        private void MarkSync()
        {
            BiofitBLEMonitor monitor = GetMonitor();
            if (monitor == null || !monitor.GameCaptureActive)
            {
                ShowCommandFeedback("SYNC NO MARCADO · INICIÁ REGISTRO");
                return;
            }
            string previous = monitor.SyncLabel;
            monitor.MarkVideoSync();
            ShowCommandFeedback(monitor.SyncLabel != previous ?
                monitor.SyncLabel + " MARCADO" : "SYNC NO MARCADO");
        }

        private void ExportCapture(bool saf)
        {
            BiofitBLEMonitor monitor = GetMonitor();
            if (monitor == null)
            {
                ShowCommandFeedback("EXPORTACIÓN NO SOLICITADA · SIN RECEPTOR");
                return;
            }
            bool blocked = monitor.GameCaptureActive || monitor.CaptureSaving ||
                monitor.ExportBusy;
            monitor.RequestExport(saf);
            ShowCommandFeedback(blocked ? "EXPORTACIÓN NO DISPONIBLE · FINALIZÁ PRIMERO" :
                (saf ? "SAF" : "DESCARGAS") +
                " SOLICITADA · ESPERÁ CONFIRMACIÓN");
        }

        private void RefreshBleStatus()
        {
            if (connectionLabel == null || eventLabel == null ||
                captureLabel == null || performanceLabel == null ||
                statusLabel == null) return;
            var m = GetMonitor();
            var bridge = FindFirstObjectByType<BiofitGeomeTryLegacyBridge>();
            if (m == null)
            {
                connectionLabel.text = "RECEPTOR BLE SIN INICIAR";
                eventLabel.text = "PARES: 0\nSALTOS CUBO: 0";
                captureLabel.text = "CAPTURA: INACTIVA  |  RAW: OFF";
                statusLabel.text = "Reabrí Main Menu para iniciar la conexión.";
            }
            else
            {
                connectionLabel.text =
                    "TRACKERS: " + (m.BothTrackersConnected ?
                        "2 / 2 CONECTADOS" : "ESPERANDO 2 TRACKERS") +
                    "\nRELOJES: " + (m.ClockAlignmentReady ?
                        "SINCRONIZADOS" : "ESPERANDO STATUS");
                eventLabel.text =
                    "PARES RECIBIDOS: " + (bridge == null ?
                        "0" : bridge.AcceptedPairs.ToString()) +
                    (BiofitThreeModesBootstrap.Active ? "\nACCIONES PERSONAJE: " : "\nSALTOS CUBO: ") + (bridge == null ?
                        "0" : bridge.ExecutedJumps.ToString());
                captureLabel.text =
                    "CAPTURA: " + (m.GameCaptureActive ? "ACTIVA" : "INACTIVA") +
                    "    |    " + m.RawButtonLabel;
                statusLabel.text = m.CaptureBanner;
            }
#if UNITY_EDITOR
            // An uncapped Editor can report 300-500+ FPS even if the actual
            // Android phone cannot sustain 60. This value is not a device test.
            performanceLabel.text = "EDITOR · FPS observados: " +
                (fpsSamples == 0 ? "calculando..." :
                 Mathf.RoundToInt(sampledFps).ToString()) +
                "\nNO equivale a FPS de la APK";
#else
            performanceLabel.text = "ANDROID · FPS observados: " +
                (fpsSamples == 0 ? "calculando..." :
                 Mathf.RoundToInt(sampledFps).ToString()) +
                "\nObjetivo solicitado: 60 FPS (no garantizados)";
#endif
        }

        private void BuildOtherScene(string scene)
        {
            Fill(scene + " background", canvasRoot, Blue);
            ArcadeCorners(canvasRoot);
            Text(scene + " title", canvasRoot, scene.ToUpperInvariant(),
                new Vector2(.5f, .7f), new Vector2(990, 110), Vector2.zero, 54, Lime);
            Text(scene + " body", canvasRoot,
                "VOLVER AL MENU PRINCIPAL",
                new Vector2(.5f, .45f), new Vector2(700, 70), Vector2.zero, 28, Ice);
        }

        private void BuildLevelHud()
        {
            ImageButton("Pause game", canvasRoot, pauseIcon, "Ⅱ",
                new Vector2(.948f, .884f), new Vector2(119, 123),
                Vector2.zero, TogglePause);
        }

        private void TogglePause()
        {
            if (currentScene != LevelScene) return;
            if (paused) { RestorePause(); BuildScreen(LevelScene); }
            else { EnterPause(); BuildPauseScreen(); }
        }

        private void EnterPause()
        {
            paused = true;
            priorTimeScale = Time.timeScale;
            priorPlayMode = GameManager.Instance != null &&
                GameManager.Instance.PlayMode;
            if (GameManager.Instance != null) GameManager.Instance.PlayMode = false;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void RestorePause()
        {
            if (!paused) return;
            paused = false;
            AudioListener.pause = false;
            Time.timeScale = priorTimeScale > 0f ? priorTimeScale : 1.5f;
            if (GameManager.Instance != null)
                GameManager.Instance.PlayMode = priorPlayMode;
        }

        private void BuildPauseScreen()
        {
            // Unlike the original project's PauseManager, this overlay does not
            // rewrite the 1.5x gameplay speed. It restores the exact prior value.
            RectTransform veil = Fill("pause dimmer", canvasRoot,
                new Color(0f, 0f, 0f, .77f));
            Button("RESUME", veil, "CONTINUAR",
                new Vector2(.5f, .63f), new Vector2(390, 84),
                Vector2.zero, () => { RestorePause(); BuildScreen(LevelScene); },
                Lime, 36);
            Button("RESTART", veil, "REINICIAR NIVEL",
                new Vector2(.5f, .47f), new Vector2(390, 84),
                Vector2.zero, RestartLevel, Ice, 31);
            Button("BACK SELECT", veil, "SELECCION DE NIVELES",
                new Vector2(.5f, .31f), new Vector2(500, 84),
                Vector2.zero, () => Navigate(Select), Ice, 29);
            Text("pause title", veil, "PAUSA",
                new Vector2(.5f, .80f), new Vector2(560, 85),
                Vector2.zero, 60, Lime);
            // Back remains visible and functional even over the pause dimmer.
            BuildBackButton(LevelScene);
        }

        private void RestartLevel()
        {
            if (paused) RestorePause();
            if (GameManager.Instance != null)
                GameManager.Instance.PlayMode = true;
            SceneManager.LoadScene(LevelScene);
        }
    }
}
