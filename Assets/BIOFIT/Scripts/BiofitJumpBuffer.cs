using System;

/// <summary>
/// One-slot, strictly expiring landing assist for an already accepted bilateral
/// command. It never buffers weak/old/unpaired BLE impulses, and never queues
/// multiple inputs. The expiry uses the caller's monotonic REAL clock.
/// </summary>
public sealed class BiofitJumpBuffer
{
    public const double WindowSeconds = 0.120d;
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
        byte foot, string source, int commandId, bool allowBuffer)
    {
        if (game == null) throw new ArgumentNullException(nameof(game));
        if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            throw new ArgumentOutOfRangeException(nameof(nowSeconds));
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
        if (landingSeconds <= 0d || landingSeconds > WindowSeconds)
            return BiofitDashCore.JumpResult.Airborne;
        pending = true;
        armedAt = nowSeconds;
        expiresAt = nowSeconds + WindowSeconds;
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
        if (double.IsNaN(nowSeconds) || nowSeconds > expiresAt)
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
