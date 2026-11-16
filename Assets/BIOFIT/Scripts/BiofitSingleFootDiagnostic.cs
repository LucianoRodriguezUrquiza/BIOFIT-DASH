using System;

/// <summary>
/// Pure diagnostic logic for one-foot BLE controls. Counts only fresh UP events
/// received by Unity while that foot's diagnostic mode is selected.
/// These are sensor candidates, not confirmed human movements.
/// </summary>
public sealed class BiofitSingleFootDiagnostic
{
    public enum Decision { Accepted, BelowThreshold, Refractory, Invalid }

    public sealed class Counters
    {
        public int Received { get; internal set; }
        public int BelowThreshold { get; internal set; }
        public int Refractory { get; internal set; }
        public int Invalid { get; internal set; }
        public int Accepted { get; internal set; }
        public int Jumped { get; internal set; }
        public int GameUnavailable { get; internal set; }
    }

    public Counters Left { get; private set; } = new Counters();
    public Counters Right { get; private set; } = new Counters();

    public float LeftThresholdG { get; private set; } = 0.25f;
    public float RightThresholdG { get; private set; } = 0.25f;
    public float Threshold(byte foot) => foot == 1 ? LeftThresholdG : RightThresholdG;
    public void AdjustThreshold(byte foot, float delta)
    {
        if ((foot != 1 && foot != 2) || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        float v = Math.Max(0.25f, Math.Min(1f, Threshold(foot) + delta));
        if (foot == 1) LeftThresholdG = v; else RightThresholdG = v;
    }

    private double lastLeftAccepted = double.NegativeInfinity;
    private double lastRightAccepted = double.NegativeInfinity;

    public void Reset()
    {
        Left = new Counters();
        Right = new Counters();
        lastLeftAccepted = double.NegativeInfinity;
        lastRightAccepted = double.NegativeInfinity;
    }

    public Decision Observe(byte foot, float verticalG, double eventSeconds,
        float thresholdG, double refractorySeconds)
    {
        if (foot != 1 && foot != 2) return Decision.Invalid;
        Counters c = foot == 1 ? Left : Right;
        c.Received++;
        if (float.IsNaN(verticalG) || float.IsInfinity(verticalG) ||
            double.IsNaN(eventSeconds) || double.IsInfinity(eventSeconds) ||
            float.IsNaN(thresholdG) || float.IsInfinity(thresholdG) || thresholdG < 0f ||
            double.IsNaN(refractorySeconds) || double.IsInfinity(refractorySeconds) || refractorySeconds < 0d)
        {
            c.Invalid++;
            return Decision.Invalid;
        }
        if (verticalG < thresholdG)
        {
            c.BelowThreshold++;
            return Decision.BelowThreshold;
        }
        double last = foot == 1 ? lastLeftAccepted : lastRightAccepted;
        if (eventSeconds - last < refractorySeconds)
        {
            c.Refractory++;
            return Decision.Refractory;
        }
        if (foot == 1) lastLeftAccepted = eventSeconds;
        else lastRightAccepted = eventSeconds;
        c.Accepted++;
        return Decision.Accepted;
    }

    public void RecordGameResult(byte foot, bool jumped)
    {
        Counters c = foot == 1 ? Left : foot == 2 ? Right : null;
        if (c == null) return;
        if (jumped) c.Jumped++;
        else c.GameUnavailable++;
    }
}
