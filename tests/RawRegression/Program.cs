using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UniBLE;
class Program
{
 static int checks;
 static void Check(bool v,string reason) { if(!v)throw new Exception(reason);checks++; }
 static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
 class Characteristic:IBleCharacteristic
 {
  public Action<byte[]> Callback;public List<string> Calls=new List<string>();public bool FailOn,FailOff,Hang,NoAck;
  public Task SubscribeAsync(Action<byte[]> cb,CancellationToken cancellationToken=default) { Calls.Add("subscribe");Callback=cb;return Task.CompletedTask; }
  public Task UnsubscribeAsync(CancellationToken cancellationToken=default) { Calls.Add("unsubscribe");Callback=null;return Task.CompletedTask; }
  public async Task WriteAsync(byte[] data,bool withResponse=true,CancellationToken cancellationToken=default)
  {
   string text=System.Text.Encoding.ASCII.GetString(data);Calls.Add(text);
   if(Hang)await Task.Delay(Timeout.Infinite,cancellationToken);
   if((FailOn&&text=="STREAM,1") || (FailOff&&text=="STREAM,0"))throw new IOException("injected GATT failure");
   if(!NoAck)Callback?.Invoke(System.Text.Encoding.ASCII.GetBytes("OK,"+text));
  }
 }
 static byte[] Raw(byte id,ushort seq,uint ms)
 {
  var b=new byte[20];b[0]=2;b[1]=id;BitConverter.GetBytes(seq).CopyTo(b,2);BitConverter.GetBytes(ms).CopyTo(b,4);
  short[] axes={-1000,500,1234,-123,456,-789};for(int i=0;i<6;i++)BitConverter.GetBytes(axes[i]).CopyTo(b,8+2*i);return b;
 }
 static string Temp(string root,string name)=>Path.Combine(root,"BIOFIT_v095_"+name+".csv");
 static void ConnectForTest(BiofitBLEMonitor monitor,BiofitRawLink a,BiofitRawLink b)
 {
  typeof(BiofitBLEMonitor).GetField("leftRaw",Hidden).SetValue(monitor,a);
  typeof(BiofitBLEMonitor).GetField("rightRaw",Hidden).SetValue(monitor,b);
  foreach(string name in new[]{"left","right"}) {
   var tracker=typeof(BiofitBLEMonitor).GetField(name,Hidden).GetValue(monitor);
   tracker.GetType().GetField("connection").SetValue(tracker,"CONECTADO - NOTIFICACIONES ACTIVAS");
  }
 }
 static async Task Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"biofit-raw-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  UnityEngine.Application.persistentDataPath=root;
  try
  {
   var sink=new BiofitSessionFile(Temp(root,"decode"),BiofitRawCapture.Header);
   var raw=new BiofitRawCapture(sink);raw.Segment="A";
   raw.Record(1,Raw(1,65535,uint.MaxValue-49),10);raw.Record(1,Raw(1,0,0),10.05);
   raw.Record(1,Raw(1,2,100),10.15);raw.Record(1,Raw(1,2,100),10.16);raw.Record(1,Raw(1,1,50),10.17);
   raw.Record(2,Raw(2,9,400),10.18);raw.Record(1,Raw(2,3,150),10.2);raw.Record(1,new byte[19],10.3);
   await raw.FinishAsync();string csv=File.ReadAllText(sink.Path);
   Check(raw.Valid==4 && raw.Missing==1 && raw.Duplicate==2 && raw.Invalid==2,"signed wrap/gap/invalid counters");
   Check(csv.Contains(";-1.000;0.500;1.234;-12.300;45.600;-78.900;"),"all six signed axes and scaling");
   Check(BiofitCaptureArchive.Verify(csv),"RAW hash/15 columns/counts");
   Check(raw.LeftCount==3 && raw.RightCount==1,"per foot receive counts");
   Check(csv.Contains(";0;0.000;"),"millis and sequence rollover maintain interval metric");
   var truncated=csv.Substring(0,csv.Length-10);Check(!BiofitCaptureArchive.Verify(truncated),"truncation rejected");
   var rc=new Characteristic();var cfg=new Characteristic();var link=new BiofitRawLink(rc,cfg);
   await link.SetAsync(true,_=>{});await link.SetAsync(false,null);
   Check(rc.Calls.SequenceEqual(new[]{"subscribe","unsubscribe"}) && cfg.Calls.Where(x=>x.StartsWith("STREAM,")).SequenceEqual(new[]{"STREAM,1","STREAM,0"}),"enable and stop command/subscription lifecycle");
   cfg.FailOn=true;bool failed=false;try{await link.SetAsync(true,_=>{});}catch(IOException){failed=true;}
   Check(failed&&cfg.Calls.Where(x=>x.StartsWith("STREAM,")).Last()=="STREAM,0"&&rc.Calls.Last()=="unsubscribe","partial activation rolls back");
   cfg.FailOn=false;cfg.FailOff=true;failed=false;await link.SetAsync(true,_=>{});
   try{await link.SetAsync(false,null);}catch(IOException){failed=true;}
   Check(failed&&rc.Calls.Last()=="unsubscribe","disconnect failed write still unsubscribes");
   var waiting=new Characteristic{Hang=true};var timed=new BiofitRawLink(new Characteristic(),waiting);
   var timer=System.Diagnostics.Stopwatch.StartNew();failed=false;
   try{await timed.SetAsync(false,null);}catch(OperationCanceledException){failed=true;}
   Check(failed && timer.Elapsed.TotalSeconds<8,"GATT cancellation bounded, no blocking wait");
   var noAck=new BiofitRawLink(new Characteristic(),new Characteristic{NoAck=true});
   failed=false;try { await noAck.SetAsync(false,null); }catch(OperationCanceledException){failed=true;}
   Check(failed,"write completion without firmware ACK is not successful STREAM0");
   // Real receiver integration: RAW never enters pending gameplay packets.
   var m=new BiofitBLEMonitor();var lr=new Characteristic();var rr=new Characteristic();var lc=new Characteristic();var rconf=new Characteristic();
   ConnectForTest(m,new BiofitRawLink(lr,lc),new BiofitRawLink(rr,rconf));
   Check(m.StartGameCapture(),"streaming game session");await m.SetRawCaptureAsync(true);
   Check(m.RawActive,"both links enabled");var stale=lr.Callback;
   var capture=(BiofitEventCapture)typeof(BiofitBLEMonitor).GetField("eventCapture",Hidden).GetValue(m);
   for(int i=0;i<12000;i++)
   {
    lr.Callback(Raw(1,(ushort)i,(uint)(i*50)));rr.Callback(Raw(2,(ushort)i,(uint)(i*50)));
    // Yield lets worker consume this intentionally much faster than 20Hz host input.
    if(i%100==0)await Task.Delay(5);
    m.RecordGameAction(i%2==0?"MODO_EXP_UMBRAL_ON":"MODO_EXP_UMBRAL_OFF");
    if(i%100==0) { m.RecordSingleFootConfig(2,.25f);m.MarkVideoSync();capture.Record(i,1,3,(ushort)i,(uint)i,3,.3f,false); }
   }
   Check(m.AppQueuePending==0 && m.AppQueueDropped==0,"24000 RAW do not use/overflow game queue");
   await m.SetRawCaptureAsync(false);stale(Raw(1,20000,1000000));
   Check(!m.RawActive,"disable invalidates stale callback");
   await m.SetRawCaptureAsync(true);
   lr.Callback(Raw(1,1,2000));rr.Callback(Raw(2,1,2000));
   await m.SetRawCaptureAsync(false);
   Check(lc.Calls.Count(x=>x=="STREAM,1")==2 && lc.Calls.Count(x=>x=="STREAM,0")==2,"repeated RAW toggles serialize commands");
   Check(m.StopGameCapture(),"async stop accepted");await m.CaptureCompletion;
   Check(File.Exists(m.SavedCapturePath),"streamed session finalized");
   csv=File.ReadAllText(m.SavedCapturePath);
   Check(BiofitCaptureArchive.Verify(csv) && csv.Split('\n').Count(x=>x.StartsWith("APP;"))==12000,"all critical rows beyond 6000 preserved");
   Check(csv.Split('\n').Count(x=>x.StartsWith("EV;"))==120 && csv.Contains("FIN_JUEGO") && m.DroppedEventCount==0,"EV and end marker preserved past 6000 rows");
   Check(csv.Contains("MODO_EXP_UMBRAL_ON")&&csv.Contains("MODO_EXP_UMBRAL_OFF")&&csv.Contains("SOLO_UP_DER=0.25g"),"EXP/config transitions actually exported");
   Check(csv.Contains("SYNC 001")&&csv.Contains("callbackClockS="),"visible markers carry both clocks");
   string rawPath=Path.Combine(root,"RawCaptures",Path.GetFileNameWithoutExtension(m.SavedCapturePath)+"_RAW.csv");
   Check(BiofitCaptureArchive.Verify(File.ReadAllText(rawPath)),"companion RAW valid");
   Check(File.ReadAllText(rawPath).Contains("RAW_SEG_2"),"new segment resets sequence baseline");
   var recorded=(BiofitRawCapture)typeof(BiofitBLEMonitor).GetField("rawCapture",Hidden).GetValue(m);
   Check(recorded.Valid==24002 && recorded.QueueDropped==0 && recorded.File.Rejected==0,"all stress RAW retained; stale callback excluded");
   Check(m.ExportGameCapture(),"export primary and RAW");
   Check(File.Exists(Path.Combine(root,"BiofitEditorExports",Path.GetFileName(rawPath))),"RAW filename distinct in Downloads equivalent");
   var reopened=new BiofitBLEMonitor();Check(reopened.ExportGameCapture(),"restart finds main plus matching RAW");
   var interrupted=new BiofitBLEMonitor();var ir=new Characteristic();var jr=new Characteristic();var ic=new Characteristic();var jc=new Characteristic();
   ConnectForTest(interrupted,new BiofitRawLink(ir,ic),new BiofitRawLink(jr,jc));
   interrupted.StartGameCapture();await interrupted.SetRawCaptureAsync(true);
   var packetType=typeof(BiofitBLEMonitor).GetNestedType("Packet",BindingFlags.NonPublic);
   object reset=Activator.CreateInstance(packetType);packetType.GetField("expectedId").SetValue(reset,(byte)1);packetType.GetField("reset").SetValue(reset,true);
   typeof(BiofitBLEMonitor).GetMethod("Enqueue",Hidden).Invoke(interrupted,new[]{reset});
   typeof(BiofitBLEMonitor).GetMethod("Update",Hidden).Invoke(interrupted,null);
   await interrupted.RawOperation;
   Check(!interrupted.RawActive && ic.Calls.Contains("STREAM,0") && jc.Calls.Contains("STREAM,0"),"disconnect path disables both raw links");
   Check(interrupted.GameCaptureActive,"disconnect preserves session recording");
   typeof(BiofitBLEMonitor).GetMethod("OnApplicationPause",Hidden).Invoke(interrupted,new object[]{true});await interrupted.CaptureCompletion;
   Check(!interrupted.GameCaptureActive && File.ReadAllText(interrupted.SavedCapturePath).Contains("APP_PAUSA_FIN"),"pause safely closes current archive");
   // IO fault cannot publish a successful final archive or touch a previous session.
   string badDir=Path.Combine(root,"not-a-directory");File.WriteAllText(badDir,"keep");
   var bad=new BiofitSessionFile(Path.Combine(badDir,"BIOFIT_v095_bad.csv"),BiofitRawCapture.Header);bad.Add("RAW;x;0;1;1;1;0;0;0;0;0;0;0;0;0");await bad.Finish();
   Check(bad.Error.Length>0&&!File.Exists(bad.Path)&&File.ReadAllText(badDir)=="keep","IO failure visible, no false success");
   var full=new BiofitSessionFile(Temp(root,"overflow"),BiofitRawCapture.Header,1);
   for(int i=0;i<100000;i++)full.Add("RAW;x;0;1;1;1;0;0;0;0;0;0;0;0;0");await full.Finish();
   Check(full.Rejected>0&&full.Error.Length>0&&!File.Exists(full.Path),"overflow explicit and never published as complete");
   Console.WriteLine($"PASS {checks} RAW/session host checks; 24000 RAW and 12000 APP stress, no Android radio validation");
  }
  finally { Directory.Delete(root,true); }
 }
}
