using System;

// Conservative gameplay gate. A STATUS offset is not absolute BLE latency.
// Unknown clocks fail closed; all rejected packets still belong in the CSV.
public sealed class BiofitEventGate
{
    public const double MaxAgeMs = 250d;
    private bool hasSequence;
    private ushort lastSequence;
    public int Missing { get; private set; }
    public int DuplicateOrReordered { get; private set; }
    public void Reset() { hasSequence = false; }
    public bool ObserveSequence(ushort sequence)
    {
        if (!hasSequence) { lastSequence = sequence; hasSequence = true; return true; }
        int delta = (ushort)(sequence - lastSequence);
        if (delta == 0 || delta >= 32768) { DuplicateOrReordered++; return false; }
        Missing += delta - 1;
        lastSequence = sequence;
        return true;
    }
    public static string Rejection(bool sequenceValid, bool mapped,
        double callbackSeconds, double mappedSeconds, double queueMs, int firmwareQueueMs)
    {
        if (!sequenceValid) return "SECUENCIA";
        if (double.IsNaN(queueMs) || double.IsInfinity(queueMs) || queueMs > MaxAgeMs ||
            firmwareQueueMs + queueMs > MaxAgeMs) return "ANTIGUO";
        if (!mapped) return "SIN_RELOJ";
        double age = Math.Max(0d, (callbackSeconds - mappedSeconds) * 1000d) + queueMs;
        if (double.IsNaN(age) || double.IsInfinity(age) || age > MaxAgeMs) return "ANTIGUO";
        return "";
    }
}
