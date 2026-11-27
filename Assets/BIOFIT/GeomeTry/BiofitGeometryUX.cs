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
        private static readonly int[] CooldownOptionsMs = { 900, 800, 700, 600, 500, 400 };
        private const string PrefSkin = "BIOFIT_GEOMETRY_SKIN_V1";
        private static readonly string[] MapKeys = {
            "BackOnTrack", "JumpyNightmare", "Polargeist",
            "StereoMadness", "WaveVibe"
        };
        private static readonly string[] MapTitles = {
            "STEREO MADNESS", "BACK ON TRACK", "POLARGEIST", "CAN'T LET GO", "JUMPER"
        };
        private static readonly string[] MapModes = {
            "CUBO / BOLA / SPIDER", "CUBO / BOLA", "CUBO / SPIDER", "CUBO / BOLA", "CUBO / BOLA / SPIDER"
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
        private TMP_Text trackerLeftLabel,trackerRightLabel,settingsTimer,settingsDetails,settingsSystem,settingsMainText,settingsRawText,settingsCooldownText;
        private Button settingsMainButton,settingsRawButton,settingsSyncButton,settingsReexportButton,settingsSafButton;
        private TMP_Text captureLabel;
        private TMP_Text statusLabel;
        private TMP_Text commandFeedbackLabel;
        private TMP_Text cooldownValueLabel;
        private TMP_Text cooldownHudLabel;
        private TMP_Text stereoCoinHudLabel;
        private readonly Button[] cooldownOptionButtons = new Button[6];
        private bool cooldownPage;
        private bool bmdPage;
        private int bmdSelected;
        private TMP_Text bmdStatusLabel,bmdSelectedLabel,bmdPauseLabel;
        private RectTransform bmdProgressFill;
        private readonly Button[] bmdOptions=new Button[4];
        private float bmdRefreshAt;
        // Discovery only: listen to legacy Unity input while the Joystick diagnostic
        // page is open. No gameplay binding is changed until Android HID codes are known.
        private Button connectTrackersButton;
        private bool joystickPage;
        private TMP_Text joystickDevicesLabel;
        private TMP_Text joystickEventsLabel;
        private TMP_Text joystickAxesLabel;
        private readonly List<string> joystickHistory = new List<string>();
        private int joystickEventCount;
        private float joystickRefreshAt;
        private float refreshCooldownAt;
        private float commandFeedbackUntil;
        private const int SkinColumns=10, SkinRows=3, SkinsPerPage=SkinColumns*SkinRows;
        private Texture2D skinGradient;
        // Shared runtime artwork; generated once per corner radius, not per skin.
        private readonly Dictionary<int,Sprite> roundedMenuSprites=new Dictionary<int,Sprite>();
        private readonly List<Texture2D> roundedMenuTextures=new List<Texture2D>();
        private readonly int[] skinPages=new int[3];
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
        private int skinTab;
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
            backIcon = BiofitV2BitmapText.Art("back") ?? Resources.Load<Texture2D>("BiofitUi/Back");
            pauseIcon = BiofitV2BitmapText.Art("pause") ?? Resources.Load<Texture2D>("BiofitUi/Pause");
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
            if (skinGradient != null) Destroy(skinGradient);
            foreach(var sprite in roundedMenuSprites.Values)if(sprite!=null)Destroy(sprite);
            foreach(var texture in roundedMenuTextures)if(texture!=null)Destroy(texture);
            roundedMenuSprites.Clear();roundedMenuTextures.Clear();
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != Settings)
            {
                bmdPage = false;
                cooldownPage = false;
                joystickPage = false;
            }
            if (paused) RestorePause();
            if(scene.name==Custom)for(int i=0;i<3;i++)skinPages[i]=SkinSelected(i)/SkinsPerPage;
            BuildScreen(scene.name);
        }

        private void Update()
        {
            if (!IsGeomeTryScene(currentScene)) return;
#if UNITY_EDITOR
            if(currentScene==Menu && GameManager.Instance!=null && BiofitV65EditorSession.TryTakeLaunch(out int slot)) StartLevel(slot-1);
#endif
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
                // Keep the button feedback component in charge of press scaling.
            }

            if (currentScene == Settings && commandFeedbackLabel != null &&
                commandFeedbackLabel.text.Length > 0 &&
                Time.unscaledTime >= commandFeedbackUntil)
                commandFeedbackLabel.text = "";

            if(currentScene==Settings && bmdPage && Time.unscaledTime>=bmdRefreshAt)
            {bmdRefreshAt=Time.unscaledTime+.4f;RefreshBmd();}
            if(paused&&bmdPauseLabel!=null&&Time.unscaledTime>=bmdRefreshAt)
            {bmdRefreshAt=Time.unscaledTime+.4f;var m=GetMonitor();bmdPauseLabel.text=m==null?"BMD no disponible":m.BmdStatus;}
            if (currentScene == Settings && joystickPage)
                RefreshJoystickDiagnostics();
            if (currentScene == Settings && statusLabel != null &&
                Time.unscaledTime >= refreshStatusAt)
            {
                refreshStatusAt = Time.unscaledTime + 0.4f;
                RefreshBleStatus();
            }
            if (currentScene == LevelScene && cooldownHudLabel != null &&
                Time.unscaledTime >= refreshCooldownAt)
            {
                refreshCooldownAt = Time.unscaledTime + .4f;
                RefreshCooldownHud();
                if (stereoCoinHudLabel != null)
                    stereoCoinHudLabel.text =
                        "MONEDAS " + BiofitStereoCoinPickup.Collected + " / " + BiofitStereoLocalRuntime.TotalCoins;
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
            if (currentMotor != null && currentMotor.Mode != BiofitMode.Cube && !currentMotor.IsMini) return;
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
            Rect region=CubeSkinRegion(index);
            if(region.width<=0||region.height<=0)return null;
            // Match the footprint of the ORIGINAL cuboid sprite in world units.
            float originalWidth = baseCubeSprite == null ? 1f :
                Mathf.Max(0.1f, baseCubeSprite.bounds.size.x);
            float ppu = region.width / originalWidth;
            Sprite value = Sprite.Create(atlas, region,
                new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            cachedSkins[index] = value;
            return value;
        }

        // Shared exact source rectangles: UI and game read the same atlas cells.
        // UI never creates/changes the gameplay Sprite or its pixels-per-unit.
        private Rect CubeSkinRegion(int index)
        {
            if(atlas==null||index<0||index>=70)return new Rect();
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
            return new Rect(x0,y0,x1-x0,y1-y0);
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

        }

        private void BuildScreen(string sceneName)
        {
            currentScene = sceneName;
            bmdStatusLabel=null;bmdSelectedLabel=null;bmdPauseLabel=null;
            for(int i=0;i<bmdOptions.Length;i++)bmdOptions[i]=null;
            statusLabel = null;
            commandFeedbackLabel = null;
            cooldownValueLabel = null;
            cooldownHudLabel = null;
            stereoCoinHudLabel = null;
            joystickDevicesLabel = null;
            joystickEventsLabel = null;
            joystickAxesLabel = null;
            refreshCooldownAt = 0f;
            for (int i = 0; i < cooldownOptionButtons.Length; i++)
                cooldownOptionButtons[i] = null;
            commandFeedbackUntil = 0f;
            connectionLabel = null;
            trackerLeftLabel=trackerRightLabel=settingsTimer=settingsDetails=settingsSystem=settingsMainText=settingsRawText=settingsCooldownText=null;
            settingsMainButton=settingsRawButton=settingsSyncButton=settingsReexportButton=settingsSafButton=null;
            captureLabel = null;
            connectTrackersButton = null;
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
            // BIOFIT_MENU_V6_RUNTIME_FIX
            Texture2D[] menuV6 = null;
            if (sceneName == Menu)
            {
                menuV6 = LoadMenuV6Art();
                if (menuV6 == null)
                {
                    ConfigureOriginalMenu(); AttachSceneFonts(); return;
                }
            }
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
                Fill("Fondo exterior BIOFIT", hostRect, sceneName==Custom?new Color32(75,75,75,255):Blue);
            safeRoot = Rect("BIOFIT Android safe area", hostRect,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            designStage = Rect("BIOFIT 16:9 responsive design", safeRoot,
                new Vector2(.5f, .5f), new Vector2(1600f, 900f),
                Vector2.zero);
            canvasRoot = designStage;
            UpdateViewport(true);

            if (sceneName == Menu) BuildMenuV6(menuV6);
            else if (sceneName == Select) BuildLevelSelection();
            else if (sceneName == Custom) BuildSkinSelection();
            else if (sceneName == Settings)
            {
                if (bmdPage) BuildBmd();
                else if (joystickPage) BuildJoystickDiagnostics();
                else if (cooldownPage) BuildCooldownSettings();
                else BuildSettings();
            }
            else if (sceneName == LevelScene) BuildLevelHud();
            else BuildOtherScene(sceneName);
            if (sceneName != Menu) BuildBackButton(sceneName);
            AttachSceneFonts();
        }

        private static void AttachSceneFonts()
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t is TextMeshProUGUI) BiofitV2BitmapText.Attach(t);
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

        private static Texture2D[] LoadMenuV6Art()
        {
            string[] names = { "Fondo_Menu", "Logo_Biofit_Dash", "Boton_Skin",
                "Boton_Play", "Boton_Configuracion" };
            var result = new Texture2D[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = Resources.Load<Texture2D>("BiofitUiV6/" + names[i]);
                if (result[i] == null)
                {
                    Debug.LogWarning("BIOFIT Menu V6: recurso ausente: " + names[i]);
                    return null;
                }
            }
            return result;
        }

        private void BuildMenuV6(Texture2D[] art)
        {
            // Disable ONLY old scene-local canvases and raycasters. Keep their
            // game objects, GameManager, BLE receiver and EventSystem alive.
            var scene = SceneManager.GetActiveScene();
            foreach (Canvas old in FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (old == canvas || old.transform.IsChildOf(transform) ||
                    old.gameObject.scene != scene) continue;
                old.enabled = false;
                foreach (GraphicRaycaster ray in old.GetComponents<GraphicRaycaster>())
                    ray.enabled = false;
            }
            // The existing 1600x900 safe-area stage is used for all UI screens.
            // Convert the supplied 1670x942 design without stretching sprites.
            var bg = Rect("BIOFIT V6 Fondo", canvasRoot, new Vector2(.5f,.5f),
                new Vector2(1600f, 900f), Vector2.zero).gameObject.AddComponent<RawImage>();
            bg.texture = art[0];
            bg.raycastTarget = false;
            MenuV6Art("BIOFIT V6 Logo", art[1], 838.5f, 219.5f, 185f, null);
            MenuV6Art("BIOFIT V6 Personaje", art[2], 512f, 494.5f, 287f, () => Navigate(Custom));
            MenuV6Art("BIOFIT V6 Play", art[3], 835.5f, 496f, 354f, () => Navigate(Select));
            MenuV6Art("BIOFIT V6 Configuracion", art[4], 1160.5f, 495f, 288f, () => Navigate(Settings));
            Debug.Log("BIOFIT MENU V6: 3 botones vinculados a Customisation, Level Selection, Settings.");
        }

        private void MenuV6Art(string name, Texture2D texture, float x, float y,
            float h, Action click)
        {
            float height = h * 900f / 942f;
            float width = height * texture.width / texture.height;
            var rect = Rect(name, canvasRoot, new Vector2(x/1670f, 1f-y/942f),
                new Vector2(width,height), Vector2.zero);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = click != null;
            if (click == null) return;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.interactable = true;
            button.onClick.AddListener(() => click());
            rect.gameObject.AddComponent<BiofitSettingsButtonFeedback>();
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
                if (b.GetComponent<BiofitSettingsButtonFeedback>() == null)
                    b.gameObject.AddComponent<BiofitSettingsButtonFeedback>();
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
            BiofitV2BitmapText.Attach(t, label.Length>95 ? "secondary" : "main");
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
            if (background.a > 0.05f)
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
            rect.gameObject.AddComponent<BiofitSettingsButtonFeedback>();
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
                b.transition = Selectable.Transition.None;
                if (b.GetComponent<BiofitSettingsButtonFeedback>() == null)
                    b.gameObject.AddComponent<BiofitSettingsButtonFeedback>();
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
            Action goBack = () =>
            {
                if(scene==Settings && bmdPage)
                {
                    var m=GetMonitor();if(m!=null&&m.BmdBusy)return;
                    bmdPage=false;BuildScreen(Settings);
                }
                else if (scene == Settings && (cooldownPage || joystickPage))
                {
                    cooldownPage = false;
                    joystickPage = false;
                    BuildScreen(Settings);
                }
                else Navigate(target);
            };
            // Level selector already includes its OWN back-arrow artwork.
            // Add only a touch hit region, never a second image over the first.
            if (scene == Select && artwork != null)
            {
                Hotspot("VOLVER sobre flecha original", canvasRoot,
                    new Vector2(.051f, .913f), new Vector2(144f, 135f),
                    () => Navigate(target));
                return;
            }
            ImageButton("VOLVER", canvasRoot, backIcon, "<",
                scene == LevelScene ? new Vector2(.06f, .93f) : new Vector2(.080f, .902f), new Vector2(88f, 88f),
                Vector2.zero, goBack);
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
            map = BiofitStereoLocalRuntime.SelectMap(i, map);
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

            // Never show the unprocessed backdrop: it may contain baked old
            // level names, which would remain visible behind live cards.
            Texture2D backdrop = artwork != null ? artwork.background : null;
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

            // Build ALL five cards from live UI objects. The source image may
            // contain obsolete English names; it is NOT used as the card art.
            // CardLeft is naturally ordered from left to right (index 0 first).
            // The visual and clickable surface share the SAME RectTransform,
            // so press feedback is visible for every card on 16:9 displays.
            Color[] accents = {
                new Color(.35f, .95f, .38f), new Color(.25f, .88f, 1f),
                new Color(.79f, .46f, 1f), new Color(1f, .77f, .26f),
                new Color(1f, .46f, .55f)
            };
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

                Button btn = Button("BIOFIT " + MapTitles[i], board, "",
                    center, new Vector2(width, height), Vector2.zero,
                    () => StartLevel(index), accents[i], 31);
                selectorHotspots[i] = btn;
                separateCards[i] = btn.transform as RectTransform;

                BiofitMode mode = BiofitMode.Cube;
                RectTransform iconBox = Rect("Icono " + MapTitles[i], btn.transform,
                    new Vector2(.5f, .68f), new Vector2(122f, 122f), Vector2.zero);
                Image icon = iconBox.gameObject.AddComponent<Image>();
                icon.sprite = BiofitThreeModeArt.Make(mode, false);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                if (i >= 0)
                {
                    // All five levels begin with Cube and contain one or two other vehicles.
                    iconBox.sizeDelta = new Vector2(82f, 82f);
                    iconBox.anchoredPosition += new Vector2(-56f, 0f);
                    foreach (BiofitMode other in (i==0 || i==4) ? new[] {BiofitMode.Ball,BiofitMode.Spider} : new[] {i==2?BiofitMode.Spider:BiofitMode.Ball})
                    {
                        RectTransform second = Rect("Modo mixto " + other,
                            btn.transform, new Vector2(.5f, .68f),
                            new Vector2(72f, 72f),
                            new Vector2((i!=0 && i!=4) ? 48f : other == BiofitMode.Ball ? 20f : 85f, 0f));
                        Image image = second.gameObject.AddComponent<Image>();
                        image.sprite = BiofitThreeModeArt.Make(other, false);
                        image.preserveAspect = true;
                        image.raycastTarget = false;
                    }
                }
                Text("Nombre " + MapTitles[i], btn.transform, MapTitles[i],
                    new Vector2(.5f, .31f), new Vector2(width - 12f, 64f),
                    Vector2.zero, 23, Navy);
                Text("Modo " + MapTitles[i], btn.transform, MapModes[i],
                    new Vector2(.5f, .18f), new Vector2(width - 16f, 36f),
                    Vector2.zero, 19, Navy);
                Text("Duración " + MapTitles[i], btn.transform, new[]{"~ 91 s","~ 86 s","~ 95 s","~ 85 s","~ 92 s"}[i],
                    new Vector2(.5f, .07f), new Vector2(width - 20f, 32f),
                    Vector2.zero, 18, Navy);
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
            if (artwork != null)
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
                focusLabel.text = "SELECCIONADO: " + MapTitles[focusLevel];
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

        // Customisation only. Reference: user's GD menu screenshots, 27/11.
        // Background is a clean reconstruction, not a flattened screenshot.
        private Texture2D SkinBackground()
        {
            if(skinGradient!=null)return skinGradient;
            skinGradient=new Texture2D(2,256,TextureFormat.RGBA32,false);
            skinGradient.name="BIOFIT selector gris";
            skinGradient.wrapMode=TextureWrapMode.Clamp;
            skinGradient.filterMode=FilterMode.Bilinear;
            var colors=new Color32[512];
            for(int y=0;y<256;y++){
                byte v=(byte)Mathf.RoundToInt(Mathf.Lerp(66f,149f,y/255f));
                colors[y*2]=colors[y*2+1]=new Color32(v,v,v,255);
            }
            skinGradient.SetPixels32(colors);skinGradient.Apply(false,false);
            return skinGradient;
        }
        private BiofitMode SkinMode(int tab)=>tab==1?BiofitMode.Ball:BiofitMode.Spider;
        private int SkinCount(int tab)=>tab==0?(atlas==null?0:70):BiofitV65Skins.Names(SkinMode(tab)).Length;
        private int SkinSelected(int tab)=>tab==0?skinIndex:BiofitV65Skins.Selected(SkinMode(tab));

        private void SkinArt(string name,Transform parent,int tab,int index,Vector2 anchor,Vector2 bounds)
        {
            if(tab==0){
                Rect region=CubeSkinRegion(index);
                if(region.width<=0||region.height<=0)return;
                float scale=Mathf.Min(bounds.x/region.width,bounds.y/region.height);
                var raw=Rect(name,parent,anchor,new Vector2(region.width*scale,region.height*scale),Vector2.zero).gameObject.AddComponent<RawImage>();
                raw.texture=atlas;
                raw.uvRect=new Rect(region.x/atlas.width,region.y/atlas.height,region.width/atlas.width,region.height/atlas.height);
                raw.raycastTarget=false;
            }else{
                var sprite=BiofitV65Skins.Get(SkinMode(tab),index);
                if(sprite==null)return;
                var img=Rect(name,parent,anchor,bounds,Vector2.zero).gameObject.AddComponent<Image>();
                img.sprite=sprite;img.preserveAspect=true;img.raycastTarget=false;
            }
        }
        private void SkinCategoryIcon(Transform parent,int tab)
        {
            if(SkinCount(tab)>0){SkinArt("Icono categoria",parent,tab,0,new Vector2(.5f,.5f),new Vector2(65,65));return;}
            var sprite=BiofitThreeModeArt.Make(tab==0?BiofitMode.Cube:SkinMode(tab),false);
            if(sprite!=null){
                var img=Rect("Icono reserva",parent,new Vector2(.5f,.5f),new Vector2(65,65),Vector2.zero).gameObject.AddComponent<Image>();
                img.sprite=sprite;img.preserveAspect=true;img.raycastTarget=false;
            }
        }
        private void SkinDisc(string name,Transform parent,Vector2 size,Color color)
        {
            var rim=Rect(name+" borde",parent,new Vector2(.5f,.5f),size,Vector2.zero).gameObject.AddComponent<Image>();
            rim.sprite=GetDotSprite();rim.color=Color.black;rim.raycastTarget=false;
            var inner=Rect(name+" centro",rim.transform,new Vector2(.5f,.5f),size-new Vector2(8,8),Vector2.zero).gameObject.AddComponent<Image>();
            inner.sprite=GetDotSprite();inner.color=color;inner.raycastTarget=false;
        }
        private void SkinFrame(Transform parent)
        {
            // Eight white corner strokes, not a lock or a disabled state.
            for(int x=0;x<2;x++)for(int y=0;y<2;y++){
                Vector2 at=new Vector2(x,y);
                AddSkinStroke(parent,at,new Vector2(18,3),new Vector2(x==0?15:-15,y==0?6:-6));
                AddSkinStroke(parent,at,new Vector2(3,18),new Vector2(x==0?6:-6,y==0?15:-15));
            }
        }
        private void AddSkinStroke(Transform parent,Vector2 anchor,Vector2 size,Vector2 offset)
        {
            var image=Rect("Marco seleccion",parent,anchor,size,offset).gameObject.AddComponent<Image>();
            image.color=Color.white;image.raycastTarget=false;
        }
        // One rounded, 9-sliced visual language for Settings and Customisation.
        // Radius is expressed in canonical UI units; corners do not stretch.
        private Sprite RoundedMenuSprite(int radius)
        {
            radius=Mathf.Max(2,radius);
            if(roundedMenuSprites.TryGetValue(radius,out var sprite))return sprite;
            int border=radius+2,side=2*border+4;
            var tex=new Texture2D(side,side,TextureFormat.RGBA32,false);
            tex.name="BIOFIT rounded "+radius;tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
            var pixels=new Color32[side*side];
            float half=side*.5f-1f;
            for(int y=0;y<side;y++)for(int x=0;x<side;x++){
                float qx=Mathf.Abs(x+.5f-side*.5f)-(half-radius);
                float qy=Mathf.Abs(y+.5f-side*.5f)-(half-radius);
                float ox=Mathf.Max(qx,0),oy=Mathf.Max(qy,0);
                float distance=Mathf.Sqrt(ox*ox+oy*oy)+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
                pixels[y*side+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f-distance)*255));
            }
            tex.SetPixels32(pixels);tex.Apply(false,true);
            sprite=Sprite.Create(tex,new Rect(0,0,side,side),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,
                new Vector4(border,border,border,border));
            roundedMenuTextures.Add(tex);roundedMenuSprites.Add(radius,sprite);return sprite;
        }
        private void RoundedMenuSurface(Image surface,int radius,Color color,bool hit=false)
        {
            surface.sprite=RoundedMenuSprite(radius);surface.type=Image.Type.Sliced;
            surface.color=color;surface.raycastTarget=hit;
        }
        private Image RoundedMenuLayer(string name,Transform parent,Vector2 size,Vector2 offset,int radius,Color color)
        {
            var image=Rect(name,parent,new Vector2(.5f,.5f),size,offset).gameObject.AddComponent<Image>();
            RoundedMenuSurface(image,radius,color);return image;
        }
        private RectTransform RoundedMenuPanel(string name,Transform parent,Vector2 anchor,Vector2 size,bool major)
        {
            var plate=Rect(name,parent,anchor,size,Vector2.zero);
            int radius=Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(size.x,size.y)*.18f),6,major?24:18);
            int rim=major?5:3,inner=major?10:6;
            var surface=plate.gameObject.AddComponent<Image>();
            RoundedMenuSurface(surface,radius,new Color32(7,12,23,255));
            surface.raycastTarget=false;
            if(major){
                var shadow=plate.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(0,0,0,.65f);shadow.effectDistance=new Vector2(4,-5);
            }
            RoundedMenuLayer("Bisel azul "+name,plate,size-new Vector2(rim*2,rim*2),Vector2.zero,
                radius-rim,major?new Color32(79,177,207,255):new Color32(69,98,126,255));
            RoundedMenuLayer("Centro opaco "+name,plate,size-new Vector2(inner*2,inner*2),Vector2.zero,
                radius-inner,major?new Color32(22,37,58,255):new Color32(34,45,61,255));
            return plate;
        }
        private RectTransform SkinPlate(string name,Transform parent,Vector2 anchor,Vector2 size)
        {
            return RoundedMenuPanel(name,parent,anchor,size,false);
        }
        // Preserve original bitmap font (Text attaches it) with internal
        // padding and exact ink bounds; TMP fallback also fits its own rect.
        private TMP_Text MenuText(string name,Transform parent,string label,Vector2 anchor,Vector2 size,Vector2 offset,int fontSize,Color color)
        {
            Vector2 inner=new Vector2(Mathf.Max(1,size.x-16),Mathf.Max(1,size.y-8));
            var t=Text(name,parent,label,anchor,inner,offset,fontSize,color);
            t.enableAutoSizing=true;t.fontSizeMin=Mathf.Min(10,fontSize);t.fontSizeMax=fontSize;
            t.overflowMode=TextOverflowModes.Ellipsis;
            BiofitV2BitmapText.ConstrainToRect(t);
            return t;
        }
        private Image RoundedMenuButtonFace(Button button,Vector2 size,Color accent,bool enabled)
        {
            var outer=(Image)button.targetGraphic;
            RoundedMenuSurface(outer,18,new Color32(8,13,22,255),true);
            Color edge=enabled?accent:new Color32(99,111,127,255);
            RoundedMenuLayer("Marco arcade",button.transform,size-new Vector2(6,6),Vector2.zero,15,edge);
            RoundedMenuLayer("Borde interior",button.transform,size-new Vector2(12,12),Vector2.zero,12,new Color32(12,23,35,255));
            var face=RoundedMenuLayer("Superficie arcade",button.transform,size-new Vector2(18,18),Vector2.zero,9,edge);
            RoundedMenuLayer("Brillo superior",face.transform,new Vector2(size.x-30,Mathf.Max(6,(size.y-18)*.26f)),
                new Vector2(0,(size.y-18)*.24f),6,new Color(1,1,1,.20f));
            RoundedMenuLayer("Sombra inferior",face.transform,new Vector2(size.x-30,6),
                new Vector2(0,-(size.y-18)*.32f),3,new Color(0,0,0,.22f));
            button.targetGraphic=face;button.transition=Selectable.Transition.ColorTint;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=Color.white;
            colors.pressedColor=new Color(.83f,.83f,.83f,1f);colors.disabledColor=new Color(.62f,.62f,.62f,1f);button.colors=colors;
            return face;
        }
        
        private void SkinArrow(string name,int direction,int pages)
        {
            int target=skinPages[skinTab]+direction;
            bool available=target>=0&&target<pages;
            var btn=Hotspot(name,canvasRoot,new Vector2(direction<0?.125f:.875f,.305f),
                new Vector2(76,76),()=>{skinPages[skinTab]=Mathf.Clamp(target,0,pages-1);BuildScreen(Custom);});
            // Rounded arcade page arrow, distinct from the circular back arrow.
            var face=RoundedMenuButtonFace(btn,new Vector2(76,76),SettingsCyan,available).rectTransform;
            btn.interactable=available;
            for(int sign=-1;sign<=1;sign+=2){
                var line=Rect("Chevron pagina",face,new Vector2(.5f,.5f),
                    new Vector2(34,8),new Vector2(0,sign*11)).gameObject.AddComponent<Image>();
                line.rectTransform.localEulerAngles=new Vector3(0,0,-direction*sign*45f);
                line.color=available?new Color32(12,28,43,255):new Color32(208,214,221,255);
                line.raycastTarget=false;
            }
        }

        private void BuildSkinSelection()
        {
            // Rebuilds in the existing 1600x900 safe-area stage, only this scene.
            var vehicleBackground=Resources.Load<Texture2D>("BiofitUi/VehicleMenu/vehicle_menu_bg");
            var bg=Rect("Fondo personalizacion",canvasRoot,new Vector2(.5f,.5f),new Vector2(1600,900),Vector2.zero).gameObject.AddComponent<RawImage>();
            bg.texture=vehicleBackground!=null?vehicleBackground:SkinBackground();
            bg.raycastTarget=false;
            // Only a tiny aspect-fill crop for the supplied 1672x941 image.
            if(vehicleBackground!=null){
                float aspect=vehicleBackground.width/(float)vehicleBackground.height;
                float target=1600f/900f;
                float w=Mathf.Min(1f,target/aspect),h=Mathf.Min(1f,aspect/target);
                bg.uvRect=new Rect((1f-w)*.5f,(1f-h)*.5f,w,h);
                bg.color=new Color(.82f,.82f,.82f,1f);
            }
            var heading=SkinPlate("Fondo titulo personaje",canvasRoot,new Vector2(.5f,.89f),new Vector2(570,80));
            MenuText("Titulo personajes",heading,"PERSONAJE",new Vector2(.5f,.5f),new Vector2(550,74),Vector2.zero,65,Color.white);
            string[] labels={"CUBO","BALL","SPIDER"};
            int count=SkinCount(skinTab),selected=SkinSelected(skinTab);
            int pages=Mathf.Max(1,Mathf.CeilToInt(count/(float)SkinsPerPage));
            int page=skinPages[skinTab]=Mathf.Clamp(skinPages[skinTab],0,pages-1);
            var preview=SkinPlate("Fondo vista previa",canvasRoot,new Vector2(.5f,.751f),new Vector2(172,154));
            if(count>0)SkinArt("Vista previa personaje",preview,skinTab,selected,new Vector2(.5f,.5f),new Vector2(128,128));
            var current=SkinPlate("Fondo skin actual",canvasRoot,new Vector2(.5f,.641f),new Vector2(310,40));
            MenuText("Skin actual",current,count>0?labels[skinTab]+" "+(selected+1)+" / "+count:labels[skinTab],
                new Vector2(.5f,.5f),new Vector2(286,34),Vector2.zero,23,Color.white);
            var separator=Rect("Separador categorias",canvasRoot,new Vector2(.5f,.615f),new Vector2(1100,3),Vector2.zero).gameObject.AddComponent<Image>();
            separator.color=new Color(1,1,1,.55f);separator.raycastTarget=false;
            for(int tab=0;tab<3;tab++){
                int choice=tab;
                Vector2 pos=new Vector2(.5f+(tab-1)*.125f,.549f);
                var category=SkinPlate("Fondo categoria "+labels[tab],canvasRoot,new Vector2(pos.x,.535f),new Vector2(160,128));
                var button=Hotspot("Categoria "+labels[tab],category,new Vector2(.5f,.5f),new Vector2(160,128),()=>{
                    skinTab=choice;
                    // When changing category, return to the selected skin's page.
                    skinPages[choice]=SkinSelected(choice)/SkinsPerPage;
                    BuildScreen(Custom);
                });
                var iconRoot=Rect("Categoria icono",button.transform,new Vector2(.5f,.635f),new Vector2(82,82),Vector2.zero);
                SkinDisc("Categoria",iconRoot,new Vector2(78,78),skinTab==tab?new Color32(25,221,229,255):new Color32(171,171,171,255));
                SkinCategoryIcon(iconRoot,tab);
                MenuText("Nombre categoria",button.transform,labels[tab],new Vector2(.5f,.15f),new Vector2(140,28),Vector2.zero,21,Color.white);
            }
            var panel=RoundedMenuPanel("Panel de skins",canvasRoot,new Vector2(.5f,.295f),new Vector2(1100,300),true);
            var grid=Rect("Interior grilla skins",panel,new Vector2(.5f,.5f),new Vector2(1076,276),Vector2.zero);
            if(count==0){
                MenuText("Arte ausente",panel,skinTab==0?"NO SE ENCONTRO SKINS.PNG":"IMPORTA EL ARTE LOCAL V6.5",new Vector2(.5f,.5f),new Vector2(970,90),Vector2.zero,28,Color.white);
            }else{
                for(int j=0;j<SkinsPerPage;j++){
                    int index=page*SkinsPerPage+j;if(index>=count)break;
                    int col=j%SkinColumns,row=j/SkinColumns;
                    var tile=Hotspot("Elegir skin "+(index+1),grid,
                        new Vector2((col+.5f)/SkinColumns,1f-(row+.5f)/SkinRows),
                        new Vector2(94,84),()=>{
                            if(skinTab==0)SetSkin(index);else BiofitV65Skins.Select(SkinMode(skinTab),index);
                            BuildScreen(Custom);
                        });
                    SkinPlate("Fondo individual skin",tile.transform,new Vector2(.5f,.5f),new Vector2(94,84));
                    SkinArt("Skin",tile.transform,skinTab,index,new Vector2(.5f,.5f),new Vector2(74,66));
                    if(index==selected)SkinFrame(tile.transform);
                }
            }
            SkinArrow("Pagina anterior",-1,pages);SkinArrow("Pagina siguiente",1,pages);
            var pageLabel=SkinPlate("Fondo numero pagina",canvasRoot,new Vector2(.5f,.072f),new Vector2(240,42));
            MenuText("Pagina actual",pageLabel,(page+1)+" / "+pages,new Vector2(.5f,.5f),new Vector2(230,36),Vector2.zero,25,Color.white);
        }

        private static readonly Color SettingsCyan=new Color32(95,222,235,255);
        private RectTransform SettingsPanel(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            return RoundedMenuPanel(name,parent,pos,size,true);
        }
        private Button SettingsButton(string name,Transform parent,string label,Vector2 pos,Vector2 size,Action action,Color accent,out TMP_Text caption,int font=25)
        {
            var button=Hotspot(name,parent,pos,size,action);
            var face=RoundedMenuButtonFace(button,size,accent,true);
            caption=MenuText(name+" texto",face.transform,label,new Vector2(.5f,.5f),size-new Vector2(26,24),Vector2.zero,font,new Color32(9,24,39,255));
            return button;
        }
        private Button SettingsButton(string name,Transform parent,string label,Vector2 pos,Vector2 size,Action action,Color accent,int font=25)
        {
            return SettingsButton(name,parent,label,pos,size,action,accent,out _,font);
        }
        private void BuildSettings()
        {
            Fill("BIOFIT configuracion fondo",canvasRoot,new Color32(7,23,38,255));
            var texture=Resources.Load<Texture2D>("BiofitUi/SettingsMenu/settings_menu_bg");
            if(texture!=null)
            {
                var bg=Rect("Fondo laboratorio configuracion",canvasRoot,new Vector2(.5f,.5f),new Vector2(1600,900),Vector2.zero).gameObject.AddComponent<RawImage>();
                bg.texture=texture;bg.raycastTarget=false;
                float aspect=texture.width/(float)texture.height,target=1600f/900f;
                float w=Mathf.Min(1f,target/aspect),h=Mathf.Min(1f,aspect/target);
                bg.uvRect=new Rect((1f-w)*.5f,(1f-h)*.5f,w,h);
            }
            var heading=SettingsPanel("Titulo ajustes",canvasRoot,new Vector2(.5f,.91f),new Vector2(620,82));
            MenuText("Titulo de ajustes",heading,"CONFIGURACIÓN",new Vector2(.5f,.5f),new Vector2(600,72),Vector2.zero,48,Color.white);
            SettingsButton("Abrir BMD",canvasRoot,"BMD",new Vector2(.90f,.91f),new Vector2(150,70),()=>{bmdPage=true;cooldownPage=false;joystickPage=false;var m=GetMonitor();bmdSelected=m==null||m.BmdMask<0?0:m.BmdMask;BuildScreen(Settings);},SettingsCyan,28);

            var trackers=SettingsPanel("Conexion BLE",canvasRoot,new Vector2(.18f,.54f),new Vector2(440,574));
            MenuText("Seccion trackers",trackers,"CONEXIÓN WAVESHARE",new Vector2(.5f,.93f),new Vector2(424,48),Vector2.zero,28,SettingsCyan);
            var leftCard=SkinPlate("Tracker izquierdo placa",trackers,new Vector2(.5f,.745f),new Vector2(402,142));
            var rightCard=SkinPlate("Tracker derecho placa",trackers,new Vector2(.5f,.48f),new Vector2(402,142));
            MenuText("Tracker izquierdo titulo",leftCard,"TRACKER IZQUIERDO",new Vector2(.5f,.77f),new Vector2(390,37),Vector2.zero,26,Color.white);
            MenuText("Tracker derecho titulo",rightCard,"TRACKER DERECHO",new Vector2(.5f,.77f),new Vector2(390,37),Vector2.zero,26,Color.white);
            trackerLeftLabel=MenuText("Tracker izquierdo estado",leftCard,"",new Vector2(.5f,.36f),new Vector2(390,76),Vector2.zero,26,Color.white);
            trackerRightLabel=MenuText("Tracker derecho estado",rightCard,"",new Vector2(.5f,.36f),new Vector2(390,76),Vector2.zero,26,Color.white);
            connectionLabel=MenuText("Estado trackers",trackers,"",new Vector2(.5f,.257f),new Vector2(406,84),Vector2.zero,22,Color.white);
            connectTrackersButton=SettingsButton("Buscar y conectar trackers",trackers,"BUSCAR Y CONECTAR",new Vector2(.5f,.095f),new Vector2(400,74),()=>{GetMonitor()?.RequestTrackerConnection();RefreshBleStatus();},Lime,26);

            var captures=SettingsPanel("Captura y archivos",canvasRoot,new Vector2(.5f,.54f),new Vector2(536,574));
            MenuText("Seccion captura",captures,"REGISTRO Y EXPORTACIÓN",new Vector2(.5f,.93f),new Vector2(520,48),Vector2.zero,30,SettingsCyan);
            captureLabel=MenuText("Estado capturas",captures,"",new Vector2(.5f,.818f),new Vector2(512,50),Vector2.zero,29,Color.white);
            var timerPlate=SkinPlate("Fondo cronometro",captures,new Vector2(.5f,.685f),new Vector2(300,100));
            settingsTimer=MenuText("Cronometro captura",timerPlate,"00:00",new Vector2(.5f,.5f),new Vector2(290,92),Vector2.zero,68,Color.white);
            settingsDetails=MenuText("Detalle registro",captures,"",new Vector2(.5f,.463f),new Vector2(510,142),Vector2.zero,23,Color.white);
            statusLabel=MenuText("Aviso captura",captures,"",new Vector2(.5f,.238f),new Vector2(510,103),Vector2.zero,20,Color.white);
            settingsMainButton=SettingsButton("Registro accion principal",captures,"INICIAR REGISTRO",new Vector2(.5f,.078f),new Vector2(508,70),CapturePrimaryAction,Lime,out settingsMainText,26);

            var tools=SettingsPanel("Herramientas configuracion",canvasRoot,new Vector2(.82f,.54f),new Vector2(440,574));
            MenuText("Titulo herramientas",tools,"HERRAMIENTAS",new Vector2(.5f,.93f),new Vector2(420,48),Vector2.zero,29,SettingsCyan);
            settingsRawButton=SettingsButton("RAW",tools,"RAW: OFF",new Vector2(.5f,.795f),new Vector2(400,68),ToggleRawCapture,SettingsCyan,out settingsRawText);
            settingsSyncButton=SettingsButton("SYNC",tools,"MARCAR SYNC",new Vector2(.5f,.66f),new Vector2(400,68),MarkSync,SettingsCyan);
            SettingsButton("Abrir cooldown",tools,"COOLDOWN",new Vector2(.5f,.525f),new Vector2(400,68),()=>{joystickPage=false;cooldownPage=true;BuildScreen(Settings);},SettingsCyan,out settingsCooldownText);
            SettingsButton("Abrir joystick",tools,"JOYSTICK",new Vector2(.5f,.39f),new Vector2(400,68),()=>{cooldownPage=false;joystickPage=true;BuildScreen(Settings);},SettingsCyan);
            settingsSafButton=SettingsButton("Export SAF",tools,"GUARDAR ARCHIVOS (SAF)",new Vector2(.5f,.255f),new Vector2(400,68),()=>ExportCapture(true),SettingsCyan,23);
            settingsReexportButton=SettingsButton("Downloads",tools,"REEXPORTAR CSV",new Vector2(.5f,.12f),new Vector2(400,68),()=>ExportCapture(false),SettingsCyan,23);
            var footer=SettingsPanel("Estado general configuracion",canvasRoot,new Vector2(.5f,.124f),new Vector2(1464,118));
            settingsSystem=MenuText("Estado sistema",footer,"",new Vector2(.5f,.68f),new Vector2(1430,48),Vector2.zero,25,Color.white);
            commandFeedbackLabel=MenuText("Confirmacion boton",footer,"",new Vector2(.5f,.27f),new Vector2(1430,38),Vector2.zero,23,SettingsCyan);
            RefreshBleStatus();
        }

        private void BuildBmd()
        {
            Fill("BMD fondo",canvasRoot,Blue);ArcadeCorners(canvasRoot);
            Text("BMD titulo",canvasRoot,"BMD V1.1 · BIOFIT MOTION DIAGNOSTICS",new Vector2(.5f,.90f),new Vector2(1200,65),Vector2.zero,36,Lime);
            bmdStatusLabel=Text("BMD estado",canvasRoot,"",new Vector2(.5f,.748f),new Vector2(1380,176),Vector2.zero,22,Color.white);
            var progress=Rect("BMD progreso",canvasRoot,new Vector2(.5f,.625f),new Vector2(900,10),Vector2.zero);
            Fill("BMD pista",progress,new Color32(22,35,57,255));
            bmdProgressFill=Fill("BMD avance",progress,Lime);
            bmdProgressFill.anchorMax=new Vector2(0,1);
            string[] labels={"UMBRAL 0,12 g","REARME SUPERVISADO","GRAVEDAD ADAPTATIVA","AISLAR LIFT TX"};
            for(int i=0;i<4;i++)
            {
                int bit=1<<i;
                bmdOptions[i]=Button("BMD opcion "+bit,canvasRoot,labels[i],new Vector2(.25f+(i%2)*.5f,.57f-(i/2)*.10f),new Vector2(675,68),Vector2.zero,()=>{var m=GetMonitor();if(m!=null&&m.BmdBusy)return;bmdSelected^=bit;RefreshBmd();},Ice,24);
            }
            bmdSelectedLabel=Text("BMD seleccion",canvasRoot,"",new Vector2(.5f,.39f),new Vector2(1280,42),Vector2.zero,24,Lime);
            string[] commands={"CONSULTAR","APLICAR AMBOS","RESTAURAR 0","INICIAR TRACE","CONGELAR TRACE","DESCARGAR + EXPORTAR","EXPORTAR TRACE","CANCELAR"};
            for(int i=0;i<commands.Length;i++)
            {
                int action=i;
                Button("BMD accion "+i,canvasRoot,commands[i],new Vector2(.14f+(i%4)*.24f,.282f-(i/4)*.103f),new Vector2(350,72),Vector2.zero,()=>BmdAction(action),i==7?Ice:Lime,22);
            }
            Button("BMD duracion",canvasRoot,"DURACION TRACE",new Vector2(.25f,.07f),new Vector2(400,55),Vector2.zero,()=>{GetMonitor()?.CycleBmdDuration();RefreshBmd();},Ice,18);
            Button("BMD montaje",canvasRoot,"UBICACION SENSOR",new Vector2(.51f,.07f),new Vector2(400,55),Vector2.zero,()=>{GetMonitor()?.CycleBmdPlacement();RefreshBmd();},Ice,18);
            Button("BMD auto",canvasRoot,"GFA AUTO ON/OFF",new Vector2(.77f,.07f),new Vector2(400,55),Vector2.zero,()=>{GetMonitor()?.ToggleBmdAuto();RefreshBmd();},Ice,18);
            RefreshBmd();
        }
        private void BmdAction(int action)
        {
            var m=GetMonitor();if(m==null)return;
            switch(action){case 0:m.RequestBmd("query");break;case 1:m.RequestBmd("profile",bmdSelected);break;case 2:bmdSelected=0;m.RequestBmd("profile",0);break;case 3:m.RequestBmd("start");break;case 4:m.RequestBmd("freeze");break;case 5:m.RequestBmd((m.BmdAutoEnabled||m.BmdAutoPending)?"autoDownloadExport":"downloadExport");break;case 6:m.ExportBmdTrace();break;case 7:m.CancelBmd();break;}
            RefreshBmd();
        }
        private void RefreshBmd()
        {
            var m=GetMonitor();
            if(bmdStatusLabel!=null)bmdStatusLabel.text=m==null?"Receptor no disponible":m.BmdTrackerSummary+"\n"+m.BmdDetectorSummary+"\n"+m.GfaSummary+" | "+(m.BmdAutoEnabled?"AUTO ON":"AUTO OFF")+"\n"+m.BmdStatus+" ("+(m.BmdProgress*100).ToString("F0")+"%)\n"+m.BmdAutoStatus+" | "+m.ExportStatus;
            if(bmdProgressFill!=null)bmdProgressFill.anchorMax=new Vector2(m==null?0:Mathf.Clamp01(m.BmdProgress),1);
            if(bmdSelectedLabel!=null)bmdSelectedLabel.text="SELECCIÓN: "+bmdSelected+" · "+(bmdSelected==0?"LEGACY":"EXPERIMENTAL")+" · "+(m==null?"":(m.BmdTraceSeconds==0?"4.6s":m.BmdTraceSeconds+"s")+" · "+m.BmdPlacement);
            for(int i=0;i<bmdOptions.Length;i++)if(bmdOptions[i]!=null)
            {
                Color color=(bmdSelected&(1<<i))!=0?Lime:Ice;
                var image=bmdOptions[i].GetComponent<Image>();if(image!=null)image.color=color;
                foreach(Image child in bmdOptions[i].GetComponentsInChildren<Image>())if(child.name.EndsWith("lime inset",StringComparison.Ordinal))child.color=color;
            }
        }

        // Experimental diagnostic page for Bluetooth HID mini remotes.
        // The mode may expose a keyboard, gamepad, mouse, or system volume keys.
        // We deliberately do not invent a physical-button -> KeyCode mapping.
        private void BuildJoystickDiagnostics()
        {
            Fill("BIOFIT joystick fondo", canvasRoot, Blue);
            ArcadeCorners(canvasRoot);
            ArcadeLogo(canvasRoot);

            RectTransform heading = ArcadePanel("Joystick encabezado", canvasRoot,
                new Vector2(.5f, .793f), new Vector2(1060f, 76f), Vector2.zero);
            Text("Joystick titulo", heading, "DIAGNÓSTICO DE JOYSTICK",
                new Vector2(.5f, .5f), new Vector2(1030f, 60f),
                Vector2.zero, 36, Color.white);
            Text("Joystick instrucciones", canvasRoot,
                "CONECTÁ EL MANDO. MODO JUEGO COMPROBADO: CAMBIO + C.",
                new Vector2(.5f, .700f), new Vector2(1290f, 64f),
                Vector2.zero, 24, Ice);

            RectTransform devices = ArcadePanel("Joystick dispositivo",
                canvasRoot, new Vector2(.5f, .568f),
                new Vector2(1210f, 126f), Vector2.zero);
            joystickDevicesLabel = Text("Joystick dispositivo texto", devices,
                "", new Vector2(.5f, .5f), new Vector2(1150f, 106f),
                Vector2.zero, 25, Color.white);

            RectTransform eventsPanel = ArcadePanel("Joystick eventos",
                canvasRoot, new Vector2(.5f, .341f),
                new Vector2(1210f, 258f), Vector2.zero);
            joystickEventsLabel = Text("Joystick eventos texto", eventsPanel,
                "", new Vector2(.5f, .5f), new Vector2(1130f, 235f),
                Vector2.zero, 22, Lime);

            joystickAxesLabel = Text("Joystick ejes", canvasRoot, "",
                new Vector2(.5f, .165f), new Vector2(1140f, 40f),
                Vector2.zero, 22, Ice);
            Button("Joystick limpiar", canvasRoot, "LIMPIAR EVENTOS",
                new Vector2(.5f, .075f), new Vector2(440f, 66f),
                Vector2.zero, () => {
                    joystickHistory.Clear();
                    joystickEventCount = 0;
                    RefreshJoystickDiagnostics(true);
                }, Lime, 26);
            RefreshJoystickDiagnostics(true);
        }

        private void RefreshJoystickDiagnostics(bool force = false)
        {
            if (joystickDevicesLabel == null || joystickEventsLabel == null ||
                joystickAxesLabel == null) return;

            // Input.anyKeyDown and GetKeyDown are supported by this project's
            // activeInputHandler=0 (legacy Input Manager). Enum scan is only
            // performed on a down-edge while this diagnostic page is visible.
            if (Input.anyKeyDown)
            {
                var names = new List<string>();
                foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
                {
                    if (code != KeyCode.None && Input.GetKeyDown(code))
                        names.Add(code.ToString());
                }
                if (names.Count > 0)
                {
                    string observation = string.Join(", ", names.ToArray());
                    joystickEventCount++;
                    joystickHistory.Insert(0,
                        joystickEventCount + ": " + observation);
                    if (joystickHistory.Count > 5)
                        joystickHistory.RemoveAt(joystickHistory.Count - 1);
                    Debug.Log("BIOFIT JOYSTICK DIAGNOSTIC (Unity KeyCode): " +
                        observation);
                }
            }

            if (!force && Time.unscaledTime < joystickRefreshAt) return;
            joystickRefreshAt = Time.unscaledTime + .12f;
            string[] devices = Input.GetJoystickNames();
            var nonempty = new List<string>();
            foreach (string device in devices)
                if (!string.IsNullOrEmpty(device))
                    nonempty.Add(device);
            joystickDevicesLabel.text =
                "JOYSTICK EN UNITY: " +
                (nonempty.Count == 0 ? "NO DETECTADO COMO GAMEPAD" :
                    string.Join(" / ", nonempty.ToArray())) +
                "\nPuede funcionar como mouse o teclado sin aparecer en esta lista.";
            joystickEventsLabel.text = "PULSACIONES RECONOCIDAS POR UNITY (" +
                joystickEventCount + ")\n" +
                (joystickHistory.Count == 0 ? "Esperando una tecla o clic..." :
                    string.Join("\n", joystickHistory.ToArray())) +
                "\nEl clic táctil también puede aparecer como Mouse0.";
            joystickAxesLabel.text =
                "STICK FÍSICO X: " + Input.GetAxisRaw("BIOFIT VR Stick X").ToString("F2") +
                "  Y: " + Input.GetAxisRaw("BIOFIT VR Stick Y").ToString("F2") +
                "  | TECLADO H: " + Input.GetAxisRaw("Horizontal").ToString("F2") +
                " V: " + Input.GetAxisRaw("Vertical").ToString("F2");
        }

        private void BuildCooldownSettings()
        {
            Fill("BIOFIT cooldown fondo", canvasRoot, Blue);
            ArcadeCorners(canvasRoot);
            ArcadeLogo(canvasRoot);

            RectTransform heading = ArcadePanel("Cooldown titulo",
                canvasRoot, new Vector2(.5f, .793f),
                new Vector2(1060f, 76f), Vector2.zero);
            Text("Cooldown titulo texto", heading, "COOLDOWN BILATERAL",
                new Vector2(.5f, .5f), new Vector2(1000f, 60f),
                Vector2.zero, 38, Color.white);
            Text("Cooldown indicacion", canvasRoot,
                "ELEGÍ EL TIEMPO ENTRE ACCIONES BILATERALES",
                new Vector2(.5f, .686f), new Vector2(1200f, 52f),
                Vector2.zero, 26, Ice);

            for (int i = 0; i < CooldownOptionsMs.Length; i++)
            {
                int milliseconds = CooldownOptionsMs[i];
                float x = .23f + (i % 3) * .27f;
                float y = i < 3 ? .525f : .355f;
                cooldownOptionButtons[i] = Button(
                    "Cooldown opcion " + milliseconds, canvasRoot,
                    milliseconds + " ms",
                    new Vector2(x, y), new Vector2(310f, 105f),
                    Vector2.zero, () => SelectCooldown(milliseconds), Ice, 33);
            }

            RectTransform current = ArcadePanel("Cooldown actual panel",
                canvasRoot, new Vector2(.5f, .195f),
                new Vector2(890f, 76f), Vector2.zero);
            cooldownValueLabel = Text("Cooldown real del detector",
                current, "", new Vector2(.5f, .5f),
                new Vector2(845f, 60f), Vector2.zero, 31, Color.white);
            Button("Restaurar cooldown", canvasRoot, "RESTABLECER A 900 ms",
                new Vector2(.5f, .077f), new Vector2(575f, 65f),
                Vector2.zero, () => SelectCooldown(900), Lime, 27);
            RefreshCooldownControls();
        }

        private void SelectCooldown(int milliseconds)
        {
            BiofitBLEMonitor monitor = GetMonitor();
            if (monitor == null || !monitor.SetPairCooldownMilliseconds(milliseconds))
            {
                if (cooldownValueLabel != null)
                    cooldownValueLabel.text = "ERROR: RECEPTOR BLE NO DISPONIBLE";
                Debug.LogError("BIOFIT cooldown: no se pudo aplicar al detector real.");
                return;
            }
            RefreshCooldownControls();
        }

        private void RefreshCooldownControls()
        {
            BiofitBLEMonitor monitor = GetMonitor();
            int active = monitor == null ? -1 : monitor.PairCooldownMilliseconds;
            bool consistent = monitor != null && monitor.CooldownPreferenceMatchesDetector;
            if (cooldownValueLabel != null)
                cooldownValueLabel.text = monitor == null ?
                    "COOLDOWN: RECEPTOR NO DISPONIBLE" :
                    "COOLDOWN ACTUAL: " + active + " ms" +
                    (consistent ? "" : "  · ERROR DE PREFERENCIA");
            for (int i = 0; i < cooldownOptionButtons.Length; i++)
            {
                Button option = cooldownOptionButtons[i];
                if (option == null) continue;
                bool selected = active == CooldownOptionsMs[i];
                Color color = selected ? Lime : Ice;
                Image outer = option.GetComponent<Image>();
                if (outer != null) outer.color = color;
                foreach (Image image in option.GetComponentsInChildren<Image>())
                    if (image.name.EndsWith("lime inset", StringComparison.Ordinal))
                        image.color = color;
                TMP_Text label = option.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = CooldownOptionsMs[i] + " ms" +
                        (selected ? "\nACTIVO" : "");
            }
        }

        private void RefreshCooldownHud()
        {
            if (cooldownHudLabel == null) return;
            BiofitBLEMonitor monitor = GetMonitor();
            cooldownHudLabel.text = monitor == null ? "CD: --" :
                "CD: " + monitor.PairCooldownMilliseconds + " ms" +
                (monitor.CooldownPreferenceMatchesDetector ? "" : " !");
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

        private void CapturePrimaryAction()
        {
            var monitor=GetMonitor();
            if(monitor==null)return;
            if(monitor.GameCaptureActive)
            {
                bool started=monitor.FinishAndExportCapture();
                ShowCommandFeedback(started?"FINALIZANDO Y DESCARGANDO SESIÓN":"NO SE PUDO FINALIZAR · REVISÁ ESTADO");
            }
            else if(monitor.CanStartCapture)StartCapture();
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
                monitor.ExportBusy || monitor.BmdBusy;
            monitor.RequestExport(saf);
            ShowCommandFeedback(blocked ? "EXPORTACIÓN NO DISPONIBLE · FINALIZÁ PRIMERO" :
                (saf ? "SAF" : "DESCARGAS") +
                " SOLICITADA · ESPERÁ CONFIRMACIÓN");
        }

        private void RefreshBleStatus()
        {
            if(connectionLabel==null||captureLabel==null||statusLabel==null||settingsMainButton==null)return;
            var m=GetMonitor();
            bool left=m!=null&&m.LeftTrackerConnected,right=m!=null&&m.RightTrackerConnected;
            trackerLeftLabel.text=left?"CONECTADO\nFW "+(m.LeftTrackerFirmware>0?m.LeftTrackerFirmware.ToString():"PENDIENTE"):"DESCONECTADO";
            trackerRightLabel.text=right?"CONECTADO\nFW "+(m.RightTrackerFirmware>0?m.RightTrackerFirmware.ToString():"PENDIENTE"):"DESCONECTADO";
            trackerLeftLabel.color=left?Lime:Ice;trackerRightLabel.color=right?Lime:Ice;
            connectTrackersButton.interactable=m!=null&&m.CanRequestTrackerConnection;
            connectionLabel.text=m==null?"RECEPTOR BLE NO DISPONIBLE":m.TrackerConnectionStatus;
            bool active=m!=null&&m.GameCaptureActive,saving=m!=null&&m.CaptureSaving,exporting=m!=null&&m.ExportBusy;
            bool closed=m!=null&&!string.IsNullOrEmpty(m.SavedCapturePath);
            string export=m==null?"Sin receptor":m.ExportStatus;
            bool error=export.StartsWith("ERROR",StringComparison.OrdinalIgnoreCase)||export.StartsWith("CANCEL",StringComparison.OrdinalIgnoreCase)||(m!=null&&m.CaptureFileStatus.StartsWith("ERROR",StringComparison.OrdinalIgnoreCase));
            bool done=!exporting&&!saving&&!active&&(export.StartsWith("EXPORTADO Y VERIFICADO",StringComparison.Ordinal)||export.StartsWith("EDITOR:",StringComparison.Ordinal));
            captureLabel.text=active?"GRABANDO SESIÓN":saving?"FINALIZANDO":exporting?"EXPORTANDO":error?"REVISAR ARCHIVOS":done?"EXPORTACIÓN COMPLETADA":closed?"REGISTRO GUARDADO":"REGISTRO INACTIVO";
            captureLabel.color=active?new Color32(255,117,117,255):error?new Color32(255,190,100,255):done?Lime:SettingsCyan;
            int seconds=m==null?0:m.CaptureElapsedSeconds;
            settingsTimer.text=(seconds/60).ToString("D2")+":"+(seconds%60).ToString("D2");
            settingsDetails.text=m==null?"CSV: INACTIVO\nRAW: OFF\nGFA: NO DISPONIBLE":
                "CSV: "+(active?"GRABANDO":saving?"CERRANDO":closed?"GUARDADO":"INACTIVO")+"  |  "+m.RawButtonLabel+
                "\nGFA AUTO: "+(m.BmdAutoEnabled?"ON":"OFF")+"  |  PERFIL: "+(m.BmdMask>=0?m.BmdMask.ToString():"--")+
                "\nUBICACIÓN: "+m.BmdPlacement+"  |  SYNC: "+m.CaptureSyncCount+
                "\nEVENTOS: "+m.CapturedEventCount+"  |  RAW I/D: "+m.RawCount;
            statusLabel.text=m==null?"Sin receptor BLE":active?m.RawStatus:saving?"Cerrando y verificando archivos privados":exporting||error||done?export+"\n"+m.CaptureFileStatus:m.CaptureFileStatus;
            settingsMainText.text=active?"FINALIZAR Y DESCARGAR":saving?"FINALIZANDO...":exporting?"EXPORTANDO...":"INICIAR REGISTRO";
            settingsMainButton.interactable=m!=null&&(active?!m.BmdBusy&&!exporting:!saving&&!exporting&&m.CanStartCapture);
            settingsRawText.text=m==null?"RAW: OFF":m.RawButtonLabel;
            settingsRawButton.interactable=active&&!saving&&!exporting&&m!=null&&!m.BmdBusy&&!m.BmdAutoSession&&m.RawOperation.IsCompleted&&(m.RawActive||m.BothTrackersConnected);
            settingsSyncButton.interactable=active;
            settingsSafButton.interactable=settingsReexportButton.interactable=m!=null&&m.CanExportCapture;
            settingsCooldownText.text=m==null?"COOLDOWN":"COOLDOWN: "+m.PairCooldownMilliseconds+" ms";
            settingsSystem.text=m==null?"0/2 TRACKERS · RECEPTOR NO DISPONIBLE":m.ConnectedTrackerCount+"/2 TRACKERS · RELOJES "+(m.ClockAlignmentReady?"SINCRONIZADOS":"PENDIENTES")+" · "+(m.BmdControlBlocked?"CONTROL BLE BLOQUEADO":m.BothTrackersConnected?(m.ClockAlignmentReady?"CONTROL BLE HABILITADO":"ESPERANDO SINCRONIZACIÓN"):"ESPERANDO TRACKERS");
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
            var hud=canvasRoot.gameObject.AddComponent<BiofitV2Hud>();
            hud.bar=Rect("BIOFIT V2 progress",canvasRoot,new Vector2(.5f,.945f),new Vector2(660,32),Vector2.zero);
            hud.percentage=Text("BIOFIT V2 percent",canvasRoot,"0%",new Vector2(.755f,.945f),new Vector2(125,50),Vector2.zero,38,Color.white);
            cooldownHudLabel=Text("Cooldown BLE activo HUD",canvasRoot,"",new Vector2(.86f,.855f),new Vector2(235,44),Vector2.zero,28,Color.white);
            RefreshCooldownHud();
            ImageButton("Pause game",canvasRoot,pauseIcon,"II",new Vector2(.945f,.93f),new Vector2(88,88),Vector2.zero,TogglePause);
            if(GameManager.Instance!=null && GameManager.Instance.SelectedLevel!=null && BiofitStereoLocalRuntime.IsLocal(GameManager.Instance.SelectedLevel))
            {
                hud.coins=new RawImage[3];
                for(int i=0;i<3;i++)
                {
                    var coin=Rect("BIOFIT coin "+i,canvasRoot,new Vector2(.14f+i*.037f,.945f),new Vector2(45,45),Vector2.zero).gameObject.AddComponent<RawImage>();
                    coin.raycastTarget=false;hud.coins[i]=coin;
                }
            }
            if(BiofitStereoPractice.Enabled)
            {
                hud.practiceStatus=Text("Practice status",canvasRoot,"PRACTICA",new Vector2(.5f,.865f),new Vector2(680,42),Vector2.zero,28,Color.white);
                BiofitStereoPracticeButton.Create(canvasRoot,new Vector2(.46f,.085f),new Vector2(112,88),BiofitV2BitmapText.Art("add_checkpoint"),true);
                BiofitStereoPracticeButton.Create(canvasRoot,new Vector2(.54f,.085f),new Vector2(112,88),BiofitV2BitmapText.Art("remove_checkpoint"),false);
            }
        }

        private void TogglePause()
        {
            if (currentScene != LevelScene) return;
            if(paused&&GetMonitor()?.BmdBusy==true)return;
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
            RectTransform veil=Fill("pause dimmer",canvasRoot,new Color(0f,0f,0f,.83f));
            Text("pause title",veil,"PAUSA",new Vector2(.5f,.82f),new Vector2(560,85),Vector2.zero,62,Color.white);
            ImageButton("RESUME",veil,BiofitV2BitmapText.Art("play"),"PLAY",new Vector2(.5f,.57f),new Vector2(150,150),Vector2.zero,()=>{if(GetMonitor()?.BmdBusy==true)return;RestorePause();BuildScreen(LevelScene);});
            ImageButton("RESTART",veil,BiofitV2BitmapText.Art("restart"),"RETRY",new Vector2(.34f,.57f),new Vector2(115,115),Vector2.zero,RestartLevel);
            ImageButton("BACK SELECT",veil,BiofitV2BitmapText.Art("menu"),"MENU",new Vector2(.66f,.57f),new Vector2(115,115),Vector2.zero,()=>Navigate(Select));
            Text("resume caption",veil,"CONTINUAR",new Vector2(.5f,.43f),new Vector2(230,50),Vector2.zero,28,Color.white);
            Text("restart caption",veil,"REINICIAR",new Vector2(.34f,.43f),new Vector2(230,50),Vector2.zero,26,Color.white);
            Text("menu caption",veil,"NIVELES",new Vector2(.66f,.43f),new Vector2(230,50),Vector2.zero,26,Color.white);
            if(BiofitStereoPractice.Available)
            {
                bool practice=BiofitStereoPractice.Enabled;
                ImageButton("Cambiar modo",veil,BiofitV2BitmapText.Art(practice?"normal":"practice"),"MODO",new Vector2(.39f,.25f),new Vector2(98,98),Vector2.zero,()=>{BiofitStereoPractice.ToggleMode();RestartLevel();});
                Text("mode caption",veil,practice?"MODO NORMAL":"MODO PRACTICA",new Vector2(.55f,.25f),new Vector2(350,48),Vector2.zero,31,Color.white);
                if(practice)
                    Button("Auto checkpoint",veil,"AUTO CHECKPOINT: "+(BiofitStereoPractice.Auto?"SI":"NO"),new Vector2(.5f,.105f),new Vector2(450,64),Vector2.zero,()=>{BiofitStereoPractice.ToggleAuto();BuildScreen(LevelScene);BuildPauseScreen();},Ice,25);
            }
            var bmd=GetMonitor();
            if(bmd!=null&&bmd.BmdTraceArmed)
            {
                Button("BMD pausa congelar",veil,"CONGELAR TRACE",new Vector2(.84f,.25f),new Vector2(330,68),Vector2.zero,()=>bmd.RequestBmd("freeze"),Ice,23);
                bmdPauseLabel=Text("BMD pausa estado",veil,bmd.BmdStatus,new Vector2(.84f,.135f),new Vector2(410,100),Vector2.zero,18,Color.white);
            }
            BuildBackButton(LevelScene);
        }

        // BIOFIT V6.3: completion uses the original end collider, not percentage rounding.
        public static bool ShowV63Completion()
        {
            if(instance==null || instance.currentScene!=LevelScene || instance.canvasRoot==null) return false;
            instance.BuildV63Completion();return true;
        }

        private void BuildV63Completion()
        {
            if(paused) RestorePause();
            EnterPause();
            for(int i=0;i<canvasRoot.childCount;i++) canvasRoot.GetChild(i).gameObject.SetActive(false);
            RectTransform veil=Fill("V63 completed veil",canvasRoot,new Color(0f,0f,0f,.84f));
            veil.GetComponent<Image>().raycastTarget=true;
            RectTransform panel=Rect("V63 completed panel",veil,new Vector2(.5f,.5f),new Vector2(1040,590),Vector2.zero);
            var bg=panel.gameObject.AddComponent<Image>();bg.color=new Color(.025f,.15f,.22f,.98f);bg.raycastTarget=false;
            var rim=panel.gameObject.AddComponent<Outline>();rim.effectColor=new Color(.48f,.96f,.1f,1);rim.effectDistance=new Vector2(4,-4);
            Text("Completion title",panel,"NIVEL COMPLETO",new Vector2(.5f,.77f),new Vector2(980,120),Vector2.zero,80,Lime);
            Text("Completion status",panel,BiofitStereoPractice.Enabled?"PRACTICA COMPLETADA":"100%",new Vector2(.5f,.60f),new Vector2(850,65),Vector2.zero,38,Color.white);
            ImageButton("V63 retry",panel,BiofitV2BitmapText.Art("restart"),"REINICIAR",new Vector2(.32f,.33f),new Vector2(124,124),Vector2.zero,RestartLevel);
            ImageButton("V63 main menu",panel,BiofitV2BitmapText.Art("menu"),"MENU",new Vector2(.68f,.33f),new Vector2(124,124),Vector2.zero,()=>Navigate(Menu));
            Text("Retry label",panel,"REINICIAR",new Vector2(.32f,.13f),new Vector2(290,55),Vector2.zero,32,Color.white);
            Text("Menu label",panel,"MENU PRINCIPAL",new Vector2(.68f,.13f),new Vector2(340,55),Vector2.zero,32,Color.white);
            panel.gameObject.AddComponent<BiofitV63CompletionAnimation>();
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
