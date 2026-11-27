using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// All state/control transitions run on the Unity main synchronization context.
// Native CONFIG callbacks only complete a TaskCompletionSource in BiofitBmdLink.
public sealed partial class BiofitBLEMonitor
{
    private BiofitBmdLink leftBmd,rightBmd;
    private readonly BiofitBmdShadow bmdShadow=new BiofitBmdShadow();
    private CancellationTokenSource bmdCancel;
    private bool bmdVerified,bmdUncertain;
    private double bmdResumeAfter;
    private int bmdMask=-1;
    public bool BmdBusy=>bmdCancel!=null;
    private static bool DiagnosticFirmware(int v)=>v==230||v==231;
    public bool BmdFirmwareReady=>BothTrackersConnected&&DiagnosticFirmware(left.firmwareVersion)&&left.firmwareVersion==right.firmwareVersion&&leftBmd!=null&&rightBmd!=null;
    public bool BmdFastTrace=>BmdFirmwareReady&&left.firmwareVersion==231;
    public int BmdTraceSeconds {get;private set;}=0; // 0 = compatible legacy ring
    public string BmdPlacement {get;private set;}="SIN_ETIQUETAR";
    private string bmdTraceOwner="";
    private int bmdQueryLeft=-1,bmdQueryRight=-1;
    public void CycleBmdDuration(){if(!BmdBusy&&!GameCaptureActive&&!BmdTraceArmed)BmdTraceSeconds=BmdTraceSeconds==0?5:BmdTraceSeconds==5?15:BmdTraceSeconds==15?30:BmdTraceSeconds==30?60:0;}
    public void CycleBmdPlacement(){if(!GameCaptureActive&&!BmdBusy)BmdPlacement=BmdPlacement=="SIN_ETIQUETAR"?"BANCO":BmdPlacement=="BANCO"?"MANOS":BmdPlacement=="MANOS"?"TOBILLOS":BmdPlacement=="TOBILLOS"?"MIXTO":"SIN_ETIQUETAR";}
    private void BmdAutoQuery()
    {
        if(!BmdFirmwareReady||bmdVerified||GameCaptureActive||BmdBusy||busy||!RawOperation.IsCompleted||rawActive||rawMayBeStreaming||CaptureSaving||ExportBusy)return;
        if(bmdQueryLeft==left.generation&&bmdQueryRight==right.generation)return;
        bmdQueryLeft=left.generation;bmdQueryRight=right.generation;
        RequestBmd("query");
    }
    public bool BmdControlBlocked=>BmdBusy||bmdUncertain||
        ((DiagnosticFirmware(left.firmwareVersion)||DiagnosticFirmware(right.firmwareVersion))&&(!BmdFirmwareReady||!bmdVerified));
    private bool BmdEventAllowed(double callback)=>!BmdControlBlocked&&callback>=bmdResumeAfter;
    public string BmdStatus {get;private set;}="BMD: consultar ambos trackers";
    public string BmdTracePath {get;private set;}="";
    public string BmdShadowSummary=>bmdShadow.Summary();
    public string BmdTrackerSummary=>"I FW "+left.firmwareVersion+" | D FW "+right.firmwareVersion+
        " | perfil confirmado: "+(bmdVerified?bmdMask.ToString():"--");
    private int gfaAttempts,gfaDeaths;
    private double gfaStarted,gfaAttemptAt,gfaDecisionAt=-10000,gfaOutcomeAt=-10000;
    private string gfaDecision="",gfaOutcome="";
    public string GfaSummary=>"GFA intentos="+gfaAttempts+" muertes="+gfaDeaths;
    private void GfaBegin(){gfaAttempts=gfaDeaths=0;gfaStarted=MonotonicNow;gfaAttemptAt=gfaStarted;gfaDecisionAt=gfaOutcomeAt=-10000;gfaDecision=gfaOutcome="";}
    public void GfaAttempt(string level)
    {
        if(!GameCaptureActive)return;
        ++gfaAttempts;gfaAttemptAt=MonotonicNow;gfaDecisionAt=gfaOutcomeAt=-10000;gfaDecision=gfaOutcome="";
        RecordControlDiagnostic("GFA_ATTEMPT attempt="+gfaAttempts+" level="+GfaSafe(level));
    }
    private static string GfaSafe(string s)=>(s??"").Replace(';',',').Replace('\r',' ').Replace('\n',' ').Replace(' ','_');
    private void GfaObserve(string decision){if(GameCaptureActive){gfaDecision=decision;gfaDecisionAt=MonotonicNow;}}
    public void GfaAction(string outcome,string reason)
    {
        if(!GameCaptureActive)return;
        gfaOutcome=outcome+"/"+reason;gfaOutcomeAt=MonotonicNow;
        RecordControlDiagnostic("GFA_ACTION outcome="+GfaSafe(outcome)+" reason="+GfaSafe(reason)+" "+CurrentEventTrace);
    }
    public void GfaDeath(string level,string mode,float x,float y,float progress,bool practice,float vy)
    {
        if(!GameCaptureActive)return;
        if(gfaAttempts==0)GfaAttempt(level); // recording begun during a live attempt
        ++gfaDeaths;
        double now=MonotonicNow,da=now-gfaDecisionAt,oa=now-gfaOutcomeAt;
        string decision=da<=1.5?gfaDecision:"NO_RECENT_DECISION",outcome=oa<=1.5?gfaOutcome:"NO_RECENT_ACTION";
        // Evidence label only: a death never proves an intended physical action.
        string suspect="UNDETERMINED";
        if(oa<=.65&&outcome.StartsWith("NO_",StringComparison.Ordinal))suspect="ACTION_NOT_EXECUTED";
        else if(da<=.65)switch(decision){
            case "BelowThreshold":suspect="BELOW_THRESHOLD";break;
            case "OutsideWindow":suspect="OUTSIDE_WINDOW";break;
            case "SameAnkleRefractory":suspect="REFRACTORY_REJECTED";break;
            case "PairCooldown":suspect="COOLDOWN_REJECTED";break;
            case "ClockNotReady":case "BMD_BLOCKED":suspect="BLE_OR_CONTROL_BLOCKED";break;
        }
        RecordControlDiagnostic(FormattableString.Invariant($"GAMEPLAY_FAILURE id={gfaDeaths} attempt={gfaAttempts} sessionS={now-gfaStarted:F6} attemptS={now-gfaAttemptAt:F6} monoS={now:F6} level={GfaSafe(level)} mode={GfaSafe(mode)} x={x:F3} y={y:F3} progressPct={progress:F2} vy={vy:F3} practice={practice} profile={BmdMask} cooldownMs={PairCooldownMilliseconds} evidence={suspect} decision={decision} decisionAgeMs={(da<=1.5?da*1000:-1):F1} outcome={GfaSafe(outcome)} outcomeAgeMs={(oa<=1.5?oa*1000:-1):F1} physicalIntent=UNKNOWN"));
    }
    public string BmdDetectorSummary=>"I UP="+left.upImpulses+" D UP="+right.upImpulses+
        " | pares="+TotalPairs+" | "+(BmdControlBlocked?"CONTROL BLE BLOQUEADO":"CONTROL BLE HABILITADO");
    public int BmdMask=>bmdVerified?bmdMask:-1;
    public bool BmdTraceArmed {get;private set;}
    public void CancelBmd(){bmdCancel?.Cancel();}
    private void BmdDisconnected(Tracker tracker)
    {
        if(tracker.id==1)leftBmd=null;else rightBmd=null;
        if(bmdVerified||bmdUncertain||DiagnosticFirmware(tracker.firmwareVersion)){bmdUncertain=true;bmdVerified=false;}
        BmdTraceArmed=false;bmdQueryLeft=bmdQueryRight=-1;ClearBmdDownloadCache();CancelBmd();
    }
    private void BmdResetBoundary()
    {
        InvalidateControl();
        bmdResumeAfter=MonotonicNow+.25;
    }
    private bool BmdCanOperate(string op)
    {
        if(BmdBusy||busy||!RawOperation.IsCompleted||rawActive||rawMayBeStreaming||CaptureSaving||ExportBusy)
        {BmdStatus="BMD: esperar BLE/RAW/exportación; RAW debe estar OFF";return false;}
        if(!BmdFirmwareReady){BmdStatus="BMD: conectar ambos FW230 o ambos FW231 y esperar versión";return false;}
        if((op=="profile"||op=="download")&&GameCaptureActive)
        {BmdStatus="BMD: FINALIZÁ el CSV antes de cambiar perfil o descargar";return false;}
        return true;
    }
    public void RequestBmd(string op,int mask=0)
    {
        bool autoExport=op=="downloadExport";
        if(autoExport)op="download";
        if(op!="query"&&op!="profile"&&op!="start"&&op!="freeze"&&op!="download")return;
        if(mask<0||mask>15||!BmdCanOperate(op))return;
        if((op=="start"||op=="freeze"||op=="download")&&!bmdVerified)
        {BmdStatus="BMD: consultá y confirmá perfiles primero";return;}
        bmdCancel=new CancellationTokenSource();
        _=RunBmd(op,mask,bmdCancel,autoExport);
    }
    private readonly Dictionary<string,List<string>> bmdDownloadCache=new Dictionary<string,List<string>>();
    private void ClearBmdDownloadCache()=>bmdDownloadCache.Clear();
    private async Task RunBmd(string op,int mask,CancellationTokenSource operation,bool autoExport)
    {
        bool acquired=false,downloaded=false;
        var l=leftBmd;var r=rightBmd;int lg=left.generation,rg=right.generation;
        var token=operation.Token;
        try
        {
            await rawTransition.WaitAsync(token);acquired=true;
            BmdResetBoundary();
            BmdStatus="BMD "+op+" en curso";
            if(op=="query"||op=="profile")
            {
                bmdVerified=false;bmdUncertain=true;
                BiofitBmdProtocol.Profile lp=default,rp=default;
                // Read both first. Unique token per tracker; never overwrite a pending request.
                await l.Batch(async x=>lp=await x.GetProfile(token),token);
                await r.Batch(async x=>rp=await x.GetProfile(token),token);
                if(op=="profile")
                {
                    if(lp.State==1||rp.State==1)throw new InvalidOperationException("Orden anterior pendiente; consultar nuevamente");
                    int lt=lp.Token%65535+1,rt=rp.Token%65535+1;
                    await l.Batch(x=>x.RequestProfile(mask,lt,token),token);
                    await r.Batch(x=>x.RequestProfile(mask,rt,token),token);
                    await l.Batch(async x=>lp=await x.WaitApplied(mask,lt,token),token);
                    await r.Batch(async x=>rp=await x.WaitApplied(mask,rt,token),token);
                    // Final readback after BOTH applied; not merely write ACK.
                    await l.Batch(async x=>lp=await x.GetProfile(token),token);
                    await r.Batch(async x=>rp=await x.GetProfile(token),token);
                    if(lp.Token!=lt||rp.Token!=rt||!lp.Applied(mask)||!rp.Applied(mask))
                        throw new InvalidOperationException("Readback incorrecto");
                }
                if(!lp.Applied(lp.Active)||!rp.Applied(lp.Active))throw new InvalidOperationException("Perfiles distintos o pendientes: I "+lp+" D "+rp);
                CheckBmdConnection(l,r,lg,rg,token);
                bmdMask=lp.Active;bmdVerified=true;bmdUncertain=false;
                BmdStatus="Ambos confirmados: perfil "+bmdMask;
                RecordControlDiagnostic("BMD_PROFILE mask="+bmdMask+" left="+lp+" right="+rp);
            }
            else if(op=="start"||op=="freeze")
            {
                bool fast=BmdFastTrace;
                if(op=="start"&&BmdTraceSeconds!=0&&!fast)throw new InvalidOperationException("Duración ampliada requiere FW231");
                string cmd=op=="start"?(BmdTraceSeconds==0?"D,T,1":"D,S,"+BmdTraceSeconds):"D,T,0";
                if(op=="start"){
                    if(!BmdTraceArmed){ClearBmdDownloadCache();BmdTracePath="";bmdTraceOwner=GameCaptureActive&&sessionFile!=null?sessionFile.Path:"";}
                    BmdTraceArmed=true; // partial delivery: freeze remains available
                }
                BiofitBmdProtocol.Trace a=default,b=default;
                await l.Batch(async x=>a=await x.TraceCommand(cmd,token,fast),token);
                await r.Batch(async x=>b=await x.TraceCommand(cmd,token,fast),token);
                CheckBmdConnection(l,r,lg,rg,token);
                if(op=="freeze"&&(a.State!=2||b.State!=2))throw new InvalidOperationException("No hay dos trazas congeladas");
                BmdTraceArmed=op=="start";
                BmdStatus="Trace I "+a+" | D "+b;
                RecordControlDiagnostic("BMD_TRACE "+op+" I "+a+" D "+b);
            }
            else
            {
                BmdTracePath=""; // never export an earlier trace as the result of this download
                bool fast=BmdFastTrace;
                var rows=new List<string>(fast?2*BiofitBmdProtocol.MaxTrace:2048);
                var watch=System.Diagnostics.Stopwatch.StartNew();
                int newRows=0,side=0;
                foreach(var link in new[]{l,r})
                {
                    byte foot=(byte)(++side);
                    await link.Batch(async x=>{
                        var t=await x.TraceCommand("D,T",token,fast);
                        if(t.State!=2||t.Count<1)throw new InvalidOperationException("Congelar ambas trazas primero");
                        string key=foot+":"+t.Generation+":"+t.Count;
                        if(!bmdDownloadCache.TryGetValue(key,out var cached)){cached=new List<string>(t.Count);bmdDownloadCache[key]=cached;}
                        while(cached.Count<t.Count)
                        {
                            token.ThrowIfCancellationRequested();
                            CheckBmdConnection(l,r,lg,rg,token);
                            int i=cached.Count;
                            var block=fast?await x.GetBlock(foot,t.Generation,i,Math.Min(BiofitBmdProtocol.MaxBlock,t.Count-i),token):
                                new[]{await x.GetRow(foot,t.Generation,i,token)};
                            CheckBmdConnection(l,r,lg,rg,token);
                            cached.AddRange(block);newRows+=block.Length;
                            double rate=newRows/Math.Max(.001,watch.Elapsed.TotalSeconds);
                            BmdStatus=FormattableString.Invariant($"Descarga {foot}/2: {cached.Count}/{t.Count} muestras | {rate:F1} m/s | resta pie ~{(t.Count-cached.Count)/rate:F0}s");
                        }
                        rows.AddRange(cached);
                    },token);
                }
                CheckBmdConnection(l,r,lg,rg,token);
                string stem=string.IsNullOrEmpty(bmdTraceOwner)?"BIOFIT_BMD_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"):Path.GetFileNameWithoutExtension(bmdTraceOwner)+"_TRACE";
                string path=Path.Combine(Application.persistentDataPath,"BMD",stem+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
                var file=new BiofitSessionFile(path,BiofitBmdProtocol.TraceHeader,Math.Max(8192,rows.Count+1));
                foreach(string row in rows)if(!file.Add(row)){await file.Finish();throw new IOException("Cola TRACE: "+file.Error);}
                await file.Finish();
                if(file.Error.Length!=0||!BiofitCaptureArchive.VerifyFileStreaming(path))throw new IOException("Trace no verificado: "+file.Error);
                BmdTracePath=path;downloaded=true;ClearBmdDownloadCache();BmdStatus="Trace guardado y SHA256 OK: "+rows.Count+" muestras";
            }
        }
        catch(Exception ex)
        {
            if(op=="query"||op=="profile"){bmdVerified=false;bmdUncertain=true;}
            BmdStatus="BMD "+(token.IsCancellationRequested?"cancelado":ex.Message)+
                ((bmdUncertain)?". Consultar/restaurar ambos a 0 antes de jugar.":"");
            RecordControlDiagnostic("BMD_ERROR "+op+" "+BmdStatus);
        }
        finally
        {
            if(acquired)rawTransition.Release();
            BmdResetBoundary();bmdCancel=null;operation.Dispose();
        }
        if(downloaded&&autoExport){
            if(!string.IsNullOrEmpty(bmdTraceOwner)&&bmdTraceOwner==SavedCapturePath)RequestExport(false);
            else ExportBmdTrace();
        }
    }
    private void CheckBmdConnection(BiofitBmdLink l,BiofitBmdLink r,int lg,int rg,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!BmdFirmwareReady||l!=leftBmd||r!=rightBmd||lg!=left.generation||rg!=right.generation)
            throw new InvalidOperationException("Conexión cambió durante la operación");
    }
    public void ExportBmdTrace()
    {
        if(!BmdBusy&&!GameCaptureActive&&!CaptureSaving&&string.IsNullOrEmpty(BmdTracePath)&&!string.IsNullOrEmpty(SavedCapturePath))
        {
            string folder=Path.Combine(Application.persistentDataPath,"BMD");
            if(Directory.Exists(folder))
                foreach(string candidate in Directory.GetFiles(folder,Path.GetFileNameWithoutExtension(SavedCapturePath)+"_TRACE_*.csv"))
                    if(BiofitCaptureArchive.VerifyFileStreaming(candidate))BmdTracePath=candidate;
        }
        if(BmdBusy||ExportBusy||GameCaptureActive||CaptureSaving||string.IsNullOrEmpty(BmdTracePath))
        {BmdStatus="BMD: descargar trace y finalizar registro primero";return;}
        ExportBusy=true;
        try
        {
            if(!BiofitCaptureArchive.VerifyFileStreaming(BmdTracePath))throw new IOException("SHA256 incorrecto");
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var bridge=new AndroidJavaClass("com.biofit.game.BiofitExportBridge"))
                bridge.CallStatic("startDownloads",activity,new[]{BmdTracePath},gameObject.name);
            ExportStatus="BMD: copiando trace a Descargas";
#else
            if(!BiofitAndroidExport.Open(BmdTracePath))throw new IOException(BiofitAndroidExport.LastError);
            ExportStatus="BMD: trace abierto";ExportBusy=false;
#endif
        }
        catch(Exception ex){ExportStatus="ERROR BMD export: "+ex.Message;ExportBusy=false;}
    }
}
