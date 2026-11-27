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
    public bool BmdFirmwareReady=>BothTrackersConnected&&left.firmwareVersion==230&&right.firmwareVersion==230&&leftBmd!=null&&rightBmd!=null;
    public bool BmdControlBlocked=>BmdBusy||bmdUncertain||
        ((left.firmwareVersion==230||right.firmwareVersion==230)&&(!BmdFirmwareReady||!bmdVerified));
    private bool BmdEventAllowed(double callback)=>!BmdControlBlocked&&callback>=bmdResumeAfter;
    public string BmdStatus {get;private set;}="BMD: consultar ambos trackers";
    public string BmdTracePath {get;private set;}="";
    public string BmdShadowSummary=>bmdShadow.Summary();
    public string BmdTrackerSummary=>"I FW "+left.firmwareVersion+" | D FW "+right.firmwareVersion+
        " | perfil confirmado: "+(bmdVerified?bmdMask.ToString():"--");
    public string BmdDetectorSummary=>"I UP="+left.upImpulses+" D UP="+right.upImpulses+
        " | pares="+TotalPairs+" | "+(BmdControlBlocked?"CONTROL BLE BLOQUEADO":"CONTROL BLE HABILITADO");
    public int BmdMask=>bmdVerified?bmdMask:-1;
    public bool BmdTraceArmed {get;private set;}
    public void CancelBmd(){bmdCancel?.Cancel();}
    private void BmdDisconnected(Tracker tracker)
    {
        if(tracker.id==1)leftBmd=null;else rightBmd=null;
        if(bmdVerified||bmdUncertain||tracker.firmwareVersion==230){bmdUncertain=true;bmdVerified=false;}
        BmdTraceArmed=false;CancelBmd();
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
        if(!BmdFirmwareReady){BmdStatus="BMD: conectar ambos FW230 y esperar versión";return false;}
        if((op=="profile"||op=="download")&&GameCaptureActive)
        {BmdStatus="BMD: FINALIZÁ el CSV antes de cambiar perfil o descargar";return false;}
        return true;
    }
    public void RequestBmd(string op,int mask=0)
    {
        if(op!="query"&&op!="profile"&&op!="start"&&op!="freeze"&&op!="download")return;
        if(mask<0||mask>15||!BmdCanOperate(op))return;
        if((op=="start"||op=="freeze"||op=="download")&&!bmdVerified)
        {BmdStatus="BMD: consultá y confirmá perfiles primero";return;}
        bmdCancel=new CancellationTokenSource();
        _=RunBmd(op,mask,bmdCancel);
    }
    private async Task RunBmd(string op,int mask,CancellationTokenSource operation)
    {
        bool acquired=false;
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
                string cmd=op=="start"?"D,T,1":"D,T,0";
                if(op=="start")BmdTraceArmed=true; // may be partially delivered; keep freeze accessible
                BiofitBmdProtocol.Trace a=default,b=default;
                await l.Batch(async x=>a=await x.TraceCommand(cmd,token),token);
                await r.Batch(async x=>b=await x.TraceCommand(cmd,token),token);
                CheckBmdConnection(l,r,lg,rg,token);
                if(op=="freeze"&&(a.State!=2||b.State!=2))throw new InvalidOperationException("No hay dos trazas congeladas");
                BmdTraceArmed=op=="start";
                BmdStatus="Trace I "+a+" | D "+b;
                RecordControlDiagnostic("BMD_TRACE "+op+" I "+a+" D "+b);
            }
            else
            {
                BmdTracePath=""; // never export an earlier trace as the result of this download
                var rows=new List<string>(2048);
                int side=0;
                foreach(var link in new[]{l,r})
                {
                    byte foot=(byte)(++side);
                    await link.Batch(async x=>{
                        var t=await x.TraceCommand("D,T",token);
                        if(t.State!=2||t.Count<1)throw new InvalidOperationException("Congelar ambas trazas primero");
                        for(int i=0;i<t.Count;i++)
                        {
                            token.ThrowIfCancellationRequested();
                            rows.Add(await x.GetRow(foot,t.Generation,i,token));
                            BmdStatus="Descarga "+foot+"/2: "+(i+1)+"/"+t.Count+" muestras";
                        }
                    },token);
                }
                CheckBmdConnection(l,r,lg,rg,token);
                string path=Path.Combine(Application.persistentDataPath,"BMD","BIOFIT_BMD_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
                var file=new BiofitSessionFile(path,BiofitBmdProtocol.TraceHeader);
                foreach(string row in rows)file.Add(row);
                await file.Finish();
                if(file.Error.Length!=0||!BiofitCaptureArchive.VerifyFileStreaming(path))throw new IOException("Trace no verificado: "+file.Error);
                BmdTracePath=path;BmdStatus="Trace guardado y SHA256 OK: "+rows.Count+" muestras";
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
    }
    private void CheckBmdConnection(BiofitBmdLink l,BiofitBmdLink r,int lg,int rg,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!BmdFirmwareReady||l!=leftBmd||r!=rightBmd||lg!=left.generation||rg!=right.generation)
            throw new InvalidOperationException("Conexión cambió durante la operación");
    }
    public void ExportBmdTrace()
    {
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
