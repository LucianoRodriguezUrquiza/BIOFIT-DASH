using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
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
        public int Writes,AutoToken;public bool Armed,FailSecondMark;
        public readonly List<(int id,uint target)> Marks=new List<(int,uint)>();
        static void Put16(byte[] b,int p,int v){b[p]=(byte)v;b[p+1]=(byte)(v>>8);}
        static void Put32(byte[] b,int p,uint v){for(int i=0;i<4;i++)b[p+i]=(byte)(v>>(8*i));}
        byte[] Page(int gen,int row,int pg){var b=new byte[20];b[0]=0xd3;b[1]=id;b[2]=(byte)pg;b[3]=1;Put16(b,4,gen);Put16(b,6,row);return b;}
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
            else if(command=="D,N")Emit("N,"+(100+id));
            else if(command=="D,A"||command.StartsWith("D,A,"))
            {
                if(command!="D,A"){int token=int.Parse(command.Substring(4));if(token==0)Armed=false;else{AutoToken=token;Armed=true;Marks.Clear();}}
                Emit("A,"+AutoToken+","+Marks.Count+","+(Armed?1:0));
            }
            else if(command.StartsWith("D,M,"))
            {
                var f=command.Split(',');int fid=int.Parse(f[2]);
                if(!Armed||(FailSecondMark&&fid==2))Emit("ERR,D,MARK");
                else{Marks.Add((fid,uint.Parse(f[3])));Emit("M,"+fid+","+(Marks.Count-1));}
            }
            else if(command.StartsWith("D,I,"))
            {
                int slot=int.Parse(command.Substring(4));var mark=Marks[slot];var b=new byte[20];b[0]=0xd5;b[1]=id;b[2]=(byte)slot;b[3]=3;
                Put16(b,4,32768+slot+1);Put16(b,6,mark.id);Put32(b,8,mark.target);Put32(b,12,mark.target+50);Put16(b,16,2);callback?.Invoke(b);
            }
            else if(command.StartsWith("D,B,"))
            {
                var f=command.Split(',');int gen=int.Parse(f[2]),start=int.Parse(f[3]),count=int.Parse(f[4]);uint crc=0xffffffff;
                for(int row=start;row<start+count;row++)for(int pg=0;pg<3;pg++){
                    byte[] page=Page(gen,row,pg);crc=BiofitBmdProtocol.CrcUpdate(crc,page);
                    if(row!=start||pg!=1)callback?.Invoke(page); // repair one missing page
                }
                var b=new byte[20];b[0]=0xd4;b[1]=id;b[2]=(byte)count;b[3]=1;Put16(b,4,gen);Put16(b,6,start);Put32(b,8,crc^0xffffffff);callback?.Invoke(b);
            }
            else if(command.StartsWith("D,R,"))
            {
                var f=command.Split(',');callback?.Invoke(Page(int.Parse(f[2]),int.Parse(f[3]),int.Parse(f[4])));
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
        Attach(m,l,r,230);
        await Legacy(m,l,r);
        await Automatic();
    }
    static void Attach(BiofitBLEMonitor m,TrackerConfig l,TrackerConfig r,int version)
    {
        foreach(var side in new[]{"left","right"})
        {
            var tracker=typeof(BiofitBLEMonitor).GetField(side,Private).GetValue(m);
            tracker.GetType().GetField("connection").SetValue(tracker,"CONECTADO - NOTIFICACIONES ACTIVAS");
            tracker.GetType().GetField("firmwareVersion").SetValue(tracker,version);
            typeof(BiofitBLEMonitor).GetField(side+"Bmd",Private).SetValue(m,new BiofitBmdLink(side=="left"?l:r));
        }
    }
    static async Task Legacy(BiofitBLEMonitor m,TrackerConfig l,TrackerConfig r)
    {
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
    static Task AutoTask(BiofitBLEMonitor m)=>(Task)typeof(BiofitBLEMonitor).GetField("autoOperation",Private).GetValue(m);
    static async Task Automatic()
    {
        var m=new BiofitBLEMonitor();var l=new TrackerConfig(1);var r=new TrackerConfig(2){FailSecondMark=true};Attach(m,l,r,231);
        m.RequestBmd("query");await Idle(m);Check(m.BmdFastTrace&&!m.BmdControlBlocked,"FW231 ready");
        m.ToggleBmdAuto();Check(m.StartGameCapture(),"start automatic");await AutoTask(m);
        Check(l.Armed&&r.Armed&&l.AutoToken==r.AutoToken&&!m.BmdControlBlocked,"automatic leaves live control active");
        var align=(BiofitClockAligner)typeof(BiofitBLEMonitor).GetField("timeAligner",Private).GetValue(m);
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        for(int i=0;i<10;i++)for(byte foot=1;foot<=2;foot++)align.ObserveStatus(foot,(uint)(1000+i*100),now-.9+i*.1);
        m.GfaAttempt("test");m.GfaDeath("test","Cube",1,1,2,false,0);m.GfaAttempt("test");m.GfaDeath("test","Cube",2,1,4,false,0);
        await AutoTask(m);Check(l.Marks.Count==2&&r.Marks.Count==1,"marker failure is visible");
        Check(m.StopGameCapture(),"stop automatic");await m.CaptureCompletion;
        Check(!l.Armed&&!r.Armed&&m.BmdAutoPending,"bank retained");
        // Restart simulation: never overwrite the unsaved bank.
        var restart=new BiofitBLEMonitor();Attach(restart,l,r,231);restart.RequestBmd("query");await Idle(restart);
        restart.ToggleBmdAuto();Check(restart.StartGameCapture(),"restart capture");await AutoTask(restart);
        Check(l.Marks.Count==2&&r.Marks.Count==1&&!l.Armed&&!r.Armed,"preflight preserves old bank");
        Check(restart.StopGameCapture(),"stop restart");await restart.CaptureCompletion;
        m.RequestBmd("autoDownload");await Idle(m);
        Check(BiofitCaptureArchive.VerifyFileStreaming(m.BmdTracePath),"auto trace SHA");
        var rows=System.IO.File.ReadAllLines(m.BmdTracePath);
        Check(rows.Count(x=>x.EndsWith(";SAMPLE"))==6&&rows.Count(x=>x.EndsWith(";SNAPSHOT"))==3&&rows.Count(x=>x.EndsWith(";MISSING_SNAPSHOT"))==1,"exact partial bilateral archive");
        Check(m.BmdStatus.Contains("faltantes=1")&&!m.BmdAutoPending,"partial explicit");
        restart.RequestBmd("autoDownload");await Idle(restart);Check(BiofitCaptureArchive.VerifyFileStreaming(restart.BmdTracePath),"recover bank after restart");
        Console.WriteLine("PASS FW231 GFA automatic integration: arm, clock, marks, stop, partial bank, repair, SHA, restart recovery");
    }
}
