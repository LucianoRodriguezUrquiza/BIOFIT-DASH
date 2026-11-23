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
    // Stable default. The current runtime value may be changed from Settings.
    public const float PairCooldownSeconds = 0.90f;
    public const int DefaultPairCooldownMilliseconds = 900;
    public int PairCooldownMilliseconds { get; private set; } = DefaultPairCooldownMilliseconds;

    public static bool IsSupportedPairCooldownMilliseconds(int value)
    {
        return value == 900 || value == 800 || value == 700 ||
            value == 600 || value == 500 || value == 400;
    }
    // EXPERIMENTAL: only for TWO firmware-confirmed phase evidence flags.
    // Do not use a shorter timer on ordinary +Z accelerations alone.
    public const float PhaseFastMinGapSeconds = 0.60f;

    public float MinimumVerticalGThreshold { get; private set; } = MinimumVerticalG;
    public float MaxPairGapWindowSeconds { get; private set; } = MaxPairGapSeconds;

    // Dos BLE independientes pueden entregar notificaciones por lotes.
    // Guardamos candidatos recientes por pie y emparejamos el mas cercano
    // en tiempo DEL SENSOR ALINEADO, no necesariamente el ultimo recibido.
    private struct PendingImpulse
    {
        public double Time, PreviousAccepted;
        public bool PhaseEvidence;
        public PendingImpulse(double time, double previous, bool phaseEvidence)
        {
            Time = time; PreviousAccepted = previous; PhaseEvidence = phaseEvidence;
        }
    }
    private readonly List<PendingImpulse> pendingLeft = new List<PendingImpulse>(16);
    private readonly List<PendingImpulse> pendingRight = new List<PendingImpulse>(16);
    private double lastLeftAccepted = -10000d;
    private double lastRightAccepted = -10000d;
    private double lastPairSeconds = -10000d;
    // An in-flight lock is never shortened when Settings reduces the value.
    // The newly selected duration governs the NEXT accepted legacy pair.
    private double nextPairAllowedSeconds = -10000d;

    // Decision for EVERY processed UP, including a matched pair suppressed by cooldown.
    // Names describe software decisions, never a biomechanical phase.
    public enum Decision { Invalid, BelowThreshold, SameAnkleRefractory, WaitingForOther,
        OutsideWindow, PairCooldown, Paired }
    public Decision LastDecision { get; private set; }
    public int PairCooldownRejected { get; private set; }
    public bool LastPairUsedFastPhase { get; private set; }
    public int FastPhasePairsAccepted { get; private set; }
    public double LastPairIntervalSeconds { get; private set; } = double.NaN;

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

    public void SetPairCooldownMilliseconds(int value)
    {
        if (!IsSupportedPairCooldownMilliseconds(value))
            throw new ArgumentOutOfRangeException(nameof(value),
                "Only 400, 500, 600, 700, 800 or 900 ms are supported.");
        PairCooldownMilliseconds = value;
        if (lastPairSeconds > -9999d)
            nextPairAllowedSeconds = Math.Max(nextPairAllowedSeconds,
                lastPairSeconds + value / 1000d);
    }

    public void ClearPending()
    {
        pendingLeft.Clear();
        pendingRight.Clear();
        lastLeftAccepted = -10000d;
        lastRightAccepted = -10000d;
    }

    /// <param name="eventSeconds">Reloj compartido y monotónico, double.</param>
    public bool ProcessImpulse(byte trackerId, double eventSeconds, float verticalG,
        bool phaseEvidence = false, bool phaseFastEnabled = false)
    {
        LastPairUsedFastPhase = false;
        if ((trackerId != 1 && trackerId != 2) ||
            double.IsNaN(eventSeconds) || double.IsInfinity(eventSeconds) ||
            float.IsNaN(verticalG) || float.IsInfinity(verticalG))
        {
            LastDecision = Decision.Invalid;
            return false;
        }

        if (verticalG < MinimumVerticalGThreshold)
        {
            LastDecision = Decision.BelowThreshold;
            WeakRejected++;
            LastStatus = "Impulso insuficiente (< " +
                         MinimumVerticalGThreshold.ToString("F2") + " g)";
            return false;
        }

        double lastSame = trackerId == 1 ? lastLeftAccepted : lastRightAccepted;
        // Eventos de un mismo sensor conservan orden de secuencia.
        if (eventSeconds - lastSame < SameAnkleRefractorySeconds)
        {
            LastDecision = Decision.SameAnkleRefractory;
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

        List<PendingImpulse> own = trackerId == 1 ? pendingLeft : pendingRight;
        List<PendingImpulse> opposite = trackerId == 1 ? pendingRight : pendingLeft;

        int bestIndex = -1;
        double bestGap = double.PositiveInfinity;
        for (int i = 0; i < opposite.Count; i++)
        {
            double gap = Math.Abs(eventSeconds - opposite[i].Time);
            if (gap < bestGap)
            {
                bestGap = gap;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0 && bestGap <= MaxPairGapWindowSeconds)
        {
            PendingImpulse other = opposite[bestIndex];
            double otherTime = other.Time;
            opposite.RemoveAt(bestIndex);
            double pairTime = Math.Max(eventSeconds, otherTime);

            LastPairGapMs = (float)(bestGap * 1000d);
            LastPairIntervalSeconds = lastPairSeconds == -10000d ? double.NaN : pairTime - lastPairSeconds;
            // Fast phase mode needs independently qualified READY->UP cycles
            // on BOTH ankles. A single strong +Z impulse is never sufficient.
            bool eligibleFast = phaseFastEnabled && phaseEvidence && other.PhaseEvidence;
            double cooldown = eligibleFast ? PhaseFastMinGapSeconds :
                PairCooldownMilliseconds / 1000d;
            // Preserve the previous interval on a shorter selection; for
            // LEGACY also enforce the newly selected duration if increased.
            // Inactive PhaseFastPairs retains its historical replay behavior.
            double requiredUntil = eligibleFast ? lastPairSeconds + cooldown :
                Math.Max(nextPairAllowedSeconds, lastPairSeconds + cooldown);
            if (pairTime < requiredUntil)
            {
                LastDecision = Decision.PairCooldown;
                PairCooldownRejected++;
                // A consumed, suppressed pair must not restart the per-ankle
                // refractory and hide the next pair AFTER the 900ms interval.
                // Roll back only this pair's timestamps, never a newer pending UP
                // delivered by the other connection. Freshness/sequence gate remains upstream.
                if (trackerId == 1)
                {
                    lastLeftAccepted = lastSame;
                    if (lastRightAccepted == otherTime) lastRightAccepted = other.PreviousAccepted;
                }
                else
                {
                    lastRightAccepted = lastSame;
                    if (lastLeftAccepted == otherTime) lastLeftAccepted = other.PreviousAccepted;
                }
                LastStatus = "Par descartado por tiempo de seguridad";
                return false;
            }

            own.Clear();
            lastPairSeconds = pairTime;
            nextPairAllowedSeconds = pairTime + cooldown;
            LastPairUsedFastPhase = eligibleFast &&
                LastPairIntervalSeconds < PairCooldownSeconds;
            if (LastPairUsedFastPhase) FastPhasePairsAccepted++;
            LastDecision = Decision.Paired;
            BilateralCandidates++;
            LastPairGapMs = (float)(bestGap * 1000d);
            LastStatus = "POSIBLE SALTO BILATERAL (NO CONFIRMADO)";
            return true;
        }

        if (bestIndex >= 0 && bestGap < 1.2d)
        {
            LastDecision = Decision.OutsideWindow;
            NearMisses++;
            LastNearMissGapMs = (float)(bestGap * 1000d);
            LastStatus = "UP no emparejado: separacion " +
                LastNearMissGapMs.ToString("F0") + " ms (maximo " +
                (MaxPairGapWindowSeconds * 1000f).ToString("F0") + " ms)";
        }
        else
        {
            LastDecision = Decision.WaitingForOther;
            LastStatus = "UP " + (trackerId == 1 ? "izquierdo" : "derecho") +
                         " aislado; esperando otro tobillo";
        }

        own.Add(new PendingImpulse(eventSeconds, lastSame, phaseEvidence));
        if (own.Count > 16)
            own.RemoveAt(0);
        return false;
    }
}
