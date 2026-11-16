using System;
using System.Reflection;
using System.Diagnostics;
class Program {
 static int checks;
 static void Check(bool v,string s) { if(!v)throw new Exception(s); checks++; }
 static long Ticks(double t)=>(long)(t*Stopwatch.Frequency);
 static byte[] Packet(byte id,bool status,ushort seq,uint ms) {
  var b=new byte[status?20:16]; b[0]=2;b[1]=id;b[2]=3;b[3]=0x80;
  BitConverter.GetBytes(seq).CopyTo(b,4); BitConverter.GetBytes(ms).CopyTo(b,6);
  if(!status)BitConverter.GetBytes((short)400).CopyTo(b,12);
  return b;
 }
 static object Wrap(byte[] bytes,double t,bool status=false) {
  var type=typeof(BiofitBLEMonitor).GetNestedType("Packet",BindingFlags.NonPublic);
  var p=Activator.CreateInstance(type);
  type.GetField("expectedId").SetValue(p,bytes[1]);type.GetField("bytes").SetValue(p,bytes);
  type.GetField("callbackTicks").SetValue(p,Ticks(t));type.GetField("isStatus").SetValue(p,status);return p;
 }
 static void Read(BiofitBLEMonitor m,byte[] b,double t,bool status=false) {
  typeof(BiofitBLEMonitor).GetMethod(status?"ReadStatus":"ReadEvent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,new[]{Wrap(b,t,status)});
 }
 static void Main() {
  var m=new BiofitBLEMonitor(); int solo=0,pairs=0;
  m.AnkleUpCandidate+=(a,b,c)=>solo++;m.BilateralJumpCandidate+=g=>pairs++;
  Check(m.StartGameCapture(),"capture starts");
  double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
  for(int i=0;i<12;i++) { Read(m,Packet(1,true,(ushort)i,(uint)(100000+100*i)),now-1.1+i*.1,true);Read(m,Packet(2,true,(ushort)i,(uint)(500000+100*i)),now-1.1+i*.1,true); }
  Read(m,Packet(1,false,1,99600),now);Read(m,Packet(2,false,1,499600),now);
  Check(solo==0&&pairs==0&&m.CapturedEventCount==2,"stale batch logged, no gameplay");
  Read(m,Packet(1,false,2,101100),now);Read(m,Packet(2,false,2,501111),now);
  Check(solo==2&&pairs==1,"fresh reverse-clock pair acts");
  Read(m,Packet(1,false,2,101100),now);
  Check(solo==2&&pairs==1&&m.RejectedEvents==3,"duplicate cannot act");
  // A callback held >250ms inside Unity cannot act even with a recent sensor time.
  Read(m,Packet(1,false,3,100100),now-1);
  Check(solo==2&&m.RejectedEvents==4,"Unity stalled packet rejected");
  Check(m.StopGameCapture()&&m.CopyGameCapture(),"export finishes");
  Check(UnityEngine.GUIUtility.systemCopyBuffer.Contains("RECHAZO_ANTIGUO")&&UnityEngine.GUIUtility.systemCopyBuffer.Contains("RECHAZO_SECUENCIA"),"CSV keeps rejection reasons");
  var cold=new BiofitBLEMonitor();cold.AnkleUpCandidate+=(a,b,c)=>solo++;
  Read(cold,Packet(1,false,1,1),now);
  Check(solo==2&&cold.RejectedEvents==1,"no STATUS cannot jump");
  // Actual bounded callback queue + disconnect epoch invalidation in Update().
  var q=new BiofitBLEMonitor(); int qActions=0; q.AnkleUpCandidate+=(a,b,c)=>qActions++;
  var enqueue=typeof(BiofitBLEMonitor).GetMethod("Enqueue",BindingFlags.Instance|BindingFlags.NonPublic);
  var update=typeof(BiofitBLEMonitor).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
  for(int i=0;i<1100;i++) enqueue.Invoke(q,new[]{Wrap(Packet(1,false,(ushort)i,100),now)});
  Check((int)typeof(BiofitBLEMonitor).GetField("pendingCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(q)==1024,"queue bounded");
  Check((int)typeof(BiofitBLEMonitor).GetField("callbackDropped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(q)==76,"queue overflow counted");
  var reset=Wrap(Packet(1,false,1,0),now);reset.GetType().GetField("reset").SetValue(reset,true);
  enqueue.Invoke(q,new[]{reset});
  for(int i=0;i<12;i++)update.Invoke(q,null);
  Check(qActions==0,"unsynchronized queue cannot jump");
  Check((int)typeof(BiofitBLEMonitor).GetField("pendingCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(q)==0,"queue drained including disconnect");
  Check(System.IO.File.Exists(m.SavedCapturePath),"capture saved to disk");
  string archive=System.IO.File.ReadAllText(m.SavedCapturePath);
  Check(BiofitCaptureArchive.Verify(archive),"saved archive hash and counts valid");
  Check(!BiofitCaptureArchive.Verify(archive.Substring(0,archive.Length-20)),"truncated archive rejected");
  Check(!BiofitCaptureArchive.Verify(archive.Replace("RECHAZO_ANTIGUO","ALTERADO")),"tampered archive rejected");
  Check(!BiofitCaptureArchive.Verify(BiofitCaptureArchive.Build("tipo;etapa;\n# filas_total=1; finalizada=1\n# filas_eventos=0\n","")),"wrong row count rejected even with valid hash");
  var readiness=new BiofitTrackerReadiness();
  var status=Packet(1,true,1,100);status[2]=0x2c;
  readiness.Observe(status,1,10);
  Check(readiness.Describe(10).Contains("CAL:OK ARM:?"),"v226 arm unknown");
  status[2]=0xec;BitConverter.GetBytes((uint)200).CopyTo(status,6);
  readiness.Observe(status,1,10.1);
  Check(readiness.Describe(10.2).Contains("CAL:OK ARM:SI"),"v227 calibrated and armed");
  readiness.Observe(status,1,11.2);
  Check(!readiness.Current(11.2),"frozen timestamp cannot renew readiness");
  readiness.Reset();Check(!readiness.Current(11.2),"disconnect clears readiness");
  var capture=new BiofitEventCapture();capture.BeginGame(0);
  capture.RecordGameAction("SOLO_DER_DESCARTADO_UMBRAL",1);
  capture.RecordDiagnostic(2,1,"line\r\nwith;delimiters");
  for(int i=0;i<6100;i++)capture.RecordDiagnostic(i,1,"stress");
  capture.FinishGame(6200);
  Check(capture.Csv.Contains("SOLO_DER_DESCARTADO_UMBRAL"),"solo rejection retained");
  Check(capture.DroppedRows>0 && BiofitCaptureArchive.Verify(BiofitCaptureArchive.Build(capture.Csv,"stress")),"bounded large archive verified with drop count");
  var telemetry=new BiofitBLEMonitor();telemetry.StartGameCapture();
  var readTransport=typeof(BiofitBLEMonitor).GetMethod("ReadTransport",BindingFlags.Instance|BindingFlags.NonPublic);
  var page=new byte[20];page[0]=2;page[1]=1;page[3]=0xec;
  BitConverter.GetBytes((short)-125).CopyTo(page,12);BitConverter.GetBytes((short)-80).CopyTo(page,14);
  readTransport.Invoke(telemetry,new[]{Wrap(page,now)});
  page[2]=3;BitConverter.GetBytes((ushort)227).CopyTo(page,18);
  readTransport.Invoke(telemetry,new[]{Wrap(page,now)});
  telemetry.StopGameCapture();telemetry.CopyGameCapture();
  Check(UnityEngine.GUIUtility.systemCopyBuffer.Contains("upMg=-125 velocityMmS=-80"),"firmware v2 signed diagnostic fields decoded");
  Check(UnityEngine.GUIUtility.systemCopyBuffer.Contains("fwVersion=227"),"v227 identity decoded");
  // A failed private-file save must NOT be misreported as successful finalization.
  string originalPath=UnityEngine.Application.persistentDataPath;
  string obstructed=System.IO.Path.GetTempFileName();
  try {
    UnityEngine.Application.persistentDataPath=obstructed; // existing file, not directory
    var failed=new BiofitBLEMonitor();
    Check(failed.StartGameCapture(),"failure test capture starts");
    Check(!failed.StopGameCapture() && failed.CaptureFileStatus.StartsWith("ERROR archivo:"),
          "failed private save reported, not hidden behind FINALIZAR");
    Check(failed.CopyGameCapture() &&
          BiofitCaptureArchive.Verify(UnityEngine.GUIUtility.systemCopyBuffer),
          "lossless hashed clipboard fallback after storage failure");
  } finally {
    UnityEngine.Application.persistentDataPath=originalPath;
    System.IO.File.Delete(obstructed);
  }
  Console.WriteLine("PASS: "+checks+" receiver integration checks using API shims (not a Unity build).");
 }
}
