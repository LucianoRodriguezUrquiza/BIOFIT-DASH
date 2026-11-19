using System;

/// <summary>
/// One-slot, strictly expiring landing assist for an already accepted bilateral
/// command. It never buffers weak/old/unpaired BLE impulses, and never queues
/// multiple inputs. The expiry uses the caller's monotonic REAL clock.
/// </summary>
public sealed class BiofitJumpBuffer
{
    public const double WindowSeconds = 0.180d; // legacy unchanged
    public const double CycleWindowSeconds = 0.300d; // EXP onset-to-execution ceiling
    private bool pending;
    private double armedAt, expiresAt;
    private byte pendingFoot;
    private int pendingCommandId;
    private string pendingSource;

    public bool Pending => pending;
    public enum Completion { None, Executed, Expired, Cancelled }

    public struct Outcome
    {
        public Completion State;
        public byte Foot;
        public string Source;
        public int CommandId;
        public double AgeMilliseconds;
        public Outcome(Completion state, byte foot, string source, int id, double ageMs)
        {
            State = state; Foot = foot; Source = source; CommandId = id; AgeMilliseconds = ageMs;
        }
    }

    public void Clear()
    {
        pending = false;
        pendingSource = null;
        pendingCommandId = 0;
    }

    public BiofitDashCore.JumpResult Submit(BiofitDashCore game, double nowSeconds,
        byte foot, string source, int commandId, bool allowBuffer, double windowSeconds = WindowSeconds, double originSeconds = double.NaN)
    {
        if (game == null) throw new ArgumentNullException(nameof(game));
        if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            throw new ArgumentOutOfRangeException(nameof(nowSeconds));
        if (double.IsNaN(windowSeconds) || windowSeconds <= 0 || windowSeconds > .300d)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        if (double.IsInfinity(originSeconds)) return BiofitDashCore.JumpResult.InputExpired;
        double origin = double.IsNaN(originSeconds) ? nowSeconds : Math.Min(nowSeconds,originSeconds);
        double deadline = origin + windowSeconds;
        // Experimental inputs retain their sensor-aligned age, not a fresh TTL on receipt.
        if (double.IsInfinity(origin) || nowSeconds > deadline) return BiofitDashCore.JumpResult.InputExpired;
        var result = game.TryJump();
        if (result == BiofitDashCore.JumpResult.Accepted)
        {
            // A grounded immediate action supersedes any expired old slot.
            Clear();
            return result;
        }
        if (result != BiofitDashCore.JumpResult.Airborne || !allowBuffer)
            return result;
        if (pending && nowSeconds <= expiresAt)
            return BiofitDashCore.JumpResult.AlreadyBuffered;
        if (pending) Clear(); // Expired: never accumulate/refresh an old slot.
        double landingSeconds = game.EstimatedSecondsUntilLanding;
        // Euler integration + frame quantization can land later than the continuous
        // estimate. Experimental admission reserves two recent simulation steps.
        if(!double.IsNaN(originSeconds)) landingSeconds += 2d*game.LastStepSeconds;
        if (landingSeconds <= 0d || landingSeconds > deadline-nowSeconds)
            return BiofitDashCore.JumpResult.Airborne;
        pending = true;
        armedAt = nowSeconds;
        expiresAt = deadline;
        pendingFoot = foot;
        pendingSource = source;
        pendingCommandId = commandId;
        return BiofitDashCore.JumpResult.Buffered;
    }

    public Outcome Tick(BiofitDashCore game, double nowSeconds)
    {
        if (!pending) return default(Outcome);
        if (game == null) throw new ArgumentNullException(nameof(game));
        double age = Math.Max(0d, (nowSeconds - armedAt) * 1000d);
        if (!game.Playing || game.GameOver || game.Paused)
        {
            var cancelled = new Outcome(Completion.Cancelled, pendingFoot,
                pendingSource, pendingCommandId, age);
            Clear();
            return cancelled;
        }
        // Strict expiry: a stalled Unity frame must not execute an old command.
        if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) || nowSeconds < armedAt || nowSeconds > expiresAt)
        {
            var expired = new Outcome(Completion.Expired, pendingFoot,
                pendingSource, pendingCommandId, age);
            Clear();
            return expired;
        }
        if (game.JumpAltitude > 0f) return default(Outcome);
        var jump = game.TryJump();
        var state = jump == BiofitDashCore.JumpResult.Accepted ?
            Completion.Executed : Completion.Cancelled;
        var outcome = new Outcome(state, pendingFoot, pendingSource, pendingCommandId, age);
        Clear();
        return outcome;
    }
}
