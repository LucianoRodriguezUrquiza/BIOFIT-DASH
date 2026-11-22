using System;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// Continuous "hold jump" input for the GeomeTry cube. The firmware's
    /// existing MOV_ON/MOV_OFF classifier and STATUS MOV flag are the ONLY
    /// sources of movement. No new acceleration thresholds, pairing cooldown,
    /// prediction, or jump buffer are introduced here.
    ///
    /// This is intentionally a deterministic, Unity-independent state machine.
    /// Each foot must have a fresh, healthy STATUS heartbeat. A new hold begins
    /// only once both ankles are moving; during a hold, one moving ankle keeps
    /// the control pressed and both resting release it after a short gap.
    /// </summary>
    public sealed class BiofitGeometryHeldMotion
    {
        // Firmware normally sends STATUS every 100 ms; 550 ms allows a few
        // delayed packets, but cannot leave jump held after an BLE outage.
        public const double StatusTimeoutSeconds = 0.55;
        // 160 ms only bridges short MOV_OFF gaps. The firmware itself already
        // has a separate MOV_OFF hysteresis/release delay (~130 ms defaults).
        public const double BothRestGraceSeconds = 0.16;

        private struct FootState
        {
            public bool HasSample;
            public bool HasStatus;
            public bool Moving;
            public bool Healthy;
            public uint LastSensorMs;
            public double LastStatusAt;
        }

        private readonly FootState[] feet = new FootState[2];
        private double restSince = double.NaN;
        private double previousTick = double.NaN;
        public bool Held { get; private set; }
        public bool LeftMoving => feet[0].Moving;
        public bool RightMoving => feet[1].Moving;
        public int PressCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public void Reset()
        {
            feet[0] = default(FootState);
            feet[1] = default(FootState);
            restSince = previousTick = double.NaN;
            Held = false;
        }

        private static bool TimeOkay(double seconds)
        {
            return !double.IsNaN(seconds) && !double.IsInfinity(seconds) &&
                seconds >= 0d;
        }

        private static bool IsOlder(uint incoming, uint previous)
        {
            // Handles uint32 millis rollover (24.9 days for signed delta).
            return unchecked((int)(incoming - previous)) < 0;
        }

        /// <summary>Called from BLE main-thread dispatch for each STATUS.</summary>
        public void ObserveStatus(byte foot, byte flags, uint sensorMs,
            double callbackSeconds)
        {
            if (foot < 1 || foot > 2 || !TimeOkay(callbackSeconds)) return;
            int index = foot - 1;
            FootState f = feet[index];
            if (f.HasSample && (IsOlder(sensorMs, f.LastSensorMs) ||
                sensorMs == f.LastSensorMs)) return;
            if (f.HasStatus && callbackSeconds < f.LastStatusAt) return;

            f.HasSample = true;
            f.HasStatus = true;
            f.LastSensorMs = sensorMs;
            f.LastStatusAt = callbackSeconds;
            // Firmware flags: 0x01 MOV, 0x04 CAL, 0x08 IMU,
            // 0x10 CALIBRATING. A stale/uncalibrated sensor never holds input.
            f.Healthy = (flags & 0x0c) == 0x0c && (flags & 0x10) == 0;
            f.Moving = f.Healthy && (flags & 0x01) != 0;
            feet[index] = f;
        }

        /// <summary>
        /// Fast path for a validated MOV_ON/OFF EVENT, from the SAME device
        /// sensor clock. Old/out-of-order events cannot override newer STATUS.
        /// The event is not enough without a fresh healthy STATUS heartbeat.
        /// </summary>
        public void ObserveEvent(byte foot, bool moving, uint sensorMs,
            double callbackSeconds)
        {
            if (foot < 1 || foot > 2 || !TimeOkay(callbackSeconds)) return;
            int index = foot - 1;
            FootState f = feet[index];
            if (!f.HasStatus || !f.Healthy ||
                callbackSeconds < f.LastStatusAt ||
                (f.HasSample && IsOlder(sensorMs, f.LastSensorMs))) return;
            f.HasSample = true;
            f.LastSensorMs = sensorMs;
            f.Moving = moving;
            feet[index] = f;
        }

        private bool FreshAndHealthy(int index, double now)
        {
            FootState f = feet[index];
            double age = now - f.LastStatusAt;
            return f.HasStatus && f.Healthy && age >= 0d &&
                age <= StatusTimeoutSeconds;
        }

        /// <summary>Must run on Unity main thread during active gameplay.</summary>
        public bool Tick(double now, bool bothConnected, bool clocksReady,
            bool playing)
        {
            if (!TimeOkay(now) ||
                (!double.IsNaN(previousTick) && now < previousTick))
            {
                Reset();
                return false;
            }
            previousTick = now;
            if (!bothConnected || !clocksReady || !playing ||
                !FreshAndHealthy(0, now) || !FreshAndHealthy(1, now))
            {
                Release();
                return false;
            }

            bool left = feet[0].Moving;
            bool right = feet[1].Moving;
            if (!Held)
            {
                // An isolated ankle twitch cannot start an autonomous jump.
                if (left && right)
                {
                    Held = true;
                    PressCount++;
                    restSince = double.NaN;
                }
                return Held;
            }
            if (left || right)
            {
                // Maintain across alternating/unequal ankle activity.
                restSince = double.NaN;
                return true;
            }
            if (double.IsNaN(restSince)) restSince = now;
            if (now - restSince >= BothRestGraceSeconds)
                Release();
            return Held;
        }

        private void Release()
        {
            if (Held) ReleaseCount++;
            Held = false;
            restSince = double.NaN;
        }
    }
}
