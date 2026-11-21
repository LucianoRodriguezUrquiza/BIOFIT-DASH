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
            "BACK ON TRACK", "JUMPY NIGHTMARE", "POLARGEIST",
            "STEREO MADNESS", "WAVE VIBE"
        };
        private static BiofitGeometryUX instance;
        private static readonly FieldInfo CharacterSpriteField =
            typeof(Character).GetField("spriteRenderer",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<int, Sprite> cachedSkins = new Dictionary<int, Sprite>();
        private Canvas canvas;
        private RectTransform canvasRoot;
        private TMP_Text statusLabel;
        private TMP_Text skinLabel;
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
        private float averageFps = 60f;
        private int skinIndex;
        private int focusLevel;
        private readonly Button[] selectorHotspots = new Button[5];
        private RectTransform selectorContentRoot;
        private RectTransform selectorFocusCard;
        private RawImage selectorFocusArt;
        private TMP_Text focusLabel;
        private float focusSince;
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
            if (Time.unscaledDeltaTime > 0.0001f)
                averageFps = Mathf.Lerp(averageFps,
                    1f / Time.unscaledDeltaTime, 0.075f);

            if (currentScene == Select && selectorFocusCard != null)
            {
                // Tiny idle bounce, no yellow overlay or altered game input.
                float t = Time.unscaledTime - focusSince;
                float bounce = Mathf.Sin(t * 5.5f) * 2.5f;
                selectorFocusCard.anchoredPosition = new Vector2(0f, 9f + bounce);
                selectorFocusCard.localScale = Vector3.one *
                    (1.045f + Mathf.Sin(t * 4f) * 0.007f);
            }

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
            int x0 = Mathf.RoundToInt(col * atlas.width / 10f);
            int x1 = Mathf.RoundToInt((col + 1) * atlas.width / 10f);
            int y0 = Mathf.RoundToInt((6 - row) * atlas.height / 7f);
            int y1 = Mathf.RoundToInt((7 - row) * atlas.height / 7f);
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
                skinLabel.text = "PERSONAJE " + (skinIndex + 1) + " / 70  -  GUARDADO";
        }

        private void BuildScreen(string sceneName)
        {
            currentScene = sceneName;
            statusLabel = null;
            skinLabel = null;
            selectorContentRoot = null;
            selectorFocusCard = null;
            selectorFocusArt = null;
            focusLabel = null;
            for (int i = 0; i < selectorHotspots.Length; i++)
                selectorHotspots[i] = null;
            activeCube = null;
            cubeRenderer = null;
            cachedSkins.Clear();
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
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
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = host.GetComponent<RectTransform>();

            if (sceneName == Select) BuildLevelSelection();
            else if (sceneName == Custom) BuildSkinSelection();
            else if (sceneName == Settings) BuildSettings();
            else if (sceneName == LevelScene) BuildLevelHud();
            else BuildOtherScene(sceneName);
            if (sceneName != Menu) BuildBackButton(sceneName);
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
            BindOriginalMenuButton("Editor Button", Settings);
            BindOriginalMenuButton("Custom Button", Custom);
            // Do not replace the original Play button or navigation.
        }

        private void BindOriginalMenuButton(string objectName, string targetScene)
        {
            var all = FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button b in all)
            {
                if (b.name != objectName) continue;
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
                    Color.white);
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
            Transform parent = (scene == Select && selectorContentRoot != null) ?
                selectorContentRoot : canvasRoot;
            ImageButton("VOLVER", parent, backIcon, "<",
                new Vector2(.055f, .90f), new Vector2(98, 105),
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
            TextAsset map = Resources.Load<TextAsset>("maps/BASE/" + MapKeys[i]);
            if (map == null || GameManager.Instance == null)
            {
                Debug.LogError("BIOFIT UI: mapa/GameManager faltante: " + MapKeys[i]);
                return;
            }
            GameManager.Instance.LoadLevel(map);
        }

        private void BuildLevelSelection()
        {
            GameObject oldContainer = GameObject.Find("Level Container");
            if (oldContainer != null) oldContainer.SetActive(false);
            Fill("BIOFIT blue exterior", canvasRoot, Blue);

            // Preserve the original 1672x941 artwork's 16:9 aspect ratio.
            // Never stretch a 16:9 screenshot across 18:9/20:9 phones.
            RectTransform board = Rect("LEVEL BOARD 16:9", canvasRoot,
                new Vector2(.5f, .5f), new Vector2(1600f, 900f), Vector2.zero);
            var fitter = board.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1672f / 941f;
            selectorContentRoot = board;

            if (levelBackdrop != null)
            {
                RectTransform bg = Rect("BIOFIT DASH HD art", board,
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
                bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
                var art = bg.gameObject.AddComponent<RawImage>();
                art.texture = levelBackdrop;
                art.raycastTarget = false;
            }
            else
            {
                Fill("fallback navy", board, Navy);
                Text("fallback title", board, "BIOFIT DASH",
                    new Vector2(.5f, .84f), new Vector2(900, 100),
                    Vector2.zero, 65, Lime);
            }

            // Five real hit regions, aligned with cards baked into the art.
            // Card focus uses a pixel-perfect crop of the SAME reference art.
            float[] xs = { .165f, .329f, .493f, .658f, .822f };
            for (int i = 0; i < MapKeys.Length; i++)
            {
                int slot = i;
                if (levelBackdrop != null)
                    selectorHotspots[i] = Hotspot("Abrir " + MapKeys[i], board,
                        new Vector2(xs[i], .475f), new Vector2(252f, 315f),
                        () => StartLevel(slot));
                else
                    selectorHotspots[i] = Button("Nivel " + MapKeys[i], board,
                        MapTitles[i], new Vector2(xs[i], .475f),
                        new Vector2(244f, 300f), Vector2.zero,
                        () => StartLevel(slot), Lime, 31);
            }

            if (levelBackdrop != null && levelBackdrop.width >= 1600)
            {
                // Cover the static heading with a matching movable name plate.
                RectTransform plate = ArcadePanel("NIVEL ACTUAL", board,
                    new Vector2(.5f, .223f), new Vector2(920f, 78f), Vector2.zero);
                focusLabel = Text("nombre nivel", plate, "", new Vector2(.5f, .5f),
                    new Vector2(880f, 65f), Vector2.zero, 37, Color.white);

                // Repaint the five baked indicator dots with stateful ones.
                RectTransform dots = Rect("marcadores", board,
                    new Vector2(.5f, .141f), new Vector2(415f, 75f), Vector2.zero);
                var cover = dots.gameObject.AddComponent<Image>();
                cover.color = new Color(.011f, .15f, .59f, 1f);
                cover.raycastTarget = false;

                // Artwork crop allows small focus animations without resampling
                // each of the five cards from a low resolution texture.
                selectorFocusCard = Rect("CARTA SELECCIONADA", board,
                    new Vector2(xs[0], .475f), new Vector2(262f, 306f),
                    Vector2.zero);
                selectorFocusCard.SetSiblingIndex(board.childCount - 1);
                selectorFocusArt = selectorFocusCard.gameObject.AddComponent<RawImage>();
                selectorFocusArt.texture = levelBackdrop;
                selectorFocusArt.raycastTarget = false;
                selectorFocusArt.color = Color.white;
            }
            if (levelBackdrop != null)
            {
                Hotspot("Anterior", board, new Vector2(.043f, .48f),
                    new Vector2(115f, 180f), () => CycleFocus(-1));
                Hotspot("Siguiente", board, new Vector2(.957f, .48f),
                    new Vector2(115f, 180f), () => CycleFocus(1));
                UpdateFocus();
            }
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

            if (selectorFocusCard == null || selectorFocusArt == null)
                return;
            float[] xs = { .165f, .329f, .493f, .658f, .822f };
            int[] cardLeft = { 138, 413, 687, 962, 1235 };
            selectorFocusCard.anchorMin = selectorFocusCard.anchorMax =
                new Vector2(xs[focusLevel], .475f);
            selectorFocusCard.anchoredPosition = new Vector2(0, 9);
            selectorFocusCard.localScale = Vector3.one * 1.045f;
            // Cropped texture coordinates are bottom-up.
            float tw = levelBackdrop.width, th = levelBackdrop.height;
            selectorFocusArt.uvRect = new Rect(
                cardLeft[focusLevel] / tw, (941f - 649f) / th,
                270f / tw, 306f / th);
        }

        private void BuildSkinSelection()
        {
            Fill("skins background", canvasRoot, Navy);
            Text("skins title", canvasRoot, "PERSONALIZAR CUBO",
                new Vector2(.5f, .94f), new Vector2(740, 65),
                Vector2.zero, 47, Lime);
            skinLabel = Text("current skin", canvasRoot,
                "PERSONAJE " + (skinIndex + 1) + " / 70",
                new Vector2(.5f, .84f), new Vector2(780, 48),
                Vector2.zero, 26, Ice);
            if (atlas == null)
            {
                Text("missing atlas", canvasRoot,
                    "FALTA EL PACK DE IMAGENES BIOFIT.\n" +
                    "Copia Assets/BIOFIT/Resources/BiofitUi/Skins.png\n" +
                    "desde BIOFIT_GeomeTry_UI_Assets.zip.",
                    new Vector2(.5f, .52f), new Vector2(950, 230),
                    Vector2.zero, 28, Ice);
                return;
            }
            RectTransform grid = Rect("70 skins grid", canvasRoot,
                new Vector2(.5f, .43f), new Vector2(1075, 570), Vector2.zero);
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(93f, 75f);
            layout.spacing = new Vector2(11f, 4f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 10;
            for (int i = 0; i < 70; i++)
            {
                int index = i;
                var obj = new GameObject("Skin " + i,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                obj.transform.SetParent(grid, false);
                Image frame = obj.GetComponent<Image>();
                frame.color = i == skinIndex ? Lime : Blue;
                var b = obj.GetComponent<Button>();
                b.targetGraphic = frame;
                b.onClick.AddListener(() => { SetSkin(index); BuildScreen(Custom); });
                var img = new GameObject("original atlas tile", typeof(RectTransform),
                    typeof(RawImage));
                img.transform.SetParent(obj.transform, false);
                RectTransform rect = img.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4f, 3f);
                rect.offsetMax = new Vector2(-4f, -3f);
                RawImage raw = img.GetComponent<RawImage>();
                raw.texture = atlas;
                raw.uvRect = new Rect((i % 10) / 10f,
                    (6 - i / 10) / 7f, .1f, 1f / 7f);
                raw.raycastTarget = false;
            }
        }

        private void BuildSettings()
        {
            Fill("settings background", canvasRoot, Navy);
            Text("settings title", canvasRoot, "CONFIGURACION · BIOFIT",
                new Vector2(.5f, .92f), new Vector2(1000, 78),
                Vector2.zero, 52, Lime);
            Text("ble subtitle", canvasRoot, "SENSORES IMU · BLUETOOTH BILATERAL LEGACY",
                new Vector2(.5f, .79f), new Vector2(1200, 55),
                Vector2.zero, 30, Ice);
            statusLabel = Text("ble state", canvasRoot, "Cargando estado...",
                new Vector2(.5f, .62f), new Vector2(1220, 210),
                Vector2.zero, 27, Ice);
            Button("Start capture", canvasRoot, "INICIAR REGISTRO",
                new Vector2(.27f, .38f), new Vector2(350, 66),
                Vector2.zero, StartCapture, Lime, 24);
            Button("Stop capture", canvasRoot, "FINALIZAR",
                new Vector2(.73f, .38f), new Vector2(350, 66),
                Vector2.zero, () => GetMonitor()?.StopGameCapture(), Lime, 24);
            Button("RAW", canvasRoot, "RAW",
                new Vector2(.2f, .24f), new Vector2(240, 65),
                Vector2.zero, () => GetMonitor()?.ToggleRawCapture(), Ice, 25);
            Button("SYNC", canvasRoot, "MARCAR SYNC",
                new Vector2(.5f, .24f), new Vector2(240, 65),
                Vector2.zero, () => GetMonitor()?.MarkVideoSync(), Ice, 25);
            Button("Downloads", canvasRoot, "DESCARGAS",
                new Vector2(.8f, .24f), new Vector2(240, 65),
                Vector2.zero, () => GetMonitor()?.RequestExport(false), Ice, 25);
            Button("Export SAF", canvasRoot, "GUARDAR ARCHIVOS (SAF)",
                new Vector2(.5f, .12f), new Vector2(560, 64),
                Vector2.zero, () => GetMonitor()?.RequestExport(true), Lime, 24);
            RefreshBleStatus();
        }

        private static BiofitBLEMonitor GetMonitor()
        {
            return FindFirstObjectByType<BiofitBLEMonitor>();
        }

        private void StartCapture()
        {
            BiofitBLEMonitor monitor = GetMonitor();
            if (monitor != null && monitor.StartGameCapture())
                monitor.RecordControlDiagnostic("BIOFIT UI GAME=GEOMETRY INPUT=BILATERAL_LEGACY");
        }

        private void RefreshBleStatus()
        {
            if (statusLabel == null) return;
            var m = GetMonitor();
            var bridge = FindFirstObjectByType<BiofitGeomeTryLegacyBridge>();
            if (m == null)
            {
                statusLabel.text = "Receptor BLE no iniciado.\n" +
                    "Volvé a Main Menu para iniciar la conexion automatica.";
                return;
            }
            statusLabel.text =
                "WAVESHARE: " + (m.BothTrackersConnected ? "2/2 CONECTADAS" : "ESPERANDO AMBOS TRACKERS") +
                "\nRELOJES: " + (m.ClockAlignmentReady ? "SINCRONIZADOS" : "ESPERANDO STATUS") +
                "\nPARES: " + (bridge == null ? "0" : bridge.AcceptedPairs.ToString()) +
                "  |  SALTOS CUBO: " + (bridge == null ? "0" : bridge.ExecutedJumps.ToString()) +
                "\nCAPTURA: " + (m.GameCaptureActive ? "ACTIVA" : "INACTIVA") +
                "  |  " + m.RawButtonLabel +
                "\nFPS ACTUALES (ESTIMACION): " + Mathf.RoundToInt(averageFps) +
                "\n" + m.CaptureBanner;
        }

        private void BuildOtherScene(string scene)
        {
            Fill(scene + " background", canvasRoot, Navy);
            Text(scene + " title", canvasRoot, scene.ToUpperInvariant(),
                new Vector2(.5f, .7f), new Vector2(990, 110), Vector2.zero, 54, Lime);
            Text(scene + " body", canvasRoot,
                "VOLVER AL MENU PRINCIPAL",
                new Vector2(.5f, .45f), new Vector2(700, 70), Vector2.zero, 28, Ice);
        }

        private void BuildLevelHud()
        {
            ImageButton("Pause game", canvasRoot, pauseIcon, "Ⅱ",
                new Vector2(.95f, .89f), new Vector2(97, 97),
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
