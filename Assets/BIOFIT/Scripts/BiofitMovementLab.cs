using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BIOFIT GAME - laboratorio VISUAL de entradas experimentales.
/// Reutiliza el receptor BLE de la escena DiagnosticoBLE; no modifica firmware.
/// En Editor: A/D simulan MOV_ON; Q/E simulan UP; J simula un par UP;
/// ESPACIO realiza un salto manual.
/// El salto bilateral NO esta validado biomecanicamente.
/// </summary>
[RequireComponent(typeof(BiofitBLEMonitor))]
public sealed class BiofitMovementLab : MonoBehaviour
{
    private const float TrackTravelStart = 960f;
    private const float TrackTravelEnd = -60f;
    private const float CharacterX = 204f;
    private const float CharacterSize = 70f;

    private BiofitBLEMonitor monitor;
    private bool labVisible;
    private bool isJumping;
    private bool allowBleJumpDemo;   // Desactivado por defecto, especialmente en Android.

    private float jumpHeight;
    private float jumpVelocity;
    private float obstacleX = TrackTravelStart;
    private float previewSpeed;
    private int obstaclesPassed;
    private int collisions;
    private int demoJumps;

    private int leftKeyPresses;
    private int rightKeyPresses;
    private float leftFlashUntil;
    private float rightFlashUntil;
    private string lastKeyStatus = "Sin pulsaciones de tobillo";
    private string lastJumpStatus = "Sin saltos de demostracion";

    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        monitor = GetComponent<BiofitBLEMonitor>();
        monitor.ShowDiagnostics = true;
    }

    private void OnEnable()
    {
        if (monitor == null) monitor = GetComponent<BiofitBLEMonitor>();
        monitor.BilateralJumpCandidate += OnRealBilateralCandidate;
    }

    private void OnDisable()
    {
        if (monitor != null)
            monitor.BilateralJumpCandidate -= OnRealBilateralCandidate;
    }

    private void OnRealBilateralCandidate(float gapMs)
    {
        // SOLO vista previa voluntaria; el detector de par no confirma salto real.
        if (labVisible && allowBleJumpDemo)
            TryDemoJump("PAR BLE EXPERIMENTAL (" + gapMs.ToString("F0") + " ms)");
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
            SwitchView();

#if UNITY_EDITOR
        if (labVisible && Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) SimulateFoot(1);
            if (Keyboard.current.dKey.wasPressedThisFrame) SimulateFoot(2);
            if (Keyboard.current.qKey.wasPressedThisFrame) SimulateUp(1);
            if (Keyboard.current.eKey.wasPressedThisFrame) SimulateUp(2);
            if (Keyboard.current.jKey.wasPressedThisFrame) SimulatePairUps();
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                TryDemoJump("ESPACIO (MANUAL)");
        }
#endif

        if (!labVisible) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (isJumping)
        {
            jumpHeight += jumpVelocity * dt;
            jumpVelocity -= 1450f * dt;
            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                jumpVelocity = 0f;
                isJumping = false;
            }
        }

        // Sin avance por pulsacion: solo hay desplazamiento continuo si
        // existe cadencia actual calculada con alternancias validas.
        float desiredSpeed = TargetSpeedForCadence(monitor.InputEngine.EstimatedCadence);
        float rate = desiredSpeed > previewSpeed ? 470f : 620f;
        previewSpeed = Mathf.MoveTowards(previewSpeed, desiredSpeed, rate * dt);
        obstacleX -= previewSpeed * dt;

        if (obstacleX < TrackTravelEnd)
        {
            obstacleX = TrackTravelStart;
            obstaclesPassed++;
        }

        // Colision meramente grafica: personaje ocupa X=204..274.
        bool overlapsCharacter = obstacleX < CharacterX + CharacterSize &&
                                 obstacleX + 48f > CharacterX;
        if (previewSpeed > 1f && overlapsCharacter && jumpHeight < 78f)
        {
            collisions++;
            obstacleX = TrackTravelStart;
        }
    }

    /// <summary>
    /// Conversion SOLO VISUAL de la cadencia tentativa a velocidad en pantalla.
    /// Debajo de 55/min no inicia desplazamiento continuo.
    /// </summary>
    public static float TargetSpeedForCadence(float cadence)
    {
        if (cadence < 55f) return 0f;
        return Mathf.Lerp(150f, 385f,
            Mathf.Clamp01((cadence - 55f) / 135f));
    }

    private void SimulateFoot(byte side)
    {
        BiofitInputEngine engine = monitor.InputEngine;
        int before = side == 1 ? engine.LeftCandidates : engine.RightCandidates;
        float now = Time.realtimeSinceStartup;
        engine.ProcessEvent(side, 1, now);
        engine.ProcessEvent(side, 2, now);
        int after = side == 1 ? engine.LeftCandidates : engine.RightCandidates;

        if (side == 1)
        {
            leftKeyPresses++;
            leftFlashUntil = now + 0.18f;
        }
        else
        {
            rightKeyPresses++;
            rightFlashUntil = now + 0.18f;
        }
        lastKeyStatus = (side == 1 ? "A IZQ" : "D DER") +
            (after > before ? " | candidato aceptado" :
                " | filtrado por tiempo minimo de tobillo");
        // NO mover el obstaculo aqui. El movimiento se deriva de la cadencia.
    }

    private bool SimulateUp(byte side)
    {
        bool pair = monitor.JumpDetector.ProcessImpulse(
            side, Time.realtimeSinceStartup, 0.32f);
        if (pair)
            TryDemoJump("PAR UP SIMULADO (Q/E/J) - NO VALIDADO");
        return pair;
    }

    private void SimulatePairUps()
    {
        // Unicamente en Editor: genera un par con tiempo igual para ambos.
        SimulateUp(1);
        SimulateUp(2);
    }

    private bool TryDemoJump(string source)
    {
        if (isJumping)
        {
            lastJumpStatus = source + ": cubo en el aire, salto ignorado";
            return false;
        }
        isJumping = true;
        jumpVelocity = 560f;
        jumpHeight = 0f;
        demoJumps++;
        lastJumpStatus = source + " | salto demostrativo #" + demoJumps;
        return true;
    }

    private void SwitchView()
    {
        labVisible = !labVisible;
        monitor.ShowDiagnostics = !labVisible;
    }

    private void InitializeStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, wordWrap = true };
        textStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 27, wordWrap = true };
        smallStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 22, wordWrap = true };

        titleStyle.normal.textColor = Color.white;
        textStyle.normal.textColor = Color.white;
        smallStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
    }

    private void OnGUI()
    {
        // Toda la pista y el suelo caben en un lienzo virtual de 1120x740.
        // Se escala uniformemente para no ocultar los cuadrados bajo la pantalla.
        float scale = Mathf.Min(Screen.width / 1120f, Screen.height / 740f);
        if (scale <= 0f) return;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = Screen.width / scale;
        float h = Screen.height / scale;

        InitializeStyles();

        if (!labVisible)
        {
            if (GUI.Button(new Rect(20f, h - 85f,
                Mathf.Min(w - 40f, 485f), 64f),
                "ABRIR LABORATORIO 2D", buttonStyle))
                SwitchView();
            return;
        }

        float x = 23f;
        float bodyW = Mathf.Min(w - 46f, 1070f);
        GUI.Box(new Rect(9f, 9f, w - 18f, h - 18f), "");

        GUI.Label(new Rect(x, 17f, bodyW, 55f),
            "BIOFIT | LABORATORIO DE MOVIMIENTO", titleStyle);

        if (GUI.Button(new Rect(x, 79f, 272f, 54f),
            "VOLVER AL DIAGNOSTICO", buttonStyle))
        {
            SwitchView();
            return;
        }
        if (GUI.Button(new Rect(x + 282f, 79f, 270f, 54f),
            "SALTO MANUAL (ESPACIO)", buttonStyle))
            TryDemoJump("BOTON MANUAL");

        if (GUI.Button(new Rect(x + 562f, 79f, 470f, 54f),
            allowBleJumpDemo ? "SALTAR CON PARES BLE: SI (DEMO)" :
                               "SALTAR CON PARES BLE: NO", buttonStyle))
            allowBleJumpDemo = !allowBleJumpDemo;

        GUI.Label(new Rect(x, 144f, bodyW, 41f),
            "A/D = movimiento simulado | Q/E = impulso vertical | J = par bilateral y salto DEMO",
            smallStyle);

        float now = Time.realtimeSinceStartup;
        DrawSolid(new Rect(x, 190f, 94f, 44f),
            now < leftFlashUntil ? new Color(0.2f, 0.75f, 0.53f) :
                                   new Color(0.17f, 0.32f, 0.42f));
        DrawSolid(new Rect(x + 105f, 190f, 94f, 44f),
            now < rightFlashUntil ? new Color(0.2f, 0.75f, 0.53f) :
                                    new Color(0.17f, 0.32f, 0.42f));
        GUI.Label(new Rect(x + 7f, 191f, 88f, 43f), "A IZQ", smallStyle);
        GUI.Label(new Rect(x + 112f, 191f, 88f, 43f), "D DER", smallStyle);
        GUI.Label(new Rect(x + 223f, 193f, bodyW - 223f, 44f),
            "Pulsaciones: A " + leftKeyPresses + " | D " + rightKeyPresses +
            " | " + lastKeyStatus, smallStyle);

        BiofitInputEngine engine = monitor.InputEngine;
        BiofitBilateralJumpDetector jump = monitor.JumpDetector;

        GUI.Label(new Rect(x, 243f, bodyW, 48f),
            "MOV_ON candidatos: izq " + engine.LeftCandidates +
            " | der " + engine.RightCandidates +
            " | alternancias " + engine.Alternations, textStyle);

        GUI.Label(new Rect(x, 285f, bodyW, 48f),
            "Cadencia tentativa: " + engine.EstimatedCadence.ToString("F0") +
            "/min | " + engine.Activity +
            " | velocidad visual " + previewSpeed.ToString("F0"), smallStyle);

        GUI.Label(new Rect(x, 327f, bodyW, 48f),
            "UP aceptados: izq " + jump.LeftUpCandidates +
            " | der " + jump.RightUpCandidates +
            " | PARES BILATERALES: " + jump.BilateralCandidates, textStyle);

        GUI.Label(new Rect(x, 370f, bodyW, 35f),
            jump.LastStatus + " | ultimo par: " +
            jump.LastPairGapMs.ToString("F0") + " ms", smallStyle);

        // Pista completa, SIN ScrollView. Todos los elementos quedan a la vista.
        float trackTop = 413f;
        float trackHeight = 227f;
        DrawSolid(new Rect(x, trackTop, bodyW, trackHeight),
            new Color(0.12f, 0.20f, 0.32f));

        float ground = trackTop + trackHeight - 31f;
        DrawSolid(new Rect(x, ground, bodyW, 7f),
            new Color(0.55f, 0.73f, 0.87f));
        DrawSolid(new Rect(x + CharacterX, ground - jumpHeight - CharacterSize,
            CharacterSize, CharacterSize),
            new Color(0.34f, 0.85f, 0.77f));
        DrawSolid(new Rect(x + obstacleX, ground - 65f, 48f, 65f),
            new Color(0.96f, 0.45f, 0.43f));

        GUI.Label(new Rect(x, 650f, bodyW, 35f),
            "Saltos demo: " + demoJumps + " | colisiones demo: " +
            collisions + " | obstaculos superados: " + obstaclesPassed,
            smallStyle);
        GUI.Label(new Rect(x, 683f, bodyW, 45f),
            lastJumpStatus + ". Son candidatos experimentales, NO saltos reales confirmados.",
            smallStyle);
    }

    private static void DrawSolid(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
