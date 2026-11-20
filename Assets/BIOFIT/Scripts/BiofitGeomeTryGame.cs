using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First playable BIOFIT/GeomeTry-inspired level. Self-contained assets so it
/// can be generated without replacing the user's untracked Unity scenes.
/// Uses legacy UP_IMPULSE pairs, never the LIFT_ONSET experimental channel.
/// </summary>
[RequireComponent(typeof(BiofitBLEMonitor))]
public sealed class BiofitGeomeTryGame : MonoBehaviour
{
    private const double BleMaximumAgeSeconds = 0.18d;
    private const float ObstacleStartX = 10f;
    private const float ObstacleSpacing = 8.4f; // 1.29 simulated/real s at 6.5 units/s
    private const int ObstacleCount = 10;
    private const float StartX = 0f;
    private const float FinishX = ObstacleStartX + ObstacleCount * ObstacleSpacing;

    private readonly BiofitGeomeTryCommandGate commandGate = new BiofitGeomeTryCommandGate();
    private BiofitBLEMonitor monitor;
    private BiofitGeomeTryCube cube;
    private Camera gameCamera;
    private GameObject levelRoot;
    private Sprite unitSprite;
    private Sprite spikeSprite;
    private bool bleEnabled = true; // selected control: BILATERAL LEGACY
    private bool paused;
    private bool completed;
    private bool died;
    private float diedAt;
    private long nextCommandId;
    private int executedJumps;
    private int rejectedJumps;
    private int blePairs;
    private string lastDecision = "Listo: LEGACY o boton SALTAR";
    private float previousTimeScale = 1f;

    private static double NowSeconds =>
        System.Diagnostics.Stopwatch.GetTimestamp() /
        (double)System.Diagnostics.Stopwatch.Frequency;

    private void Awake()
    {
        monitor = GetComponent<BiofitBLEMonitor>();
        monitor.ShowDiagnostics = false;
        monitor.PhaseFastPairs = false;
        monitor.LiftCycleMode = false;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f; // deliberately slower than original GeomeTry 1.5
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif
        BuildNewLevel();
    }

    private void OnEnable()
    {
        if (monitor == null) monitor = GetComponent<BiofitBLEMonitor>();
        monitor.LegacyBilateralCandidateTimed += OnLegacyPair;
    }

    private void OnDisable()
    {
        if (monitor != null)
            monitor.LegacyBilateralCandidateTimed -= OnLegacyPair;
    }

    private void OnDestroy()
    {
        if (cube != null) cube.Died -= OnCubeDied;
        Time.timeScale = previousTimeScale;
        if (unitSprite != null)
        {
            Texture2D texture = unitSprite.texture;
            Destroy(unitSprite);
            Destroy(texture);
        }
        if (spikeSprite != null)
        {
            Texture2D texture = spikeSprite.texture;
            Destroy(spikeSprite);
            Destroy(texture);
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        // No pending commands to carry across foreground/background.
        if (isPaused && !paused) SetPaused(true);
    }

    private void OnLegacyPair(float gapMilliseconds, double originSeconds)
    {
        blePairs++;
        if (!bleEnabled || paused || died || completed || monitor.ShowDiagnostics ||
            !monitor.BothTrackersConnected || monitor.LiftCycleMode)
        {
            Reject("BLE_IGNORADO_MODO_ESTADO_CONEXION");
            return;
        }

        // Do not replace originSeconds with now: stale BLE bursts must not
        // become fresh commands merely because Unity received them late.
        SubmitJump("BLE_LEGACY", originSeconds, BleMaximumAgeSeconds,
            gapMilliseconds);
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys != null)
        {
            if (keys.spaceKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame)
                ManualJump();
            if (keys.rKey.wasPressedThisFrame) BuildNewLevel();
            if (keys.pKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                SetPaused(!paused);
        }

        if (died && !paused && Time.unscaledTime - diedAt >= 0.85f)
            BuildNewLevel();

        if (!completed && !died && cube != null && cube.Alive &&
            cube.transform.position.x >= FinishX)
        {
            completed = true;
            cube.Running = false;
            lastDecision = "NIVEL COMPLETADO";
            Record("LEVEL_COMPLETED");
        }
    }

    private void LateUpdate()
    {
        if (gameCamera == null || cube == null) return;
        Vector3 p = cube.transform.position;
        gameCamera.transform.position = new Vector3(p.x + 3.4f, 2.8f, -10f);
    }

    private void ManualJump()
    {
        SubmitJump("MANUAL", NowSeconds, 1d, 0f);
    }

    private void SubmitJump(string source, double originSeconds, double maxAge, float gapMs)
    {
        long id = ++nextCommandId;
        double now = NowSeconds;
        var gate = commandGate.Evaluate(id, originSeconds, now, maxAge);
        string reason;

        if (gate != BiofitGeomeTryCommandGate.Decision.Accepted)
            reason = gate.ToString();
        else if (paused || monitor.ShowDiagnostics)
            reason = "Paused";
        else if (died || completed || cube == null || !cube.Alive)
            reason = "Unavailable";
        else if (!cube.TryJump())
            reason = "AirborneOrNotGrounded";
        else
        {
            executedJumps++;
            lastDecision = "SALTO " + source + " #" + id;
            Record("JUMP_EXECUTED id=" + id + " src=" + source +
                " gapMs=" + gapMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                " eventAgeMs=" + ((now - originSeconds) * 1000d).ToString("F1",
                    System.Globalization.CultureInfo.InvariantCulture));
            monitor.RecordGameAction("GEOMETRY_SALTO_" + source);
            return;
        }

        Reject(source + " id=" + id + " reason=" + reason +
               " ageMs=" + ((now - originSeconds) * 1000d).ToString("F1",
                   System.Globalization.CultureInfo.InvariantCulture));
    }

    private void Reject(string reason)
    {
        rejectedJumps++;
        lastDecision = reason;
        Record("JUMP_REJECTED " + reason);
    }

    private void Record(string message)
    {
        if (monitor != null) monitor.RecordControlDiagnostic("GEOMETRY " + message);
    }

    private void SetPaused(bool pause)
    {
        paused = pause;
        Time.timeScale = paused ? 0f : 1f;
        if (cube != null && cube.Alive) cube.Running = !paused && !completed && !died;
        Record(paused ? "PAUSED" : "RESUMED");
    }

    private void OnCubeDied()
    {
        died = true;
        diedAt = Time.unscaledTime;
        lastDecision = "COLISION - REINICIO AUTOMATICO";
        Record("LEVEL_DEATH");
    }

    private void BuildNewLevel()
    {
        if (cube != null)
        {
            cube.Died -= OnCubeDied;
            cube.Running = false;
            cube = null;
        }
        if (levelRoot != null) Destroy(levelRoot);
        levelRoot = new GameObject("BIOFIT_GEOMETRY_LEVEL");
        // Keep generated visuals lightweight; no original music, sprites or
        // third-party prefab assets are imported into the user's project.
        if (unitSprite == null) unitSprite = CreateUnitSprite();
        if (spikeSprite == null) spikeSprite = CreateSpikeSprite();

        GameObject ground = VisualObject("Ground", new Vector3(48f, -0.5f, 0f),
            new Vector3(135f, 1f, 1f), new Color(0.15f, 0.55f, 0.64f),
            unitSprite, -1);
        BoxCollider2D groundCollider = ground.AddComponent<BoxCollider2D>();

        GameObject player = new GameObject("BIOFIT_GEOMETRY_CUBE");
        player.transform.SetParent(levelRoot.transform);
        player.transform.position = new Vector3(StartX, 0.53f, 0f);
        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = BiofitGeomeTryCube.GravityScale;
        player.AddComponent<BoxCollider2D>();
        GameObject drawing = new GameObject("CubeArtwork");
        drawing.transform.SetParent(player.transform, false);
        SpriteRenderer sprite = drawing.AddComponent<SpriteRenderer>();
        sprite.sprite = unitSprite;
        sprite.color = new Color(0.86f, 0.94f, 1f);
        sprite.sortingOrder = 2;
        cube = player.AddComponent<BiofitGeomeTryCube>();
        cube.Configure(groundCollider, sprite);
        cube.Died += OnCubeDied;

        for (int i = 0; i < ObstacleCount; i++)
        {
            float x = ObstacleStartX + i * ObstacleSpacing;
            GameObject spike = VisualObject("BIOFIT_SPIKE_" + (i + 1),
                new Vector3(x, 0.46f, 0f), new Vector3(0.95f, 0.92f, 1f),
                new Color(1f, 0.38f, 0.40f), spikeSprite, 1);
            PolygonCollider2D collider = spike.AddComponent<PolygonCollider2D>();
            collider.pathCount = 1;
            collider.SetPath(0, new[]
            {
                new Vector2(-0.48f, -0.49f),
                new Vector2(0.48f, -0.49f),
                new Vector2(0f, 0.49f)
            });
            collider.isTrigger = true;
        }
        VisualObject("FinishMarker", new Vector3(FinishX, 2.0f, 0f),
            new Vector3(0.17f, 4f, 1f), Color.green, unitSprite, 0);

        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject c = new GameObject("Main Camera");
            c.tag = "MainCamera";
            gameCamera = c.AddComponent<Camera>();
        }
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.065f, 0.085f, 0.16f);
        gameCamera.transform.position = new Vector3(3.4f, 2.8f, -10f);

        died = false;
        paused = false;
        completed = false;
        executedJumps = 0;
        rejectedJumps = 0;
        blePairs = 0;
        lastDecision = "NIVEL INICIADO - BLE BILATERAL LEGACY";
        Time.timeScale = 1f;
        monitor.LiftCycleMode = false;
        monitor.PhaseFastPairs = false;
        Record("LEVEL_RESET BLE_LEGACY=" + bleEnabled + " cooldown=900ms gap=180ms" +
               " obstacleSpacing=" + ObstacleSpacing + " timeScale=1");
    }

    private GameObject VisualObject(string name, Vector3 position,
        Vector3 scale, Color color, Sprite sprite, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(levelRoot.transform);
        go.transform.position = position;
        go.transform.localScale = scale;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return go;
    }

    private static Sprite CreateUnitSprite()
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++) t.SetPixel(x, y, Color.white);
        t.Apply();
        return Sprite.Create(t, new Rect(0f, 0f, 2f, 2f),
            new Vector2(0.5f, 0.5f), 2f);
    }

    private static Sprite CreateSpikeSprite()
    {
        const int size = 32;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                t.SetPixel(x, y,
                    Mathf.Abs((x + 0.5f) - size * 0.5f) <=
                    (y + 0.5f) * 0.5f ? Color.white : Color.clear);
        t.Apply();
        return Sprite.Create(t, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
    }

    private void OnGUI()
    {
        int depth = GUI.depth;
        GUI.depth = -100;
        float width = Screen.width;
        float h = Mathf.Clamp(Screen.height * 0.085f, 42f, 85f);
        float margin = 12f;
        if (monitor.ShowDiagnostics)
        {
            if (GUI.Button(new Rect(width - 270f, 12f, 255f, h),
                "VOLVER A GEOMETRY"))
            {
                monitor.ShowDiagnostics = false;
                SetPaused(false);
            }
            GUI.depth = depth;
            return;
        }

        GUI.Box(new Rect(10f, 10f, Mathf.Min(width - 20f, 680f), h * 1.55f),
            "BIOFIT / GEOMETRY - NIVEL 1\n" +
            "LEGACY: " + (bleEnabled ? "ON" : "OFF") +
            "  BLE: " + (monitor.BothTrackersConnected ? "2/2" : "SIN CONEXION") +
            "  PARES: " + blePairs + "  SALTOS: " + executedJumps +
            "  RECHAZOS: " + rejectedJumps);
        GUI.Label(new Rect(15f, h * 1.65f, Mathf.Min(width - 20f, 850f), h),
            lastDecision);
        float controlY = Screen.height - h - margin;
        float gap = 8f;
        float w = (width - 2f * margin - gap * 4f) / 5f;
        if (GUI.Button(new Rect(margin, controlY, w, h), "SALTAR"))
            ManualJump();
        if (GUI.Button(new Rect(margin + (w + gap), controlY, w, h),
            bleEnabled ? "BLE LEGACY ON" : "BLE LEGACY OFF"))
        {
            bleEnabled = !bleEnabled;
            Record("BLE_LEGACY_MODE " + bleEnabled);
        }
        if (GUI.Button(new Rect(margin + 2f * (w + gap), controlY, w, h),
            paused ? "REANUDAR" : "PAUSA"))
            SetPaused(!paused);
        if (GUI.Button(new Rect(margin + 3f * (w + gap), controlY, w, h), "REINICIAR"))
            BuildNewLevel();
        if (GUI.Button(new Rect(margin + 4f * (w + gap), controlY, w, h), "DIAG BLE"))
        {
            SetPaused(true);
            monitor.ShowDiagnostics = true;
        }
        if (completed)
            GUI.Box(new Rect(width * 0.3f, Screen.height * 0.36f,
                width * 0.4f, 105f), "NIVEL COMPLETADO\nREINICIAR para jugar otra vez");
        else if (died)
            GUI.Box(new Rect(width * 0.3f, Screen.height * 0.36f,
                width * 0.4f, 105f), "COLISION\nReiniciando...");
        GUI.depth = depth;
    }
}
