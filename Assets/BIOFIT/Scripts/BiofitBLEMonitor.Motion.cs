using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;

// Generic receiver health/recovery. No dependency on a game, movement threshold,
// calibration command or new BLE characteristic. All state decisions on main thread.
public sealed partial class BiofitBLEMonitor
{
    private sealed partial class Tracker
    {
        public IBleDevice device;
        public Action<BleConnectionState> connectionHandler;
        public bool subscriptionsReady, statusUsable;
        public long nativePackets, nativeEvents, nativeStatuses, nativeTransport;
        public long lastNativeTicks, lastNativeStatusTicks;
        public int statusProcessed, statusRejected, eventsProcessed, eventsAccepted,
            eventsRejected, oldGenerationPackets, recoveries;
        public byte statusFlags;
        public ushort imuHz, i2cErrors;
        public short verticalMg, gyroX10, activityMg;
        public double statusQueueMs, statusRelativeMs, linkEstablishedAt;
        public double lastMovOnAt = double.NaN, lastMovOffAt = double.NaN;
        public string lastStatusRejection = "no_status", lastEventRejection = "none";
    }
    public struct MotionTelemetry
    {
        public byte Id, Flags;
        public bool Connected, StatusUsable;
        public int Firmware, Generation, StatusProcessed, StatusRejected, EventsProcessed,
            EventsAccepted, EventsRejected, SequenceMissing, SequenceRejected, Recoveries;
        public long Native, NativeStatus, NativeEvents;
        public uint SensorMs;
        public ushort ImuHz, I2cErrors;
        public short VerticalMg, GyroX10, ActivityMg;
        public double LastPacketAge, NativeStatusAge, StatusAge, StatusHz, StatusGapMaxMs,
            StatusQueueMs, StatusRelativeMs, LastMovOnAt, LastMovOffAt;
        public string StatusRejection, EventRejection;
        private static string F(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "unknown" :
            v.ToString("F3", CultureInfo.InvariantCulture);
        public override string ToString() =>
            "foot=" + Id + " link=" + Connected + " flags=" + Flags + " fw=" + (Firmware == 0 ? "unknown" : Firmware.ToString()) +
            " rxAge=" + F(LastPacketAge) + " nativeStatusAge=" + F(NativeStatusAge) + " sampleAge=" + F(StatusAge) +
            " native=" + Native + " rxStatus=" + NativeStatus + " rxEvent=" + NativeEvents +
            " statusN=" + StatusProcessed + " statusBad=" + StatusRejected + " statusWhy=" + StatusRejection +
            " statusHz=" + F(StatusHz) + " gapMaxMs=" + F(StatusGapMaxMs) +
            " queueMs=" + F(StatusQueueMs) + " relativeMs=" + F(StatusRelativeMs) +
            " usable=" + StatusUsable + " sensorMs=" + SensorMs + " imuHz=" + ImuHz + " i2c=" + I2cErrors +
            " upMg=" + VerticalMg + " gyroX10=" + GyroX10 + " activityMg=" + ActivityMg +
            " eventN=" + EventsProcessed + " eventOK=" + EventsAccepted + " eventBad=" + EventsRejected + " eventWhy=" + EventRejection +
            " seqMissing=" + SequenceMissing + " seqBad=" + SequenceRejected +
            " movOnAt=" + F(LastMovOnAt) + " movOffAt=" + F(LastMovOffAt) +
            " generation=" + Generation + " recoveries=" + Recoveries;
    }
    public MotionTelemetry GetMotionTelemetry(byte id, double now)
    {
        Tracker t = id == 1 ? left : right;
        long packet = Interlocked.Read(ref t.lastNativeTicks), status = Interlocked.Read(ref t.lastNativeStatusTicks);
        return new MotionTelemetry {
            Id = id, Flags = t.statusFlags, Connected = t.connection == "CONECTADO - NOTIFICACIONES ACTIVAS",
            Firmware = t.firmwareVersion, Generation = t.generation, StatusUsable = t.statusUsable,
            Native = Interlocked.Read(ref t.nativePackets), NativeStatus = Interlocked.Read(ref t.nativeStatuses),
            NativeEvents = Interlocked.Read(ref t.nativeEvents),
            LastPacketAge = packet == 0 ? double.PositiveInfinity : now - packet / (double)System.Diagnostics.Stopwatch.Frequency,
            NativeStatusAge = status == 0 ? double.PositiveInfinity : now - status / (double)System.Diagnostics.Stopwatch.Frequency,
            StatusAge = t.hasStatus ? now - t.lastStatusCallback : double.PositiveInfinity,
            StatusProcessed = t.statusProcessed, StatusRejected = t.statusRejected, SensorMs = t.lastStatusMs,
            ImuHz = t.imuHz, I2cErrors = t.i2cErrors, VerticalMg = t.verticalMg, GyroX10 = t.gyroX10, ActivityMg = t.activityMg, StatusHz = t.statusHz, StatusGapMaxMs = t.maxStatusGapMs,
            StatusQueueMs = t.statusQueueMs, StatusRelativeMs = t.statusRelativeMs,
            StatusRejection = t.lastStatusRejection, EventRejection = t.lastEventRejection,
            EventsProcessed = t.eventsProcessed, EventsAccepted = t.eventsAccepted, EventsRejected = t.eventsRejected,
            SequenceMissing = t.gate.Missing + t.statusGate.Missing,
            SequenceRejected = t.gate.DuplicateOrReordered + t.statusGate.DuplicateOrReordered,
            LastMovOnAt = t.lastMovOnAt, LastMovOffAt = t.lastMovOffAt, Recoveries = t.recoveries
        };
    }
    public string MotionResetReason { get; private set; } = "startup";
    public int MotionResetCount { get; private set; }
    public string MotionConsumerSummary => "statusListeners=" + (MotionStatusReceived?.GetInvocationList().Length ?? 0) +
        " eventListeners=" + (MotionTransitionReceived?.GetInvocationList().Length ?? 0) +
        " resetListeners=" + (MotionEpochReset?.GetInvocationList().Length ?? 0);
    private void NotifyMotionReset(string reason)
    {
        MotionResetReason = reason; MotionResetCount++;
        RecordControlDiagnostic("MOTION_RESET reason=" + reason + " epoch=" + ControlEpoch + " n=" + MotionResetCount);
        MotionEpochReset?.Invoke();
    }
    private void ObserveNativePacket(Packet p)
    {
        if (p.reset || (p.expectedId != 1 && p.expectedId != 2)) return;
        Tracker t = p.expectedId == 1 ? left : right;
        if (p.generation != Volatile.Read(ref t.generation)) return;
        Interlocked.Increment(ref t.nativePackets);
        Interlocked.Exchange(ref t.lastNativeTicks, p.callbackTicks);
        if (p.isStatus)
        { Interlocked.Increment(ref t.nativeStatuses); Interlocked.Exchange(ref t.lastNativeStatusTicks, p.callbackTicks); }
        else if (p.isTransport) Interlocked.Increment(ref t.nativeTransport);
        else Interlocked.Increment(ref t.nativeEvents);
    }
    private void ObserveMotionStatusQuality(Tracker t, byte[] b, double callback)
    {
        t.statusFlags = b[2]; t.imuHz = U16(b, 16); t.i2cErrors = U16(b, 18);
        t.verticalMg = I16(b, 10); t.gyroX10 = I16(b, 12); t.activityMg = I16(b, 14);
        t.statusQueueMs = Math.Max(0, (MonotonicNow - callback) * 1000);
        bool mapped = timeAligner.TryMapEvent(t.id, U32(b, 6), callback, out double at);
        t.statusRelativeMs = mapped ? Math.Max(0, (callback - at) * 1000) : 0;
        t.statusUsable = t.statusQueueMs + t.statusRelativeMs <= BiofitEventGate.MaxAgeMs;
        t.lastStatusRejection = t.statusUsable ? "none" : "old_batch";
        if (!t.statusUsable) t.statusRejected++;
    }

    // Opt-in only for this game's persistent receiver; old diagnostic scenes
    // keep their existing connect workflow. A 550 ms input outage never extends
    // the virtual press. Reconnect starts only after 2 s without STATUS callbacks.
    public bool MotionLinkRecoveryEnabled { get; set; }
    public Task MotionRecoveryTask { get; private set; } = Task.CompletedTask;
    private readonly CancellationTokenSource motionLifetime = new CancellationTokenSource();
    private bool motionPermissionGranted, motionAppPaused, motionDestroyed;
    private double nextRecoveryAt;
    private int recoveryFailures, lastRecoveryFoot;
    private static double NativeStatusAge(Tracker t, double now)
    {
        long ticks = Interlocked.Read(ref t.lastNativeStatusTicks);
        return ticks == 0 ? now - t.linkEstablishedAt : now - ticks / (double)System.Diagnostics.Stopwatch.Frequency;
    }
    private static bool TrackerLinkFresh(Tracker t, double now) =>
        t.subscriptionsReady && NativeStatusAge(t, now) < 2.0;
    private void ObserveConnection(Tracker t, IBleDevice device, BleConnectionState state)
    {
        if (motionDestroyed || !ReferenceEquals(t.device, device)) return;
        if (state == BleConnectionState.Disconnected)
        {
            if (t.connection == state.ToString()) return; // some native stacks report twice
            t.subscriptionsReady = false;
            Interlocked.Increment(ref t.generation);
            Enqueue(new Packet { expectedId = t.id, reset = true });
        }
        // A redundant Connected callback must not replace the subscribed state
        // string and permanently make BothTrackersConnected false.
        if (state != BleConnectionState.Connected || !t.subscriptionsReady)
            t.connection = state.ToString();
    }
    private void TickMotionRecovery(double now)
    {
        if (!MotionLinkRecoveryEnabled || !motionPermissionGranted || motionAppPaused || motionDestroyed ||
            busy || !MotionRecoveryTask.IsCompleted || now < nextRecoveryAt) return;
        bool leftBad = !TrackerLinkFresh(left, now), rightBad = !TrackerLinkFresh(right, now);
        // A permanently offline left tracker must not starve recovery of right.
        Tracker bad = leftBad && rightBad ? (lastRecoveryFoot == 1 ? right : left) :
            leftBad ? left : rightBad ? right : null;
        if (bad == null) { recoveryFailures = 0; return; }
        lastRecoveryFoot = bad.id;
        nextRecoveryAt = now + Math.Min(15, 2 * Math.Pow(2, Math.Min(3, recoveryFailures)));
        MotionRecoveryTask = RecoverMotionLinkAsync(bad);
    }
    private async Task RecoverMotionLinkAsync(Tracker t)
    {
        busy = true;
        try
        {
            RecordControlDiagnostic("MOTION_LINK_RECOVERY foot=" + t.id + " state=" + t.connection);
            if (rawActive) await SetRawCaptureAsync(false); // never silently restart STREAM
            var adapter = BleManager.Adapter;
            if (!await adapter.IsAvailableAsync(motionLifetime.Token)) { recoveryFailures++; return; }
            t.recoveries++;
            await ConnectOneAsync(adapter, t.id == 1 ? leftAddress : rightAddress, t);
            if (!t.subscriptionsReady) recoveryFailures++;
        }
        catch (Exception ex)
        {
            recoveryFailures++;
            RecordControlDiagnostic("MOTION_LINK_RECOVERY_ERROR foot=" + t.id + " error=" + ex.GetType().Name);
        }
        finally
        {
            nextRecoveryAt = MonotonicNow + Math.Min(15, 2 * Math.Pow(2, Math.Min(3, recoveryFailures)));
            busy = false;
        }
    }
    private async Task ReleaseTrackerLinkAsync(Tracker t)
    {
        var device = t.device;
        if (device != null && t.connectionHandler != null) device.OnConnectionStateChanged -= t.connectionHandler;
        t.connectionHandler = null; t.subscriptionsReady = false; t.statusUsable = false;
        Interlocked.Increment(ref t.generation);
        Interlocked.Exchange(ref t.lastNativeStatusTicks, 0);
        if (!motionDestroyed) Enqueue(new Packet { expectedId = t.id, reset = true });
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            var ev = t.id == 1 ? leftEvents : rightEvents;
            var st = t.id == 1 ? leftStatus : rightStatus;
            var tx = t.id == 1 ? leftTransport : rightTransport;
            foreach (var characteristic in new[] { ev, st, tx })
                if (characteristic != null) try { await characteristic.UnsubscribeAsync(timeout.Token); } catch { }
            if (device != null) try { await device.DisconnectAsync(timeout.Token); } catch { }
        }
        t.device = null;
        if (t.id == 1) { leftDevice = null; leftEvents = leftStatus = leftTransport = null; leftRaw = null; }
        else { rightDevice = null; rightEvents = rightStatus = rightTransport = null; rightRaw = null; }
    }
    private void ShutdownMotionRecovery()
    {
        motionDestroyed = true; MotionLinkRecoveryEnabled = false; motionLifetime.Cancel();
        foreach (Tracker t in new[] { left, right })
            if (t.device != null && t.connectionHandler != null)
                t.device.OnConnectionStateChanged -= t.connectionHandler;
        // Let the existing archive/RAW finalizer finish its STREAM,0 best effort.
        _ = CloseMotionLinksAfterCaptureAsync();
    }
    private async Task CloseMotionLinksAfterCaptureAsync()
    {
        try { await CaptureCompletion; await RawOperation; } catch { }
        await ReleaseTrackerLinkAsync(left); await ReleaseTrackerLinkAsync(right);
    }
}
