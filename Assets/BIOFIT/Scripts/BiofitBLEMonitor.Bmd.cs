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
    public bool BmdAutoEnabled {get;private set;}
    public float BmdProgress {get;private set;}
    public string BmdAutoStatus {get;private set;}="GFA auto OFF";
    private bool autoAcceptMarkers,autoSaved=true;
    private int autoToken,autoSavedToken;
    private string autoOwner="";
    private uint autoBootL,autoBootR,autoSavedBootL,autoSavedBootR;
    private bool autoArmedL,autoArmedR;
    private int autoGenL,autoGenR;
    private Task autoOperation=Task.CompletedTask;
    public bool BmdAutoSession=>autoAcceptMarkers||!autoOperation.IsCompleted;
    public void ToggleBmdAuto(){if(!BmdBusy&&!GameCaptureActive&&!CaptureSaving&&!BmdAutoSession&&!BmdTraceArmed){BmdAutoEnabled=!BmdAutoEnabled;BmdAutoStatus=BmdAutoEnabled?"GFA auto listo (FW231)":"GFA auto OFF";}}
    private string bmdTraceOwner="";
    private int bmdQueryLeft=-1,bmdQueryRight=-1;
    public void CycleBmdDuration(){if(!BmdBusy&&!GameCaptureActive&&!BmdTraceArmed)BmdTraceSeconds=BmdTraceSeconds==0?5:BmdTraceSeconds==5?15:BmdTraceSeconds==15?30:BmdTraceSeconds==30?60:0;}
    public void CycleBmdPlacement(){if(!GameCaptureActive&&!BmdBusy)BmdPlacement=BmdPlacement=="SIN_ETIQUETAR"?"BANCO":BmdPlacement=="BANCO"?"MANOS":BmdPlacement=="MANOS"?"TOBILLOS":BmdPlacement=="TOBILLOS"?"MIXTO":"SIN_ETIQUETAR";}
    private void BmdAutoQuery()
    {
        if(!BmdFirmwareReady||bmdVerified||BmdBusy||busy||!RawOperation.IsCompleted||!autoOperation.IsCompleted||rawActive||rawMayBeStreaming||CaptureSaving||ExportBusy)return;
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
        if(gfaAttempts==0){gfaAttempts=1;gfaAttemptAt=gfaStarted;RecordControlDiagnostic("GFA_ATTEMPT attempt=1 partial=True level="+GfaSafe(level));}
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
        QueueAutoMarker(gfaDeaths,now);
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
        BmdTraceArmed=false;bmdQueryLeft=bmdQueryRight=-1;
        if(tracker.id==1)autoArmedL=false;else autoArmedR=false;
        if(tracker.firmwareVersion!=231)ClearBmdDownloadCache(); // FW231 cache includes boot nonce
        CancelBmd();
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
        if(BmdAutoSession&&op!="query"){BmdStatus="GFA: finalizar sesión automática antes de operar";return false;}
        if((op=="profile"||op=="download"||op=="autoDownload")&&GameCaptureActive)
        {BmdStatus="BMD: FINALIZÁ el CSV antes de cambiar perfil o descargar";return false;}
        return true;
    }
    public void RequestBmd(string op,int mask=0)
    {
        bool autoExport=op=="downloadExport"||op=="autoDownloadExport";
        if(op=="downloadExport")op="download";
        if(op=="autoDownloadExport")op="autoDownload";
        if(op!="query"&&op!="profile"&&op!="start"&&op!="freeze"&&op!="download"&&op!="autoDownload")return;
        if(mask<0||mask>15||!BmdCanOperate(op))return;
        if(op=="autoDownload"&&!BmdFastTrace){BmdStatus="GFA automático requiere ambos FW231";return;}
        if((op=="start"||op=="freeze"||op=="download"||op=="autoDownload")&&!bmdVerified)
        {BmdStatus="BMD: consultá y confirmá perfiles primero";return;}
        bmdCancel=new CancellationTokenSource();
        _=RunBmd(op,mask,bmdCancel,autoExport);
    }
    private readonly Dictionary<string,List<string>> bmdDownloadCache=new Dictionary<string,List<string>>();
    private void ClearBmdDownloadCache()=>bmdDownloadCache.Clear();
    private async Task RunBmd(string op,int mask,CancellationTokenSource operation,bool autoExport)
    {
        bool acquired=false,downloaded=false;string exportOwner="";
        var l=leftBmd;var r=rightBmd;int lg=left.generation,rg=right.generation;
        var token=operation.Token;
        try
        {
            await rawTransition.WaitAsync(token);acquired=true;
            BmdResetBoundary();
            BmdStatus="BMD "+op+" en curso";BmdProgress=0;
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
            else if(op=="autoDownload")
            {
                BmdTracePath="";
                await DownloadAutoBank(l,r,lg,rg,token);
                downloaded=true;exportOwner=autoOwner;
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
                        uint nonce=fast?await x.GetNonce(token):0;
                        string key=foot+":"+nonce+":"+t.Generation+":"+t.Count;
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
                            BmdProgress=((foot-1)+cached.Count/(float)t.Count)/2f;
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
                BmdTracePath=path;downloaded=true;exportOwner=bmdTraceOwner;BmdProgress=1;ClearBmdDownloadCache();BmdStatus="Trace guardado y SHA256 OK: "+rows.Count+" muestras";
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
            if(!string.IsNullOrEmpty(exportOwner)&&exportOwner==SavedCapturePath)RequestExport(false);
            else ExportBmdTrace();
        }
    }
    // GFA operations use CONFIG serialization but deliberately do not set BmdBusy,
    // reset pending jumps, change profile, or block the live EVENT path.
    private void AutoLog(string detail)
    {
        if(gameCaptureActive)RecordControlDiagnostic(detail);
        else if(sessionFile!=null&&sessionFile.Path==autoOwner)
            sessionFile.Add("DIAG;FINAL;"+Time.realtimeSinceStartup.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+";0;"+detail.Replace(';',',').Replace('\n',' ').Replace('\r',' ')+";;;;;;;;;;");
    }
    private void BeginAutoSession()
    {
        if(!BmdAutoEnabled)return;
        if(!BmdFastTrace||!bmdVerified||BmdTraceArmed||rawActive||rawMayBeStreaming||!autoOperation.IsCompleted){
            BmdAutoStatus="GFA no armado: verificar FW231/perfil y RAW OFF";AutoLog("GFA_AUTO_SKIPPED "+BmdAutoStatus);return;
        }
        if(!autoSaved&&autoToken!=0){BmdAutoStatus="GFA previo pendiente: descargar antes de nueva captura";AutoLog("GFA_AUTO_SKIPPED "+BmdAutoStatus);return;}
        autoToken=(Guid.NewGuid().GetHashCode()&65535);if(autoToken==0)autoToken=1;
        if(autoToken==autoSavedToken)autoToken=autoToken%65535+1;
        autoOwner=sessionFile.Path;autoSaved=false;autoAcceptMarkers=true;autoArmedL=autoArmedR=false;autoBootL=autoBootR=0;
        autoGenL=left.generation;autoGenR=right.generation;
        ClearBmdDownloadCache();BmdTracePath="";
        AutoLog("GFA_AUTO_BEGIN token="+autoToken+" preMs=3000 postMs=1000 maxSlots=16");
        BmdAutoStatus="GFA armando ambos...";
        autoOperation=ArmAutoSession(leftBmd,rightBmd,autoToken);
    }
    private async Task ArmAutoSession(BiofitBmdLink l,BiofitBmdLink r,int id)
    {
        await rawTransition.WaitAsync();
        try
        {
            for(int side=1;side<=2;side++)
            {
                int foot=side;var link=foot==1?l:r;
                try{
                    await link.Batch(async x=>{
                        uint nonce=await x.GetNonce(default);
                        var old=await x.AutoCommand("D,A",default);
                        uint savedBoot=foot==1?autoSavedBootL:autoSavedBootR;
                        if(old.Armed||old.Count>0&&(old.Token!=autoSavedToken||nonce!=savedBoot))
                            throw new InvalidOperationException("Banco anterior sin recuperar");
                        if(foot==1)autoBootL=nonce;else autoBootR=nonce;
                        var state=await x.AutoCommand("D,A,"+id,default);
                        if(state.Token!=id||!state.Armed)throw new InvalidOperationException("Armado no confirmado");
                        if(foot==1&&leftBmd==l&&left.generation==autoGenL)autoArmedL=true;
                        if(foot==2&&rightBmd==r&&right.generation==autoGenR)autoArmedR=true;
                        AutoLog("GFA_AUTO_ARM foot="+foot+" token="+id+" boot="+nonce);
                    },default);
                }catch(Exception ex){AutoLog("GFA_AUTO_ARM_ERROR foot="+foot+" "+ex.Message);}
            }
            if(autoArmedL&&autoArmedR)BmdAutoStatus="GFA AUTO armado 2/2 · máx 16 fallos";
            else BmdAutoStatus="GFA AUTO parcial I="+autoArmedL+" D="+autoArmedR;
            AutoLog("GFA_AUTO_READY token="+id+" bootI="+autoBootL+" bootD="+autoBootR+" armedI="+autoArmedL+" armedD="+autoArmedR);
        }
        finally{rawTransition.Release();}
    }
    private void QueueAutoMarker(int id,double now)
    {
        if(!autoAcceptMarkers)return;
        if(id>BiofitBmdProtocol.MaxAutoSlots){AutoLog("GFA_AUTO_LIMIT failure="+id);return;}
        bool okL=timeAligner.TryMapPhoneTime(1,now,out uint msL),okR=timeAligner.TryMapPhoneTime(2,now,out uint msR);
        AutoLog("GFA_AUTO_TARGET failure="+id+" I="+(okL?msL.ToString():"UNKNOWN")+" D="+(okR?msR.ToString():"UNKNOWN")+" clock=ESTIMATED");
        autoOperation=SendAutoMarker(autoOperation,id,okL,msL,okR,msR);
    }
    private async Task SendAutoMarker(Task previous,int id,bool okL,uint msL,bool okR,uint msR)
    {
        try{await previous;}catch(Exception ex){AutoLog("GFA_AUTO_PREVIOUS "+ex.Message);}
        await rawTransition.WaitAsync();
        try
        {
            for(int side=1;side<=2;side++)
            {
                int foot=side;bool ok=foot==1?okL&&autoArmedL&&left.generation==autoGenL:okR&&autoArmedR&&right.generation==autoGenR;
                var link=foot==1?leftBmd:rightBmd;
                if(!ok||link==null){AutoLog("GFA_AUTO_MARK_MISSING failure="+id+" foot="+foot);continue;}
                try{
                    using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                        await link.Batch(x=>x.MarkFailure(id,foot==1?msL:msR,deadline.Token),deadline.Token);
                    AutoLog("GFA_AUTO_MARK_OK failure="+id+" foot="+foot);
                }catch(Exception ex){AutoLog("GFA_AUTO_MARK_ERROR failure="+id+" foot="+foot+" "+ex.Message);}
            }
        }
        finally{rawTransition.Release();}
    }
    private async Task FinishAutoSession()
    {
        if(sessionFile==null||sessionFile.Path!=autoOwner||autoToken==0||autoSaved)return;
        autoAcceptMarkers=false;
        try{await autoOperation;}catch(Exception ex){AutoLog("GFA_AUTO_PENDING_ERROR "+ex.Message);}
        await rawTransition.WaitAsync();
        try
        {
            for(int side=1;side<=2;side++)
            {
                int foot=side;var link=foot==1?leftBmd:rightBmd;uint expected=foot==1?autoBootL:autoBootR;
                if(link==null||expected==0){AutoLog("GFA_AUTO_STOP_MISSING foot="+foot);continue;}
                try{
                    await link.Batch(async x=>{
                        if(await x.GetNonce(default)!=expected)throw new InvalidOperationException("tracker reiniciado");
                        var before=await x.AutoCommand("D,A",default);
                        if(before.Token!=autoToken)throw new InvalidOperationException("token de sesión distinto");
                        var after=await x.AutoCommand("D,A,0",default);
                        AutoLog("GFA_AUTO_STOP foot="+foot+" token="+after.Token+" snapshots="+after.Count);
                    },default);
                }catch(Exception ex){AutoLog("GFA_AUTO_STOP_ERROR foot="+foot+" "+ex.Message);}
            }
        }
        finally{autoArmedL=autoArmedR=false;rawTransition.Release();}
        BmdAutoStatus="GFA: banco retenido · DESCARGAR GFA";
    }
    private async Task DownloadAutoBank(BiofitBmdLink l,BiofitBmdLink r,int lg,int rg,CancellationToken token)
    {
        int target=autoToken;uint[] boot=new uint[2];
        var states=new BiofitBmdProtocol.AutoState[2];var links=new[]{l,r};
        for(int s=0;s<2;s++){
            int k=s;
            await links[k].Batch(async x=>{boot[k]=await x.GetNonce(token);states[k]=await x.AutoCommand("D,A",token);},token);
        }
        CheckBmdConnection(l,r,lg,rg,token);
        // Recovery after an app restart: only accept an unambiguous shared
        // token or a single bank. Never merge different sessions silently.
        if(target==0){
            if(states[0].Token!=0&&states[1].Token!=0&&states[0].Token!=states[1].Token)
                throw new InvalidOperationException("Bancos GFA de sesiones distintas; no mezclar");
            target=states[0].Token!=0?states[0].Token:states[1].Token;
        }
        if(target==0)throw new InvalidOperationException("No existe banco GFA");
        var rows=new List<string>(2*BiofitBmdProtocol.MaxAutoSlots*1025);
        bool both=true;int sampleTotal=0,completeSnapshots=0,partialSnapshots=0;
        var watch=System.Diagnostics.Stopwatch.StartNew();int transferred=0;
        int expectedSlots=states[0].Count+states[1].Count,doneSlots=0;
        for(int s=0;s<2;s++){
            int k=s;byte foot=(byte)(s+1);uint known=k==0?autoBootL:autoBootR;
            if(states[k].Token!=target||(known!=0&&known!=boot[k])){
                both=false;rows.Add(BiofitBmdProtocol.AutoMetadata(foot,default,"MISSING_BANK"));continue;
            }
            await links[k].Batch(async x=>{
                if(states[k].Armed)states[k]=await x.AutoCommand("D,A,0",token);
                if(states[k].Count==0)rows.Add(BiofitBmdProtocol.AutoMetadata(foot,default,"EMPTY_BANK"));
                for(int slot=0;slot<states[k].Count;slot++){
                    CheckBmdConnection(l,r,lg,rg,token);
                    var snap=await x.GetSnapshot(foot,slot,token);
                    rows.Add(BiofitBmdProtocol.AutoMetadata(foot,snap,"SNAPSHOT"));
                    if(snap.State!=3||snap.Flags!=0)partialSnapshots++;else completeSnapshots++;
                    string key=foot+":"+boot[k]+":"+snap.Generation+":"+snap.Count;
                    if(!bmdDownloadCache.TryGetValue(key,out var cached)){cached=new List<string>(snap.Count);bmdDownloadCache[key]=cached;}
                    while(cached.Count<snap.Count){
                        token.ThrowIfCancellationRequested();CheckBmdConnection(l,r,lg,rg,token);
                        var block=await x.GetBlock(foot,snap.Generation,cached.Count,Math.Min(BiofitBmdProtocol.MaxBlock,snap.Count-cached.Count),token);
                        CheckBmdConnection(l,r,lg,rg,token);cached.AddRange(block);transferred+=block.Length;
                        BmdProgress=(doneSlots+cached.Count/(float)Math.Max(1,snap.Count))/Math.Max(1,expectedSlots);
                        BmdStatus=FormattableString.Invariant($"GFA {foot}/2 fallo {snap.FailureId}: {cached.Count}/{snap.Count} | {transferred/Math.Max(.001,watch.Elapsed.TotalSeconds):F1} m/s");
                    }
                    string suffix=BiofitBmdProtocol.AutoSuffix(snap,"SAMPLE");
                    foreach(string line in cached)rows.Add(line+suffix);
                    sampleTotal+=cached.Count;doneSlots++;
                }
            },token);
        }
        CheckBmdConnection(l,r,lg,rg,token);
        if(rows.Count==0)throw new InvalidOperationException("Banco GFA sin metadatos");
        string stem=string.IsNullOrEmpty(autoOwner)?"BIOFIT_GFA_"+target:Path.GetFileNameWithoutExtension(autoOwner)+"_TRACE_GFA_"+target;
        string path=Path.Combine(Application.persistentDataPath,"BMD",stem+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
        var file=new BiofitSessionFile(path,BiofitBmdProtocol.AutoHeader,Math.Max(8192,rows.Count+1));
        foreach(string row in rows)if(!file.Add(row)){await file.Finish();throw new IOException("Cola GFA: "+file.Error);}
        await file.Finish();
        if(file.Error.Length!=0||!BiofitCaptureArchive.VerifyFileStreaming(path))throw new IOException("GFA no verificado: "+file.Error);
        BmdTracePath=path;BmdProgress=1;autoSaved=true;autoSavedToken=target;autoSavedBootL=boot[0];autoSavedBootR=boot[1];
        autoToken=target;ClearBmdDownloadCache();
        BmdStatus="GFA SHA256 OK: "+sampleTotal+" muestras | ventanas completas="+completeSnapshots+" parciales="+partialSnapshots+(both?"":" | BANCO BILATERAL INCOMPLETO");
        BmdAutoStatus=BmdStatus;
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
