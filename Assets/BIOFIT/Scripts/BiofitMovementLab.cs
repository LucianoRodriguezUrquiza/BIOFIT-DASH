using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BIOFIT GAME: laboratorio 2D de entradas experimentales.
/// Usa el mismo receptor BLE de la escena y no altera el firmware.
/// En Editor, A/D simulan inicios de movimiento (NO pasos validados);
/// ESPACIO simula exclusivamente el salto del personaje.
/// </summary>
[RequireComponent(typeof(BiofitBLEMonitor))]
public sealed class BiofitMovementLab : MonoBehaviour
{
    private BiofitBLEMonitor monitor;
    private bool labVisible;
    private bool isJumping;

    private float jumpHeight;
    private float jumpVelocity;
    private float obstacleX = 830f;
    private int obstaclesPassed;
    private int collisions;

    // Diagnostico de teclas separado del filtro biomecanico.
    private int leftKeyPresses;
    private int rightKeyPresses;
    private string lastKeyStatus = "Aun no pulsaste A ni D";
    private Vector2 scrollPosition;

    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        monitor = GetComponent<BiofitBLEMonitor>();
        monitor.ShowDiagnostics = true;
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            SwitchView();
        }

#if UNITY_EDITOR
        if (labVisible && Keyboard.current != null)
        {
            // Solo una pulsacion al PRESIONAR la tecla.
            // Soltar y pulsar otra vez; mantener no simula pasos nuevos.
            if (Keyboard.current.aKey.wasPressedThisFrame)
                SimulateFoot(1);
            if (Keyboard.current.dKey.wasPressedThisFrame)
                SimulateFoot(2);
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                SimulateJump();
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

        // Avance continuo SOLO cuando se estima cadencia alternada.
        // En Editor cada A/D aceptada tambien da un avance visible puntual.
        float cadence = monitor.InputEngine.EstimatedCadence;
        float speed = Mathf.Clamp(cadence / 120f, 0f, 2.4f) * 190f;
        obstacleX -= speed * dt;

        if (obstacleX < -65f)
        {
            obstacleX = 830f;
            obstaclesPassed++;
        }

        // Colision exclusivamente ilustrativa, no resultado biomecanico.
        if (speed > 0f && obstacleX > 135f &&
            obstacleX < 210f && jumpHeight < 78f)
        {
            collisions++;
            obstacleX = 830f;
        }
    }

    private void SimulateFoot(byte side)
    {
        BiofitInputEngine engine = monitor.InputEngine;
        int previousCandidates = side == 1 ?
            engine.LeftCandidates : engine.RightCandidates;

        float now = Time.realtimeSinceStartup;
        engine.ProcessEvent(side, 1, now);
        engine.ProcessEvent(side, 2, now);

        int currentCandidates = side == 1 ?
            engine.LeftCandidates : engine.RightCandidates;
        bool accepted = currentCandidates > previousCandidates;

        if (side == 1) leftKeyPresses++;
        else rightKeyPresses++;

        lastKeyStatus = (side == 1 ? "A = IZQUIERDO" : "D = DERECHO") +
            (accepted ? " | candidato aceptado" :
                        " | ignorado por antirrebote (< 0,30 s)");

        // Feedback visible de cada pulsacion, independientemente de que
        // existan suficientes eventos para calcular una cadencia continua.
        obstacleX -= 52f;
        if (obstacleX < -65f)
        {
            obstacleX = 830f;
            obstaclesPassed++;
        }
    }

    private void SimulateJump()
    {
        if (isJumping) return;
        isJumping = true;
        jumpVelocity = 560f;
    }

    private void SwitchView()
    {
        labVisible = !labVisible;
        monitor.ShowDiagnostics = !labVisible;
        scrollPosition = Vector2.zero;
    }

    private void InitializeStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 37, fontStyle = FontStyle.Bold, wordWrap = true };
        textStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, wordWrap = true };
        smallStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 23, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 24, wordWrap = true };

        titleStyle.normal.textColor = Color.white;
        textStyle.normal.textColor = Color.white;
        smallStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(0.42f, Mathf.Min(
            Screen.width / 1100f, Screen.height / 670f));
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float w = Screen.width / scale;
        float h = Screen.height / scale;

        InitializeStyles();

        if (!labVisible)
        {
            if (GUI.Button(new Rect(20, h - 90f,
                Mathf.Min(w - 40f, 510f), 65f),
                "ABRIR LABORATORIO 2D", buttonStyle))
            {
                SwitchView();
            }
            return;
        }

        GUI.Box(new Rect(8f, 8f, w - 16f, h - 16f), "");

        // Todo el contenido esta en una pagina desplazable.
        // Antes, la pista (pintada mas tarde) tapaba la informacion.
        float contentWidth = Mathf.Max(600f, w - 65f);
        float viewportWidth = w - 22f;
        float viewportHeight = h - 22f;
        scrollPosition = GUI.BeginScrollView(
            new Rect(11f, 11f, viewportWidth, viewportHeight),
            scrollPosition, new Rect(0f, 0f, contentWidth, 985f),
            false, true);

        float x = 20f;
        float bodyW = contentWidth - 40f;

        GUI.Label(new Rect(x, 12f, bodyW, 58f),
            "BIOFIT | LABORATORIO DE MOVIMIENTO", titleStyle);

        if (GUI.Button(new Rect(x, 79f, 358f, 59f),
            "VOLVER AL DIAGNOSTICO", buttonStyle))
        {
            GUI.EndScrollView();
            SwitchView();
            return;
        }

        if (GUI.Button(new Rect(x + 375f, 79f, 305f, 59f),
            "SALTO DE PRUEBA", buttonStyle))
        {
            SimulateJump();
        }

        GUI.Label(new Rect(x, 153f, bodyW, 58f),
            "PC: A = tobillo izquierdo | D = derecho | ESPACIO = salto manual", smallStyle);
        GUI.Label(new Rect(x, 204f, bodyW, 48f),
            "Teclas recibidas: A = " + leftKeyPresses +
            "   D = " + rightKeyPresses, textStyle);
        GUI.Label(new Rect(x, 253f, bodyW, 44f),
            "Ultima tecla: " + lastKeyStatus, smallStyle);

        BiofitInputEngine engine = monitor.InputEngine;
        GUI.Label(new Rect(x, 300f, bodyW, 58f),
            "Candidatos: izq " + engine.LeftCandidates +
            " | der " + engine.RightCandidates +
            " | alternancias " + engine.Alternations, textStyle);
        GUI.Label(new Rect(x, 352f, bodyW, 58f),
            "Cadencia estimada: " + engine.EstimatedCadence.ToString("F0") +
            "/min | " + engine.Activity, smallStyle);

        // PISTA: se dibuja debajo de todos los indicadores.
        float trackTop = 427f;
        float trackW = bodyW;
        float trackH = 238f;
        DrawSolid(new Rect(x, trackTop, trackW, trackH),
            new Color(0.12f, 0.20f, 0.32f));

        float ground = trackTop + trackH - 50f;
        DrawSolid(new Rect(x, ground, trackW, 7f),
            new Color(0.55f, 0.73f, 0.87f));

        // Cuadrado turquesa (personaje) y obstaculo salmon.
        DrawSolid(new Rect(x + 145f, ground - 73f - jumpHeight, 73f, 73f),
            new Color(0.34f, 0.85f, 0.77f));

        float obstacleScreenX = x + Mathf.Min(trackW - 50f, obstacleX);
        DrawSolid(new Rect(obstacleScreenX, ground - 68f, 48f, 68f),
            new Color(0.96f, 0.45f, 0.43f));

        GUI.Label(new Rect(x, 682f, bodyW, 54f),
            "Obstaculos superados: " + obstaclesPassed +
            "   |   colisiones ilustrativas: " + collisions,
            textStyle);
        GUI.Label(new Rect(x, 746f, bodyW, 93f),
            "PRUEBA: pulsá A, soltá, luego D, soltá, y repetí " +
            "(aprox. una tecla cada 0,5 s). Cada tecla mueve el obstaculo " +
            "un poco. Tras varios pares, deberia aparecer cadencia.",
            smallStyle);
        GUI.Label(new Rect(x, 864f, bodyW, 102f),
            "Importante: esto simula eventos de tobillo. Los candidatos " +
            "no son pasos confirmados. El salto de ESPACIO no detecta " +
            "un salto real con los sensores.",
            smallStyle);

        GUI.EndScrollView();
    }

    private static void DrawSolid(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
