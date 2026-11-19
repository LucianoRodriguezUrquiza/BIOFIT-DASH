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
    private const string RawUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0005";
    private const string ConfigUuid = "f9710000-6d68-4b7d-a742-6fae7b5f0004";
    private BiofitRawLink leftRaw, rightRaw;
    private BiofitSessionFile sessionFile;
    private BiofitRawCapture rawCapture;
    private readonly System.Threading.SemaphoreSlim rawTransition = new System.Threading.SemaphoreSlim(1,1);
    private readonly object rawReceiveGate = new object();
    private volatile bool rawActive;
    // Tracks any STREAM,1 attempt until both STREAM,0 ACKs are confirmed.
    // Prevents repeated spurious STREAM,0 calls on pause or after FINALIZAR.
    private bool rawMayBeStreaming;
    private int rawEpoch, rawSegment, syncNumber;
    private double nextRawDiagnostic, previousUpdate, updateGapMax, eventQueueSum, eventQueueMax;
    private int perfEvents;
    // Opt-in. Old fw2.2.7 or unqualified UP pairs keep the 900ms cooldown.
    public bool PhaseFastPairs { get; set; } // retained for offline v0910 replay, not selected by UI
    private readonly BiofitLiftPairDetector liftPairs = new BiofitLiftPairDetector();
    private bool liftCycleMode;
    public int ControlEpoch { get; private set; }
    public bool LiftCycleMode {
        get => liftCycleMode;
        set { if(liftCycleMode==value)return;liftCycleMode=value;InvalidateControl(); }
    }
    public bool CycleFirmwareReady => BothTrackersConnected && left.firmwareVersion==229 && right.firmwareVersion==229;
    public double LastCyclePairSeconds => liftPairs.LastPairSeconds;
    private int cyclePairsAtCaptureStart;
    public int CyclePairCount => liftPairs.Pairs-cyclePairsAtCaptureStart;
    private void InvalidateControl() {liftPairs.Clear();jumpDetector.ClearPending();ControlEpoch++;}

    public bool LastPairedWasFastPhase { get; private set; }
    private int phaseFastPairsAtCaptureStart;
    public int FastPhasePairCount =>
        jumpDetector.FastPhasePairsAccepted - phaseFastPairsAtCaptureStart;
    public bool RawActive => rawActive;
    public string RawCount => rawCapture==null?"0":rawCapture.LeftCount+"/"+rawCapture.RightCount;
    public string RawStatus { get; private set; } = "RAW OFF (20 Hz opcional)";
    public string SyncLabel { get; private set; } = "SYNC VIDEO";
    public Task CaptureCompletion { get; private set; } = Task.CompletedTask;
    public Task RawOperation { get; private set; } = Task.CompletedTask;
    public bool CaptureSaving => !CaptureCompletion.IsCompleted;

    public void MarkVideoSync()
    {
        if(!gameCaptureActive) return;
        SyncLabel="SYNC "+(++syncNumber).ToString("D3");
        eventCapture.MarkSync(SyncLabel,Time.realtimeSinceStartup);
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,0,SyncLabel+" callbackClockS="+
            MonotonicNow.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
    }
    // The RAW switch is meaningful only during an active capture. Show an
    // actionable reason instead of silently ignoring taps after ARCHIVO.
    // Do not label a partially acknowledged BLE start as fully enabled.
    public string RawButtonLabel => !RawOperation.IsCompleted ? "RAW: ESPERE" :
        rawActive ? "RAW: ON" : "RAW: OFF";
    public void ToggleRawCapture()
    {
        if(CaptureSaving)
        {
            RawStatus="RAW: esperar FINALIZANDO; no cambiar durante el cierre";
            return;
        }
        if(!RawOperation.IsCompleted)
        {
            RawStatus="RAW: esperar respuesta BLE; no volver a tocar";
            return;
        }
        if(!rawActive && !gameCaptureActive)
        {
            RawStatus="RAW OFF: primero pulse INICIAR; despues RAW";
            return;
        }
        if(!rawActive && (!BothTrackersConnected || leftRaw==null || rightRaw==null))
        {
            RawStatus="RAW OFF: conectar ambos tobillos en DIAGNOSTICO BLE";
            return;
        }
        RawOperation=SetRawCaptureAsync(!rawActive);
    }
    public async Task SetRawCaptureAsync(bool enabled)
    {
        await rawTransition.WaitAsync();
        try
        {
            if(!enabled && !rawMayBeStreaming)
            {
                lock(rawReceiveGate) { rawActive=false;rawEpoch++; }
                RawStatus="RAW OFF: ambos STREAM,0 ya confirmados o RAW nunca activado";
                return;
            }
            if(enabled && (!gameCaptureActive || !BothTrackersConnected || leftRaw==null || rightRaw==null))
            { RawStatus="RAW: iniciar captura y conectar ambos sensores";return; }
            lock(rawReceiveGate) { rawActive=false;rawEpoch++; }
            if(enabled)
            {
                if(sessionFile.Error.Length!=0)throw new System.IO.IOException("Archivo principal: "+sessionFile.Error);
                if(rawCapture==null)
                {
                    string path=System.IO.Path.Combine(Application.persistentDataPath,"RawCaptures",System.IO.Path.GetFileNameWithoutExtension(sessionFile.Path)+"_RAW.csv");
                    rawCapture=new BiofitRawCapture(new BiofitSessionFile(path,BiofitRawCapture.Header));
                }
                if(rawCapture.File.Error.Length!=0) throw new System.IO.IOException(rawCapture.File.Error);
                rawCapture.Segment="RAW_SEG_"+(++rawSegment);
                // A partially delivered STREAM,1 may start a remote stream even
                // when ACK is lost. Keep stop attempts enabled until confirmed.
                rawMayBeStreaming=true;
                int epoch=rawEpoch;
                lock(rawReceiveGate)rawActive=true;
                RawStatus="RAW activando ambos...";
                await leftRaw.SetAsync(true,b=>ReceiveRaw(1,b,epoch));
                await rightRaw.SetAsync(true,b=>ReceiveRaw(2,b,epoch));
                RawStatus="RAW ON 20 Hz (ver contador)";
            }
            else
            {
                string errors=await StopRawLinks();
                if(errors.Length==0)rawMayBeStreaming=false;
                RawStatus=errors.Length==0?"RAW OFF (ambos STREAM,0 confirmados)":"RAW OFF local; STREAM,0 no confirmado: "+errors;
            }
        }
        catch(Exception ex)
        {
            lock(rawReceiveGate) { rawActive=false;rawEpoch++; }
            string cleanup=await StopRawLinks();
            if(cleanup.Length==0)rawMayBeStreaming=false;
            RawStatus="ERROR RAW: "+ex.Message+" "+cleanup;
        }
        finally
        {
            eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,0,RawStatus+" segment="+rawSegment+" callbackS="+MonotonicNow.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" "+TransportSummary+" "+FirmwareSummary);
            rawTransition.Release();
        }
    }
    private async Task<string> StopRawLinks()
    {
        string errors="";
        foreach(var link in new[]{leftRaw,rightRaw})
        {
            if(link==null) {errors+="GATT RAW no disponible; ";continue;}
            try { await link.SetAsync(false,null); }
            catch(Exception ex){errors+=ex.Message+" ";}
        }
        return errors;
    }
    private void ReceiveRaw(byte id,byte[] bytes,int epoch)
    {
        double received=MonotonicNow;
        lock(rawReceiveGate)
            if(rawActive && epoch==rawEpoch)rawCapture.Record(id,bytes,received);
    }
    private async Task CompleteCaptureAsync()
    {
        await SetRawCaptureAsync(false);
        if(rawCapture!=null)await rawCapture.FinishAsync();
        // Include final RAW quality even though gameplay recording has ended.
        sessionFile.Add("DIAG;FINAL;"+Time.realtimeSinceStartup.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
            ";0;"+(rawCapture==null?RawStatus:RawStatus+" "+rawCapture.Summary).Replace(';',',').Replace('\n',' ').Replace('\r',' ')+";;;;;;;;;;");
        await sessionFile.Finish();
        if(sessionFile.Error.Length!=0) { CaptureFileStatus="ERROR archivo: "+sessionFile.Error+"; conservar .partial";return; }
        SavedCapturePath=sessionFile.Path;
        CaptureFileStatus="CSV guardado SHA256 OK";
        if(rawCapture!=null) CaptureFileStatus+=(rawCapture.File.Error.Length>0 || rawCapture.QueueDropped>0 ? "; ERROR RAW: " : " + RAW: ")+rawCapture.Summary;
    }
    private void OnApplicationPause(bool paused)
    {
        if(!paused)return;
        if(gameCaptureActive) { eventCapture.MarkSync("APP_PAUSA_FIN",Time.realtimeSinceStartup); StopGameCapture(); }
        else RawOperation=SetRawCaptureAsync(false);
    }
    private void OnDestroy()
    {
        if(gameCaptureActive)StopGameCapture(); else RawOperation=SetRawCaptureAsync(false);
    }

    // v0.9.6: archived sessions remain private and untouched until independently
    // verified copies have been made. A newer short session must not hide an
    // older long session from the user.
    public sealed class StoredSession
    {
        public string MainPath;
        public string RawPath;
        public string Label;
        public bool MainValid;
        public bool RawValid;
        public bool MainPartial;
        public bool RawPartial;
    }
    private StoredSession[] storedSessions = new StoredSession[0];
    private int selectedStoredSession = -1;
    private bool scanningSessions;
    private string archiveStatus = "Abrir SESIONES para buscar capturas anteriores";
    public StoredSession[] StoredSessions => storedSessions;
    public int SelectedStoredSession => selectedStoredSession;
    public string ArchiveStatus => archiveStatus;
    public bool ScanningSessions => scanningSessions;
    public bool ExportBusy { get; private set; }
    public string ExportStatus { get; private set; } = "Exportacion no iniciada";
    public string CaptureBanner
    {
        get
        {
            if (ExportBusy) return "EXPORTANDO CSV - " + ExportStatus;
            if (CaptureSaving) return "FINALIZANDO - esperando cierre de archivos";
            if (gameCaptureActive) return "GRABANDO - ETAPA " + CaptureStage;
            if (CaptureFileStatus.StartsWith("ERROR",StringComparison.Ordinal)) return "ERROR - " + CaptureFileStatus;
            if (!string.IsNullOrEmpty(SavedCapturePath)) return "ARCHIVO PRIVADO GUARDADO - exportar";
            return "SIN GRABACION - presionar 1. INICIAR";
        }
    }

    public void ScanSessionHistory()
    {
        if(scanningSessions || gameCaptureActive || CaptureSaving || ExportBusy)return;
        _ = ScanSessionHistoryAsync();
    }
    private async Task ScanSessionHistoryAsync()
    {
        scanningSessions=true;
        archiveStatus="Buscando y verificando archivos privados...";
        try
        {
            string root=Application.persistentDataPath;
            var found=await Task.Run(()=>
            {
                var list=new System.Collections.Generic.List<StoredSession>();
                if(!System.IO.Directory.Exists(root))return list;
                string[] paths=System.IO.Directory.GetFiles(root,"BIOFIT_v0*");
                System.Array.Sort(paths,(a,b)=>System.IO.File.GetLastWriteTimeUtc(b).CompareTo(System.IO.File.GetLastWriteTimeUtc(a)));
                foreach(string path in paths)
                {
                    if(list.Count>=80)break;
                    bool partial=path.EndsWith(".csv.partial",StringComparison.OrdinalIgnoreCase);
                    if(!partial && !path.EndsWith(".csv",StringComparison.OrdinalIgnoreCase))continue;
                    string expected=partial?path.Substring(0,path.Length-8):path;
                    string raw=System.IO.Path.Combine(root,"RawCaptures",System.IO.Path.GetFileNameWithoutExtension(expected)+"_RAW.csv");
                    bool valid=false,rawValid=false;
                    try { if(!partial)valid=BiofitCaptureArchive.VerifyFileStreaming(path); }
                    catch(System.Exception) {}
                    try { if(System.IO.File.Exists(raw))rawValid=BiofitCaptureArchive.VerifyFileStreaming(raw); }
                    catch(System.Exception) {}
                    bool rawPart=System.IO.File.Exists(raw+".partial");
                    long size=0;
                    try { size=new System.IO.FileInfo(path).Length; } catch(System.Exception) {}
                    string status=partial?"PARCIAL":(valid?"SHA OK":"ERROR SHA");
                    string rawStatus=rawValid?" + RAW OK":(System.IO.File.Exists(raw)?" + RAW SHA ERROR":(rawPart?" + RAW PARCIAL":""));
                    list.Add(new StoredSession {
                        MainPath=path,RawPath=raw,MainValid=valid,RawValid=rawValid,
                        MainPartial=partial,RawPartial=rawPart,
                        Label=System.IO.Path.GetFileName(path)+" | "+(size/1048576d).ToString("F1")+" MB | "+status+rawStatus
                    });
                }
                return list;
            });
            storedSessions=found.ToArray();
            selectedStoredSession=-1;
            archiveStatus=found.Count+" sesion(es) encontradas. Seleccionar archivo.";
        }
        catch(System.Exception ex){archiveStatus="ERROR buscando sesiones: "+ex.Message;}
        finally { scanningSessions=false; }
    }
    public void SelectStoredSession(int index)
    {
        if(index<0 || index>=storedSessions.Length)return;
        selectedStoredSession=index;
        var entry=storedSessions[index];
        archiveStatus=entry.Label+(entry.MainValid?" | LISTO PARA EXPORTAR":" | NO ES ARCHIVO COMPLETO");
    }

    // Primary = streamed Android MediaStore. Secondary = Android system document
    // picker (SAF). Both retain the original private source even on error.
    public void RequestExport(bool alternate)
    {
        if(gameCaptureActive || CaptureSaving || ExportBusy)
        {
            ExportStatus="Finalizar antes de exportar";
            return;
        }
        ExportBusy=true;
        ExportStatus=alternate?"Validando para selector Android...":"Validando para Descargas...";
        _=RequestExportAsync(alternate);
    }
    private async Task RequestExportAsync(bool alternate)
    {
        try
        {
            string selected=selectedStoredSession>=0 && selectedStoredSession<storedSessions.Length
                ?storedSessions[selectedStoredSession].MainPath:SavedCapturePath;
            string root=Application.persistentDataPath;
            if(string.IsNullOrEmpty(selected))
                selected=await Task.Run(()=>BiofitCaptureArchive.FindNewestValid(root));
            if(string.IsNullOrEmpty(selected))throw new System.IO.FileNotFoundException("No hay CSV privado valido; abrir SESIONES");
            string raw=System.IO.Path.Combine(root,"RawCaptures",System.IO.Path.GetFileNameWithoutExtension(selected)+"_RAW.csv");
            string[] paths=await Task.Run(()=>
            {
                if(!System.IO.File.Exists(selected) || !BiofitCaptureArchive.VerifyFileStreaming(selected))
                    throw new System.IO.InvalidDataException("CSV principal incompleto o SHA incorrecto");
                if(!System.IO.File.Exists(raw))return new[]{selected};
                if(!BiofitCaptureArchive.VerifyFileStreaming(raw))
                    return new[]{selected}; // Preserve invalid RAW; allow rescuing valid master.
                return new[]{selected,raw};
            });
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var bridge=new AndroidJavaClass("com.biofit.game.BiofitExportBridge"))
            {
                bridge.CallStatic(alternate?"startSaf":"startDownloads",activity,paths,gameObject.name);
            }
            ExportStatus=alternate?"Abrir selector del sistema, elegir destino para cada CSV":
                                   "Copiando a Descargas (por bloques)...";
#else
            foreach(string file in paths)
                if(!BiofitAndroidExport.Open(file))
                    throw new System.IO.IOException(BiofitAndroidExport.LastError);
            ExportStatus="EDITOR: "+paths.Length+" archivo(s) guardados y verificados";
            ExportBusy=false;
#endif
        }
        catch(System.Exception ex)
        {
            ExportStatus="ERROR exportacion: "+ex.Message+" (archivos privados conservados)";
            ExportBusy=false;
        }
    }
    public void OnBiofitExportResult(string messageFromAndroid)
    {
        string value=messageFromAndroid??"";
        ExportStatus=value.Replace('|',' ');
        if(value.StartsWith("DONE|",StringComparison.Ordinal))
        {
            ExportStatus="EXPORTADO Y VERIFICADO: "+value.Substring(5).Replace('|',' ');
            ExportBusy=false;
        }
        else if(value.StartsWith("ERROR|",StringComparison.Ordinal) ||
                value.StartsWith("CANCEL|",StringComparison.Ordinal))
        {
            ExportStatus=value.Replace('|',' ')+ " (privados conservados)";
            ExportBusy=false;
        }
    }

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
    public int DroppedEventCount => eventCapture.DroppedRows + (sessionFile==null?0:sessionFile.Rejected);
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
        if(gameCaptureActive || eventCapture.Recording || CaptureSaving || ExportBusy || !RawOperation.IsCompleted) return false;
        string name="BIOFIT_v0911_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv";
        sessionFile=new BiofitSessionFile(System.IO.Path.Combine(Application.persistentDataPath,name),eventCapture.Csv.Split('\n')[0].TrimEnd('\r'));
        eventCapture.RowSink=row=>sessionFile.Add(row);
        rawCapture=null;rawSegment=0;syncNumber=0;SyncLabel="SYNC VIDEO";
        RawStatus="RAW OFF: sesion iniciada, pulsar RAW para activar";
        if (!eventCapture.BeginGame(Time.realtimeSinceStartup)) return false;
        gameCaptureActive = true;
        gLeftOn = left.movOn; gRightOn = right.movOn;
        gLeftUp = left.upImpulses; gRightUp = right.upImpulses;
        gPairs = jumpDetector.BilateralCandidates;
        phaseFastPairsAtCaptureStart = jumpDetector.FastPhasePairsAccepted;
        cyclePairsAtCaptureStart = liftPairs.Pairs;
        gCompressed = TimeCompressedEvents;
        gAlignedEvents = alignedEvents;
        gNearMisses = jumpDetector.NearMisses;
        lastGameSummary = ""; SavedCapturePath = ""; selectedStoredSession=-1; ExportStatus="Exportacion no iniciada"; CaptureFileStatus = "Grabando";
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

    public void RecordControlDiagnostic(string detail)
    {
        eventCapture.RecordDiagnostic(Time.realtimeSinceStartup, 0, detail);
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
        if (gameCaptureActive || CaptureSaving)
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
        // Validate the private SHA256 archive first. ARCHIVO then saves it
        // DIRECTLY in Downloads; no picker, clipboard or Java export Activity.
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
        string mainStatus=BiofitAndroidExport.LastStatus;
        string rawPath=System.IO.Path.Combine(Application.persistentDataPath,"RawCaptures",System.IO.Path.GetFileNameWithoutExtension(SavedCapturePath)+"_RAW.csv");
        if(System.IO.File.Exists(rawPath))
        {
            if(!BiofitAndroidExport.Open(rawPath)) { CaptureFileStatus=mainStatus+"; ERROR exportando RAW: "+BiofitAndroidExport.LastError;return false; }
            CaptureFileStatus=mainStatus+" + RAW guardado";
        }
        else CaptureFileStatus=mainStatus+(System.IO.File.Exists(rawPath+".partial")?"; RAW INCOMPLETO: conservar datos privados":"");
        return true;
    }
    private bool SaveCaptureFile()
    {
        if(sessionFile!=null) { CaptureFileStatus="ERROR: revisar cierre de archivo; no exportar vista parcial";return false; }
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
            " | PARES FASE RAPIDOS=" + FastPhasePairCount +
            " | PARES CICLO=" + CyclePairCount +
            " | EVENTOS=" + eventCapture.EventRows +
            " | DESCARTADOS=" + DroppedEventCount +
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
        eventCapture.RowSink=null;
        sessionFile.Add("DIAG;FINAL;;0;"+lastGameSummary.Replace(';',',').Replace('\n',' ').Replace('\r',' ')+";;;;;;;;;;");
        CaptureFileStatus="Cerrando CSV y RAW...";
        CaptureCompletion=CompleteCaptureAsync();
        return true;
    }

    public bool CopyGameCapture()
    {
        if (gameCaptureActive || CaptureSaving) return false;
        if(sessionFile!=null)
        {
            if(string.IsNullOrEmpty(SavedCapturePath))
            {
                // Only a complete short preview may be a clipboard fallback.
                if(eventCapture.TotalRows>6000)return false;
                string fallback=BiofitCaptureArchive.Build(eventCapture.Csv,lastGameSummary);
                if(!BiofitCaptureArchive.Verify(fallback))return false;
                GUIUtility.systemCopyBuffer=fallback;return true;
            }
            GUIUtility.systemCopyBuffer=System.IO.File.ReadAllText(SavedCapturePath);
            return true;
        }
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
        public int firmwareVersion;
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
            try
            {
                var raw=await service.GetCharacteristicAsync(RawUuid);
                var config=await service.GetCharacteristicAsync(ConfigUuid);
                var link=new BiofitRawLink(raw,config);
                await link.SetAsync(false,null);
                if(id==1)leftRaw=link;else rightRaw=link;
            }
            catch(Exception ex) { RawStatus="RAW no disponible: "+ex.Message; }
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
        double updateAt=MonotonicNow;
        if(previousUpdate>0)updateGapMax=Math.Max(updateGapMax,(updateAt-previousUpdate)*1000);
        previousUpdate=updateAt;
        int processed = 0;
        while (processed++ < 100 && pending.TryDequeue(out Packet packet))
        {
            System.Threading.Interlocked.Decrement(ref pendingCount);
            Tracker tracker = packet.expectedId == 1 ? left : right;
            if (packet.reset)
            {
                if(rawActive) RawOperation=SetRawCaptureAsync(false);
                eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,packet.expectedId,"DESCONEXION: STREAM,0 best effort; sin enlace no se confirma apagado remoto");
                tracker.moving = false; tracker.hasLastTimestamp = false;
                tracker.hasStatus = false; tracker.gate.Reset(); tracker.statusGate.Reset();
                tracker.readiness.Reset(); Array.Clear(tracker.configPages,0,tracker.configPages.Length);
                tracker.statusWindowStart = 0; tracker.lastStatusCallback = 0;
                tracker.statusInWindow = 0; tracker.statusHz = 0;
                inputEngine.ProcessEvent(packet.expectedId, 2, Time.realtimeSinceStartup);
                timeAligner.Reset(packet.expectedId); InvalidateControl(); tracker.firmwareVersion=0;
                continue;
            }
            if (packet.generation != tracker.generation) continue;
            if (packet.isTransport) ReadTransport(packet);
            else if (packet.isStatus) ReadStatus(packet);
            else ReadEvent(packet);
        }
        if(gameCaptureActive && MonotonicNow>=nextRawDiagnostic)
        {
            nextRawDiagnostic=MonotonicNow+1;
            eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,0,"RAW_ACTIVE="+rawActive+" "+
                (rawCapture==null?"raw=0":rawCapture.Summary)+" eventDisk="+sessionFile.Error+" "+TransportSummary+
                " eventN="+perfEvents+" eventQueueMeanMs="+(perfEvents==0?0:eventQueueSum/perfEvents).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " eventQueueMaxMs="+eventQueueMax.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " updateGapMaxMs="+updateGapMax.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
            perfEvents=0;eventQueueSum=eventQueueMax=updateGapMax=0;
            if(sessionFile.Error.Length!=0)CaptureFileStatus="ERROR CAPTURA: "+sessionFile.Error;
            if(rawActive && (sessionFile.Error.Length!=0 || rawCapture.File.Error.Length!=0 || rawCapture.QueueDropped>0))
            {
                CaptureFileStatus="ERROR CAPTURA: "+sessionFile.Error+" RAW "+rawCapture.Summary;
                RawOperation=SetRawCaptureAsync(false);
            }
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
        if(gameCaptureActive && rawCapture!=null)
            eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,packet.expectedId,
                "RAW_CLOCK_ANCHOR sensorMs="+sensor+" callbackS="+callbackSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
    }

    private void ReadTransport(Packet packet)
    {
        byte[] b = packet.bytes;
        if (b.Length != 20 || b[1] != packet.expectedId) return;
        if (b[0] == 3) {
            eventCapture.RecordDiagnostic(Time.realtimeSinceStartup,packet.expectedId,
                "LIFT_STATE sensorMs="+U32(b,4)+" state="+b[2]+" reason="+b[3]+
                " velocityMmS="+I16(b,8)+" minVelocityMmS="+I16(b,10)+
                " onsets="+U16(b,12)+" rearms="+U16(b,14)+" faults="+U16(b,16)+" fwVersion="+U16(b,18));
            return;
        }
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
        if(page==3) tracker.firmwareVersion=U16(b,18);
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
            case 4: break; // LIFT_ONSET extension; still passes common sequence/age gate
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
        perfEvents++;eventQueueSum+=queueDelayMs;eventQueueMax=Math.Max(eventQueueMax,queueDelayMs);
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
            InvalidateControl();
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
        else if(eventType<=3) inputEngine.ProcessEvent(state.id, eventType,
            eventSeconds - (float)Math.Max(0d, callbackSeconds - pairSeconds));
        CurrentEventTrace = "seq=" + sequence + " sensorMs=" + millis + " callbackS=" +
            callbackSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture);
        dispatchStarted=MonotonicNow; currentCallbackSeconds=callbackSeconds;
        bool phaseEvidence = (b[3] & 0x10) != 0; // fw2.2.8 V2 EVENT flag bit4
        bool paired = fresh && timeAligner.IsReady(callbackSeconds) && eventType == 3 && !LiftCycleMode &&
            jumpDetector.ProcessImpulse(state.id, pairSeconds, verticalG,
                phaseEvidence, PhaseFastPairs);
        LastPairedWasFastPhase = paired && jumpDetector.LastPairUsedFastPhase;
        if (fresh && eventType == 3 && !LiftCycleMode)
            eventCapture.RecordDiagnostic(receivedSeconds, state.id,
                "BILATERAL_DECISION=" + (timeAligner.IsReady(callbackSeconds) ?
                    jumpDetector.LastDecision.ToString() : "ClockNotReady") + " " + CurrentEventTrace +
                " pairClockS=" + pairSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture) +
                " cooldownRejected=" + jumpDetector.PairCooldownRejected +
                " phaseHint=" + (phaseEvidence ? 1 : 0) +
                " phaseMode=" + (PhaseFastPairs ? 1 : 0) +
                " fastPhasePair=" + (LastPairedWasFastPhase ? 1 : 0) +
                (jumpDetector.LastDecision == BiofitBilateralJumpDetector.Decision.Paired ||
                 jumpDetector.LastDecision == BiofitBilateralJumpDetector.Decision.PairCooldown ?
                    " gapMs=" + jumpDetector.LastPairGapMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture) +
                    " intervalS=" + jumpDetector.LastPairIntervalSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture) : ""));
        if (fresh && eventType == 3)
            AnkleUpCandidate?.Invoke(state.id, verticalG,
                eventSeconds - (float)Math.Max(0d, callbackSeconds - pairSeconds));

        if(eventType==4) {
            bool eligible=fresh && timeAligner.IsReady(callbackSeconds) && LiftCycleMode && CycleFirmwareReady && (b[3]&0x20)!=0;
            paired=eligible && liftPairs.Observe(state.id,pairSeconds,verticalG);
            eventCapture.RecordDiagnostic(receivedSeconds,state.id,
                "LIFT_PAIR="+(eligible?liftPairs.LastDecision.ToString():"IgnoredOrUnavailable")+" "+CurrentEventTrace+
                " enabled="+LiftCycleMode+" fwReady="+CycleFirmwareReady+" fresh="+fresh+
                " pairClockS="+pairSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
        }
        if (paired)
        {
            if (LastPairedWasFastPhase)
                eventCapture.RecordDiagnostic(receivedSeconds,0,
                    "PHASE_FAST_ACCEPTED pairS=" +
                    pairSeconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture) +
                    " intervalS=" + jumpDetector.LastPairIntervalSeconds.ToString("F6",
                        System.Globalization.CultureInfo.InvariantCulture));
            Debug.Log("BIOFIT: POSIBLE SALTO BILATERAL (NO CONFIRMADO), intervalo " +
                      (eventType==4?liftPairs.LastGapMs:jumpDetector.LastPairGapMs).ToString("F0") + " ms");
            // ReadEvent se ejecuta desde Update(), en el hilo principal.
            BilateralJumpCandidate?.Invoke(eventType==4?(float)liftPairs.LastGapMs:jumpDetector.LastPairGapMs);
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
                           eventType == 2 ? "MOV_OFF" : eventType==4 ? "LIFT_ONSET" : "UP_IMPULSE";
        state.lastEvent = eventName + (eventType == 3 ? " fase=" +
            (phaseEvidence ? "REARME" : "SIN_PRUEBA") : "") +
            "   t=" + millis + " ms   seq=" + sequence +
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
                if(CaptureSaving) { message="Esperar cierre de archivos";return; }
                eventCapture.RowSink=null;
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
