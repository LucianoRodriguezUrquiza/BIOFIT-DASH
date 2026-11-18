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
    // OFF=0, bilateral LEGACY=1, unilateral izquierda=2, derecha=3, bilateral FASE=4.
    // Los modos 2 y 3 sirven para diagnosticar la cadena BLE, NO saltos reales.
    private int bleMode;
    private byte thresholdFoot = 1;
    private readonly BiofitSingleFootDiagnostic soloDiagnostic = new BiofitSingleFootDiagnostic();
    private int singleCandidates;
    private int singleJumps;
    private int blePairsObserved;
    private int bleJumpAttempts;
    private int bleJumpsAccepted;
    private int jumpCommandNumber;
    private BiofitDashCore.JumpResult lastJumpResult;
    private readonly BiofitJumpBuffer jumpBuffer = new BiofitJumpBuffer();
    private int clampedFrames;
    private float nextFrameDiagnostic;
    private string lastInput = "ESPACIO o boton SALTAR";
    private GUIStyle heading, info, helper, control, center;
    private Texture2D spikeTex;
    private float roadOffset;
    private bool showTestPanel = true;
    private string experimentStatus = "Registro sin iniciar. Una sola sesion.";
    private bool visualJumpPending;
    private double acceptedJumpAt;
    private byte acceptedJumpFoot;
    private string acceptedJumpSource;
    private GUIStyle compact, panelSmall, hudStatus, captureHeading;
    private bool showArchivedSessions;
    private int archivePage;

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
        monitor.PhaseFastPairs = bleMode == 4;
        monitor.BilateralJumpCandidate += OnCandidatePair;
        monitor.AnkleUpCandidate += OnSingleCandidate;
    }

    private void OnDisable()
    {
        if (monitor != null) monitor.PhaseFastPairs = false;
        jumpBuffer.Clear();
        if (monitor != null)
        {
            monitor.BilateralJumpCandidate -= OnCandidatePair;
            monitor.AnkleUpCandidate -= OnSingleCandidate;
        }
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
        if ((bleMode != 1 && bleMode != 4) || !monitor.BothTrackersConnected)
        {
            monitor.RecordDecision(0, "PAR_IGNORADO_MODO_O_CONEXION");
            return;
        }
        bleJumpAttempts++;
        if (bleMode == 4 && monitor.LastPairedWasFastPhase)
            monitor.RecordGameAction("PAR_FASE_RAPIDO_VALIDADO");
        lastInput = (bleMode == 4 ? "PAR FASE " : "PAR BLE ") +
            (monitor.LastPairedWasFastPhase ? "RAPIDO " : "") +
            "(" + gapMs.ToString("F0") + " ms)";
        bool jumped = SubmitJump(0, "BILATERAL");
        if (jumped) bleJumpsAccepted++;
        bool buffered = lastJumpResult == BiofitDashCore.JumpResult.Buffered;
        monitor.RecordDecision(0, jumped ? "PAR_SALTO" :
            buffered ? "PAR_BUFFERED_180MS" : "PAR_RECHAZADO_" + lastJumpResult);
        monitor.RecordGameAction(jumped ? "SALTO_BLE_REALIZADO" :
            buffered ? "SALTO_BLE_BUFFERED" : "SALTO_BLE_NO_REALIZADO");
    }

    private void OnSingleCandidate(byte foot, float verticalG,
        float eventSeconds)
    {
        if (!monitor.BothTrackersConnected || bleMode < 2 || bleMode == 4) return;
        if ((bleMode == 2 && foot != 1) ||
            (bleMode == 3 && foot != 2)) return;

        BiofitSingleFootDiagnostic.Decision decision = soloDiagnostic.Observe(
            foot, verticalG, eventSeconds, soloDiagnostic.Threshold(foot),
            BiofitBilateralJumpDetector.SameAnkleRefractorySeconds);
        string side = foot == 1 ? "IZQ" : "DER";
        if (decision != BiofitSingleFootDiagnostic.Decision.Accepted)
        {
            string reason = decision == BiofitSingleFootDiagnostic.Decision.BelowThreshold ?
                "UMBRAL" : decision == BiofitSingleFootDiagnostic.Decision.Refractory ?
                "ANTIRREBOTE" : "INVALIDO";
            monitor.RecordGameAction("SOLO_" + side + "_DESCARTADO_" + reason);
            monitor.RecordDecision(foot,reason);
            lastInput = "SOLO " + side + ": " + reason +
                " (" + verticalG.ToString("F2") + " g)";
            return;
        }

        singleCandidates++;
        monitor.RecordGameAction("UP_SOLO_CANDIDATO");
        bool jumped = SubmitJump(foot, side);
        soloDiagnostic.RecordGameResult(foot, jumped);
        monitor.RecordDecision(foot,jumped ? "SALTO" : "RECHAZADO_" + lastJumpResult);
        if (jumped)
        {
            singleJumps++;
        }
        lastInput = "UP SOLO " + side + " (" + verticalG.ToString("F2") +
            " g)" + (jumped ? " SALTO" : " CUBO OCUPADO");
        monitor.RecordGameAction(jumped ?
            "SALTO_SOLO_REALIZADO" : "SALTO_SOLO_NO_REALIZADO");
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
        if (Time.deltaTime > 0.05f && game.Playing && !game.Paused)
        {
            clampedFrames++;
            if (Time.realtimeSinceStartup >= nextFrameDiagnostic)
            {
                nextFrameDiagnostic = Time.realtimeSinceStartup + 1f;
                monitor.RecordControlDiagnostic("FRAME_CLAMP count=" + clampedFrames +
                    " realDt=" + Time.deltaTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " simulatedDt=0.05");
            }
        }
        game.Step(dt);
        BiofitJumpBuffer.Outcome completed = jumpBuffer.Tick(game, MonotonicSeconds());
        if (completed.State != BiofitJumpBuffer.Completion.None)
        {
            monitor.RecordControlDiagnostic("JUMP_BUFFER_" + completed.State +
                " id=" + completed.CommandId + " source=" + completed.Source +
                " elapsedMs=" + completed.AgeMilliseconds.ToString("F1",
                    System.Globalization.CultureInfo.InvariantCulture));
            if (completed.State == BiofitJumpBuffer.Completion.Executed)
            {
                TrackAcceptedJump(completed.Foot, completed.Source);
                if (completed.Source == "BILATERAL")
                {
                    bleJumpsAccepted++;
                    monitor.RecordDecision(0, "PAR_SALTO_BUFFERED");
                    monitor.RecordGameAction("SALTO_BLE_REALIZADO");
                }
            }
        }
        if (visualJumpPending && game.JumpAltitude > 0f)
        {
            double now = System.Diagnostics.Stopwatch.GetTimestamp() /
                (double)System.Diagnostics.Stopwatch.Frequency;
            monitor.RecordFirstJumpFrame(acceptedJumpFoot, acceptedJumpSource,
                (now - acceptedJumpAt) * 1000.0);
            visualJumpPending = false;
        }
        if (game.Playing && !game.Paused)
            roadOffset = (roadOffset + game.Speed * dt) % 90f;
    }

    private void TrackAcceptedJump(byte foot, string source)
    {
        visualJumpPending = true;
        acceptedJumpAt = System.Diagnostics.Stopwatch.GetTimestamp() /
            (double)System.Diagnostics.Stopwatch.Frequency;
        acceptedJumpFoot = foot;
        acceptedJumpSource = source;
    }

    private static double MonotonicSeconds()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp() /
            (double)System.Diagnostics.Stopwatch.Frequency;
    }

    private bool SubmitJump(byte foot, string source)
    {
        int id = ++jumpCommandNumber;
        // Only a valid BILATERAL pair may use the 120ms assist.
        // Solo diagnostics and manual input retain v0.9.7 immediate semantics.
        BiofitDashCore.JumpResult result = jumpBuffer.Submit(
            game, MonotonicSeconds(), foot, source, id, source == "BILATERAL");
        lastJumpResult = result;
        monitor.RecordControlDiagnostic("JUMP_COMMAND id=" + id +
            " source=" + source + " result=" + result +
            " altitude=" + game.JumpAltitude.ToString("F3",
                System.Globalization.CultureInfo.InvariantCulture) +
            " timeToLandMs=" + (game.EstimatedSecondsUntilLanding * 1000d).ToString("F1",
                System.Globalization.CultureInfo.InvariantCulture) +
            " gameJumps=" + game.SuccessfulJumps);
        if (result == BiofitDashCore.JumpResult.Accepted) TrackAcceptedJump(foot, source);
        return result == BiofitDashCore.JumpResult.Accepted;
    }

    private void Jump(string input)
    {
        if (SubmitJump(0, "MANUAL"))
        {
            lastInput = input;
            monitor.RecordGameAction("SALTO_MANUAL");
        }
    }

    private void ResetGame()
    {
        jumpBuffer.Clear(); // Never carry a command into a new run.
        visualJumpPending = false;
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
        panelSmall = new GUIStyle(GUI.skin.label)
            { fontSize = 17, wordWrap = true };
        hudStatus = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
        captureHeading = new GUIStyle(GUI.skin.label) {
            fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = false,
            alignment = TextAnchor.MiddleLeft };
        captureHeading.normal.textColor = new Color(0.95f, 0.96f, 0.72f);
        hudStatus.normal.textColor = Color.white;
        panelSmall.normal.textColor = new Color(0.86f, 0.95f, 1f);
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
        BiofitDashLayout.Layout layout = BiofitDashLayout.ForScreen(
            Screen.width, Screen.height, showTestPanel);
        float viewW = layout.Width;
        Matrix4x4 prev = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1f));

        Draw(new Rect(0, 0, viewW, layout.Height),
            new Color(0.055f, 0.105f, 0.18f));
        Draw(new Rect(12f, 12f, viewW - 24f, 122f),
            new Color(0.11f, 0.18f, 0.28f));

        GUI.Label(new Rect(32f, 21f, 375f, 57f), "BIOFIT DASH", heading);
        float controlsX = viewW - 719f;
        string score = controlsX < 645f ?
            "P: " + game.ObstaclesPassed + "   M: " + game.BestObstaclesPassed +
            "   R: " + game.Distance.ToString("F0") :
            "Puntos: " + game.ObstaclesPassed + "   Mejor: " +
            game.BestObstaclesPassed + "   Recorrido: " +
            game.Distance.ToString("F0");
        GUI.Label(new Rect(34f, 84f, Mathf.Max(330f, controlsX - 48f), 38f),
            score, info);
        if (GUI.Button(new Rect(controlsX, 29f, 208f, 46f),
            game.PracticeMode ? "SIN COLISION: SI" : "SIN COLISION: NO", compact))
        {
            game.PracticeMode = !game.PracticeMode;
            if (game.GameOver) ResetGame();
        }
        if (GUI.Button(new Rect(controlsX + 217f, 29f, 211f, 46f),
            showTestPanel ? "OCULTAR PANEL" : "MOSTRAR PANEL", compact))
            showTestPanel = !showTestPanel;
        if (GUI.Button(new Rect(controlsX + 439f, 29f, 118f, 46f),
            game.Paused ? "REANUDAR" : "PAUSA", compact))
            game.TogglePause();
        if (GUI.Button(new Rect(controlsX + 568f, 29f, 126f, 46f),
            "REINICIAR", compact))
            ResetGame();

        string modeLabel = bleMode == 0 ? "BLE: OFF" :
            bleMode == 1 ? "BLE: BILATERAL" :
            bleMode == 2 ? "BLE: SOLO IZQ" :
            bleMode == 3 ? "BLE: SOLO DER" : "BLE: FASE EXP";
        if (GUI.Button(new Rect(controlsX, 83f, 315f, 43f),
            modeLabel, compact))
        {
            int next = (bleMode + 1) % 5;
            if (next != 0 && !monitor.BothTrackersConnected)
                lastInput = "Faltan trackers para activar BLE";
            else
            {
                bleMode = next;
                monitor.PhaseFastPairs = bleMode == 4;
                string action = bleMode == 0 ? "BLE_MODO_APAGADO" :
                    bleMode == 1 ? "BLE_ACTIVADO" :
                    bleMode == 2 ? "BLE_SOLO_IZQUIERDO" :
                    bleMode == 3 ? "BLE_SOLO_DERECHO" : "BLE_FASE_EXPERIMENTAL";
                monitor.RecordGameAction(action);
                monitor.RecordSingleFootConfig(1,soloDiagnostic.LeftThresholdG);
                monitor.RecordSingleFootConfig(2,soloDiagnostic.RightThresholdG);
                lastInput = "Modo BLE: " + (bleMode == 0 ? "OFF" :
                    bleMode == 1 ? "BILATERAL" :
                    bleMode == 2 ? "SOLO IZQ" :
                    bleMode == 3 ? "SOLO DER" : "FASE EXP (FW2.2.8)");
            }
        }
        string connection = monitor.BothTrackersConnected ? "2/2" : "ESPERANDO";
        string sync = monitor.ClockAlignmentReady ? "ALINEADO" : "SIN ANCLAS";
        GUI.Label(new Rect(controlsX + 327f, 86f, 359f, 40f),
            "Sensores: " + connection + "   Reloj: " + sync +
            "\n" + monitor.ReadinessSummary, hudStatus);

        // Game and diagnostics occupy separate, non-overlapping rectangles.
        GUI.BeginGroup(new Rect(layout.GameX, BiofitDashLayout.WorldTop,
            layout.GameWidth, BiofitDashLayout.WorldHeight));
        Draw(new Rect(0f, 0f, layout.GameWidth, BiofitDashLayout.WorldHeight),
            new Color(0.12f, 0.22f, 0.33f));
        float floorLocal = BiofitDashCore.FloorY - BiofitDashLayout.WorldTop;
        Draw(new Rect(0f, floorLocal, layout.GameWidth, 8f),
            new Color(0.57f, 0.81f, 0.93f));
        int dashCount = Mathf.CeilToInt(layout.GameWidth / 90f) + 2;
        for (int i = 0; i < dashCount; i++)
            Draw(new Rect(i * 90f - roadOffset, floorLocal + 2f, 46f, 6f),
                new Color(0.18f, 0.42f, 0.56f));

        float heroTop = BiofitDashCore.FloorY -
            BiofitDashCore.HeroSize - game.JumpAltitude;
        float hx = BiofitDashCore.HeroX - layout.GameX;
        Draw(new Rect(hx, heroTop - BiofitDashLayout.WorldTop,
            BiofitDashCore.HeroSize, BiofitDashCore.HeroSize),
            new Color(0.38f, 0.93f, 0.87f));
        Draw(new Rect(hx + 18f, heroTop - BiofitDashLayout.WorldTop + 20f,
            8f, 8f), new Color(0.2f, 0.32f, 0.43f));
        Draw(new Rect(hx + 46f, heroTop - BiofitDashLayout.WorldTop + 20f,
            8f, 8f), new Color(0.2f, 0.32f, 0.43f));

        for (int i = 0; i < game.ObstacleCount; i++)
        {
            BiofitDashCore.Obstacle o = game.GetObstacle(i);
            Rect rect = new Rect(o.X - layout.GameX,
                floorLocal - o.Height, o.Width, o.Height);
            if (spikeTex != null)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.46f, 0.54f);
                GUI.DrawTexture(rect, spikeTex, ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            else Draw(rect, new Color(1f, 0.46f, 0.54f));
        }

        if (game.GameOver)
        {
            float cx = Mathf.Max(30f, (layout.GameWidth - 440f) / 2f);
            Draw(new Rect(cx, 150f, 440f, 200f),
                new Color(0.06f, 0.12f, 0.21f));
            GUI.Label(new Rect(cx + 25f, 166f, 390f, 60f),
                "FIN DEL JUEGO", center);
            if (GUI.Button(new Rect(cx + 88f, 266f, 264f, 53f),
                "VOLVER A JUGAR", control))
                ResetGame();
        }
        else if (game.Paused)
        {
            GUI.Label(new Rect(Mathf.Max(0f,
                (layout.GameWidth - 350f) / 2f), 160f, 350f, 68f),
                "PAUSA", center);
        }
        GUI.EndGroup();

        if (showTestPanel)
            DrawExperimentPanel(layout.PanelX, layout.PanelWidth);

        if (GUI.Button(new Rect(29f, 647f, 217f, 67f),
            "SALTAR", control))
            Jump("BOTON TACTIL");
        if (GUI.Button(new Rect(258f, 647f, 250f, 67f),
            "DIAGNOSTICO BLE", compact))
        {
            ReturnToDiagnostics();
            GUI.matrix = prev;
            return;
        }
        GUI.Label(new Rect(527f, 651f, showTestPanel ? layout.PanelX - 545f : viewW - 552f, 63f),
            "Ultima entrada: " + lastInput, helper);
        GUI.matrix = prev;
    }

    /// <summary>
    /// Diagnostic sidebar: no longer drawn on top of the gameplay.
    /// Every button is available in landscape without scrolling or overlap.
    /// </summary>
    private void DrawExperimentPanel(float x, float pw)
    {
        Draw(new Rect(x, BiofitDashLayout.WorldTop, pw,
            BiofitDashLayout.WorldHeight), new Color(0.09f, 0.16f, 0.24f));
        GUI.Label(new Rect(x + 12f, 149f, pw - 272f, 34f),
            "v0.9.10", info);
        // EXP and RAW together. Extra RAW width accommodates RAW: ESPERE.
        if (GUI.Button(new Rect(x + pw - 252f, 149f, 108f, 32f),
            soloDiagnostic.ExperimentalMode ? "EXP: SI" : "EXP: NO", compact))
        {
            soloDiagnostic.SetExperimentalMode(!soloDiagnostic.ExperimentalMode);
            monitor.RecordGameAction(soloDiagnostic.ExperimentalMode ?
                "MODO_EXP_UMBRAL_ON" : "MODO_EXP_UMBRAL_OFF");
            monitor.RecordSingleFootConfig(1, soloDiagnostic.LeftThresholdG);
            monitor.RecordSingleFootConfig(2, soloDiagnostic.RightThresholdG);
            experimentStatus = soloDiagnostic.ExperimentalMode ?
                "EXP ACTIVO: umbral unilateral minimo 0.15 g" :
                "EXP OFF: umbrales >= 0.25 g restaurados";
        }
        if (GUI.Button(new Rect(x + pw - 136f, 149f, 125f, 32f),
            monitor.RawButtonLabel, compact))
            monitor.ToggleRawCapture();
        string phase = monitor.RawStatus.StartsWith("RAW OFF: primero") ?
            "RAW: PRESIONAR INICIAR PRIMERO" :
            monitor.RawStatus.StartsWith("RAW OFF: conectar") ?
            "RAW: CONECTAR AMBOS SENSORES" :
            monitor.ExportBusy ? "EXPORTANDO - ver progreso abajo" :
            monitor.CaptureSaving ? "FINALIZANDO - esperando CSV" :
            monitor.GameCaptureActive ? "GRABANDO: " + monitor.CaptureStage :
            monitor.ExportStatus.StartsWith("ERROR") ? "ERROR EXPORTAR - ver abajo" :
            monitor.CaptureFileStatus.StartsWith("ERROR") ? "ERROR ARCHIVO - ver abajo" :
            monitor.ExportStatus.StartsWith("EXPORTADO") ? "EXPORTADO: CSV VERIFICADO" :
            !string.IsNullOrEmpty(monitor.SavedCapturePath) ? "GUARDADO PRIVADO: OK" :
            "SIN GRABACION - iniciar";
        GUI.Label(new Rect(x + 12f, 182f, pw - 24f, 25f),
            phase, captureHeading);

        float cw = (pw - 32f) / 3f;
        if (GUI.Button(new Rect(x + 10f, 208f, cw, 38f),
            "INICIAR", compact))
        {
            bool started = monitor.StartGameCapture();
            if (started)
            {
                jumpBuffer.Clear();
                soloDiagnostic.Reset();
                singleCandidates = 0;
                singleJumps = 0;
                blePairsObserved = bleJumpAttempts = bleJumpsAccepted = jumpCommandNumber = clampedFrames = 0;
                nextFrameDiagnostic = 0;
                monitor.RecordControlDiagnostic("CONTROL_VERSION=0.9.10 pairCooldownS=0.90 fastPhaseMinGapS=0.60 bothEvidence=1 sameAnkleS=0.32 jumpBufferMs=180 oneSlot=1 bleMode=" + bleMode + " practice=" + game.PracticeMode);
                monitor.RecordGameAction(soloDiagnostic.ExperimentalMode ? "MODO_EXP_UMBRAL_ON" : "MODO_EXP_UMBRAL_OFF");
                monitor.RecordSingleFootConfig(1,soloDiagnostic.LeftThresholdG);
                monitor.RecordSingleFootConfig(2,soloDiagnostic.RightThresholdG);
            }
            experimentStatus = started ?
                "Registro iniciado: comenzar con 10 s quieto." :
                "Ya existe un registro activo.";
        }
        if (GUI.Button(new Rect(x + 16f + cw, 208f, cw, 38f),
            "FINALIZAR", compact))
        {
            monitor.StopGameCapture();
            experimentStatus = monitor.CaptureFileStatus;
        }
        if (GUI.Button(new Rect(x + 22f + 2f*cw, 208f, cw, 38f),
            "ARCHIVO", compact))
        {
            monitor.RequestExport(false);
            experimentStatus = monitor.ExportStatus;
        }

        string[] stages = { "QUIETO", "CAMINAR", "CORRER", "ELEVACION", "DASH" };
        for (int i = 0; i < stages.Length; i++)
        {
            int col = i < 3 ? i : i - 3;
            float sy = i < 3 ? 254f : 293f;
            float sw = i < 3 ? cw : (pw - 26f) / 2f;
            float sx = x + 10f + col * (sw + 6f);
            string stage = stages[i];
            if (GUI.Button(new Rect(sx, sy, sw, 35f), stage, compact))
                experimentStatus = monitor.MarkGameStage(stage) ?
                    "Etapa: " + stage : "Inicia el registro primero.";
        }

        float labelW = pw - 150f;
        if (GUI.Button(new Rect(x + 12f, 341f, 53f, 38f), "-", compact))
            monitor.AdjustJumpThreshold(-0.02f);
        GUI.Label(new Rect(x + 72f, 347f, labelW, 30f),
            "UP: " + monitor.MinUpG.ToString("F2") + " g", panelSmall);
        if (GUI.Button(new Rect(x + pw - 66f, 341f, 53f, 38f), "+", compact))
            monitor.AdjustJumpThreshold(+0.02f);

        if (GUI.Button(new Rect(x + 12f, 386f, 53f, 38f), "-", compact))
            monitor.AdjustPairWindow(-0.02f);
        GUI.Label(new Rect(x + 72f, 392f, labelW, 30f),
            "PAR: " + monitor.PairWindowMs.ToString("F0") + " ms", panelSmall);
        if (GUI.Button(new Rect(x + pw - 66f, 386f, 53f, 38f), "+", compact))
            monitor.AdjustPairWindow(+0.02f);

        if (GUI.Button(new Rect(x + 12f, 431f, 53f, 38f), "-", compact))
        {
            soloDiagnostic.AdjustThreshold(thresholdFoot,-0.05f);
            monitor.RecordSingleFootConfig(thresholdFoot,soloDiagnostic.Threshold(thresholdFoot));
        }
        if (GUI.Button(new Rect(x + 72f, 431f, labelW, 38f),
            (thresholdFoot==1?"IZQ: ":"DER: ") + soloDiagnostic.Threshold(thresholdFoot).ToString("F2") + " g", compact))
            thresholdFoot=thresholdFoot==1?(byte)2:(byte)1;
        if (GUI.Button(new Rect(x + pw - 66f, 431f, 53f, 38f), "+", compact))
        {
            soloDiagnostic.AdjustThreshold(thresholdFoot,0.05f);
            monitor.RecordSingleFootConfig(thresholdFoot,soloDiagnostic.Threshold(thresholdFoot));
        }

        BiofitSingleFootDiagnostic.Counters l = soloDiagnostic.Left;
        BiofitSingleFootDiagnostic.Counters r = soloDiagnostic.Right;
        GUI.Label(new Rect(x + 11f, 478f, pw - 22f, 47f),
            "IZQ | UP " + l.Received + "  bajo " + l.BelowThreshold +
            "  anti " + l.Refractory + "  salto " + l.Jumped +
            "  ocupado " + l.GameUnavailable, panelSmall);
        GUI.Label(new Rect(x + 11f, 519f, pw - 22f, 47f),
            "DER | UP " + r.Received + "  bajo " + r.BelowThreshold +
            "  anti " + r.Refractory + "  salto " + r.Jumped +
            "  ocupado " + r.GameUnavailable, panelSmall);
        GUI.Label(new Rect(x + 11f, 562f, pw - 22f, 27f),
            "Pares " + blePairsObserved + " | intentos " + bleJumpAttempts +
            " | saltos " + bleJumpsAccepted + " | faseRap " + monitor.FastPhasePairCount +
            (jumpBuffer.Pending ? " | BUF" : ""),
            panelSmall);
        // Do not put the long RAW summary (hundreds of characters) inside
        // the narrow 55px capture status. Full diagnostics remain in CSV.
        string shortCapture = monitor.CaptureFileStatus;
        if(shortCapture.StartsWith("CSV guardado SHA256 OK"))
            shortCapture=shortCapture.Contains("ERROR RAW") ?
                "CSV OK; RAW ERROR: revisar SESIONES" :
                shortCapture.Contains("+ RAW:") ? "CSV + RAW privados SHA256 OK" :
                "CSV privado SHA256 OK";
        string shortExport=monitor.ExportStatus;
        if(shortExport.StartsWith("EXPORTADO"))
            shortExport="EXPORTADO y verificado en destino";
        GUI.Label(new Rect(x + 11f, 583f, pw - 120f, 55f),
            monitor.ExportBusy || monitor.ExportStatus.StartsWith("ERROR") ||
            monitor.ExportStatus.StartsWith("EXPORTADO") ?
                shortExport : (monitor.CaptureSaving ||
                monitor.CaptureFileStatus.StartsWith("ERROR") ||
                !string.IsNullOrEmpty(monitor.SavedCapturePath) ?
                shortCapture : experimentStatus), panelSmall);
        if(GUI.Button(new Rect(x+pw-114f,587f,103f,39f),"RESCATE",compact))
            monitor.RequestExport(true);
        float diagnosticWidth=(pw-26f)/2f;
        if(GUI.Button(new Rect(x+10f,647f,diagnosticWidth,38f),
            showArchivedSessions?"CERRAR LISTA":"SESIONES",compact))
        {
            showArchivedSessions=!showArchivedSessions;
            if(showArchivedSessions){archivePage=0;monitor.ScanSessionHistory();}
        }
        if(GUI.Button(new Rect(x+16f+diagnosticWidth,647f,diagnosticWidth,38f),
            monitor.SyncLabel,compact))
            monitor.MarkVideoSync();
        // Separate RAW status from archive/export state. Show the precise
        // prerequisite or ACK failure rather than truncate it after export.
        GUI.Label(new Rect(x+10f,688f,pw-20f,50f),
            monitor.RawStatus+"\nRAW IZQ/DER: "+monitor.RawCount,panelSmall);
        if(showArchivedSessions)DrawArchiveBrowser(x,pw);
    }

    private void DrawArchiveBrowser(float x, float pw)
    {
        // Overlay is confined to the diagnostic sidebar, not the avatar.
        Draw(new Rect(x+5f,252f,pw-10f,320f),new Color(0.055f,0.11f,0.18f));
        GUI.Label(new Rect(x+15f,259f,pw-30f,31f),
            "SESIONES PRIVADAS - elegir una",panelSmall);
        if(monitor.ScanningSessions)
        {
            GUI.Label(new Rect(x+15f,300f,pw-30f,90f),
                "Buscando y verificando SHA256...",panelSmall);
            return;
        }
        BiofitBLEMonitor.StoredSession[] items=monitor.StoredSessions;
        if(items.Length==0)
        {
            GUI.Label(new Rect(x+15f,300f,pw-30f,90f),
                monitor.ArchiveStatus,panelSmall);
            return;
        }
        int first=archivePage*3;
        for(int i=0;i<3 && first+i<items.Length;i++)
        {
            int index=first+i;
            var item=items[index];
            float yy=296f+i*70f;
            string state=item.MainValid?"SHA OK":"INCOMPLETO / ERROR SHA";
            string raw=item.RawValid?" | RAW OK":(System.IO.File.Exists(item.RawPath)?" | RAW SHA ERROR":(item.RawPartial?" | RAW PARCIAL":""));
            string name=System.IO.Path.GetFileName(item.MainPath);
            // Filename keeps the session timestamp and the stable pairing ID.
            string shown=name.Length>35?name.Substring(0,35)+"...":name;
            if(GUI.Button(new Rect(x+15f,yy,pw-30f,33f),
                (monitor.SelectedStoredSession==index?"[ELEGIDA] ":"")+
                shown,compact))
                monitor.SelectStoredSession(index);
            GUI.Label(new Rect(x+18f,yy+33f,pw-36f,28f),
                state+raw,panelSmall);
        }
        if(GUI.Button(new Rect(x+15f,527f,95f,37f),"ANTERIOR",compact) && archivePage>0)
            archivePage--;
        GUI.Label(new Rect(x+120f,532f,pw-240f,28f),
            (archivePage+1)+"/"+((items.Length+2)/3),panelSmall);
        if(GUI.Button(new Rect(x+pw-111f,527f,95f,37f),"SIGUIENTE",compact) &&
           (archivePage+1)*3<items.Length)
            archivePage++;
        // ARCHIVO and RESCATE above the overlay export the selected item.
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
