using System;
using System.Collections.Generic;

/// <summary>
/// Estima un reloj comun a partir del timestamp de STATUS de cada ESP32
/// y el reloj MONOTONICO del callback BLE en Android.
///
/// No hay sincronizacion exacta: el minimo desfase observado aproxima
/// la mejor latencia de notificacion. Su error puede ser distinto para
/// ambas placas; es un estimador EXPERIMENTAL, no referencia de laboratorio.
/// No modifica la configuracion de la IMU ni el firmware.
/// </summary>
public sealed class BiofitClockAligner
{
    private struct Observation
    {
        public double CallbackSeconds;
        public double OffsetSeconds;
    }

    private sealed class Clock
    {
        public readonly List<Observation> Recent = new List<Observation>(128);
        public double LastCallback = -10000d;
        public double BestOffset = 0d;
        public bool HasBest;
        public int Samples;
    }

    private readonly Clock left = new Clock();
    private readonly Clock right = new Clock();

    private const double WindowSeconds = 12d;
    private const int MinimumSamples = 8;
    private const int MaxSamples = 160;
    private const double MaxSampleAgeSeconds = 2.5d;

    public int LeftSamples => left.Samples;
    public int RightSamples => right.Samples;

    public void ObserveStatus(byte trackerId, uint sensorMillis, double callbackSeconds)
    {
        Clock state = GetClock(trackerId);
        if (state == null || callbackSeconds < 0d ||
            double.IsNaN(callbackSeconds) || double.IsInfinity(callbackSeconds))
            return;

        double offset = callbackSeconds - sensorMillis * 0.001d;

        // Un tracker reiniciado tendra millis~0 y cambiara mucho el
        // desfase: eliminar cualquier ancla anterior.
        if (state.HasBest && Math.Abs(offset - state.BestOffset) > 15d)
        {
            state.Recent.Clear();
            state.HasBest = false;
            state.Samples = 0;
        }

        state.Recent.Add(new Observation
        {
            CallbackSeconds = callbackSeconds,
            OffsetSeconds = offset
        });
        state.LastCallback = callbackSeconds;
        state.Samples++;

        int remove = 0;
        while (remove < state.Recent.Count &&
            (state.Recent[remove].CallbackSeconds <
                callbackSeconds - WindowSeconds ||
             state.Recent.Count - remove > MaxSamples))
            remove++;
        if (remove > 0) state.Recent.RemoveRange(0, remove);

        state.BestOffset = double.PositiveInfinity;
        foreach (Observation sample in state.Recent)
            if (sample.OffsetSeconds < state.BestOffset)
                state.BestOffset = sample.OffsetSeconds;
        state.HasBest = !double.IsInfinity(state.BestOffset);
    }

    public bool IsReady(double nowSeconds)
    {
        return Ready(left, nowSeconds) && Ready(right, nowSeconds);
    }

    public bool TryMapEvent(byte trackerId, uint sensorMillis,
        double callbackSeconds, out double alignedSeconds)
    {
        alignedSeconds = callbackSeconds;
        if (!IsReady(callbackSeconds)) return false;

        Clock state = GetClock(trackerId);
        if (state == null || !state.HasBest) return false;

        double estimate = sensorMillis * 0.001d + state.BestOffset;
        // Un evento no puede ocurrir 0,15 s en el futuro respecto del
        // callback; se toleran <5 s de retardo por colas BLE.
        if (estimate > callbackSeconds + 0.15d ||
            callbackSeconds - estimate > 5d)
            return false;

        alignedSeconds = estimate;
        return true;
    }

    private static bool Ready(Clock state, double nowSeconds)
    {
        return state.HasBest &&
            state.Recent.Count >= MinimumSamples &&
            nowSeconds - state.LastCallback >= 0d &&
            nowSeconds - state.LastCallback <= MaxSampleAgeSeconds;
    }

    private Clock GetClock(byte id)
    {
        return id == 1 ? left : id == 2 ? right : null;
    }
}
