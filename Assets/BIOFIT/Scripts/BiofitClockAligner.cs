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
        public uint LastSensor;
        public double SensorSeconds;
        public bool HasSensor;
    }

    private readonly Clock left = new Clock();
    private readonly Clock right = new Clock();

    // En Android real STATUS puede llegar bastante por debajo de la tasa
    // nominal del firmware. Ocho muestras en 12 s nunca se alcanzaban en
    // la prueba v0.4 (mas de 300 STATUS acumulados, reloj INACTIVO).
    // Conservar 8 anclas, ampliando solo la ventana de observacion.
    // Esto no convierte el estimador basado en recepcion BLE en un reloj
    // fisicamente sincronizado; su offset aun contiene demora GATT.
    private const double WindowSeconds = 32d;
    private const int MinimumSamples = 8;
    private const int MaxSamples = 160;
    private const double MaxSampleAgeSeconds = 4.0d;

    public int LeftSamples => left.Samples;
    public int RightSamples => right.Samples;
    // A diferencia de Samples, estos valores SI estan dentro de la
    // ventana temporal. Usarlos para diagnosticar IsReady().
    public int LeftRecentSamples => left.Recent.Count;
    public int RightRecentSamples => right.Recent.Count;
    public double LeftAgeSeconds(double nowSeconds) =>
        nowSeconds - left.LastCallback;
    public double RightAgeSeconds(double nowSeconds) =>
        nowSeconds - right.LastCallback;

    public void ObserveStatus(byte trackerId, uint sensorMillis, double callbackSeconds)
    {
        Clock state = GetClock(trackerId);
        if (state == null || callbackSeconds < 0d ||
            double.IsNaN(callbackSeconds) || double.IsInfinity(callbackSeconds))
            return;

        // Signed modular difference unwraps millis at 49.7 days.
        // STATUS is ordered within one GATT connection; backward time means reboot.
        int delta = state.HasSensor ? unchecked((int)(sensorMillis - state.LastSensor)) : 0;
        if (state.HasSensor && delta < 0)
        {
            Reset(trackerId);
            state = GetClock(trackerId);
        }
        else if (state.HasSensor && delta == 0) return; // duplicate is not an anchor
        state.SensorSeconds = state.HasSensor ? state.SensorSeconds + delta * 0.001d : sensorMillis * 0.001d;
        state.LastSensor = sensorMillis;
        state.HasSensor = true;
        double offset = callbackSeconds - state.SensorSeconds;

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


        Clock state = GetClock(trackerId);
        if (state == null || !Ready(state, callbackSeconds)) return false;

        double estimate = state.SensorSeconds +
            unchecked((int)(sensorMillis - state.LastSensor)) * 0.001d + state.BestOffset;
        // Preserve old estimates so the freshness gate can reject them.
        if (estimate > callbackSeconds + 0.15d) return false;

        alignedSeconds = estimate;
        return true;
    }

    public void Reset(byte id)
    {
        Clock state = GetClock(id);
        if (state == null) return;
        state.Recent.Clear(); state.HasBest = false; state.HasSensor = false;
        state.Samples = 0; state.LastCallback = -10000d;
    }

    private static bool Ready(Clock state, double nowSeconds)
    {
        return state.HasBest &&
            state.Recent.Count >= MinimumSamples &&
            // Una notificacion EVENT puede haberse encolado justo antes
            // que STATUS aunque se procese despues; tolerar 100 ms.
            nowSeconds - state.LastCallback >= -0.10d &&
            nowSeconds - state.LastCallback <= MaxSampleAgeSeconds;
    }

    private Clock GetClock(byte id)
    {
        return id == 1 ? left : id == 2 ? right : null;
    }
}
