using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniBLE;

static class BmdIntegration
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string why){if(!ok)throw new Exception("BMD integration: "+why);}
    sealed class TrackerConfig : IBleCharacteristic
    {
        readonly byte id;
        public int Active,Token,Target;public bool FailProfile,Silent;
        public int Writes;
        Action<byte[]> callback;
        public TrackerConfig(byte id){this.id=id;}
        public Task SubscribeAsync(Action<byte[]> cb,CancellationToken cancellationToken=default){callback=cb;return Task.CompletedTask;}
        public Task UnsubscribeAsync(CancellationToken cancellationToken=default){callback=null;return Task.CompletedTask;}
        void Emit(string s)=>callback?.Invoke(Encoding.ASCII.GetBytes(s));
        public Task WriteAsync(byte[] data,bool withResponse=true,CancellationToken cancellationToken=default)
        {
            cancellationToken.ThrowIfCancellationRequested();Writes++;
            if(Silent)return Task.CompletedTask;
            string command=Encoding.ASCII.GetString(data);
            if(command=="D,GET")Emit("P,"+Active+","+Target+","+Token+",0");
            else if(command.StartsWith("D,P,"))
            {
                if(FailProfile)Emit("ERR,D,PROFILE");
                else {var fields=command.Split(',');Active=Target=int.Parse(fields[2]);Token=int.Parse(fields[3]);Emit("D,Q,"+Token);}
            }
            else if(command=="D,T,1")Emit("T,1,2,3");
            else if(command=="D,T,0"||command=="D,T")Emit("T,2,2,3");
            else if(command.StartsWith("D,R,"))
            {
                var fields=command.Split(',');var page=new byte[20];
                page[0]=0xD3;page[1]=id;page[2]=byte.Parse(fields[4]);page[3]=1;page[4]=3;page[6]=byte.Parse(fields[3]);
                callback?.Invoke(page);
            }
            else Emit("ERR,D,CMD");
            return Task.CompletedTask;
        }
    }
    static async Task Idle(BiofitBLEMonitor m)
    {
        var timeout=System.Diagnostics.Stopwatch.StartNew();
        while(m.BmdBusy&&timeout.Elapsed.TotalSeconds<10)await Task.Delay(5);
        Check(!m.BmdBusy,"operation completion");
    }
    public static async Task Run()
    {
        var m=new BiofitBLEMonitor();var l=new TrackerConfig(1);var r=new TrackerConfig(2);
        foreach(var side in new[]{"left","right"})
        {
            var tracker=typeof(BiofitBLEMonitor).GetField(side,Private).GetValue(m);
            tracker.GetType().GetField("connection").SetValue(tracker,"CONECTADO - NOTIFICACIONES ACTIVAS");
            tracker.GetType().GetField("firmwareVersion").SetValue(tracker,230);
            typeof(BiofitBLEMonitor).GetField(side+"Bmd",Private).SetValue(m,new BiofitBmdLink(side=="left"?l:r));
        }
        Check(m.BmdFirmwareReady&&m.BmdControlBlocked,"unverified FW230 blocked");
        m.RequestBmd("query");await Idle(m);Check(m.BmdMask==0&&!m.BmdControlBlocked,"both profile 0 readback");
        m.RequestBmd("profile",6);await Idle(m);Check(l.Active==6&&r.Active==6&&m.BmdMask==6&&!m.BmdControlBlocked,"two real client links");
        r.FailProfile=true;m.RequestBmd("profile",1);await Idle(m);
        Check(l.Active==1&&r.Active==6&&m.BmdControlBlocked&&m.BmdMask==-1,"partial bilateral apply fails closed");
        m.RequestBmd("query");await Idle(m);Check(m.BmdControlBlocked,"readback mismatch stays blocked");
        r.FailProfile=false;m.RequestBmd("profile",0);await Idle(m);Check(l.Active==0&&r.Active==0&&!m.BmdControlBlocked,"restore from mismatch");
        m.RequestBmd("start");await Idle(m);Check(m.BmdTraceArmed,"trace armed");
        m.RequestBmd("freeze");await Idle(m);Check(!m.BmdTraceArmed,"trace frozen");
        m.RequestBmd("download");await Idle(m);Check(BiofitCaptureArchive.VerifyFileStreaming(m.BmdTracePath),"trace from both links and actual archive");
        string csv=System.IO.File.ReadAllText(m.BmdTracePath);
        Check(csv.Contains("1;3;0;")&&csv.Contains("2;3;1;")&&csv.Contains("# filas_total=4;"),"both sides included");
        l.Silent=true;m.RequestBmd("query");await Task.Delay(10);m.CancelBmd();await Idle(m);Check(m.BmdControlBlocked,"cancelled readback blocks control");
        l.Silent=false;m.RequestBmd("query");await Idle(m);Check(!m.BmdControlBlocked,"verified recovery after cancellation");
        var left=typeof(BiofitBLEMonitor).GetField("left",Private).GetValue(m);
        typeof(BiofitBLEMonitor).GetMethod("BmdDisconnected",Private).Invoke(m,new[]{left});
        Check(m.BmdControlBlocked&&!m.BmdFirmwareReady,"disconnect invalidates verification");
        Check(m.StartGameCapture(),"GFA recording start");
        m.GfaAttempt("test");m.GfaAction("NO_JUMP","NO_SUPPORT");m.GfaDeath("test","Cube",12,1,10,false,0);
        Check(m.GfaSummary=="GFA intentos=1 muertes=1","GFA counts active only");
        Check(m.StopGameCapture(),"GFA capture stop");await m.CaptureCompletion;
        Check(BiofitCaptureArchive.VerifyFileStreaming(m.SavedCapturePath)&&System.IO.File.ReadAllText(m.SavedCapturePath).Contains("GAMEPLAY_FAILURE"),"GFA persisted with SHA");
        m.GfaDeath("test","Cube",12,1,10,false,0);Check(m.GfaSummary=="GFA intentos=1 muertes=1","GFA inactive ignored");
        Console.WriteLine("PASS BMD real monitor integration: bilateral apply, partial failure, restore, two-foot trace, cancellation, disconnect");
    }
}
