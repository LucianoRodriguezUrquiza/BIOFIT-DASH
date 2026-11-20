using System;

/// <summary>
/// Single-use gate for commands received by the GeomeTry cube adapter.
/// No Unity dependency: this is host-testable and never queues an expired event.
/// The source clock MUST be the same monotonic clock as nowSeconds.
/// </summary>
public sealed class BiofitGeomeTryCommandGate
{
    public enum Decision { Accepted, Duplicate, InvalidClock, Future, Expired }

    private long lastObservedId;

    public long LastObservedId => lastObservedId;

    /// <summary>
    /// One ID may be evaluated exactly once, including if it expires or is rejected.
    /// A distinct BLE pair gets a distinct command ID; this does not prove a
    /// distinct human jump occurred.
    /// </summary>
    public Decision Evaluate(long commandId, double originSeconds,
        double nowSeconds, double maximumAgeSeconds)
    {
        if (commandId <= lastObservedId) return Decision.Duplicate;
        lastObservedId = commandId;

        if (double.IsNaN(originSeconds) || double.IsInfinity(originSeconds) ||
            double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) ||
            originSeconds <= 0d || nowSeconds <= 0d ||
            maximumAgeSeconds <= 0d)
            return Decision.InvalidClock;

        double age = nowSeconds - originSeconds;
        // A small lead is tolerated for estimation/rounding; never allow a
        // wildly future-dated sensor event to be treated as immediate.
        if (age < -0.05d) return Decision.Future;
        if (age > maximumAgeSeconds) return Decision.Expired;
        return Decision.Accepted;
    }
}
