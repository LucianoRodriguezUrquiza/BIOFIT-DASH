using System;
using System.Collections.Generic;

/// <summary>
/// Detector EXPERIMENTAL de pares UP. Las marcas de tiempo deben estar
/// expresadas en UN MISMO reloj del celular. Acepta cierto desorden de
/// entrega BLE sin confundir una notificacion con un salto humano real.
/// </summary>
public sealed class BiofitBilateralJumpDetector
{
    public const float MinimumVerticalG = 0.12f;
    public const float MaxPairGapSeconds = 0.18f;
    public const float SameAnkleRefractorySeconds = 0.32f;
    public const float PairCooldownSeconds = 0.90f;

    public float MinimumVerticalGThreshold { get; private set; } = MinimumVerticalG;
    public float MaxPairGapWindowSeconds { get; private set; } = MaxPairGapSeconds;

    // Dos BLE independientes pueden entregar notificaciones por lotes.
    // Guardamos candidatos recientes por pie y emparejamos el mas cercano
    // en tiempo DEL SENSOR ALINEADO, no necesariamente el ultimo recibido.
    private readonly List<double> pendingLeft = new List<double>(16);
    private readonly List<double> pendingRight = new List<double>(16);
    private double lastLeftAccepted = -10000d;
    private double lastRightAccepted = -10000d;
    private double lastPairSeconds = -10000d;

    public int LeftUpCandidates { get; private set; }
    public int RightUpCandidates { get; private set; }
    public int BilateralCandidates { get; private set; }
    public int WeakRejected { get; private set; }
    public int RepeatedRejected { get; private set; }
    public int NearMisses { get; private set; }
    public float LastPairGapMs { get; private set; }
    public float LastNearMissGapMs { get; private set; }
    public string LastStatus { get; private set; } = "Sin impulsos suficientes";

    public void SetThresholds(float minVerticalG, float maxPairWindowSeconds)
    {
        MinimumVerticalGThreshold = minVerticalG < 0.08f ? 0.08f :
            minVerticalG > 0.50f ? 0.50f : minVerticalG;
        MaxPairGapWindowSeconds = maxPairWindowSeconds < 0.12f ? 0.12f :
            maxPairWindowSeconds > 0.40f ? 0.40f : maxPairWindowSeconds;
        ClearPending();
    }

    public void ClearPending()
    {
        pendingLeft.Clear();
        pendingRight.Clear();
        lastLeftAccepted = -10000d;
        lastRightAccepted = -10000d;
    }

    /// <param name="eventSeconds">Reloj compartido y monotónico, double.</param>
    public bool ProcessImpulse(byte trackerId, double eventSeconds, float verticalG)
    {
        if ((trackerId != 1 && trackerId != 2) ||
            double.IsNaN(eventSeconds) || double.IsInfinity(eventSeconds))
            return false;

        if (verticalG < MinimumVerticalGThreshold)
        {
            WeakRejected++;
            LastStatus = "Impulso insuficiente (< " +
                         MinimumVerticalGThreshold.ToString("F2") + " g)";
            return false;
        }

        double lastSame = trackerId == 1 ? lastLeftAccepted : lastRightAccepted;
        // Eventos de un mismo sensor conservan orden de secuencia.
        if (eventSeconds - lastSame < SameAnkleRefractorySeconds)
        {
            RepeatedRejected++;
            LastStatus = "Impulso repetido de un mismo tobillo";
            return false;
        }

        if (trackerId == 1)
        {
            lastLeftAccepted = eventSeconds;
            LeftUpCandidates++;
        }
        else
        {
            lastRightAccepted = eventSeconds;
            RightUpCandidates++;
        }

        List<double> own = trackerId == 1 ? pendingLeft : pendingRight;
        List<double> opposite = trackerId == 1 ? pendingRight : pendingLeft;

        int bestIndex = -1;
        double bestGap = double.PositiveInfinity;
        for (int i = 0; i < opposite.Count; i++)
        {
            double gap = Math.Abs(eventSeconds - opposite[i]);
            if (gap < bestGap)
            {
                bestGap = gap;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0 && bestGap <= MaxPairGapWindowSeconds)
        {
            double otherTime = opposite[bestIndex];
            opposite.RemoveAt(bestIndex);
            double pairTime = Math.Max(eventSeconds, otherTime);

            // Un mismo salto no puede aportar varios pares cercanos.
            if (pairTime - lastPairSeconds < PairCooldownSeconds)
            {
                LastStatus = "Par descartado por tiempo de seguridad";
                return false;
            }

            own.Clear();
            lastPairSeconds = pairTime;
            BilateralCandidates++;
            LastPairGapMs = (float)(bestGap * 1000d);
            LastStatus = "POSIBLE SALTO BILATERAL (NO CONFIRMADO)";
            return true;
        }

        if (bestIndex >= 0 && bestGap < 1.2d)
        {
            NearMisses++;
            LastNearMissGapMs = (float)(bestGap * 1000d);
            LastStatus = "UP no emparejado: separacion " +
                LastNearMissGapMs.ToString("F0") + " ms (maximo " +
                (MaxPairGapWindowSeconds * 1000f).ToString("F0") + " ms)";
        }
        else
        {
            LastStatus = "UP " + (trackerId == 1 ? "izquierdo" : "derecho") +
                         " aislado; esperando otro tobillo";
        }

        own.Add(eventSeconds);
        if (own.Count > 16)
            own.RemoveAt(0);
        return false;
    }
}
