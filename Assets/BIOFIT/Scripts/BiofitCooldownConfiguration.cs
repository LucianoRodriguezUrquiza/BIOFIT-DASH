using System;

/// <summary>
/// Reusable, Unity-free preference adapter for the actual bilateral detector.
/// In Android the delegates are PlayerPrefs; host tests use an in-memory store.
/// </summary>
public sealed class BiofitCooldownConfiguration
{
    public const string PreferenceKey = "BIOFIT_BILATERAL_COOLDOWN_MS_V1";

    private readonly BiofitBilateralJumpDetector detector;
    private readonly Func<string, int, int> readInt;
    private readonly Action<string, int> writeInt;
    private readonly Action save;

    public BiofitCooldownConfiguration(BiofitBilateralJumpDetector detector,
        Func<string, int, int> readInt, Action<string, int> writeInt,
        Action save)
    {
        this.detector = detector ?? throw new ArgumentNullException(nameof(detector));
        this.readInt = readInt ?? throw new ArgumentNullException(nameof(readInt));
        this.writeInt = writeInt ?? throw new ArgumentNullException(nameof(writeInt));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
    }

    public int ActiveMilliseconds => detector.PairCooldownMilliseconds;
    public bool IsConsistent =>
        readInt(PreferenceKey, BiofitBilateralJumpDetector.DefaultPairCooldownMilliseconds)
            == ActiveMilliseconds;

    public void Load()
    {
        int stored = readInt(PreferenceKey,
            BiofitBilateralJumpDetector.DefaultPairCooldownMilliseconds);
        if (!BiofitBilateralJumpDetector.IsSupportedPairCooldownMilliseconds(stored))
        {
            stored = BiofitBilateralJumpDetector.DefaultPairCooldownMilliseconds;
            writeInt(PreferenceKey, stored);
            save();
        }
        detector.SetPairCooldownMilliseconds(stored);
    }

    public bool Select(int milliseconds)
    {
        if (!BiofitBilateralJumpDetector.IsSupportedPairCooldownMilliseconds(milliseconds))
            return false;
        detector.SetPairCooldownMilliseconds(milliseconds);
        writeInt(PreferenceKey, milliseconds);
        save();
        return true;
    }
}
