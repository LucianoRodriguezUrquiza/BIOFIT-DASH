using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Laboratorio 2D dentro de la MISMA escena BLE.
/// Cambia de vista sin cortar la conexion de las Waveshare.
/// En Unity Editor, A/D simulan MOV_ON de cada tobillo;
/// ESPACIO solo simula el salto del personaje (no un salto corporal).
/// </summary>
[RequireComponent(typeof(BiofitBLEMonitor))]
public sealed class BiofitMovementLab : MonoBehaviour
{
    private BiofitBLEMonitor monitor;
    private bool labVisible;
    private bool isJumping;
    private float jumpHeight;
    private float jumpVelocity;
    private float obstacleX = 960f;
    private int obstaclesPassed;
    private int collisions;
    private float simulatedTime;

    private GUIStyle large, textStyle, smallStyle, buttonStyle;

    private void Awake()
    {
        monitor = GetComponent<BiofitBLEMonitor>();
        monitor.ShowDiagnostics = true;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            SwitchView();

#if UNITY_EDITOR
        if (labVisible && Keyboard.current != null)
        {
            // Eventos artificiales exclusivos de Editor para probar logica sin sensores.
            if (Keyboard.current.aKey.wasPressedThisFrame) SimulateStep(1);
            if (Keyboard.current.dKey.wasPressedThisFrame) SimulateStep(2);
            if (Keyboard.current.spaceKey.wasPressedThisFrame) SimulateJump();
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

        float cadence = monitor.InputEngine.EstimatedCadence;
        float speed = Mathf.Clamp(cadence / 120f, 0f, 2.4f) * 250f;
        obstacleX -= speed * dt;

        if (obstacleX < -60f)
        {
            obstacleX = 960f;
            obstaclesPassed++;
        }

        // Colision solo ilustrativa, no una medicion biomecanica.
        if (speed > 0f && obstacleX < 280f && obstacleX > 210f &&
            jumpHeight < 78f)
        {
            collisions++;
            obstacleX = 960f;
        }
        simulatedTime += dt;
    }

    private void SimulateStep(byte side)
    {
        float now = Time.realtimeSinceStartup;
        monitor.InputEngine.ProcessEvent(side, 1, now);
        monitor.InputEngine.ProcessEvent(side, 2, now);
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
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(0.65f, Screen.width / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = Screen.width / scale;
        float h = Screen.height / scale;

        if (large == null)
        {
            large = new GUIStyle(GUI.skin.label)
                { fontSize = 42, fontStyle = FontStyle.Bold, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 28, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 23, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button)
                { fontSize = 25, wordWrap = true };
            large.normal.textColor = Color.white;
            textStyle.normal.textColor = Color.white;
            smallStyle.normal.textColor = Color.white;
        }

        if (!labVisible)
        {
            if (GUI.Button(new Rect(22, h - 90, Mathf.Min(w - 44, 510), 63),
                "ABRIR LABORATORIO 2D", buttonStyle))
                SwitchView();
            return;
        }

        GUI.Box(new Rect(10, 10, w - 20, h - 20), "");
        GUI.Label(new Rect(35, 26, w - 70, 60),
            "BIOFIT | LABORATORIO DE MOVIMIENTO", large);

        if (GUI.Button(new Rect(35, 96, 350, 60),
            "VOLVER AL DIAGNOSTICO", buttonStyle))
            SwitchView();

        GUI.Label(new Rect(35, 171, w - 70, 70),
            "Estado: " + monitor.InputEngine.Activity, textStyle);
        GUI.Label(new Rect(35, 241, w - 70, 65),
            "Cadencia ESTIMADA: " +
            monitor.InputEngine.EstimatedCadence.ToString("F0") + "/min", textStyle);
        GUI.Label(new Rect(35, 305, w - 70, 75),
            "Candidatos izq: " + monitor.InputEngine.LeftCandidates +
            "  |  der: " + monitor.InputEngine.RightCandidates +
            "  |  alternancias: " + monitor.InputEngine.Alternations, smallStyle);

        const float trackHeight = 345f;
        float trackTop = Mathf.Min(480f, h * 0.35f);
        float trackWidth = w - 80f;
        DrawSolid(new Rect(40, trackTop, trackWidth, trackHeight),
            new Color(0.12f, 0.20f, 0.32f));
        float ground = trackTop + trackHeight - 70f;

        DrawSolid(new Rect(40, ground, trackWidth, 8), new Color(0.50f, 0.69f, 0.84f));

        // Personaje 2D esquematico (prototipo de motor, no arte final).
        DrawSolid(new Rect(220, ground - 72f - jumpHeight, 72, 72),
            new Color(0.34f, 0.85f, 0.77f));
        DrawSolid(new Rect(Mathf.Min(w-100f, obstacleX), ground-70, 48, 70),
            new Color(0.96f, 0.45f, 0.43f));

        float y = trackTop + trackHeight + 30;
        GUI.Label(new Rect(35, y, w - 70, 65),
            "Obstaculos superados: " + obstaclesPassed + "  |  colisiones: " + collisions,
            textStyle);
        y += 80;

#if UNITY_EDITOR
        GUI.Label(new Rect(35, y, w - 70, 115),
            "SIMULACION EN PC: A = pie izquierdo, D = pie derecho (alternados). " +
            "ESPACIO = salto de prueba. TAB = cambiar de vista.", smallStyle);
#else
        GUI.Label(new Rect(35, y, w - 70, 115),
            "ANDROID: velocidad ilustrativa segun cadencia BLE. " +
            "El boton SALTO es MANUAL; aun no detectamos saltos reales.", smallStyle);
#endif
        y += 122;
        if (GUI.Button(new Rect(35, y, 320, 70), "SALTO DE PRUEBA", buttonStyle))
            SimulateJump();
        GUI.Label(new Rect(35, y + 84, w - 70, 140),
            "Este laboratorio reutiliza la conexion BLE sin reiniciarla. " +
            "La actividad y la velocidad son experimentales; no indican pasos confirmados.",
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
