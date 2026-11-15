using System;

/// <summary>
/// Maps an ESP32 millis() clock to the shared Stopwatch monotonic clock of
/// the Android phone. A PING write and PONG notification delimit a RTT.
/// This is an approximation: BLE/Android GATT delay can be asymmetric.
/// Accept only low-RTT samples; never treat an unreliable clock as synced.
/// No firmware change needed: BIOFIT v2.2.5 already supports PING token.
/// </summary>
public sealed class BiofitClockSync
{
    // At most 120 ms two-way delay accepted; worst-case uncertainty for
    // one device is roughly RTT/2, before OS/GATT asymmetry.
    public const double MaximumGoodRttSeconds = 0.120;
    private double bestRttSeconds = double.PositiveInfinity;
    private double offsetSeconds;
    private double latestRttSeconds = double.PositiveInfinity;
    public int GoodSamples { get; private set; }
    public int Replies { get; private set; }

    public bool IsReady => GoodSamples > 0;
    public double BestRttMs => IsReady ? bestRttSeconds * 1000.0 : -1.0;
    public double LastRttMs => double.IsInfinity(latestRttSeconds) ?
        -1.0 : latestRttSeconds * 1000.0;

    /// <param name="sensorMillis">Device millis at moment PONG was generated.</param>
    /// <param name="sentTicks">Phone Stopwatch.GetTimestamp() before PING write.</param>
    /// <param name="receivedTicks">Phone timestamp at callback of PONG.</param>
    /// <param name="frequency">Stopwatch.Frequency, supplied for pure tests.</param>
    public bool ObservePong(uint sensorMillis, long sentTicks,
        long receivedTicks, long frequency)
    {
        if (frequency <= 0 || receivedTicks < sentTicks) return false;
        double rtt = (receivedTicks - sentTicks) / (double)frequency;
        if (rtt > 10.0 || rtt < 0.0) return false;
        Replies++;
        latestRttSeconds = rtt;
        if (rtt > MaximumGoodRttSeconds) return false;

        double midpoint = (sentTicks + (receivedTicks - sentTicks) / 2.0) /
            frequency;
        double proposed = midpoint - sensorMillis * 0.001;
        // Sample with shortest RTT is less contaminated by BLE queuing.
        if (rtt <= bestRttSeconds)
        {
            bestRttSeconds = rtt;
            offsetSeconds = proposed;
        }
        GoodSamples++;
        return true;
    }

    public double ToPhoneSeconds(uint sensorMillis)
    {
        if (!IsReady) throw new InvalidOperationException("Clock not synchronized");
        return offsetSeconds + sensorMillis * 0.001;
    }

    /// <summary>Device reset or BLE reconnection invalidates old offsets.</summary>
    public void Reset()
    {
        bestRttSeconds = double.PositiveInfinity;
        latestRttSeconds = double.PositiveInfinity;
        offsetSeconds = 0;
        GoodSamples = 0;
        Replies = 0;
    }
}
