using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// BIOFIT DASH: juego 2D ORIGINAL y autónomo inspirado en el genero
/// de plataformas de avance automatico. No emplea recursos de Geometry Dash.
/// En Windows: ESPACIO o boton SALTAR. En Android: boton tactil.
/// BLE: opcional y DESACTIVADO por defecto hasta validar la IMU.
/// </summary>
[RequireComponent(typeof(BiofitBLEMonitor))]
public sealed class BiofitDashGame : MonoBehaviour
{
    private const float VirtualW = 1120f;
    private const float VirtualH = 740f;
    private readonly BiofitDashCore game = new BiofitDashCore();

    private BiofitBLEMonitor monitor;
    private bool bleJumpEnabled;
    private int blePairsObserved;
    private int bleJumpAttempts;
    private string lastInput = "ESPACIO o boton SALTAR";
    private GUIStyle heading, info, helper, control, center;
    private Texture2D spikeTex;
    private float roadOffset;
    private bool showTestPanel = true;
    private string experimentStatus = "Registro sin iniciar. Una sola sesion.";
    private GUIStyle compact;

    private void Awake()
    {
        monitor = GetComponent<BiofitBLEMonitor>();
        monitor.ShowDiagnostics = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif
        game.StartNewGame();
        BuildSpikeTexture();
    }

    private void OnEnable()
    {
        if (monitor == null) monitor = GetComponent<BiofitBLEMonitor>();
        monitor.BilateralJumpCandidate += OnCandidatePair;
    }

    private void OnDisable()
    {
        if (monitor != null)
            monitor.BilateralJumpCandidate -= OnCandidatePair;
    }

    private void OnDestroy()
    {
        if (spikeTex != null)
            Destroy(spikeTex);
    }

    private void OnCandidatePair(float gapMs)
    {
        blePairsObserved++;
        monitor.RecordGameAction("PAR_BLE_DETECTADO");
        if (!bleJumpEnabled || !monitor.BothTrackersConnected) return;
        bleJumpAttempts++;
        lastInput = "PAR BLE EXPERIMENTAL (" + gapMs.ToString("F0") + " ms)";
        bool jumped = game.Jump();
        monitor.RecordGameAction(jumped ?
            "SALTO_BLE_REALIZADO" : "SALTO_BLE_NO_REALIZADO");
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys != null)
        {
            if (keys.spaceKey.wasPressedThisFrame ||
                keys.upArrowKey.wasPressedThisFrame)
                Jump("TECLADO");
            if (keys.rKey.wasPressedThisFrame)
                ResetGame();
            if (keys.pKey.wasPressedThisFrame)
                game.TogglePause();
        }

        float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);
        game.Step(dt);
        if (game.Playing && !game.Paused)
            roadOffset = (roadOffset + game.Speed * dt) % 90f;
    }

    private void Jump(string input)
    {
        if (game.Jump())
        {
            lastInput = input;
            monitor.RecordGameAction("SALTO_MANUAL");
        }
    }

    private void ResetGame()
    {
        game.StartNewGame();
        lastInput = "NUEVA PARTIDA";
        roadOffset = 0f;
    }

    private void ReturnToDiagnostics()
    {
        // Es el MISMO BiofitBLEMonitor, sin nuevo ConnectAsync:
        // no se pierden las suscripciones ni los datos de la sesion.
        if (monitor != null) monitor.ShowDiagnostics = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.orientation = ScreenOrientation.Portrait;
#endif
        Destroy(this);
    }

    private void InitStyles()
    {
        if (heading != null) return;
        heading = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        info = new GUIStyle(GUI.skin.label)
            { fontSize = 25, fontStyle = FontStyle.Bold };
        helper = new GUIStyle(GUI.skin.label)
            { fontSize = 20, wordWrap = true };
        control = new GUIStyle(GUI.skin.button)
            { fontSize = 24, fontStyle = FontStyle.Bold };
        compact = new GUIStyle(GUI.skin.button)
            { fontSize = 19, fontStyle = FontStyle.Bold };
        center = new GUIStyle(GUI.skin.label)
            { fontSize = 51, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        heading.normal.textColor = Color.white;
        info.normal.textColor = Color.white;
        helper.normal.textColor = new Color(0.82f, 0.92f, 0.98f);
        center.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        InitStyles();
        float scale = Mathf.Min(Screen.width / VirtualW, Screen.height / VirtualH);
        if (scale <= 0f) return;

        Matrix4x4 prev = GUI.matrix;
        float xOffset = 0.5f * (Screen.width - VirtualW * scale);
        float yOffset = 0.5f * (Screen.height - VirtualH * scale);
        GUI.matrix = Matrix4x4.TRS(new Vector3(xOffset, yOffset, 0f),
            Quaternion.identity, new Vector3(scale, scale, 1f));

        Draw(new Rect(0f, 0f, VirtualW, VirtualH), new Color(0.055f, 0.10f, 0.18f));
        Draw(new Rect(12f, 12f, VirtualW - 24f, 720f),
            new Color(0.11f, 0.18f, 0.28f));

        GUI.Label(new Rect(40f, 26f, 590f, 60f),
            "BIOFIT DASH", heading);
        GUI.Label(new Rect(44f, 91f, 550f, 38f),
            "Puntos: " + game.ObstaclesPassed +
            "    Mejor: " + game.BestObstaclesPassed +
            "    Recorrido: " + game.Distance.ToString("F0"), info);

        if (GUI.Button(new Rect(692f, 33f, 173f, 60f),
            game.Paused ? "REANUDAR" : "PAUSA (P)", control))
            game.TogglePause();

        if (GUI.Button(new Rect(876f, 33f, 204f, 60f),
            "REINICIAR (R)", control))
            ResetGame();

        if (GUI.Button(new Rect(692f, 105f, 388f, 51f),
            bleJumpEnabled ? "SALTAR POR BLE: SI (EXPERIMENTAL)" :
                             "SALTAR POR BLE: NO", control))
        {
            if (!bleJumpEnabled && !monitor.BothTrackersConnected)
                lastInput = "No se habilito BLE: faltan trackers";
            else
            {
                bleJumpEnabled = !bleJumpEnabled;
                monitor.RecordGameAction(bleJumpEnabled ?
                    "BLE_ACTIVADO" : "BLE_DESACTIVADO");
            }
        }

        GUI.Label(new Rect(42f, 142f, 638f, 38f),
            "El cubo avanza solo. ESPACIO o SALTAR para esquivar las puntas.",
            helper);
        if (GUI.Button(new Rect(43f, 178f, 307f, 42f),
            showTestPanel ? "OCULTAR PANEL DE PRUEBAS" :
                            "MOSTRAR PANEL DE PRUEBAS", compact))
            showTestPanel = !showTestPanel;
        GUI.Label(new Rect(692f, 168f, 390f, 52f),
            "BLE: " + (monitor.BothTrackersConnected ? "2/2 conectados" :
                          "esperando trackers") +
            " | Pares: " + blePairsObserved +
            " | Intentos: " + bleJumpAttempts, helper);

        Draw(new Rect(34f, 223f, 1050f, 414f), new Color(0.13f, 0.24f, 0.37f));

        // Marquesina de suelo, desplazamiento solo visual.
        for (int i = 0; i < 15; i++)
            Draw(new Rect(34f + i * 90f - roadOffset, 608f, 47f, 7f),
                new Color(0.18f, 0.42f, 0.56f));
        Draw(new Rect(34f, BiofitDashCore.FloorY, 1050f, 8f),
            new Color(0.57f, 0.81f, 0.93f));

        // Personaje y obstáculos usan el mismo sistema de coordenadas que el motor.
        float heroTop = BiofitDashCore.FloorY -
            BiofitDashCore.HeroSize - game.JumpAltitude;
        Draw(new Rect(BiofitDashCore.HeroX, heroTop,
            BiofitDashCore.HeroSize, BiofitDashCore.HeroSize),
            new Color(0.27f, 0.87f, 0.79f));
        Draw(new Rect(BiofitDashCore.HeroX + 14f, heroTop + 17f, 8f, 8f),
            new Color(0.06f, 0.15f, 0.25f));
        Draw(new Rect(BiofitDashCore.HeroX + 43f, heroTop + 17f, 8f, 8f),
            new Color(0.06f, 0.15f, 0.25f));

        for (int i = 0; i < game.ObstacleCount; i++)
        {
            BiofitDashCore.Obstacle o = game.GetObstacle(i);
            Rect rect = new Rect(o.X, BiofitDashCore.FloorY - o.Height,
                o.Width, o.Height);
            if (spikeTex != null)
            {
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 0.40f, 0.42f);
                GUI.DrawTexture(rect, spikeTex, ScaleMode.StretchToFill, true);
                GUI.color = prevColor;
            }
            else
                Draw(rect, new Color(1f, 0.40f, 0.42f));
        }

        if (showTestPanel)
            DrawExperimentPanel();

        if (game.GameOver)
        {
            Draw(new Rect(320f, 326f, 480f, 197f),
                new Color(0.06f, 0.12f, 0.21f));
            GUI.Label(new Rect(350f, 341f, 422f, 68f),
                "FIN DEL JUEGO", center);
            GUI.Label(new Rect(398f, 414f, 358f, 36f),
                "Obstaculos superados: " + game.ObstaclesPassed, info);
            if (GUI.Button(new Rect(418f, 462f, 290f, 49f),
                "VOLVER A JUGAR", control))
                ResetGame();
        }
        else if (game.Paused)
        {
            GUI.Label(new Rect(380f, 358f, 350f, 68f), "PAUSA", center);
        }

        if (GUI.Button(new Rect(40f, 654f, 315f, 59f),
            "SALTAR (TACTIL / ESPACIO)", control))
            Jump("BOTON TACTIL");
        if (GUI.Button(new Rect(367f, 654f, 292f, 59f),
            "DIAGNOSTICO BLE", control))
        {
            ReturnToDiagnostics();
            GUI.matrix = prev;
            return;
        }

        GUI.Label(new Rect(673f, 650f, 405f, 68f),
            "Ultima entrada: " + lastInput +
            ". Pares BLE: NO son saltos confirmados.", helper);

        GUI.matrix = prev;
    }

    /// <summary>
    /// Herramientas de medicion dentro del MISMO juego y la MISMA sesion.
    /// Se pueden ocultar para jugar sin controles sobre la pista.
    /// Ningun parametro requiere firmware ni otra APK.
    /// </summary>
    private void DrawExperimentPanel()
    {
        Draw(new Rect(38f, 231f, 1037f, 190f),
            new Color(0.085f, 0.14f, 0.22f, 0.93f));

        if (GUI.Button(new Rect(47f, 238f, 224f, 42f),
            "1. INICIAR REGISTRO", compact))
            experimentStatus = monitor.StartGameCapture() ?
                "Registro iniciado. 10 s quieto y luego marca la actividad." :
                "Ya existe un registro activo. Finalizalo primero.";

        if (GUI.Button(new Rect(281f, 238f, 226f, 42f),
            "2. FINALIZAR", compact))
            experimentStatus = monitor.StopGameCapture() ?
                "Registro terminado. Toca COPIAR REGISTRO." :
                "No hay un registro de juego activo.";

        if (GUI.Button(new Rect(518f, 238f, 237f, 42f),
            "3. COPIAR REGISTRO", compact))
            experimentStatus = monitor.CopyGameCapture() ?
                "Resumen + CSV copiados. Pegalos en una nota o en ChatGPT." :
                "Primero INICIAR y FINALIZAR el registro.";

        GUI.Label(new Rect(767f, 240f, 293f, 42f),
            (monitor.GameCaptureActive ? "GRABANDO " : "REGISTRO ") +
            monitor.CaptureStage + " | " + monitor.CapturedEventCount +
            " eventos", helper);

        const float buttonW = 193f;
        string[] stages = {"QUIETO", "CAMINAR", "CORRER", "ELEVACION", "DASH"};
        for (int i = 0; i < stages.Length; i++)
        {
            string stage = stages[i];
            if (GUI.Button(new Rect(47f + i * 202f, 288f, buttonW, 40f),
                stage, compact))
                experimentStatus = monitor.MarkGameStage(stage) ?
                    "Marcado " + stage + " en el CSV." :
                    "Primero inicia el registro para marcar " + stage + ".";
        }

        if (GUI.Button(new Rect(47f, 338f, 124f, 39f),
            "UP -", compact)) monitor.AdjustJumpThreshold(-0.02f);
        GUI.Label(new Rect(181f, 343f, 197f, 32f),
            "UP: " + monitor.MinUpG.ToString("F2") + " g", helper);
        if (GUI.Button(new Rect(350f, 338f, 124f, 39f),
            "UP +", compact)) monitor.AdjustJumpThreshold(+0.02f);

        if (GUI.Button(new Rect(496f, 338f, 117f, 39f),
            "PAR -", compact)) monitor.AdjustPairWindow(-0.02f);
        GUI.Label(new Rect(626f, 343f, 209f, 32f),
            "Par: " + monitor.PairWindowMs.ToString("F0") + " ms", helper);
        if (GUI.Button(new Rect(853f, 338f, 120f, 39f),
            "PAR +", compact)) monitor.AdjustPairWindow(+0.02f);

        GUI.Label(new Rect(48f, 386f, 994f, 30f),
            experimentStatus + "  |  PARES: " + monitor.TotalPairs +
            "  |  descartes CSV: " + monitor.DroppedEventCount, helper);
    }

    private static void Draw(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void BuildSpikeTexture()
    {
        spikeTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        spikeTex.filterMode = FilterMode.Point;
        spikeTex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[64 * 64];
        // Unity Texture2D tiene origen (0,0) en la parte inferior.
        // Triangulo con punta arriba: anchura decrece hacia y=63.
        for (int y = 0; y < 64; y++)
        {
            float half = 31f * (1f - y / 63f);
            for (int x = 0; x < 64; x++)
                pixels[y * 64 + x] = Mathf.Abs(x - 31.5f) <= half ?
                    Color.white : new Color(1f, 1f, 1f, 0f);
        }
        spikeTex.SetPixels(pixels);
        spikeTex.Apply();
    }
}
