using Biofit.GeomeTryOriginal;

static class Verify
{
    const byte Calm = 0x8c, Move = 0x8d;
    static int checks, failed;
    static void Check(bool pass, string name)
    {
        checks++;
        if (!pass) { failed++; Console.WriteLine("FAIL " + name); }
    }
    static BiofitGeometryHeldMotion Ready()
    {
        var m = new BiofitGeometryHeldMotion();
        m.ObserveStatus(1, Calm, 1000, 0);
        m.ObserveStatus(2, Calm, 1000, 0);
        m.Tick(0, true, true, true);
        return m;
    }
    static void Reproductions()
    {
        var m = Ready();
        m.ObserveEvent(1, true, 1010, .01);
        m.Tick(.01, true, true, true);
        m.ObserveEvent(1, false, 1100, .10);
        m.Tick(.10, true, true, true);
        m.ObserveEvent(2, true, 1120, .12);
        Check(m.Tick(.12, true, true, true),
            "A: recent activity of BOTH ankles must arm even without simultaneous MOV flags");

        m = Ready();
        bool held = false;
        for (int k = 1; k <= 8; k++)
        {
            double at = k * .1;
            bool moving = k % 2 == 0;
            for (byte foot = 1; foot <= 2; foot++)
            {
                uint ms = (uint)(1000 + k * 100);
                m.ObserveEvent(foot, moving, ms, at);
                // EVENT then STATUS from the very same IMU sample is valid.
                m.ObserveStatus(foot, moving ? Move : Calm, ms, at + .001);
            }
            held = m.Tick(at + .002, true, true, true);
        }
        Check(held,
            "B: EVENT timestamp must not starve independent STATUS heartbeat after 550 ms");
    }
    static void Status(BiofitGeometryHeldMotion m, double t, bool left = true, bool right = true, byte leftFlags = 0)
    {
        uint ms = (uint)(1000 + Math.Round(t * 1000));
        m.ObserveStatus(1, leftFlags != 0 ? leftFlags : left ? Move : Calm, ms, t);
        m.ObserveStatus(2, right ? Move : Calm, ms, t);
    }
    static void RecoveryAndEndurance()
    {
        foreach (double gap in new[] { .1, .3, .6, 1.0 })
        {
            var m = Ready(); Status(m, .1); Check(m.Tick(.1, true, true, true), "start");
            Check(m.Tick(.1 + gap, true, true, true) == (gap <= .55), "gap policy " + gap);
            double recoveredAt = .1 + gap + .01;
            Status(m, recoveredAt);
            Check(m.Tick(recoveredAt, true, true, true), "recovery from STATUS, no MOV_ON " + gap);
        }
        foreach (int cycles in new[] { 10, 20, 50, 100 })
        {
            var m = Ready();
            for (int n = 1; n <= cycles; n++)
            {
                double at = n * .1; Status(m, at);
                Check(m.Tick(at, true, true, true), "intent continues across avatar life " + n);
                // A dead/missing avatar is not a pause: do NOT pass playing=false.
                Check(m.Tick(at + .01, true, true, true), "dead avatar does not erase sensor state");
            }
            Check(m.PressCount == 1 && m.ReleaseCount == 0, "no accumulating reset per respawn " + cycles);
        }
        var x = Ready(); Status(x, .1); x.Tick(.1, true, true, true);
        Check(!x.Tick(.11, true, false, true), "clock loss releases");
        Status(x, .2); x.Tick(.2, true, false, true);
        Status(x, .3); Check(x.Tick(.3, true, true, true), "clocks recover without new transition event");
        Check(!x.Tick(.31, false, true, true), "disconnect releases");
        Status(x, .4); Check(x.Tick(.4, true, true, true), "connection recovers with statuses");
        Status(x, .5, leftFlags: 0x9d); Check(!x.Tick(.5, true, true, true), "CAL in progress");
        Status(x, .6); Check(x.Tick(.6, true, true, true), "CAL restored");
        Status(x, .7, leftFlags: 0x85); Check(!x.Tick(.7, true, true, true), "IMU invalid");
        Status(x, .8); Check(x.Tick(.8, true, true, true), "IMU restored");
        Check(!x.Tick(.81, true, true, false), "pause releases");
        Status(x, .9); x.Tick(.9, true, true, false);
        Check(!x.Tick(.91, true, true, true), "resume cannot reuse paused sample");
        Status(x, 1); Check(x.Tick(1, true, true, true), "fresh post-resume evidence");
        Check(!x.Tick(.9, true, true, true), "backward monotonic clock fails closed");
        Status(x, 1.1); Check(x.Tick(1.1, true, true, true), "recover invalid clock");
        x.Reset("sensor_restart"); Status(x, .01);
        Check(x.Tick(.01, true, true, true), "explicit epoch accepts restarted sensor timestamp");
        Check(x.ResetCount >= 2, "reset history preserved");

        x = Ready(); Status(x, .1); x.Tick(.1, true, true, true);
        Status(x, .2, false, false); Check(x.Tick(.2, true, true, true), "bounded OFF gap");
        Status(x, .3, false, false); Check(x.Tick(.3, true, true, true), "quiet sample does not renew envelope");
        Status(x, .4, false, false); Check(x.Tick(.44, true, true, true), "before release deadline");
        Check(!x.Tick(.46, true, true, true), "true rest releases, no buffered command");
        Status(x, .5, true, false); Check(!x.Tick(.5, true, true, true), "one ankle cannot start after rest");
        // New STATUS can be slightly older than EVENT and still prove heartbeat.
        x = Ready();
        for (int n = 1; n <= 20; n++)
        {
            double t = n * .1;
            for (byte foot = 1; foot <= 2; foot++)
            {
                x.ObserveEvent(foot, true, (uint)(1010 + n * 100), t);
                x.ObserveStatus(foot, Calm, (uint)(1000 + n * 100), t + .001);
            }
            Check(x.Tick(t + .002, true, true, true), "STATUS health independent of newer EVENT " + n);
        }
        // Repeating the same live sample at a new RX time cannot fake an IMU.
        for (byte foot = 1; foot <= 2; foot++) x.ObserveStatus(foot, Move, 3000, 2.5);
        Check(!x.Tick(2.56, true, true, true), "frozen sensor timestamp trips watchdog");
        Check(x.IgnoredStatus >= 2, "duplicate sample diagnosed");

        // Long session, jitter and dropped EVENTs; no per-frame allocations/queues.
        x = Ready();
        for (int k = 1; k <= 12000; k++)
        {
            double t = k * .1;
            bool moving = k % 8 < 6; // 200 ms OFF phases, bounded by policy
            Status(x, t, moving, moving);
            Check(x.Tick(t, true, true, true), "20-minute synthetic sustained stream " + k);
        }
        Check(x.PressCount == 1, "continuous intent, not repeated presses");
    }
    public static int Main()
    {
        Reproductions(); RecoveryAndEndurance();
        Console.WriteLine($"ContinuousMotion: {checks - failed}/{checks} PASS, {failed} FAIL (host state machine; not Unity/Android)");
        return failed == 0 ? 0 : 1;
    }
}
