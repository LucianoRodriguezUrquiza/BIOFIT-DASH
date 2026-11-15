using System.Collections.Generic;

/// <summary>
/// BIOFIT GAME - interpretacion experimental de eventos de dos tobillos.
/// Los candidatos MOV_ON NO son pasos validados. Cadencia y actividad
/// solo se calculan cuando existe una cadena RECIENTE y CONSECUTIVA
/// de alternancias entre ambos tobillos.
/// El tiempo es el reloj compartido del telefono, no los millis de ESP32.
/// </summary>
public sealed class BiofitInputEngine
{
    private struct Candidate
    {
        public readonly int side;
        public readonly float seconds;

        public Candidate(int side, float seconds)
        {
            this.side = side;
            this.seconds = seconds;
        }
    }

    // Parametros experimentales: se ajustaran tras validar datos reales.
    private const float SameFootMinIntervalSeconds = 0.30f;
    private const float MaxAlternationSeconds = 1.50f;
    private const float MinAlternationSeconds = 0.18f;
    private const float CadenceTimeoutSeconds = 1.50f;
    private const float RecentActivitySeconds = 1.50f;
    private const int RequiredIntervals = 3;   // 4 eventos consecutivos A-D-A-D.
    private const int MaxRunCandidates = 12;   // ritmos recientes, no toda la sesion.

    private readonly List<Candidate> alternatingRun = new List<Candidate>(MaxRunCandidates);
    private float lastLeft = -10000f;
    private float lastRight = -10000f;
    private float lastAny = -10000f;
    private bool leftMoving;
    private bool rightMoving;

    public int LeftCandidates { get; private set; }
    public int RightCandidates { get; private set; }
    public int Alternations { get; private set; }
    public float EstimatedCadence { get; private set; }
    public string Activity { get; private set; } = "QUIETO";

    public void ProcessEvent(byte trackerId, byte eventType, float arrivalSeconds)
    {
        if (trackerId != 1 && trackerId != 2) return;

        if (eventType == 2)
        {
            if (trackerId == 1) leftMoving = false;
            else rightMoving = false;
            return;
        }

        // No considerar cada impulso UP como un paso.
        if (eventType != 1) return;

        if (trackerId == 1) leftMoving = true;
        else rightMoving = true;

        float previous = trackerId == 1 ? lastLeft : lastRight;
        if (arrivalSeconds - previous < SameFootMinIntervalSeconds)
            return;  // rebote de mismo tobillo

        if (trackerId == 1)
        {
            lastLeft = arrivalSeconds;
            LeftCandidates++;
        }
        else
        {
            lastRight = arrivalSeconds;
            RightCandidates++;
        }
        lastAny = arrivalSeconds;

        if (alternatingRun.Count > 0)
        {
            Candidate previousCandidate = alternatingRun[alternatingRun.Count - 1];
            float dt = arrivalSeconds - previousCandidate.seconds;

            // Una repeticion A-A o D-D invalida inmediatamente la cadena.
            // Un intervalo demasiado corto o largo tambien la corta.
            if (previousCandidate.side == trackerId ||
                dt < MinAlternationSeconds || dt > MaxAlternationSeconds)
            {
                alternatingRun.Clear();
            }
            else
            {
                // Solo contabilizar una alternancia temporalmente valida.
                Alternations++;
            }
        }

        // La candidata actual inicia una nueva cadena cuando hubo un corte.
        alternatingRun.Add(new Candidate(trackerId, arrivalSeconds));
        if (alternatingRun.Count > MaxRunCandidates)
            alternatingRun.RemoveAt(0);
    }

    public void Update(float nowSeconds)
    {
        EstimatedCadence = 0f;

        // No revivir una caminata pasada tras segundos quieto.
        if (alternatingRun.Count >= RequiredIntervals + 1 &&
            nowSeconds - lastAny <= CadenceTimeoutSeconds)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 1; i < alternatingRun.Count; i++)
            {
                float dt = alternatingRun[i].seconds - alternatingRun[i - 1].seconds;
                // Cada intervalo de la cadena fue validado en ProcessEvent.
                sum += dt;
                count++;
            }

            if (count >= RequiredIntervals && sum > 0f)
                EstimatedCadence = 60f * count / sum;
        }

        // Una sucesion unilateral puede indicar movimiento, no caminar.
        bool recentMovement = nowSeconds - lastAny <= RecentActivitySeconds ||
                              leftMoving || rightMoving;

        if (EstimatedCadence >= 150f)
            Activity = "CORRIENDO (TENTATIVO)";
        else if (EstimatedCadence >= 55f)
            Activity = "CAMINANDO (TENTATIVO)";
        else
            Activity = recentMovement ? "MOVIMIENTO SIN CLASIFICAR" : "QUIETO";
    }
}
