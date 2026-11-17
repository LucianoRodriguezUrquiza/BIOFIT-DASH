using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UniBLE;
using UnityEngine;

/// <summary>
/// BIOFIT GAME: primera prueba de dos trackers BLE en Android.
/// No modifica el firmware ni la calibracion de las Waveshare.
/// El evento UP_IMPULSE es un candidato; no equivale a un salto confirmado.
/// </summary>
public sealed class BiofitBLEMonitor : MonoBehaviour
{
    [Header("MAC de los trackers (verificadas con nRF Connect)")]
    [SerializeField] private string leftAddress = "80:45:6B:35:DE:61";
    [SerializeField] private string rightAddress = "28:84:85:55:6B:B9";

    private const string ServiceUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0001";
    private const string EventUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0002";
    // Firmware V2.2.6: STATUS nominal 100 ms; medir la tasa efectiva.
    private const string StatusUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0003";

    private const string TransportUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0007";
    private IBleCharacteristic leftTransport, rightTransport;
    public int RejectedEvents { get; private set; }
    public int AppQueueDropped => callbackDropped;
    public int AppQueuePending => pendingCount;
    private static double MonotonicNow => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
    public string ReadinessSummary => "I " + left.readiness.Describe(MonotonicNow) + " | D " + right.readiness.Describe(MonotonicNow);
    public string SavedCapturePath { get; private set; } = "";
    public string CaptureFileStatus { get; private set; } = "Sin archivo";
    public string CurrentEventTrace { get; private set; } = "";
    private double dispatchStarted, currentCallbackSeconds;
    public void RecordDecision(byte foot, string decision)
    {
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup, foot,
            "DECISION " + decision + " " + CurrentEventTrace + " dispatchMs=" +
            ((MonotonicNow-dispatchStarted)*1000).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
            " callbackToDecisionMs=" + ((MonotonicNow-currentCallbackSeconds)*1000).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
    }
    public string TransportSummary => "Rechazos: " + RejectedEvents + " | cola=" + pendingCount + " dropApp=" + callbackDropped +
        " | huecos seq: " + (left.gate.Missing + right.gate.Missing) +
        " | duplicados/desorden: " + (left.gate.DuplicateOrReordered + right.gate.DuplicateOrReordered);
    public string FirmwareSummary => "I " + left.transport + " / D " + right.transport;
    private IBleDevice leftDevice, rightDevice;
    // Mantener referencias de ambas suscripciones GATT durante toda la sesion.
    private IBleCharacteristic leftEvents, rightEvents;
    private IBleCharacteristic leftStatus, rightStatus;

    private int pendingCount, callbackDropped;
    private readonly ConcurrentQueue<Packet> pending = new ConcurrentQueue<Packet>();
    private readonly Tracker left = new Tracker("IZQUIERDO", 1);
    private readonly Tracker right = new Tracker("DERECHO", 2);
    private readonly BiofitInputEngine inputEngine = new BiofitInputEngine();
    public BiofitInputEngine InputEngine => inputEngine;
    private readonly BiofitBilateralJumpDetector jumpDetector = new BiofitBilateralJumpDetector();
    private readonly BiofitClockAligner timeAligner = new BiofitClockAligner();
    private bool previousUsedAlignedTime;
    private int alignedEvents;
    public BiofitBilateralJumpDetector JumpDetector => jumpDetector;

    /// <summary>
    /// Notificacion opcional para vistas de demostracion, en hilo principal.
    /// Representa un PAR DE IMPULSOS, no un salto humano confirmado.
    /// </summary>
    public event Action<float> BilateralJumpCandidate;

    /// <summary>
    /// Evento UP crudo para CONTROL DE DIAGNOSTICO UNILATERAL.
    /// NO implica elevacion ni salto humano confirmado.
    /// Los callbacks llegan al hilo principal desde ReadEvent().
    /// </summary>
    public event Action<byte, float, float> AnkleUpCandidate;

    // Se mantiene una unica instancia BLE mientras alternamos diagnostico y Dash.
    public bool BothTrackersConnected =>
        left.connection == "CONECTADO - NOTIFICACIONES ACTIVAS" &&
        right.connection == "CONECTADO - NOTIFICACIONES ACTIVAS";

    [SerializeField] private bool showDiagnostics = true;
    public bool ShowDiagnostics
    {
        get => showDiagnostics;
        set => showDiagnostics = value;
    }
    private readonly BiofitSessionLogger sessionLogger = new BiofitSessionLogger();
    private readonly BiofitEventCapture eventCapture = new BiofitEventCapture();
    private bool gameCaptureActive;
    private int gLeftOn, gRightOn, gLeftUp, gRightUp, gPairs;
    private int gCompressed, gAlignedEvents, gNearMisses;
    private string lastGameSummary = "";

    public bool GameCaptureActive => gameCaptureActive;
    public int CapturedEventCount => eventCapture.EventRows;
    public int DroppedEventCount => eventCapture.DroppedRows;
    public string CaptureStage => eventCapture.Stage;
    public float MinUpG => jumpDetector.MinimumVerticalGThreshold;
    public float PairWindowMs => jumpDetector.MaxPairGapWindowSeconds * 1000f;
    public int TotalPairs => jumpDetector.BilateralCandidates;
    public int TimeCompressedEvents => left.timeCompressed + right.timeCompressed;
    public int LeftTimeCompressed => left.timeCompressed;
    public int RightTimeCompressed => right.timeCompressed;
    public int ClockStatusLeft => timeAligner.LeftSamples;
    public int ClockStatusRight => timeAligner.RightSamples;
    public int RecentStatusLeft => timeAligner.LeftRecentSamples;
    public int RecentStatusRight => timeAligner.RightRecentSamples;
    public int AlignedEvents => alignedEvents;
    public int NearMisses => jumpDetector.NearMisses;
    public float LastNearMissGapMs => jumpDetector.LastNearMissGapMs;
    public bool ClockAlignmentReady
    {
        get
        {
            double now = System.Diagnostics.Stopwatch.GetTimestamp() /
                (double)System.Diagnostics.Stopwatch.Frequency;
            return timeAligner.IsReady(now);
        }
    }

    public void AdjustJumpThreshold(float deltaG)
    {
        jumpDetector.SetThresholds(jumpDetector.MinimumVerticalGThreshold + deltaG,
            jumpDetector.MaxPairGapWindowSeconds);
        eventCapture.RecordGameConfig(Time.realtimeSinceStartup,
            MinUpG, PairWindowMs);
    }

    public void AdjustPairWindow(float deltaSeconds)
    {
        jumpDetector.SetThresholds(jumpDetector.MinimumVerticalGThreshold,
            jumpDetector.MaxPairGapWindowSeconds + deltaSeconds);
        eventCapture.RecordGameConfig(Time.realtimeSinceStartup,
            MinUpG, PairWindowMs);
    }

    /// <summary>Una sola sesion para reposo, marcha, carrera, elevacion y juego.</summary>
    public bool StartGameCapture()
    {
        if (!eventCapture.BeginGame(Time.realtimeSinceStartup)) return false;
        gameCaptureActive = true;
        gLeftOn = left.movOn; gRightOn = right.movOn;
        gLeftUp = left.upImpulses; gRightUp = right.upImpulses;
        gPairs = jumpDetector.BilateralCandidates;
        gCompressed = TimeCompressedEvents;
        gAlignedEvents = alignedEvents;
        gNearMisses = jumpDetector.NearMisses;
        lastGameSummary = ""; SavedCapturePath = ""; CaptureFileStatus = "Grabando";
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,1,left.readiness.Detail(MonotonicNow));
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,2,right.readiness.Detail(MonotonicNow));
        foreach (Tracker tracker in new[] { left, right })
            foreach (string page in tracker.configPages)
                if (page != null) eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,tracker.id,page);
        eventCapture.RecordGameConfig(Time.realtimeSinceStartup,
            MinUpG, PairWindowMs);
        return true;
    }

    public bool MarkGameStage(string stage)
    {
        return gameCaptureActive &&
            eventCapture.MarkGameStage(stage, Time.realtimeSinceStartup);
    }

    public void RecordGameAction(string action)
    {
        eventCapture.RecordGameAction(action, Time.realtimeSinceStartup);
    }

    // The first Unity simulation frame with positive avatar altitude, NOT the
    // time of physical leg lift or the actual Android display photon.
    public void RecordFirstJumpFrame(byte foot, string source, double decisionToFrameMs)
    {
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup, foot,
            "PRIMER_ESTADO_SALTO origen=" + source +
            " decisionToFrameMs=" + decisionToFrameMs.ToString("F3",
                System.Globalization.CultureInfo.InvariantCulture));
    }

    public void RecordSingleFootConfig(float minVerticalG)
    {
        eventCapture.RecordSingleFootConfig(Time.realtimeSinceStartup,
            minVerticalG);
    }

    public void RecordSingleFootConfig(byte foot, float minVerticalG)
    {
        eventCapture.RecordSingleFootConfig(Time.realtimeSinceStartup,foot,minVerticalG);
    }

    public bool ExportGameCapture()
    {
        if (gameCaptureActive)
        {
            CaptureFileStatus = "Finalizar la grabacion antes de exportar";
            return false;
        }
        if (string.IsNullOrEmpty(SavedCapturePath))
        {
            if (!string.IsNullOrEmpty(lastGameSummary))
            {
                if (!SaveCaptureFile()) return false;
            }
            else
            {
                SavedCapturePath =
                    BiofitCaptureArchive.FindNewestValid(Application.persistentDataPath);
                if (string.IsNullOrEmpty(SavedCapturePath))
                {
                    CaptureFileStatus = "ERROR: no existe CSV privado valido; no borrar datos";
                    return false;
                }
            }
        }
        // Revalidate source before asking Android to duplicate it.
        try
        {
            if (!BiofitCaptureArchive.Verify(System.IO.File.ReadAllText(SavedCapturePath)))
            {
                CaptureFileStatus = "ERROR: CSV privado no integro";
                return false;
            }
        }
        catch (Exception ex) { CaptureFileStatus = "ERROR: CSV: " + ex.Message; return false; }
        if (!BiofitAndroidExport.Open(SavedCapturePath))
        {
            CaptureFileStatus = BiofitAndroidExport.LastError;
            return false;
        }
        CaptureFileStatus = "Selector solicitado; guardar y confirmar";
        return true;
    }
    private bool SaveCaptureFile()
    {
        try {
            SavedCapturePath = BiofitCaptureArchive.Save(Application.persistentDataPath,
                eventCapture.Csv,lastGameSummary);
            CaptureFileStatus = "CSV: " + eventCapture.TotalRows + " filas; descartadas=" + eventCapture.DroppedRows + "; SHA256 OK";
            return true;
        } catch (Exception ex) { CaptureFileStatus = "ERROR archivo: " + ex.Message; return false; }
    }

    public bool StopGameCapture()
    {
        if (!gameCaptureActive ||
            !eventCapture.FinishGame(Time.realtimeSinceStartup)) return false;
        gameCaptureActive = false;
        lastGameSummary = "BIOFIT DASH | RESUMEN EXPERIMENTAL\n" +
            "IZQ MOV_ON=" + (left.movOn - gLeftOn) +
            " UP=" + (left.upImpulses - gLeftUp) + "\n" +
            "DER MOV_ON=" + (right.movOn - gRightOn) +
            " UP=" + (right.upImpulses - gRightUp) + "\n" +
            "PARES UP=" + (jumpDetector.BilateralCandidates - gPairs) +
            " | EVENTOS=" + eventCapture.EventRows +
            " | DESCARTADOS=" + eventCapture.DroppedRows +
            "\nUMBRAL_UP_FINAL=" + MinUpG.ToString("F2") + " g" +
            " | VENTANA_FINAL=" + PairWindowMs.ToString("F0") + " ms" +
            "\nESTIMACION_RELOJ=" + (ClockAlignmentReady ? "ACTIVA" : "INACTIVA") +
            " | EVENTOS_ALINEADOS=" + (alignedEvents - gAlignedEvents) +
            "\nESTIMACION_STATUS: IZQ=" + ClockStatusLeft + " DER=" + ClockStatusRight +
            " | RECIENTES_32S=" + RecentStatusLeft + "/" + RecentStatusRight +
            " | PARES_CERCANOS_RECHAZADOS=" + (NearMisses - gNearMisses) +
            "\nTIEMPOS_COMPRIMIDOS=" + (TimeCompressedEvents - gCompressed) +
            " (acumulados desde inicio=" + TimeCompressedEvents + ")" +
            "\n" + TransportSummary + "\n" + FirmwareSummary +
            "\n" + ReadinessSummary + "\n" + left.detector + " / " + right.detector +
            "\nNO son pasos ni saltos humanos confirmados.\n";
        return SaveCaptureFile();
    }

    public bool CopyGameCapture()
    {
        if (gameCaptureActive) return false;
        if (!string.IsNullOrEmpty(lastGameSummary))
        {
            GUIUtility.systemCopyBuffer =
                BiofitCaptureArchive.Build(eventCapture.Csv, lastGameSummary);
            return true;
        }
        string path = BiofitCaptureArchive.FindNewestValid(Application.persistentDataPath);
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            string text = System.IO.File.ReadAllText(path);
            if (!BiofitCaptureArchive.Verify(text)) return false;
            GUIUtility.systemCopyBuffer = text;
            return true;
        }
        catch (System.IO.IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private string message = "Iniciando...";
    private bool busy;
    private GUIStyle heading, normal, small, buttonStyle;
    private Vector2 diagnosticsScroll;

    private sealed class Tracker
    {
        public readonly string name;
        public readonly byte id;
        public string connection = "Sin conectar";
        public string lastEvent = "Todavia no hay eventos";
        public bool moving;
        public int movOn, movOff, upImpulses;
        public uint lastSensorEventMs;
        public double lastCallbackEventSeconds;
        public bool hasLastTimestamp;
        public int timeCompressed;
        public readonly BiofitEventGate gate = new BiofitEventGate();
        public string transport = "sin telemetria TX";
        public bool hasStatus;
        public uint lastStatusMs;
        public int generation;
        public readonly BiofitEventGate statusGate = new BiofitEventGate();
        public double statusWindowStart, lastStatusCallback, statusHz, maxStatusGapMs;
        public int statusInWindow;
        public readonly BiofitTrackerReadiness readiness = new BiofitTrackerReadiness();
        public string detector = "Sin extension detector";
        public readonly string[] configPages = new string[4];
        public Tracker(string name, byte id) { this.name = name; this.id = id; }
    }

    private struct Packet
    {
        public byte expectedId;
        public byte[] bytes;
        public long callbackTicks;
        public bool isStatus;
        public bool isTransport;
        public bool reset;
        public int generation;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private async void Start()
    {
        await ConnectBothAsync();
    }
#else
    private void Start()
    {
        message = "Vista previa. La conexion BLE se prueba en una APK Android, no en Play de Windows.";
    }
#endif

    private async Task ConnectBothAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            // Android 12+ requiere BLUETOOTH_CONNECT antes de consultar
            // BluetoothAdapter.isEnabled() (UniBLE.IsAvailableAsync).
            // Si consultamos antes de pedir permiso puede devolver false
            // aunque el Bluetooth este encendido.
            message = "Solicitando permisos de Dispositivos cercanos...";
            IBleAdapter adapter = BleManager.Adapter;
            if (!await adapter.RequestPermissionAsync())
            {
                message = "Permiso Bluetooth denegado. En Android: Ajustes > Aplicaciones > BIOFIT-GAME-UNITY > Permisos > Dispositivos cercanos > Permitir. Luego cerra y abri la app.";
                return;
            }

            message = "Comprobando Bluetooth con permisos autorizados...";
            if (!await adapter.IsAvailableAsync())
            {
                message = "Bluetooth no disponible o apagado. Activalo desde Android y volve a abrir la app.";
                return;
            }

            // La conexion se realiza usando las direcciones conocidas.
            // No es necesario escanear ni emparejar desde Ajustes de Android.
            await ConnectOneAsync(adapter, leftAddress, left);
            await ConnectOneAsync(adapter, rightAddress, right);
            message = "Prueba BLE: mové un tobillo y observá los eventos.";
        }
        catch (Exception ex)
        {
            message = "Error al iniciar BLE: " + ex.Message;
            Debug.LogException(ex);
        }
        finally { busy = false; }
    }

    private async Task ConnectOneAsync(IBleAdapter adapter, string address, Tracker tracker)
    {
        try
        {
            tracker.connection = "Conectando " + address + "...";
            IBleDevice device = await adapter.GetDeviceAsync(address);
            if (tracker.id == 1) leftDevice = device;
            else rightDevice = device;

            device.OnConnectionStateChanged += state =>
            {
                tracker.connection = state.ToString();
                if (state == BleConnectionState.Disconnected)
                {
                    System.Threading.Interlocked.Increment(ref tracker.generation);
                    Enqueue(new Packet { expectedId = tracker.id, reset = true });
                }
            };

            await device.ConnectAsync();
            tracker.connection = "Buscando el servicio GATT...";
            IBleService service = await device.GetServiceAsync(ServiceUuid);
            IBleCharacteristic characteristic = await service.GetCharacteristicAsync(EventUuid);

            if (tracker.id == 1) leftEvents = characteristic;
            else rightEvents = characteristic;

            byte id = tracker.id;
            int generation = tracker.generation;
            await characteristic.SubscribeAsync(bytes =>
            {
                // El callback nativo puede llegar desde otro hilo.
                if (bytes != null)
                {
                    // Captura ANTES de encolar: diferencia tiempo en
                    // callback BLE vs procesamiento Unity en Update().
                    long ticksAtCallback = System.Diagnostics.Stopwatch.GetTimestamp();
                    Enqueue(new Packet
                    {
                        expectedId = id,
                        generation = generation,
                        bytes = (byte[])bytes.Clone(),
                        callbackTicks = ticksAtCallback
                    });
                }
            });

            // El firmware ya emite STATUS con millis del sensor.
            // Se usa para aproximar los relojes sin reflashear placas.
            // Si falla STATUS, se conservan EVENT en CSV pero el juego espera reloj.
            try
            {
                IBleCharacteristic status =
                    await service.GetCharacteristicAsync(StatusUuid);
                if (tracker.id == 1) leftStatus = status;
                else rightStatus = status;
                await status.SubscribeAsync(bytes =>
                {
                    if (bytes != null && bytes.Length >= 10)
                    {
                        long at = System.Diagnostics.Stopwatch.GetTimestamp();
                        Enqueue(new Packet
                        {
                            expectedId = id,
                            generation = generation,
                            bytes = (byte[])bytes.Clone(),
                            callbackTicks = at,
                            isStatus = true
                        });
                    }
                });
            }
            catch (Exception statusError)
            {
                Debug.LogWarning("BIOFIT STATUS opcional " + tracker.name +
                    ": " + statusError.Message);
            }

            // Optional extension; older V2 firmware remains readable.
            try
            {
                var transport = await service.GetCharacteristicAsync(TransportUuid);
                if (id == 1) leftTransport = transport; else rightTransport = transport;
                await transport.SubscribeAsync(bytes =>
                {
                    if (bytes != null) Enqueue(new Packet {
                        expectedId = id, generation = generation, isTransport = true,
                        bytes = (byte[])bytes.Clone(),
                        callbackTicks = System.Diagnostics.Stopwatch.GetTimestamp() });
                });
            }
            catch (Exception ex) { Debug.LogWarning("BIOFIT TX opcional: " + ex.Message); }
            tracker.connection = "CONECTADO - NOTIFICACIONES ACTIVAS";
        }
        catch (Exception ex)
        {
            tracker.connection = "ERROR: " + ex.Message;
            Debug.LogWarning("BIOFIT " + tracker.name + ": " + ex);
        }
    }

    private void Enqueue(Packet packet)
    {
        if (System.Threading.Interlocked.Increment(ref pendingCount) > 1024 && !packet.reset)
        {
            System.Threading.Interlocked.Decrement(ref pendingCount);
            System.Threading.Interlocked.Increment(ref callbackDropped);
            return;
        }
        pending.Enqueue(packet);
    }

    private void Update()
    {
        int processed = 0;
        while (processed++ < 100 && pending.TryDequeue(out Packet packet))
        {
            System.Threading.Interlocked.Decrement(ref pendingCount);
            Tracker tracker = packet.expectedId == 1 ? left : right;
            if (packet.reset)
            {
                tracker.moving = false; tracker.hasLastTimestamp = false;
                tracker.hasStatus = false; tracker.gate.Reset(); tracker.statusGate.Reset();
                tracker.readiness.Reset(); Array.Clear(tracker.configPages,0,tracker.configPages.Length);
                tracker.statusWindowStart = 0; tracker.lastStatusCallback = 0;
                tracker.statusInWindow = 0; tracker.statusHz = 0;
                inputEngine.ProcessEvent(packet.expectedId, 2, Time.realtimeSinceStartup);
                timeAligner.Reset(packet.expectedId); jumpDetector.ClearPending();
                continue;
            }
            if (packet.generation != tracker.generation) continue;
            if (packet.isTransport) ReadTransport(packet);
            else if (packet.isStatus) ReadStatus(packet);
            else ReadEvent(packet);
        }
        inputEngine.Update(Time.realtimeSinceStartup);
        sessionLogger.ObserveCadence(inputEngine.EstimatedCadence);
    }

    private void ReadStatus(Packet packet)
    {
        byte[] b = packet.bytes;
        if (b == null || b.Length != 20 ||
            b[0] != 2 || b[1] != packet.expectedId)
            return;
        double callbackSeconds = packet.callbackTicks /
            (double)System.Diagnostics.Stopwatch.Frequency;
        Tracker tracker = packet.expectedId == 1 ? left : right;
        uint sensor = U32(b, 6);
        if (tracker.hasStatus && unchecked((int)(sensor - tracker.lastStatusMs)) < 0)
        {
            tracker.gate.Reset(); tracker.statusGate.Reset(); tracker.hasLastTimestamp = false;
            jumpDetector.ClearPending();
        }
        tracker.statusGate.ObserveSequence(U16(b,4));
        if (tracker.lastStatusCallback > 0)
            tracker.maxStatusGapMs = Math.Max(tracker.maxStatusGapMs,
                (callbackSeconds - tracker.lastStatusCallback) * 1000d);
        tracker.lastStatusCallback = callbackSeconds;
        if (tracker.statusWindowStart == 0) tracker.statusWindowStart = callbackSeconds;
        tracker.statusInWindow++;
        double span = callbackSeconds - tracker.statusWindowStart;
        if (span >= 1d)
        {
            tracker.statusHz = tracker.statusInWindow / span;
            tracker.statusInWindow = 0; tracker.statusWindowStart = callbackSeconds;
        }
        tracker.lastStatusMs = sensor; tracker.hasStatus = true;
        if (tracker.readiness.Observe(b,packet.expectedId,callbackSeconds))
            eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,packet.expectedId,
                "STATUS_FLAGS=" + b[2] + " sensorMs=" + sensor + " " + tracker.readiness.Detail(callbackSeconds));
        timeAligner.ObserveStatus(packet.expectedId, sensor,
            callbackSeconds);
    }

    private void ReadTransport(Packet packet)
    {
        byte[] b = packet.bytes;
        if (b.Length != 20 || b[1] != packet.expectedId) return;
        if (b[0] == 2) { ReadDetectorPage(packet); return; }
        if (b[0] != 1) return;
        Tracker tracker = packet.expectedId == 1 ? left : right;
        tracker.transport = "qMax=" + U16(b,8) + " drop=" + U16(b,10) +
            " edadMax=" + U16(b,12) + " notifyMax=" + U16(b,14) +
            " cicloMax=" + U16(b,16) + " err=" + U16(b,18) +
            " rxHz=" + tracker.statusHz.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            " rxGapMax=" + tracker.maxStatusGapMs.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
            " statusGap=" + tracker.statusGate.Missing;
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup, packet.expectedId,
            "TX " + tracker.transport);
    }

    private void ReadDetectorPage(Packet packet)
    {
        byte[] b = packet.bytes; int page=b[2];
        if(page>3) return;
        Tracker tracker=packet.expectedId==1?left:right;
        string[] names = page==0 ? new[]{"restCount","disarmedMs","upMg","velocityMmS","biasX100","gravityMg"} :
            page==1 ? new[]{"upOnMg","upActMg","upVmmS","upGapMs","movOffMg","filterHzX100"} :
            page==2 ? new[]{"movOnDpsX10","movOffDpsX10","movOnMg","confirmMs","releaseMs","restWindowMs"} :
                      new[]{"rearms","blockMask","blockedSamples","calSamples","restRejected","fwVersion"};
        string detail="FW2 page="+page+" sensorMs="+U32(b,4)+" flags="+b[3];
        for(int i=0;i<6;i++) detail+=" "+names[i]+"="+((page==0&&(i==2||i==3))?I16(b,8+i*2):(int)U16(b,8+i*2));
        if(page==0) tracker.detector=detail; else tracker.configPages[page]=detail;
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,packet.expectedId,detail);
    }

    private void ReadEvent(Packet packet)
    {
        byte[] b = packet.bytes;
        if (b == null || b.Length != 16 || b[0] != 2 || b[1] != packet.expectedId)
            return;

        Tracker state = b[1] == 1 ? left : b[1] == 2 ? right : null;
        if (state == null) return;

        byte eventType = b[2];
        switch (eventType)
        {
            case 1: state.moving = true; state.movOn++; break;
            case 2: state.moving = false; state.movOff++; break;
            case 3: state.upImpulses++; break;
            default: return;
        }

        // Los relojes millis de las dos ESP32 NO son comparables.
        // Para el clasificador tomamos el instante del CALLBACK BLE,
        // corrigiendo solo el tiempo de espera en la cola de Unity.
        // Esto NO corrige demoras/batching previos dentro de Android BLE.
        float receivedSeconds = Time.realtimeSinceStartup;
        long ticksNow = System.Diagnostics.Stopwatch.GetTimestamp();
        float queueDelayMs = (float)((ticksNow - packet.callbackTicks) *
            1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (queueDelayMs < 0f) queueDelayMs = 0f;
        double callbackSeconds = packet.callbackTicks /
            (double)System.Diagnostics.Stopwatch.Frequency;
        float eventSeconds = receivedSeconds - queueDelayMs * 0.001f;


        ushort sequence = U16(b, 4);
        uint millis = U32(b, 6);

        // Evidencia de buffering/diferencia temporal, NO medida de
        // latencia absoluta: comparar intervalos del MISMO tracker.
        // El reloj millis se genera al detectar el evento en la ESP32.
        if (state.hasLastTimestamp)
        {
            double sensorInterval = unchecked(millis - state.lastSensorEventMs) * 0.001;
            double callbackInterval = callbackSeconds - state.lastCallbackEventSeconds;
            if (sensorInterval >= 0.15 && sensorInterval <= 4.0 &&
                callbackInterval >= 0.0 &&
                callbackInterval + 0.15 < sensorInterval)
                state.timeCompressed++;
        }
        state.lastSensorEventMs = millis;
        state.lastCallbackEventSeconds = callbackSeconds;
        state.hasLastTimestamp = true;

        float gyroDps = I16(b, 10) / 10f;
        float verticalG = I16(b, 12) / 1000f;
        // Emparejamiento por tiempo IMU si ambos STATUS tienen anclas
        // recientes. Fallback al tiempo del callback cuando no las hay.
        // Nunca mezclar relojes distintos dentro de un mismo par.
        double pairSeconds = callbackSeconds;
        bool timeAligned = timeAligner.TryMapEvent(state.id, millis,
            callbackSeconds, out double mappedEventSeconds);
        if (timeAligned)
        {
            pairSeconds = mappedEventSeconds;
            alignedEvents++;
        }
        if (timeAligned != previousUsedAlignedTime)
        {
            jumpDetector.ClearPending();
            previousUsedAlignedTime = timeAligned;
        }
        bool sequenceValid = state.gate.ObserveSequence(sequence);
        int firmwareQueueMs = (b[3] & 0x80) != 0 ? U16(b,14) : 0;
        string rejection = BiofitEventGate.Rejection(sequenceValid, timeAligned,
            callbackSeconds, pairSeconds, queueDelayMs, firmwareQueueMs);
        bool fresh = rejection.Length == 0;
        if (!fresh)
        {
            RejectedEvents++;
            eventCapture.RecordDiagnostic(receivedSeconds, state.id,
                "RECHAZO_" + rejection + " seq=" + sequence + " fwQueueMs=" + firmwareQueueMs);
        }
        else inputEngine.ProcessEvent(state.id, eventType,
            eventSeconds - (float)Math.Max(0d, callbackSeconds - pairSeconds));
        CurrentEventTrace = "seq=" + sequence + " sensorMs=" + millis + " callbackS=" +
            callbackSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture);
        dispatchStarted=MonotonicNow; currentCallbackSeconds=callbackSeconds;
        bool paired = fresh && timeAligner.IsReady(callbackSeconds) && eventType == 3 &&
            jumpDetector.ProcessImpulse(state.id, pairSeconds, verticalG);
        if (fresh && eventType == 3)
            AnkleUpCandidate?.Invoke(state.id, verticalG,
                eventSeconds - (float)Math.Max(0d, callbackSeconds - pairSeconds));

        if (paired)
        {
            Debug.Log("BIOFIT: POSIBLE SALTO BILATERAL (NO CONFIRMADO), intervalo " +
                      jumpDetector.LastPairGapMs.ToString("F0") + " ms");
            // ReadEvent se ejecuta desde Update(), en el hilo principal.
            BilateralJumpCandidate?.Invoke(jumpDetector.LastPairGapMs);
        }
        // La demora es SOLO el exceso respecto del mejor anclaje STATUS;
        // no se interpreta como latencia BLE absoluta.
        double relativeDelayMs = timeAligned ?
            Math.Max(0d, (callbackSeconds - pairSeconds) * 1000d) : 0d;
        eventCapture.Record(receivedSeconds, state.id, eventType,
            sequence, millis, gyroDps, verticalG, paired,
            queueDelayMs, callbackSeconds, pairSeconds,
            timeAligned, relativeDelayMs);

        string eventName = eventType == 1 ? "MOV_ON" :
                           eventType == 2 ? "MOV_OFF" : "UP_IMPULSE";
        state.lastEvent = eventName + "   t=" + millis + " ms   seq=" + sequence +
                          "   giro=" + gyroDps.ToString("F1") + " deg/s" +
                          "   Z=" + verticalG.ToString("+0.000;-0.000;0.000") + " g";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("BIOFIT " + state.name + " " + state.lastEvent);
#endif
    }

    private static ushort U16(byte[] b, int i)
    {
        return (ushort)(b[i] | (b[i + 1] << 8));
    }

    private static short I16(byte[] b, int i)
    {
        return unchecked((short)U16(b, i));
    }

    private static uint U32(byte[] b, int i)
    {
        return (uint)(b[i] | (b[i + 1] << 8) |
                      (b[i + 2] << 16) | (b[i + 3] << 24));
    }

    private void OnGUI()
    {
        if (!showDiagnostics) return;
        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = Mathf.Max(0.65f, Screen.width / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (heading == null)
        {
            heading = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, wordWrap = true };
            normal = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            heading.normal.textColor = Color.white;
            normal.normal.textColor = Color.white;
            small.normal.textColor = new Color(0.82f, 0.88f, 1f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, wordWrap = true };
        }

        GUI.Box(new Rect(12, 12, width - 24, height - 24), "");
        // Permite acceder a FIN CAMINAR/CORRER/SALTOS y COPIAR CSV
        // incluso en pantallas Android mas bajas.
        diagnosticsScroll = GUI.BeginScrollView(
            new Rect(18f, 18f, width - 36f, height - 36f),
            diagnosticsScroll, new Rect(0f, 0f, width - 80f, 2700f),
            false, true);
        float x = 20f;
        float y = 16f;
        float textWidth = width - 118f;

        GUI.Label(new Rect(x, y, textWidth, 64), "BIOFIT GAME | DIAGNOSTICO BLE", heading);
        y += 76;
        if (GUI.Button(new Rect(x, y, textWidth, 65),
            "JUGAR BIOFIT DASH (PROTOTIPO)", buttonStyle))
        {
            // No cambiamos de escena: el mismo monitor sigue suscrito a ambas
            // placas y mantiene contadores, estado y CSV mientras jugamos.
            if (GetComponent<BiofitDashGame>() == null)
                gameObject.AddComponent<BiofitDashGame>();
            showDiagnostics = false;
        }
        y += 80;
        GUI.Label(new Rect(x, y, textWidth, 86), message, small);
        y += 100;

        DrawTracker(left, x, ref y, textWidth);
        y += 22;
        DrawTracker(right, x, ref y, textWidth);
        y += 25;
        GUI.Label(new Rect(x, y, textWidth, 95), "UP_IMPULSE = posible elevacion. Todavia no significa JUMP.", small);
        y += 111;
        GUI.Label(new Rect(x, y, textWidth, 70),
            "PARES UP: " + jumpDetector.BilateralCandidates +
            " | Ultimo intervalo: " + jumpDetector.LastPairGapMs.ToString("F0") + " ms", normal);
        y += 65;
        GUI.Label(new Rect(x, y, textWidth, 77),
            jumpDetector.LastStatus + " | filtro preliminar, NO salto real", small);
        y += 83;
        GUI.Label(new Rect(x, y, textWidth, 86),
            "Reloj estimado: " + (ClockAlignmentReady ? "ACTIVO" : "ESPERANDO STATUS") +
            " | STATUS total: " + ClockStatusLeft + "/" + ClockStatusRight +
            " | 32s: " + RecentStatusLeft + "/" + RecentStatusRight +
            " | Casi pares: " + NearMisses, small);
        y += 91;
        GUI.Label(new Rect(x, y, textWidth, 62), "BIOFIT INPUT ENGINE (EXPERIMENTAL)", heading);
        y += 70;
        GUI.Label(new Rect(x, y, textWidth, 60),
            "Actividad: " + inputEngine.Activity, normal);
        y += 63;
        GUI.Label(new Rect(x, y, textWidth, 60),
            "Candidatos MOV_ON: Izq " + inputEngine.LeftCandidates +
            "  |  Der " + inputEngine.RightCandidates, normal);
        y += 63;
        GUI.Label(new Rect(x, y, textWidth, 60),
            "Alternancias: " + inputEngine.Alternations +
            "   |   Cadencia estimada: " + inputEngine.EstimatedCadence.ToString("F0") + " /min", normal);
        y += 70;
        GUI.Label(new Rect(x, y, textWidth, 110),
            "Aviso: los candidatos NO son pasos confirmados. Clasificacion preliminar; falta validar con pruebas.", small);
        y += 111;
        DrawSessionControls(x, y, textWidth);
        GUI.EndScrollView();
        GUI.matrix = previousMatrix;
    }

    private void DrawSessionControls(float x, float y, float width)
    {
        GUI.Label(new Rect(x, y, width, 56), "REGISTRO DE PRUEBA COMBINADA", heading);
        y += 58;
        float buttonWidth = (width - 12f) * 0.5f;
        if (GUI.Button(new Rect(x, y, buttonWidth, 63), "1. INICIAR", buttonStyle))
        {
            if (gameCaptureActive)
            {
                message = "Ya hay un registro de BIOFIT DASH en curso. " +
                          "Volvé al juego y presioná FINALIZAR para conservar los datos.";
            }
            else
            {
                sessionLogger.Begin(left.movOn, left.movOff, left.upImpulses,
                    right.movOn, right.movOff, right.upImpulses, jumpDetector.BilateralCandidates);
                eventCapture.Begin(Time.realtimeSinceStartup);
                lastGameSummary = "";
                message = "Sesion iniciada: realiza la actividad y registra el final de cada etapa.";
            }
        }
        if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 63),
            "2. FIN CAMINAR", buttonStyle))
        {
            if (eventCapture.FinishStage("CAMINAR", Time.realtimeSinceStartup))
            {
                sessionLogger.MarkStage("CAMINAR", left.movOn, left.movOff,
                    left.upImpulses, right.movOn, right.movOff, right.upImpulses,
                    jumpDetector.BilateralCandidates);
                message = "CAMINAR guardado. Siguiente etapa: " + eventCapture.Stage;
            }
            else
            {
                message = "Orden incorrecto. Etapa actual: " + eventCapture.Stage +
                          ". Segui INICIAR > FIN CAMINAR > FIN CORRER > FIN SALTOS.";
            }
        }
        y += 72;
        if (GUI.Button(new Rect(x, y, buttonWidth, 63), "3. FIN CORRER", buttonStyle))
        {
            if (eventCapture.FinishStage("CORRER", Time.realtimeSinceStartup))
            {
                sessionLogger.MarkStage("CORRER", left.movOn, left.movOff,
                    left.upImpulses, right.movOn, right.movOff, right.upImpulses,
                    jumpDetector.BilateralCandidates);
                message = "CORRER guardado. Siguiente etapa: " + eventCapture.Stage;
            }
            else
            {
                message = "Orden incorrecto. Etapa actual: " + eventCapture.Stage +
                          ". Segui INICIAR > FIN CAMINAR > FIN CORRER > FIN SALTOS.";
            }
        }
        if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 63),
            "4. FIN SALTOS", buttonStyle))
        {
            if (eventCapture.FinishStage("SALTOS", Time.realtimeSinceStartup))
            {
                sessionLogger.MarkStage("SALTOS", left.movOn, left.movOff,
                    left.upImpulses, right.movOn, right.movOff, right.upImpulses,
                    jumpDetector.BilateralCandidates);
                message = "SALTOS guardado. Siguiente etapa: " + eventCapture.Stage;
            }
            else
            {
                message = "Orden incorrecto. Etapa actual: " + eventCapture.Stage +
                          ". Segui INICIAR > FIN CAMINAR > FIN CORRER > FIN SALTOS.";
            }
        }
        y += 72;
        if (GUI.Button(new Rect(x, y, buttonWidth, 63),
            "COPIAR RESUMEN", buttonStyle))
        {
            GUIUtility.systemCopyBuffer = sessionLogger.Report;
            message = "Resumen copiado al portapapeles del celular.";
        }
        if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 63),
            "COPIAR CSV", buttonStyle))
        {
            GUIUtility.systemCopyBuffer = eventCapture.Csv;
            message = "CSV BLE copiado. Pegalo en un archivo de texto o en el chat.";
        }
        y += 76;
        GUI.Label(new Rect(x, y, width, 52),
            "Etapa: " + eventCapture.Stage + " | Registro: " +
            eventCapture.EventRows + " eventos | descartados: " +
            eventCapture.DroppedRows + " | maximo 6000 filas", small);
        y += 58;
        GUI.Label(new Rect(x, y, width, 120),
            sessionLogger.Started ? sessionLogger.LastMark :
            "Registro combinado: INICIAR -> FIN CAMINAR -> FIN CORRER -> FIN SALTOS -> COPIAR.",
            small);
    }

    private void DrawTracker(Tracker tracker, float x, ref float y, float width)
    {
        GUI.Label(new Rect(x, y, width, 57), tracker.name, heading); y += 61;
        GUI.Label(new Rect(x, y, width, 68), tracker.connection, small); y += 74;
        GUI.Label(new Rect(x, y, width, 48),
            "ESTADO: " + (tracker.moving ? "MOVIMIENTO" : "QUIETO"), normal); y += 53;
        GUI.Label(new Rect(x, y, width, 53),
            "MOV_ON: " + tracker.movOn + "    MOV_OFF: " + tracker.movOff +
            "    UP: " + tracker.upImpulses, normal); y += 59;
        GUI.Label(new Rect(x, y, width, 104), tracker.lastEvent, small); y += 104;
        GUI.Label(new Rect(x, y, width, 95), tracker.transport + " (maximos desde arranque, ms)\n" + tracker.readiness.Detail(MonotonicNow), small); y += 140;
    }
}
