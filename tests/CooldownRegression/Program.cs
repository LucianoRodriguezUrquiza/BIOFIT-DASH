using System;
using System.Collections.Generic;
using System.IO;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }

    private static bool Pair(BiofitBilateralJumpDetector detector, double seconds,
        float verticalG = .25f, double footGap = 0d)
    {
        Check(!detector.ProcessImpulse(1, seconds, verticalG),
            "One ankle never generates a bilateral command");
        return detector.ProcessImpulse(2, seconds + footGap, verticalG);
    }

    private static void TestAllBoundaries()
    {
        foreach (int ms in new[] { 900, 800, 700, 600, 500, 400 })
        {
            Check(BiofitBilateralJumpDetector.IsSupportedPairCooldownMilliseconds(ms),
                "Every listed option must be allowed");
            // For 400ms, -100ms falls inside the independent 320ms
            // same-ankle refractory, not the pair cooldown. Use -50ms
            // to isolate the bilateral timer without altering that filter.
            foreach (int relative in new[] { ms == 400 ? -50 : -100, -1, 0, 1, 100 })
            {
                var detector = new BiofitBilateralJumpDetector();
                detector.SetPairCooldownMilliseconds(ms);
                Check(detector.PairCooldownMilliseconds == ms,
                    "Selection reaches actual detector");
                Check(Pair(detector, 0d), "First pair is always accepted");
                // The pair's feet share a timestamp. 320-ms ankle
                // refractory has already elapsed for every case here.
                bool accepted = Pair(detector, (ms + relative) / 1000d);
                Check(accepted == (relative >= 0),
                    ms + "ms: boundary " + relative + " ms");
                Check(detector.PairCooldownRejected == (relative < 0 ? 1 : 0),
                    ms + "ms: cooldown rejection counter");
            }
            Console.WriteLine("PASS exact bounds " + ms + "ms");
        }
        var invalid = new BiofitBilateralJumpDetector();
        foreach (int value in new[] { -1, 0, 300, 650, 901, 1200 })
        {
            bool threw = false;
            try { invalid.SetPairCooldownMilliseconds(value); }
            catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw && invalid.PairCooldownMilliseconds == 900,
                "Unsupported value never silently changes detector");
        }
    }

    private static void TestChangesDuringActiveInterval()
    {
        var detector = new BiofitBilateralJumpDetector();
        Check(Pair(detector, 0d), "Initial 900ms pair");
        detector.SetPairCooldownMilliseconds(400);
        Check(!Pair(detector, .50d), "900->400 does not shorten active lock");
        Check(Pair(detector, .90d), "Previous lock completed at 900ms");
        Check(Pair(detector, 1.30d), "Next action uses 400ms");
        detector = new BiofitBilateralJumpDetector();
        detector.SetPairCooldownMilliseconds(400);
        Check(Pair(detector, 0d), "Initial 400ms pair");
        detector.SetPairCooldownMilliseconds(900);
        Check(!Pair(detector, .50d), "400->900 extends safety lock");
        Check(Pair(detector, .90d), "900ms accepted after raising value");
        detector.ClearPending();
        Check(!Pair(detector, 1.20d), "Scene change never bypasses active lock");
        Check(Pair(detector, 1.80d), "Scene changes preserve later eligible pairs");
    }

    private static void TestDuplicatesAndBadPackets()
    {
        var detector = new BiofitBilateralJumpDetector();
        detector.SetPairCooldownMilliseconds(400);
        Check(!detector.ProcessImpulse(1, 0d, .25f),
            "Only one foot remains pending");
        Check(!detector.ProcessImpulse(1, .10d, .25f),
            "Same-ankle rebound is rejected");
        Check(detector.RepeatedRejected == 1, "Refractory counter");
        Check(detector.ProcessImpulse(2, .015d, .25f), "Complete first pair");
        Check(!Pair(detector, .34d), "Post-launch rebound inside 400ms blocked");
        Check(!Pair(detector, .39d), "Landing oscillation inside 400ms blocked");
        Check(Pair(detector, .42d),
            "New independent pair after 400ms is accepted by temporal rule");
        Check(!detector.ProcessImpulse(3, 1d, .25f),
            "Invalid tracker never accepted");
        Check(!detector.ProcessImpulse(1, double.NaN, .25f),
            "Invalid timestamp never accepted");
        Check(!detector.ProcessImpulse(1, 2d, .05f),
            "Weak impulse never accepted");
        detector.ClearPending();
        Check(!detector.ProcessImpulse(1, 3d, .25f),
            "Single foot cannot activate even after cooldown");
        Check(!detector.ProcessImpulse(2, 3.20d, .25f),
            "Outside 180ms pairing window is rejected");
        // A second fully qualified pair at >400ms cannot be classified as
        // the same physical jump from timing alone; field trials must assess it.
    }

    private static void TestPersistenceAndCorruption()
    {
        var values = new Dictionary<string, int>();
        int saved = 0;
        Func<string, int, int> read = (key, fallback) =>
            values.TryGetValue(key, out int current) ? current : fallback;
        Action<string, int> write = (key, value) => values[key] = value;
        Action persist = () => saved++;
        var detector = new BiofitBilateralJumpDetector();
        var configuration = new BiofitCooldownConfiguration(detector, read, write, persist);
        configuration.Load();
        Check(configuration.ActiveMilliseconds == 900 && configuration.IsConsistent,
            "Fresh install defaults to 900ms");
        foreach (int ms in new[] { 900, 800, 700, 600, 500, 400 })
        {
            Check(configuration.Select(ms), "Persist listed selection");
            Check(configuration.IsConsistent && detector.PairCooldownMilliseconds == ms,
                "Saved and active detector values agree");
            var relaunchedDetector = new BiofitBilateralJumpDetector();
            var relaunched = new BiofitCooldownConfiguration(
                relaunchedDetector, read, write, persist);
            relaunched.Load();
            Check(relaunched.ActiveMilliseconds == ms && relaunched.IsConsistent,
                "App restart restores same detector cooldown");
        }
        Check(configuration.Select(900) && configuration.ActiveMilliseconds == 900,
            "RESTABLECER restores the validated default");
        int before = saved;
        Check(!configuration.Select(650) && saved == before &&
            configuration.ActiveMilliseconds == 900,
            "Invalid button cannot write a preference");
        values[BiofitCooldownConfiguration.PreferenceKey] = 321;
        var recovered = new BiofitBilateralJumpDetector();
        var recoveredSettings = new BiofitCooldownConfiguration(
            recovered, read, write, persist);
        recoveredSettings.Load();
        Check(recoveredSettings.ActiveMilliseconds == 900 &&
            recoveredSettings.IsConsistent && saved == before + 1,
            "Invalid persisted value resets to 900ms and saves");
        Console.WriteLine("PASS persistence, reload, invalid preference, restore");
    }

    private static void TestIntegrationSurface()
    {
        string detector = File.ReadAllText(
            "Assets/BIOFIT/Scripts/BiofitBilateralJumpDetector.cs");
        string monitor = File.ReadAllText("Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs");
        string ui = File.ReadAllText("Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs");
        string bridge = File.ReadAllText(
            "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs");
        Check(detector.Contains("PairCooldownMilliseconds / 1000d") &&
            detector.Contains("pairTime + 1e-9d < requiredUntil"),
            "Runtime pairing uses the selected value, not fixed 900ms");
        Check(monitor.Contains("cooldownConfiguration.Select(milliseconds)") &&
            monitor.Contains("cooldownConfiguration.Load()") &&
            monitor.Contains("COOLDOWN_MS="),
            "Receiver applies and records runtime selection");
        Check(ui.Contains("monitor.SetPairCooldownMilliseconds(milliseconds)") &&
            ui.Contains("monitor.PairCooldownMilliseconds") &&
            ui.Contains("Cooldow" + "nOptionsMs") &&
            ui.Contains("RefreshCooldownHud()"),
            "Button and HUD both use receiver's detector");
        Check(bridge.Contains("BilateralJumpCandidate += OnBilateralLegacyCandidate;") &&
            bridge.Contains("cube.onClick();") &&
            bridge.Contains("monitor.PhaseFastPairs = false;"),
            "LEGACY bridge and original Cube physics preserved");
    }

    public static void Main()
    {
        TestAllBoundaries();
        TestChangesDuringActiveInterval();
        TestDuplicatesAndBadPackets();
        TestPersistenceAndCorruption();
        TestIntegrationSurface();
        Console.WriteLine("PASS configurable cooldown regression: " + checks + " checks");
    }
}
