using System;

namespace Biofit.Geometry
{
    public enum JumpOutcome { Executed, ExecutedOrb, Airborne, Inactive, Duplicate, Expired, Invalid, Cancelled }
    public interface IGeometryJumpTarget { JumpOutcome TryJump(); }

    // One command -> at most one call to the real player's TryJump. No queue,
    // no held virtual key, no new debounce. Detection remains in BIOFIT legacy.
    public sealed class GeometryJumpCommands
    {
        // Match the existing receiver gate; do not reject otherwise eligible
        // legacy events by inventing a stricter gameplay freshness threshold.
        public const double MaximumAgeSeconds = BiofitEventGate.MaxAgeMs / 1000d;
        private long lastId;
        public int Epoch { get; private set; }
        public void Cancel() { Epoch++; } // IDs stay monotonic across retries.

        public JumpOutcome Submit(long id, int epoch, double occurredAt, double now,
                                  bool active, IGeometryJumpTarget target)
        {
            if (id <= 0 || !Finite(occurredAt) || !Finite(now)) return JumpOutcome.Invalid;
            if (id <= lastId) return JumpOutcome.Duplicate;
            lastId = id; // Rejected inputs are consumed too, never renewed later.
            if (epoch != Epoch) return JumpOutcome.Cancelled;
            if (now < occurredAt - .150) return JumpOutcome.Invalid; // existing clock estimator tolerance
            if (now - occurredAt > MaximumAgeSeconds) return JumpOutcome.Expired;
            if (!active || target == null) return JumpOutcome.Inactive;
            return target.TryJump();
        }
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
