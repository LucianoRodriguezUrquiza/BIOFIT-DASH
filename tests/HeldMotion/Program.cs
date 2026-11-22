using Biofit.GeomeTryOriginal;

static class Verify
{
    private const byte Calm = 0x8c;
    private const byte Move = 0x8d;

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("FAIL held motion: " + message);
    }

    private static BiofitGeometryHeldMotion Ready(double at = 0.0,
        uint sensorMs = 1000)
    {
        var motion = new BiofitGeometryHeldMotion();
        motion.ObserveStatus(1, Calm, sensorMs, at);
        motion.ObserveStatus(2, Calm, sensorMs, at);
        return motion;
    }

    private static void On(BiofitGeometryHeldMotion m, double when = .02,
        uint sensorMs = 1020)
    {
        m.ObserveEvent(1, true, sensorMs, when);
        m.ObserveEvent(2, true, sensorMs, when);
        Check(m.Tick(when, true, true, true), "both MOV_ON must press");
    }

    public static int Main()
    {
        int checks = 0;
        // 1. Quiet, calibrated trackers never hold input.
        {
            var m = Ready();
            Check(!m.Tick(.02, true, true, true), "quiet must not jump");
            checks++;
        }
        // 2. Single-leg or invalid ID cannot start bilateral held control.
        {
            var m = Ready();
            m.ObserveEvent(1, true, 1020, .02);
            m.ObserveEvent(3, true, 1020, .02);
            Check(!m.Tick(.02, true, true, true), "one foot cannot arm");
            checks++;
        }
        // 3. Both MOV_ON trigger hold immediately, without 900ms cooldown.
        {
            var m = Ready();
            On(m);
            Check(m.PressCount == 1, "press should be single edge");
            for (int k = 1; k <= 80; k++)
            {
                double now = .02 + k * .10;
                uint ms = 1020u + (uint)k * 100u;
                m.ObserveStatus(1, Move, ms, now);
                m.ObserveStatus(2, Move, ms, now);
                Check(m.Tick(now, true, true, true), "hold must last indefinitely with heartbeats");
            }
            Check(m.PressCount == 1, "no synthetic retrigger needed");
            checks++;
        }
        // 4. One foot may drop while the other remains moving.
        {
            var m = Ready();
            On(m);
            m.ObserveStatus(1, Calm, 1130, .13);
            m.ObserveStatus(2, Move, 1130, .13);
            Check(m.Tick(.13, true, true, true), "alternating activity should hold");
            m.ObserveStatus(1, Calm, 1430, .43);
            m.ObserveStatus(2, Move, 1430, .43);
            Check(m.Tick(.43, true, true, true), "holding one moving ankle should persist");
            checks++;
        }
        // 5. Both MOV_OFF release only after short bounded OFF hysteresis.
        {
            var m = Ready();
            On(m);
            m.ObserveEvent(1, false, 1150, .15);
            m.ObserveEvent(2, false, 1150, .15);
            Check(m.Tick(.15, true, true, true), "release grace begins");
            Check(m.Tick(.30, true, true, true), "short gap bridged");
            Check(!m.Tick(.32, true, true, true), "rest must release after grace");
            Check(m.ReleaseCount == 1, "single release edge");
            checks++;
        }
        // 6. Status overrides event and can reassert MOV without new MOV_ON.
        {
            var m = Ready();
            On(m);
            m.ObserveStatus(1, Move, 1300, .30);
            m.ObserveStatus(2, Move, 1300, .30);
            m.ObserveEvent(1, false, 1200, .31); // delayed old MOV_OFF
            m.ObserveEvent(2, false, 1200, .31);
            Check(m.Tick(.32, true, true, true),
                "out-of-order events cannot cancel fresh moving status");
            checks++;
        }
        // 7. No stale STATUS can keep the cubo jumping after a BLE stall.
        {
            var m = Ready();
            On(m);
            Check(!m.Tick(.61, true, true, true), "stale heartbeat releases");
            Check(!m.Tick(.62, true, true, true), "stale state stays off");
            checks++;
        }
        // 8. BLE disconnect resets state, reconnect needs fresh two-foot STATUS.
        {
            var m = Ready();
            On(m);
            Check(!m.Tick(.03, false, true, true), "disconnect releases");
            Check(!m.Tick(.04, true, true, true), "old states cannot rearm on reconnect");
            m.ObserveStatus(1, Move, 2000, .05);
            m.ObserveStatus(2, Move, 2000, .05);
            Check(m.Tick(.05, true, true, true), "rearmed from fresh statuses");
            checks++;
        }
        // 9. Clock loss and scene pause must cancel the input.
        {
            var m = Ready();
            On(m);
            Check(!m.Tick(.03, true, false, true), "clock loss cancels");
            m.ObserveStatus(1, Move, 2100, .04);
            m.ObserveStatus(2, Move, 2100, .04);
            Check(!m.Tick(.04, true, true, false), "pause cancels");
            Check(!m.Tick(.05, true, true, true), "resume cannot inherit old movement");
            checks++;
        }
        // 10. Calibrating or invalid IMU never produces a held button.
        {
            var m = Ready();
            On(m);
            m.ObserveStatus(1, (byte)(Move | 0x10), 1200, .20);
            Check(!m.Tick(.20, true, true, true), "calibrating releases");
            m.ObserveStatus(1, 0x81, 1300, .30); // MOV + 0x80, no IMU/CAL
            Check(!m.Tick(.30, true, true, true), "sensor unhealthy stays off");
            checks++;
        }
        // 11. uint32 sensor millis wraparound must not misclassify freshness.
        {
            var m = Ready(0d, uint.MaxValue - 90);
            m.ObserveStatus(1, Move, 20, .10);
            m.ObserveStatus(2, Move, 20, .10);
            Check(m.Tick(.10, true, true, true), "rollover 2^32 ms");
            checks++;
        }
        // 12. A slow old status may never replace newer observations.
        {
            var m = Ready();
            On(m);
            m.ObserveStatus(1, Calm, 1005, .10);
            m.ObserveStatus(2, Calm, 1005, .10);
            Check(m.Tick(.10, true, true, true),
                "old status cannot replace fresh event");
            checks++;
        }
        // 13. After ending a hold, a one-leg movement doesn't rearm.
        {
            var m = Ready();
            On(m);
            m.ObserveStatus(1, Calm, 1200, .20);
            m.ObserveStatus(2, Calm, 1200, .20);
            Check(m.Tick(.20, true, true, true), "grace");
            Check(!m.Tick(.40, true, true, true), "end hold");
            m.ObserveStatus(1, Move, 1500, .50);
            m.ObserveStatus(2, Calm, 1500, .50);
            Check(!m.Tick(.50, true, true, true), "isolated movement after rest");
            checks++;
        }
        Console.WriteLine("PASS: " + checks +
            " held-motion cases: idle, bilateral arming, continuous jumping," +
            " independent ankles, rest, stale/out-of-order BLE, reconnection," +
            " clock loss, calibration, wraparound.");
        return 0;
    }
}
