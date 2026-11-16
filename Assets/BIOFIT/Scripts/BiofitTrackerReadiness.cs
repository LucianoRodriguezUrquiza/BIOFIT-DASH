using System;

// Status flags describe acquisition state, NOT clock synchronization or physical truth.
public sealed class BiofitTrackerReadiness
{
    public byte Flags { get; private set; }
    private double lastProgress = double.NegativeInfinity;
    private uint lastMs;
    private bool observed;
    public void Reset() { observed = false; lastProgress = double.NegativeInfinity; }
    public bool Observe(byte[] b, byte id, double callbackSeconds)
    {
        if (b == null || b.Length != 20 || b[0] != 2 || b[1] != id ||
            double.IsNaN(callbackSeconds) || double.IsInfinity(callbackSeconds)) return false;
        uint ms = BitConverter.ToUInt32(b,6);
        if (!observed || ms != lastMs) lastProgress = callbackSeconds;
        bool changed = !observed || Flags != b[2];
        Flags = b[2]; lastMs = ms; observed = true;
        return changed;
    }
    public bool Current(double now) => observed && now >= lastProgress && now-lastProgress <= 1.0;
    public string Describe(double now)
    {
        if (!Current(now)) return "CAL:? ARM:?";
        string cal = (Flags & 0x10) != 0 ? "EN CURSO" : (Flags & 0x0c) == 0x0c ? "OK" : "NO";
        string arm = (Flags & 0x80) == 0 ? "?" : (Flags & 0x40) != 0 ? "SI" : "NO";
        return "CAL:" + cal + " ARM:" + arm;
    }
    public string Detail(double now) => Describe(now) + (!Current(now) ? " STATUS antiguo/ausente" :
        " IMU:" + ((Flags & 8)!=0 ? "OK" : "NO") + " EJES:" + ((Flags & 32)!=0 ? "OK" : "NO") +
        " MOV:" + ((Flags & 1)!=0 ? "SI" : "NO"));
}
