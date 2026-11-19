using System;

// Pairs ONLY firmware v2.2.9 LIFT_ONSET events, never legacy UP or MOV_OFF.
// A one-event-per-cycle producer plus freshness/sequence gate precedes this class.
public sealed class BiofitLiftPairDetector
{
    public const double MinPairSeconds = .600d;
    public const double PairWindowSeconds = .180d;
    public enum Decision { Invalid, BelowThreshold, Reordered, Waiting, OutsideWindow, TooSoon, Paired }
    private readonly double[] pending = {double.NaN,double.NaN};
    private readonly double[] last = {double.NegativeInfinity,double.NegativeInfinity};
    private double lastPair = double.NegativeInfinity;
    public Decision LastDecision { get; private set; }
    public double LastPairSeconds => lastPair;
    public double LastGapMs { get; private set; }
    public int Pairs { get; private set; }
    public void Clear()
    {
        pending[0]=pending[1]=double.NaN;
        last[0]=last[1]=double.NegativeInfinity;
        // Keep the temporal guard across reconnect/mode/reset; no bypass.
    }
    public bool Observe(byte foot,double time,float up)
    {
        LastDecision=Decision.Invalid;
        if(foot<1||foot>2||double.IsNaN(time)||double.IsInfinity(time)||float.IsNaN(up)||float.IsInfinity(up))return false;
        int i=foot-1,j=1-i;
        if(time<=last[i]) {LastDecision=Decision.Reordered;return false;}
        last[i]=time;
        // Allow quantization of the firmware's >=0.12 g onset (i16 mg rounding).
        if(up<.12f) {LastDecision=Decision.BelowThreshold;return false;}
        pending[i]=time;
        if(double.IsNaN(pending[j])) {LastDecision=Decision.Waiting;return false;}
        double gap=Math.Abs(time-pending[j]);
        if(gap>PairWindowSeconds+1e-9) {
            if(pending[i]<pending[j])pending[i]=double.NaN;else pending[j]=double.NaN;
            LastDecision=Decision.OutsideWindow;return false;
        }
        double pairTime=Math.Max(time,pending[j]);
        pending[0]=pending[1]=double.NaN; // matched events consumed even if rejected
        LastGapMs=gap*1000d;
        if(pairTime-lastPair<MinPairSeconds-1e-9) {LastDecision=Decision.TooSoon;return false;}
        lastPair=pairTime;Pairs++;LastDecision=Decision.Paired;return true;
    }
}
