using System;

namespace Biofit.GeomeTryOriginal
{
    /// <summary>
    /// Bounded activity envelope, not a jump queue or a biomechanical classifier.
    /// Two recent ankle observations start the intention; either ankle renews it.
    /// A character death does NOT reset this sensor state. Health/scene faults do.
    /// </summary>
    public sealed class BiofitGeometryHeldMotion
    {
        public const double StatusTimeoutSeconds = 0.55; // unchanged outage bound
        // Two nominal 100 ms STATUS periods + 50 ms scheduling margin.
        // Experimental policy, NOT a measured human cycle duration. An isolated
        // movement can hold for this long; physical stop-to-release must be measured.
        public const double ActivityWindowSeconds = 0.25;

        public enum State { Idle, WaitingBilateral, Held, Gap, Suspended,
            ClockUnavailable, Disconnected, LeftStatusStale, RightStatusStale,
            LeftUnhealthy, RightUnhealthy, InvalidTime }
        private struct Foot
        {
            public bool HasStatus, HasMotion, Healthy, Moving;
            public uint StatusMs, MotionMs;
            public double StatusAt, MotionAt, ActiveAt;
        }
        private readonly Foot[] feet = new Foot[2];
        private double previousTick = double.NaN;
        private bool wasPlaying = true;
        public bool Held { get; private set; }
        public State CurrentState { get; private set; }
        public bool LeftMoving => feet[0].Moving;
        public bool RightMoving => feet[1].Moving;
        public int PressCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int ResetCount { get; private set; }
        public int IgnoredStatus { get; private set; }
        public int IgnoredEvents { get; private set; }
        public string LastResetReason { get; private set; } = "startup";
        public double HeldSince { get; private set; }
        public double LastReleaseAt { get; private set; }

        public BiofitGeometryHeldMotion() { ClearFeet(); }
        private static bool Valid(double t) => t >= 0 && !double.IsNaN(t) && !double.IsInfinity(t);
        private static int Delta(uint a, uint b) => unchecked((int)(a - b));
        private void ClearFeet()
        {
            for (int i = 0; i < 2; i++) feet[i] = new Foot { ActiveAt = double.NegativeInfinity };
        }
        private void ForgetActivity()
        {
            for (int i = 0; i < 2; i++)
            { feet[i].ActiveAt = double.NegativeInfinity; feet[i].Moving = false; }
        }
        public void Reset() { Reset("external_reset"); }
        public void Reset(string reason)
        {
            Set(false, State.Suspended, Valid(previousTick) ? previousTick : 0);
            ClearFeet(); previousTick = double.NaN;
            LastResetReason = reason; ResetCount++;
        }
        public double StatusAge(byte foot, double now) => foot < 1 || foot > 2 || !feet[foot - 1].HasStatus
            ? double.PositiveInfinity : now - feet[foot - 1].StatusAt;
        public double ActivityAge(byte foot, double now) => foot < 1 || foot > 2
            ? double.PositiveInfinity : now - feet[foot - 1].ActiveAt;
        public double HoldDuration(double now) => Held ? Math.Max(0, now - HeldSince) : 0;

        public void ObserveStatus(byte foot, byte flags, uint sensorMs, double callbackSeconds)
        {
            if (foot < 1 || foot > 2 || !Valid(callbackSeconds)) { IgnoredStatus++; return; }
            int i = foot - 1;
            Foot f = feet[i];
            // STATUS health has its OWN watermark. A newer EVENT must never
            // discard a valid heartbeat (including the same IMU timestamp).
            // A repeated STATUS sample is not IMU liveness even with a new BLE seq.
            if (f.HasStatus && (Delta(sensorMs, f.StatusMs) <= 0 || callbackSeconds < f.StatusAt))
            { IgnoredStatus++; return; }
            f.HasStatus = true; f.StatusMs = sensorMs; f.StatusAt = callbackSeconds;
            f.Healthy = (flags & 0x0c) == 0x0c && (flags & 0x10) == 0;
            if (!f.Healthy) { f.Moving = false; f.ActiveAt = double.NegativeInfinity; }
            else if (!f.HasMotion || Delta(sensorMs, f.MotionMs) >= 0)
                ApplyMotion(ref f, (flags & 1) != 0, sensorMs, callbackSeconds);
            feet[i] = f;
        }
        public void ObserveEvent(byte foot, bool moving, uint sensorMs, double callbackSeconds)
        {
            if (foot < 1 || foot > 2 || !Valid(callbackSeconds)) { IgnoredEvents++; return; }
            int i = foot - 1; Foot f = feet[i];
            if (!f.HasStatus || !f.Healthy || callbackSeconds < f.StatusAt ||
                callbackSeconds - f.StatusAt > StatusTimeoutSeconds ||
                (f.HasMotion && (Delta(sensorMs, f.MotionMs) < 0 || callbackSeconds < f.MotionAt)))
            { IgnoredEvents++; return; }
            ApplyMotion(ref f, moving, sensorMs, callbackSeconds);
            feet[i] = f;
        }
        private static void ApplyMotion(ref Foot f, bool moving, uint sensorMs, double at)
        {
            // MOV was true until this OFF observation. This anchors the bounded
            // envelope to OFF, rather than truncating it at a previous 10 Hz sample.
            if (moving || (f.HasMotion && f.Moving)) f.ActiveAt = at;
            f.HasMotion = true; f.MotionMs = sensorMs; f.MotionAt = at; f.Moving = moving;
        }
        private bool Fresh(int i, double now) => feet[i].HasStatus &&
            now >= feet[i].StatusAt && now - feet[i].StatusAt <= StatusTimeoutSeconds;
        private bool Recent(int i, double now) => feet[i].Moving ||
            (now >= feet[i].ActiveAt && now - feet[i].ActiveAt <= ActivityWindowSeconds);
        private bool Set(bool held, State state, double now)
        {
            if (held != Held)
            {
                if (held) { PressCount++; HeldSince = now; }
                else { ReleaseCount++; LastReleaseAt = now; }
            }
            Held = held; CurrentState = state; return held;
        }
        private bool Fault(State reason, double now)
        {
            // Clear activity once per fault entry, not every frame. Fresh
            // STATUS received while clocks recover can then rearm without MOV_ON.
            if (CurrentState != reason) ForgetActivity();
            return Set(false, reason, now);
        }
        public bool Tick(double now, bool bothConnected, bool clocksReady, bool playing)
        {
            if (!Valid(now) || (!double.IsNaN(previousTick) && now < previousTick))
            { Reset("non_monotonic_time"); return Set(false, State.InvalidTime, 0); }
            previousTick = now;
            if (!playing)
            { wasPlaying = false; return Fault(State.Suspended, now); }
            if (!wasPlaying)
            {
                // Menus/app pause require observations AFTER resume, unlike a
                // death/respawn inside an active Level, which never suspends us.
                wasPlaying = true; ForgetActivity();
            }
            if (!bothConnected) return Fault(State.Disconnected, now);
            if (!Fresh(0, now)) return Fault(State.LeftStatusStale, now);
            if (!Fresh(1, now)) return Fault(State.RightStatusStale, now);
            if (!feet[0].Healthy) return Fault(State.LeftUnhealthy, now);
            if (!feet[1].Healthy) return Fault(State.RightUnhealthy, now);
            if (!clocksReady) return Fault(State.ClockUnavailable, now);

            bool left = Recent(0, now), right = Recent(1, now);
            if (!Held)
                return Set(left && right, left && right ? State.Held :
                    (left || right ? State.WaitingBilateral : State.Idle), now);
            if (!left && !right) return Set(false, State.Idle, now);
            return Set(true, feet[0].Moving || feet[1].Moving ? State.Held : State.Gap, now);
        }
    }
}
