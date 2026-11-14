using System;
using System.Collections.Generic;

/// <summary>
/// BIOFIT GAME - interpretacion experimental de eventos de dos tobillos.
/// Esta clase NO estima desplazamiento fisico, NO confirma saltos
/// y NO equivale a un podometro validado.
///
/// Recibe eventos BLE en el hilo principal y utiliza un reloj COMUN
/// (Time.realtimeSinceStartup en Unity), nunca los millis de cada ESP32
/// que tienen origen diferente.
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

    private const float SameFootMinIntervalSeconds = 0.30f;
    private const float RecentWindowSeconds = 8f;
    private const float CadenceTimeoutSeconds = 2.5f;
    private const float MinAlternationSeconds = 0.18f;
    private const float MaxAlternationSeconds = 1.50f;

    private readonly List<Candidate> recent = new List<Candidate>(32);
    private float lastLeft = -10000f;
    private float lastRight = -10000f;
    private float lastAny = -10000f;
    private int lastSide;

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

        // MOV_OFF: fin del episodio de movimiento de ese tracker.
        if (eventType == 2)
        {
            if (trackerId == 1) leftMoving = false;
            else rightMoving = false;
            return;
        }

        // UP_IMPULSE es una candidata de impulso vertical, no un paso.
        if (eventType != 1) return;

        if (trackerId == 1) leftMoving = true;
        else rightMoving = true;

        float previous = trackerId == 1 ? lastLeft : lastRight;

        // Debounce: no contar dos inicios casi consecutivos del mismo tobillo.
        // Esta proteccion NO garantiza un paso por cada candidato.
        if (arrivalSeconds - previous < SameFootMinIntervalSeconds) return;

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

        if (lastSide != 0 && lastSide != trackerId)
            Alternations++;

        lastSide = trackerId;
        lastAny = arrivalSeconds;
        recent.Add(new Candidate(trackerId, arrivalSeconds));
    }

    public void Update(float nowSeconds)
    {
        // Ventana movil: solo secuencias recientes para estimar ritmo.
        for (int i = recent.Count - 1; i >= 0; i--)
        {
            if (nowSeconds - recent[i].seconds > RecentWindowSeconds)
                recent.RemoveAt(i);
        }

        EstimatedCadence = 0;
        if (recent.Count >= 4 && nowSeconds - lastAny <= CadenceTimeoutSeconds)
        {
            float intervalSum = 0f;
            int validIntervals = 0;

            for (int i = 1; i < recent.Count; i++)
            {
                if (recent[i].side == recent[i - 1].side) continue;
                float dt = recent[i].seconds - recent[i - 1].seconds;
                if (dt >= MinAlternationSeconds && dt <= MaxAlternationSeconds)
                {
                    intervalSum += dt;
                    validIntervals++;
                }
            }

            if (validIntervals >= 3)
                EstimatedCadence = 60f * validIntervals / intervalSum;
        }

        // Clasificacion exploratoria: requiere alternancia y cadencia.
        // Nunca se debe presentar como caminata/carrera validada.
        if (EstimatedCadence >= 150f)
            Activity = "CORRIENDO (TENTATIVO)";
        else if (EstimatedCadence >= 55f)
            Activity = "CAMINANDO (TENTATIVO)";
        else if (leftMoving || rightMoving)
            Activity = "MOVIMIENTO SIN CLASIFICAR";
        else
            Activity = "QUIETO";
    }
}
